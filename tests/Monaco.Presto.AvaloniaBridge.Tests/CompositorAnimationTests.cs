using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Threading;

namespace Monaco.Presto.AvaloniaBridge.Tests;

/// <summary>
/// Covers what a compositor animation puts on screen (COMPOSITOR-PLAN §4.3). A server animation never writes back to the
/// client side composition visual, so the rendered frame is the only thing there is to assert on.
/// </summary>
public sealed class CompositorAnimationTests : CompositorTestBase
{
    /// <remarks>
    /// The version guard for COMPOSITOR-PLAN §2.2: invalidating forces <c>SynchronizeCompositionProperties</c> to write
    /// <c>Opacity</c> again, and the fade survives it only because the client setter's equality guard drops the unchanged
    /// write that would otherwise reach the server as <c>SetAnimatedValue</c> and remove the animation. Re-run this test
    /// whenever <c>AvaloniaVersion</c> in Directory.Packages.props moves.
    /// </remarks>
    [AvaloniaFact]
    public async Task InvalidateVisualDoesNotStopACompositorFade()
    {
        var bar = ShowBar();
        var opaque = Capture(bar);
        _ = bar.AnimateOnCompositor(new() { ["Opacity"] = 0, Duration = 2000, Ease = "linear" });
        Assert.True(await Pump(() => Difference(opaque, Capture(bar)) > 0), "the fade never reached the render thread");

        bar.InvalidateVisual();
        Dispatcher.UIThread.RunJobs();
        var interrupted = Difference(opaque, Capture(bar));

        Assert.True(await Pump(() => Difference(opaque, Capture(bar)) > interrupted), "invalidating the visual stopped the fade");
    }

    [AvaloniaFact]
    public async Task TranslationMovesThePixels()
    {
        var bar = ShowBar();
        Assert.Equal(new Rect(30, 40, 40, 20), Extent(bar, Colors.Red));

        _ = bar.AnimateOnCompositor(new() { ["X"] = 20, ["Y"] = 30, Duration = 300, Ease = "linear" });

        Assert.True(await Pump(() => Extent(bar, Colors.Red) == new Rect(50, 70, 40, 20)), "the translation never arrived");
    }

    [AvaloniaFact]
    public async Task RotationTurnsAboutTheCentreOfTheResizedControl()
    {
        var bar = ShowBar();
        _ = bar.AnimateOnCompositor(new() { ["Rotate"] = 90, Duration = 300, Ease = "linear" });

        bar.Width = 20;
        Canvas.SetLeft(bar, 40);
        Tick();

        Assert.True(await Pump(() => Extent(bar, Colors.Red)?.Height > 15), "the bar never turned");
        var turned = Extent(bar, Colors.Red)!.Value;
        Assert.Equal(50, turned.Center.X, 1);
        Assert.Equal(50, turned.Center.Y, 1);
    }

    [AvaloniaFact]
    public async Task CancelFreezesThePixelsWhereTheyAre()
    {
        var bar = ShowBar();
        var animation = bar.AnimateOnCompositor(new() { ["X"] = 40, Duration = 2000, Ease = "linear" });
        Assert.True(await Pump(() => Extent(bar, Colors.Red)?.Left > 32), "the translation never started");

        _ = animation.Cancel();
        var frozen = Extent(bar, Colors.Red);

        Assert.False(await Pump(() => Extent(bar, Colors.Red) != frozen, rounds: 8), "the cancelled animation kept running");
    }

    [AvaloniaFact]
    public async Task RevertPutsTheControlBackAsItWas()
    {
        var bar = ShowBar();
        var untouched = Capture(bar);
        var animation = bar.AnimateOnCompositor(new() { ["X"] = 40, ["Opacity"] = 0.2, Duration = 2000, Ease = "linear" });
        Assert.True(await Pump(() => Difference(untouched, Capture(bar)) > 0), "the animation never started");

        _ = animation.Revert();

        Assert.True(await Pump(() => Difference(untouched, Capture(bar)) == 0), "the reverted control is not what it was");
    }

    [AvaloniaFact]
    public async Task ThenResolvesWhenTheMirrorCompletes()
    {
        var bar = ShowBar();
        var animation = bar.AnimateOnCompositor(new() { ["X"] = 40, Duration = 200, Ease = "linear" });
        Assert.True(await Pump(() => Extent(bar, Colors.Red)?.Left > 32), "the translation never started");

        Frames.Advance(400);

        Assert.Same(animation, await animation.Then());
        Assert.True(animation.Completed);
        Assert.Equal(1, animation.Progress);
    }

    [AvaloniaFact]
    public void EveryCompletionHandsTheDrawnOpacityBackToTheControl()
    {
        var bar = ShowBar();
        bar.Opacity = 0;
        var animation = bar.AnimateOnCompositor(new() { ["Opacity"] = 1, Duration = 200, Ease = "linear" });

        Frames.Advance(400);
        Assert.Equal(1, bar.Opacity);

        _ = animation.Reverse();
        Frames.Advance(400);
        Assert.True(animation.Completed);
        Assert.Equal(0, bar.Opacity);

        _ = animation.Play();
        Frames.Advance(400);
        Assert.Equal(1, bar.Opacity);
    }

    [AvaloniaFact]
    public void RevertLeavesEveryMirrorAtZeroRatherThanBeforeItsDelay()
    {
        var bar = ShowBar();
        var animation = bar.AnimateOnCompositor(new() { ["X"] = 40, Duration = 200, Delay = 100, Ease = "linear" });
        Frames.Advance(150);

        _ = animation.Revert();

        Assert.Equal(0, animation.CurrentTime);
    }
}
