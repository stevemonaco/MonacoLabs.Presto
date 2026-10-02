using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Headless.XUnit;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;
using Monaco.Presto.AvaloniaBridge.Compositor;

namespace Monaco.Presto.AvaloniaBridge.Tests;

/// <summary>
/// <see cref="Animate.ContentSizeProperty"/> composed with <see cref="Presence"/> content swaps (GAPS-PLAN §3.2, 43a,
/// ≈ Compose <c>AnimatedContent</c> with <c>SizeTransform</c>), with <see cref="Animate.LayoutProperty"/> on the same host,
/// and with <see cref="Animate.ItemLayoutProperty"/> on the list an expanding row lives in.
/// </summary>
public sealed class ContentSizePresenceTests : CompositorTestBase
{
    private const double Duration = 100;
    private const double Small = 20;
    private const double Large = 60;

    private static AnimationBuilder Linear => Presto.Build().Duration(Duration).Ease("linear");

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void ASwapGhostsTheOldContentEntersTheNewAndResizesBetween(bool contentControl)
    {
        var (host, swap) = ShowHost(contentControl, Bar(Small));
        var next = Bar(Large);

        swap(next);
        Tick();
        Assert.Equal(Small, host.Bounds.Height);
        Assert.Single(Ghosts(host));
        Assert.True(next.Opacity < 0.1);

        Frames.Advance(Duration / 2);
        Tick();
        Assert.InRange(host.Bounds.Height, Small + 5, Large - 5);
        Assert.Single(Ghosts(host));

        Frames.Advance(Duration);
        Tick();
        Assert.Equal(Large, host.Bounds.Height);
        Assert.Equal(Large, next.Bounds.Height);
        Assert.Equal(1, next.Opacity);
        Assert.Empty(Ghosts(host));
        Assert.False(((Layoutable)host.GetVisualChildren().Single()).IsSet(Layoutable.MarginProperty));
    }

    /// <summary>The ghost is clipped to the box the host was drawn in when it was swapped out, which a resize under way had clipped too.</summary>
    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void ASwapMidResizeRetargetsFromTheCurrentSizeAndClipsItsGhostThere(bool contentControl)
    {
        var (host, swap) = ShowHost(contentControl, Bar(Small));
        swap(Bar(Large));
        Tick();
        Frames.Advance(Duration / 2);
        Tick();
        var halfway = host.Bounds.Height;

        swap(Bar(Small + 10));
        Tick();
        Assert.Equal(halfway, host.Bounds.Height);
        Assert.Equal(halfway, Ghosts(host).Last().Children.OfType<Canvas>().Single().Height);

        Frames.Advance(Duration / 2);
        Tick();
        Assert.InRange(host.Bounds.Height, Small + 12, halfway - 2);
        Frames.Advance(Duration);
        Tick();
        Assert.Equal(Small + 10, host.Bounds.Height);
    }

    /// <summary>An exit of the host itself snapshots it at the size it was drawn, not the size its content was laid out at.</summary>
    [AvaloniaFact]
    public void TheHostRemovedMidResizeGhostsAtItsDrawnSize()
    {
        var (host, swap) = ShowHost(contentControl: false, Bar(Small));
        Presence.SetExit(host, Presto.Build().Opacity(0).Duration(Held));
        swap(Bar(Large));
        Tick();
        Frames.Advance(Duration / 2);
        Tick();
        var drawn = host.Bounds.Size;

        var window = TopLevel.GetTopLevel(host)!;
        ((Panel)host.Parent!).Children.Remove(host);
        Tick();

        Assert.Contains(drawn, Ghosts(window).Select(ghost => ghost.Children.OfType<Canvas>().Single().Children.OfType<Image>().Single().Bounds.Size));
    }

    /// <summary>
    /// Without a page transition the swap is the host's own, and its content exits and enters. Under a Presto page
    /// transition the pages are that transition's, so the swap is a plain resize.
    /// </summary>
    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void AHostInsideATransitioningContentControlResizesAcrossItsSwap(bool presto)
    {
        var host = new TransitioningContentControl
        {
            PageTransition = presto ? new PrestoPageTransition { Exit = Presto.Build().Opacity(0).Duration(Duration).Ease("linear"), UseCompositor = false } : null,
            Content = Bar(Small),
            VerticalAlignment = VerticalAlignment.Top,
        };
        Animate.SetContentSize(host, Linear);
        ShowWhite(host);

        Swap(host, Bar(Large));
        Tick();
        Assert.Equal(presto ? 0 : 1, Ghosts(host).Count);

        Frames.Advance(Duration / 2);
        Tick();
        Assert.InRange(host.Bounds.Height, Small + 5, Large - 5);
        Settle(() => host.Bounds.Height == Large && Ghosts(host).Count == 0);
        Assert.Equal(Large, host.Bounds.Height);
    }

