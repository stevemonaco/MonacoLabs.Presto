using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Layout;
using Avalonia.Media;

namespace Monaco.Presto.AvaloniaBridge.Tests;

/// <summary>
/// <see cref="ContentSizeTransition"/> and <see cref="Animate.ContentSizeProperty"/> (GAPS-PLAN §3.2, 43a): the host's real
/// layout size eases on the UI thread, so <see cref="Visual.Bounds"/> and the rendered frame move together, frame by frame
/// as <c>Frames.Advance</c> ticks the engine.
/// </summary>
public sealed class ContentSizeTests : CompositorTestBase
{
    private const double Duration = 100;
    private const double Small = 20;
    private const double Large = 60;

    [AvaloniaTheory]
    [InlineData(Small, Large)]
    [InlineData(Large, Small)]
    public void TheHostEasesToItsNewSizeAndLandsOnItExactly(double from, double to)
    {
        var (host, inner) = ShowHost(from);
        _ = host.AnimateContentSize(Duration, "linear");

        inner.Height = to;
        Tick();
        Frames.Advance(Duration / 2);
        Tick();
        var halfway = Box(host).Height;
        Assert.InRange(halfway, Math.Min(from, to) + 5, Math.Max(from, to) - 5);
        Assert.Equal(halfway, host.Bounds.Height);

        Frames.Advance(Duration);
        Tick();
        Assert.Equal(to, Box(host).Height);
        Assert.Equal(to, host.Bounds.Height);
        Assert.False(inner.Parent!.IsSet(Layoutable.MarginProperty));
        Assert.False(inner.Parent!.IsSet(Visual.ClipProperty));
    }

    /// <summary>The guard on the first frame: the size has to be held in the layout pass the content change triggers.</summary>
    [AvaloniaFact]
    public void TheFrameAfterTheContentChangesStillShowsTheOldSize()
    {
        var (host, inner) = ShowHost(Small);
        _ = host.AnimateContentSize(Duration, "linear");

        inner.Height = Large;
        Tick();

        Assert.Equal(Small, Box(host).Height);
        Assert.Equal(Small, host.Bounds.Height);
    }

    /// <remarks>
    /// The content is laid out at its final 160 wide from the first frame, so a host drawn about 110 wide would show text
    /// past its right edge if nothing clipped it, and the text would rewrap every frame if the content followed the host.
    /// </remarks>
    [AvaloniaFact]
    public void WrappingTextKeepsItsFinalLayoutAndIsClippedToTheHost()
    {
        var text = new TextBlock { Text = string.Join(' ', Enumerable.Repeat("wrap", 40)), TextWrapping = TextWrapping.Wrap, Foreground = Brushes.Black };
        var content = new StackPanel { Width = 60, Children = { text } };
        var host = new Border { Child = content, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top };
        ShowWhite(host);
        _ = host.AnimateContentSize(Duration, "linear");

        content.Width = 160;
        Tick();
        var lines = text.TextLayout.TextLines.Count;
        var width = text.Bounds.Width;
        var glyphs = text.TextLayout.Width;
        Assert.Equal(60, host.Bounds.Width);
        Assert.Equal(160, width);

        for (var frame = 0; frame < 8; frame++)
        {
            Frames.Advance(Duration / 8);
            Tick();
            Assert.Equal(lines, text.TextLayout.TextLines.Count);
            Assert.Equal(width, text.Bounds.Width);
            Assert.Equal(glyphs, text.TextLayout.Width);
            var right = host.Bounds.Right;
            Assert.Equal(0, CountPixels(host, new Rect(right, 0, 200 - right, 200), Colors.White));
            if (frame == 3) Assert.InRange(right, 70, 150);
        }
        Assert.Equal(160, host.Bounds.Width);
    }

    [AvaloniaFact]
    public void TheHostKeepsItsShadowAndRoundedCornersWhileItsContentIsClipped()
    {
        var inner = new Border { Height = Small };
        var host = new Border
        {
            Child = new StackPanel { Background = Brushes.Red, Width = 60, Children = { inner } },
            CornerRadius = new CornerRadius(10),
            BoxShadow = BoxShadows.Parse("0 0 0 8 Blue"),
            Margin = new Thickness(20),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
        };
        ShowWhite(host);
        _ = host.AnimateContentSize(Duration, "linear");

        inner.Height = Large;
        Tick();
        Frames.Advance(Duration / 2);
        Tick();

        var slot = SlotOf(host);
        Assert.InRange(slot.Height, Small + 5, Large - 5);
        Assert.True(Extent(host, Colors.Blue) is { } shadow && shadow.Bottom >= slot.Bottom + 6 && shadow.Top <= slot.Top - 6);
        Assert.True(Extent(host, Colors.Red) is { } content && content.Bottom <= slot.Bottom);
        var corner = new Rect(slot.Left, slot.Bottom - 3, 2, 2);
        Assert.Equal(4, CountPixels(TopLevel.GetTopLevel(host)!, corner, Colors.Red));
    }

