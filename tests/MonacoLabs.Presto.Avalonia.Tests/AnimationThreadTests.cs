using Avalonia.Headless.XUnit;
using Avalonia.Media;

namespace MonacoLabs.Presto.Tests;

/// <summary>Covers <see cref="AnimationThread"/> and <see cref="PrestoAvalonia.CanAnimateOnCompositor(AnimationParams, out string?)"/> (BRIDGE-THREAD).</summary>
public sealed class AnimationThreadTests : CompositorTestBase
{
    private static AnimationBuilder Builder(string kind) => kind switch
    {
        "fits" => Presto.Build().X(20).Opacity(0.5).Duration(100).Ease("linear"),
        "x" => Presto.Build().X(20).Duration(100).Ease("linear"),
        "property" => Presto.Build().X(20).Property("Width", 60).Duration(100).Ease("linear"),
        "setting" => Presto.Build().X(20).Opacity(0.5).Duration(100).Ease("linear").OnUpdate(_ => { }),
        "keyframes" => Presto.Build().Keyframes(k => k.Frame(f => f.X(10))).Duration(100),
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    [AvaloniaTheory]
    [InlineData("fits", AnimationThread.UIThread, false)]
    [InlineData("fits", AnimationThread.Compositor, true)]
    [InlineData("fits", AnimationThread.PreferCompositor, true)]
    [InlineData("property", AnimationThread.UIThread, false)]
    [InlineData("property", AnimationThread.PreferCompositor, false)]
    [InlineData("setting", AnimationThread.UIThread, false)]
    [InlineData("setting", AnimationThread.PreferCompositor, false)]
    public void TheHandleReportsTheThreadTheModePicked(string kind, AnimationThread thread, bool onCompositor)
    {
        var animation = ShowBar().Animate(Builder(kind), thread);

        Assert.Equal(onCompositor, animation.OnCompositor);
        Assert.Equal(!onCompositor, animation.UIThread != null);
    }

    [AvaloniaTheory]
    [InlineData("property", "'Width' has no composition property. The compositor animates X, Y, Rotate, Scale, ScaleX, ScaleY and Opacity only.")]
    [InlineData("setting", "OnUpdate has no counterpart on the compositor.")]
    public void ForcingTheCompositorThrowsWhatAnimateOnCompositorThrows(string kind, string message)
    {
        var forced = Assert.Throws<NotSupportedException>(() => ShowBar().Animate(Builder(kind), AnimationThread.Compositor));
        var direct = Assert.Throws<NotSupportedException>(() => ShowBar().AnimateOnCompositor(Builder(kind)));

        Assert.Equal(message, forced.Message);
        Assert.Equal(direct.Message, forced.Message);
    }

    [AvaloniaTheory]
    [InlineData("whenDetached", "WhenDetached")]
    [InlineData("subclass", "A setting")]
    public void EverySettingTheCompositorDoesNotCarryIsRejected(string kind, string reason)
    {
        AnimationParams parameters = kind switch
        {
            "whenDetached" => new AvaloniaAnimationParams { ["X"] = 20, WhenDetached = WhenDetached.Continue },
            _ => new TaggedParams { ["X"] = 20, Tag = "tag" },
        };

        var thrown = Assert.Throws<NotSupportedException>(() => PrestoAvalonia.Animate(ShowBar(), parameters, AnimationThread.Compositor));

        Assert.Equal($"{reason} has no counterpart on the compositor.", thrown.Message);
        Assert.False(PrestoAvalonia.CanAnimateOnCompositor(parameters, out var actual));
        Assert.Equal(reason, actual);
    }

    private sealed record TaggedParams : AnimationParams
    {
        public string? Tag { get; init; }
    }

    [AvaloniaTheory]
    [InlineData("property")]
    [InlineData("setting")]
    public async Task AFallbackEndsWhereAnimateEnds(string kind)
    {
        var expected = ShowBar();
        var actual = ShowBar();

        var plain = expected.Animate(Builder(kind));
        var fallback = PrestoAvalonia.Animate(actual, Builder(kind), AnimationThread.PreferCompositor);
        Frames.Advance(200);

        Assert.Same(fallback, await fallback);
        Assert.True(plain.Completed);
        Assert.Equal(20, X(actual));
        Assert.Equal(X(expected), X(actual));
        Assert.Equal(expected.Width, actual.Width);
        Assert.Equal(expected.Opacity, actual.Opacity);
    }

    [AvaloniaFact]
    public void PreferringTheCompositorForAVisualAndANonVisualRunsBothOnTheUIThread()
    {
        var bar = ShowBar();
        var transform = new TranslateTransform();

        var animation = PrestoAvalonia.Animate(new object[] { bar, transform }, Builder("x"), AnimationThread.PreferCompositor);
        Frames.Advance(200);

        Assert.False(animation.OnCompositor);
        Assert.Equal(20, X(bar));
        Assert.Equal(20, transform.X);
    }

    [AvaloniaFact]
    public void ForcingTheCompositorForANonVisualThrowsNamingIt()
    {
        var thrown = Assert.Throws<NotSupportedException>(() => PrestoAvalonia.Animate(new TranslateTransform(), Builder("x"), AnimationThread.Compositor));

        Assert.Equal("TranslateTransform is not a Visual. The compositor animates Visuals only.", thrown.Message);
    }

    [AvaloniaFact]
    public async Task ACompositorHandleResolvesWithItsAnimation()
    {
        var bar = ShowBar();
        var animation = bar.Animate(Builder("fits"), AnimationThread.PreferCompositor);

        Run(10);

        Assert.Same(animation, await animation);
        Assert.True(animation.Compositor!.Completed);
    }

    [AvaloniaTheory]
    [InlineData(AnimationThread.UIThread)]
    [InlineData(AnimationThread.Compositor)]
    public void TheHandleDelegatesPlaybackToTheLiveAnimation(AnimationThread thread)
    {
        var bar = ShowBar();
        var animation = bar.Animate(Presto.Build().X(20).Duration(1000).Ease("linear"), thread);
        double Time() => animation.Compositor?.CurrentTime ?? animation.UIThread!.CurrentTime;
        Frames.Advance(100);

        Assert.Same(animation, animation.Pause());
        var paused = Time();
        Frames.Advance(100);
        Assert.Equal(paused, Time());

        Assert.Same(animation, animation.Resume());
        Frames.Advance(100);
        Assert.True(Time() > paused);

        animation.Pause();
        Assert.Same(animation, animation.Play());
        var playing = Time();
        Frames.Advance(100);
        Assert.True(Time() > playing);

        Assert.Same(animation, animation.Cancel());
        var canceled = Time();
        Frames.Advance(100);
        Assert.Equal(canceled, Time());

        Assert.Same(animation, animation.Revert());
        Assert.Equal(0, thread == AnimationThread.Compositor ? Time() : X(bar));
    }

    [AvaloniaTheory]
    [InlineData("fits", null)]
    [InlineData("property", "Width")]
    [InlineData("setting", "OnUpdate")]
    [InlineData("keyframes", "Keyframes")]
    public void CanAnimateOnCompositorNamesWhatDoesNotFit(string kind, string? reason)
    {
        Assert.Equal(reason == null, PrestoAvalonia.CanAnimateOnCompositor(Builder(kind), out var actual));
        Assert.Equal(reason, actual);
    }
}