    /// <summary>
    /// ContentSize owns the host's size, so a layout transition on it in <see cref="LayoutAnimateMode.PositionAndSize"/>
    /// eases only its moves: the host is drawn at its real size, still held in the slot it moved from.
    /// </summary>
    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void ALayoutTransitionOnAContentSizeHostEasesOnlyItsPosition(bool attached)
    {
        var above = new Border { Height = 10 };
        var inner = new Border { Height = Small };
        var host = new Border { Background = Brushes.Red, Child = new StackPanel { Children = { inner } } };
        Animate.SetContentSize(host, Linear);
        if (attached)
        {
            Animate.SetLayout(host, HeldMove);
            Animate.SetLayoutMode(host, LayoutAnimateMode.PositionAndSize);
        }
        ShowWhite(new StackPanel { Children = { above, host } });
        if (!attached) host.AnimateLayout(HeldMove, LayoutAnimateMode.PositionAndSize);

        inner.Height = Large;
        above.Height = 30;
        Tick();
        Frames.Advance(Duration / 2);
        Tick();

        var box = Box(host);
        Assert.Equal(host.Bounds.Height, box.Height);
        Assert.Equal(10, box.Top);
        Assert.Equal(host.Bounds.Size, LayoutTransition.Drawn(host, SlotOf(host)).Size);
    }

    /// <summary>The rows below an expanding row follow its eased height in the same frame rather than flying after it.</summary>
    [AvaloniaFact]
    public void TheRowsBelowAnExpandingRowFollowItAndEndInTheirSlots()
    {
        var items = new ObservableCollection<string> { "a", "b", "red" };
        var list = new ItemsControl
        {
            ItemsSource = items,
            VerticalAlignment = VerticalAlignment.Top,
            ItemTemplate = new FuncDataTemplate<string>((item, _) => new Border
            {
                Height = Small,
                Width = 40,
                HorizontalAlignment = HorizontalAlignment.Left,
                Background = item == "red" ? Brushes.Red : Brushes.Blue,
            }, supportsRecycling: false),
        };
        Animate.SetItemLayout(list, HeldMove);
        ShowWhite(list);
        var row = (Control)list.ContainerFromIndex(1)!;
        var below = (Control)list.ContainerFromIndex(2)!;
        Animate.SetContentSize(row, Linear);
        Tick();

        ((Border)row.GetVisualChildren().Single()).Height = Large;
        for (var frame = 0; frame < 6; frame++)
        {
            Tick();
            Assert.Equal(SlotOf(below).Top, Top(list));
            Assert.Equal(row.Bounds.Bottom, below.Bounds.Top);
            Frames.Advance(Duration / 4);
        }
        Tick();
        Assert.Equal(Small + Large, SlotOf(below).Top);
        Assert.Equal(SlotOf(below).Top, Top(list));
    }

    private static Border Bar(double height)
    {
        var bar = new Border { Height = height, Width = 100, HorizontalAlignment = HorizontalAlignment.Left, Background = Brushes.Red };
        Presence.SetExit(bar, Presto.Build().Opacity(0).Duration(Duration).Ease("linear"));
        Presence.SetEnter(bar, Presto.Build().Opacity(from: 0).Duration(Duration).Ease("linear"));
        return bar;
    }

    private static (Control Host, Action<Control> Swap) ShowHost(bool contentControl, Control content)
    {
        Control host;
        Action<Control> swap;
        if (contentControl)
        {
            var control = new ContentControl { Content = content };
            (host, swap) = (control, next => control.Content = next);
        }
        else
        {
            var border = new Border { Child = content };
            (host, swap) = (border, next => border.Child = next);
        }
        host.VerticalAlignment = VerticalAlignment.Top;
        Animate.SetContentSize(host, Linear);
        ShowWhite(host);
        return (host, swap);
    }
}