    [AvaloniaFact]
    public void AChangeMidFlightRetargetsFromTheCurrentSize()
    {
        var (host, inner) = ShowHost(Small);
        _ = host.AnimateContentSize(Duration, "linear");
        inner.Height = Large;
        Tick();
        Frames.Advance(Duration / 2);
        Tick();
        var halfway = Box(host).Height;

        inner.Height = Small;
        Tick();
        Assert.Equal(halfway, Box(host).Height);
        Frames.Advance(Duration / 2);
        Tick();
        Assert.InRange(Box(host).Height, Small + 2, halfway - 2);
        Frames.Advance(Duration);
        Tick();
        Assert.Equal(Small, Box(host).Height);
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void CancelOrRevertRestoresTheUsersOwnValues(bool revert)
    {
        var (host, inner) = ShowHost(Small);
        var content = (Layoutable)inner.Parent!;
        content.Margin = new Thickness(2);
        var clip = new RectangleGeometry(new Rect(0, 0, 500, 500));
        content.Clip = clip;
        Tick();
        var transition = host.AnimateContentSize(Duration, "linear");
        inner.Height = Large;
        Tick();
        Assert.NotSame(clip, content.Clip);

        if (revert) transition.Revert();
        else transition.Cancel();
        Tick();

        Assert.Equal(new Thickness(2), content.Margin);
        Assert.Same(clip, content.Clip);
        Assert.False(host.ClipToBounds);
        Assert.Equal(Large + 4, host.Bounds.Height);
    }

    [AvaloniaFact]
    public void AttachingRecordsTheSizeAndDetachingReverts()
    {
        var (host, inner) = ShowHost(Small);
        var panel = (Panel)host.Parent!;
        panel.Children.Remove(host);
        Animate.SetContentSize(host, Presto.Build().Duration(Duration).Ease("linear"));
        inner.Height = Large;
        panel.Children.Add(host);
        Tick();
        Assert.Equal(Large, Box(host).Height);

        inner.Height = Small;
        Tick();
        Assert.Equal(Large, Box(host).Height);
        Assert.True(inner.Parent!.IsSet(Layoutable.MarginProperty));
        Assert.True(inner.Parent!.IsSet(Visual.ClipProperty));

        panel.Children.Remove(host);
        Assert.False(inner.Parent!.IsSet(Layoutable.MarginProperty));
        Assert.False(inner.Parent!.IsSet(Visual.ClipProperty));
    }

    public static TheoryData<AnimationBuilder> Unsupported => new() { Presto.Build().Opacity(0), Presto.Build().Loop(2) };

    [AvaloniaTheory]
    [MemberData(nameof(Unsupported))]
    public void ABuilderAContentSizeTransitionCannotHonorThrowsWhenApplied(AnimationBuilder builder) =>
        Assert.Throws<NotSupportedException>(() => Animate.SetContentSize(new Border(), builder));

    /// <remarks><see cref="Monaco.Presto.Tests.EngineTestBase"/> gives every test fresh defaults, which resets both flags.</remarks>
    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void AReducedResizeIsInstant(bool reduceAll)
    {
        var (host, inner) = ShowHost(Small);
        if (reduceAll) Presto.Defaults.ReduceAll = true;
        else Presto.Defaults.ReducedMotion = true;
        Animate.SetContentSize(host, Presto.Build().Duration(Held).WhenReduced(reduced => reduced.Duration(0)));

        inner.Height = Large;
        Tick();

        Assert.Equal(Large, Box(host).Height);
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void ADurationResizeLandsInOneFrameOnlyUnderReducedMotion(bool reduced)
    {
        var (host, inner) = ShowHost(Small);
        var transition = host.AnimateContentSize(Held, "linear");
        Presto.Defaults.ReducedMotion = reduced;

        inner.Height = Large;
        Tick();

        Assert.Equal(reduced ? Large : Small, Box(host).Height);
        Assert.Equal(reduced, transition.Completed);
    }

    [AvaloniaFact]
    public void ThenCompletesWhenTheResizeLands()
    {
        var (host, inner) = ShowHost(Small);
        var transition = host.AnimateContentSize(Duration, "linear");
        inner.Height = Large;
        Tick();

        var landed = transition.Then();
        Assert.False(landed.IsCompleted);
        Settle(() => landed.IsCompleted);

        Assert.True(landed.IsCompleted);
        Assert.True(transition.Completed);
    }

    /// <summary>A red host whose content is a bar <paramref name="height"/> tall, at the top of a white window.</summary>
    private static (Border Host, Border Inner) ShowHost(double height)
    {
        var inner = new Border { Height = height };
        var host = new Border { Background = Brushes.Red, Child = new StackPanel { Children = { inner } }, VerticalAlignment = VerticalAlignment.Top };
        ShowWhite(host);
        return (host, inner);
    }
}
