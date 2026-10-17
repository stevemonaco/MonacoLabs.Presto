using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Threading;

namespace MonacoLabs.Presto.Tests;

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
    public void InvalidateVisualDoesNotStopACompositorFade()
    {
        var bar = ShowBar();
        var opaque = Capture(bar);
        _ = bar.AnimateOnCompositor(new() { ["Opacity"] = 0, Duration = 2000, Ease = "linear" });
        Assert.True(Pump(() => Difference(opaque, Capture(bar)) > 0), "the fade never reached the render thread");

        bar.InvalidateVisual();
        Dispatcher.UIThread.RunJobs();
        var interrupted = Difference(opaque, Capture(bar));

        Assert.True(Pump(() => Difference(opaque, Capture(bar)) > interrupted), "invalidating the visual stopped the fade");
    }

    [AvaloniaFact]
    public void TranslationMovesThePixels()
    {
        var bar = ShowBar();
        Assert.Equal(new Rect(30, 40, 40, 20), Extent(bar, Colors.Red));

        _ = bar.AnimateOnCompositor(new() { ["X"] = 20, ["Y"] = 30, Duration = 300, Ease = "linear" });

        Assert.True(Pump(() => Extent(bar, Colors.Red) == new Rect(50, 70, 40, 20)), "the translation never arrived");
    }

    [AvaloniaFact]
    public void RotationTurnsAboutTheCenterOfTheResizedControl()
    {
        var bar = ShowBar();
        _ = bar.AnimateOnCompositor(new() { ["Rotate"] = 90, Duration = 300, Ease = "linear" });

        bar.Width = 20;
        Canvas.SetLeft(bar, 40);
        Tick();

        Assert.True(Pump(() => Extent(bar, Colors.Red)?.Height > 15), "the bar never turned");
        var turned = Extent(bar, Colors.Red)!.Value;
        Assert.Equal(50, turned.Center.X, 1);
        Assert.Equal(50, turned.Center.Y, 1);
    }

    [AvaloniaTheory]
    [InlineData(null, "ScaleY", 30, 45, 40, 10)]
    [InlineData("50%,100%", "ScaleY", 30, 50, 40, 10)]
    [InlineData("0,0", "Scale", 30, 40, 20, 10)]
    public void AScalePivotsOnTheRenderTransformOrigin(string? origin, string property, double x, double y, double width, double height)
    {
        var bar = ShowBar();
        if (origin != null) bar.RenderTransformOrigin = RelativePoint.Parse(origin);

        _ = bar.AnimateOnCompositor(new() { [property] = 0.5, Duration = 300, Ease = "linear" });

        var expected = new Rect(x, y, width, height);
        Assert.True(Pump(() => Extent(bar, Colors.Red) == expected), $"the bar never scaled to {expected}, it stands at {Extent(bar, Colors.Red)}");
    }

    [AvaloniaFact]
    public void ChangingTheOriginMidAnimationMovesThePivotAtTheNextFrame()
    {
        var bar = ShowBar();
        _ = bar.AnimateOnCompositor(new() { ["ScaleY"] = new TweenValue[] { 0.5, 0.5 }, Duration = Held, Ease = "linear" });
        Assert.True(Pump(() => Extent(bar, Colors.Red) == new Rect(30, 45, 40, 10)), "the bar never scaled about its center");

        bar.RenderTransformOrigin = RelativePoint.Parse("50%,100%");
        Tick();

        Assert.Equal(new Rect(30, 50, 40, 10), Extent(bar, Colors.Red));
    }

    [AvaloniaFact]
    public void CancelFreezesThePixelsWhereTheyAre()
    {
        var bar = ShowBar();
        var animation = bar.AnimateOnCompositor(new() { ["X"] = 40, Duration = 2000, Ease = "linear" });
        Assert.True(Pump(() => Extent(bar, Colors.Red)?.Left > 32), "the translation never started");

        _ = animation.Cancel();
        var frozen = Extent(bar, Colors.Red);

        Assert.False(Pump(() => Extent(bar, Colors.Red) != frozen, rounds: 8), "the canceled animation kept running");
    }

    [AvaloniaFact]
    public void CancelHandsVisualOpacityBackToTheControl()
    {
        var bar = ShowBar();
        var opaque = Capture(bar);
        var animation = bar.AnimateOnCompositor(new() { ["Opacity"] = 0, Duration = 2000, Ease = "linear" });
        Assert.True(Pump(() => Difference(opaque, Capture(bar)) > 0), "the fade never reached the render thread");

        _ = animation.Cancel();
        var frozen = Difference(opaque, Capture(bar));
        bar.Opacity = 0;

        Assert.Equal(0, bar.Opacity);
        Assert.True(Pump(() => Difference(opaque, Capture(bar)) > frozen), "the local opacity never reached the screen");
    }

    [AvaloniaFact]
    public void RevertPutsTheControlBackAsItWas()
    {
        var bar = ShowBar();
        var untouched = Capture(bar);
        var animation = bar.AnimateOnCompositor(new() { ["X"] = 40, ["Opacity"] = 0.2, Duration = 2000, Ease = "linear" });
        Assert.True(Pump(() => Difference(untouched, Capture(bar)) > 0), "the animation never started");

        _ = animation.Revert();

        Assert.True(Pump(() => Difference(untouched, Capture(bar)) == 0), "the reverted control is not what it was");
    }

    [AvaloniaFact]
    public async Task ThenResolvesWhenTheMirrorCompletes()
    {
        var bar = ShowBar();
        var animation = bar.AnimateOnCompositor(new() { ["X"] = 40, Duration = 200, Ease = "linear" });
        Assert.True(Pump(() => Extent(bar, Colors.Red)?.Left > 32), "the translation never started");

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
    public void UniformStaggeredTargetsEachLandOnTheirEndValue()
    {
        Color[] colors = [Colors.Red, Colors.Blue, Colors.Lime];
        var bars = colors.Select(color => new Border { Background = new SolidColorBrush(color), Width = 20, Height = 10 }).ToArray();
        var canvas = new Canvas();
        for (var i = 0; i < bars.Length; i++)
        {
            Canvas.SetLeft(bars[i], 10);
            Canvas.SetTop(bars[i], 10 + (30 * i));
            canvas.Children.Add(bars[i]);
        }
        new Window { Content = canvas, Background = Brushes.White, Width = 100, Height = 100 }.Show();
        Tick();

        var animation = PrestoAvalonia.AnimateOnCompositor(bars, new()
        {
            ["X"] = new TweenValue[] { 40, 20 }, Duration = 200, Delay = Presto.Stagger(100), Ease = "linear",
        });
        Frames.Advance(300);

        Assert.False(animation.Completed);
        Assert.InRange(animation.Progress, .4, .7);

        Frames.Advance(200);

        Assert.True(animation.Completed);
        Assert.Equal(1, animation.Progress);
        for (var i = 0; i < bars.Length; i++)
        {
            var end = new Rect(30, 10 + (30 * i), 20, 10);
            Assert.True(Pump(() => Extent(bars[i], colors[i]) == end), $"bar {i} never reached {end}");
        }
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

    [AvaloniaFact]
    public void ReduceAllLandsACompositorAnimationOnItsEndValuesAtOnce()
    {
        var bar = ShowBar();
        Presto.Defaults.ReduceAll = true;

        var animation = bar.AnimateOnCompositor(Presto.Build().X(20).Duration(1000).Delay(500).Loop(2));
        Frames.Advance(16);

        Assert.True(animation.Completed);
        Assert.True(Pump(() => Extent(bar, Colors.Red) == new Rect(50, 40, 40, 20), rounds: 4), "the reduced move did not land at once");
    }

    [AvaloniaFact]
    public void ReduceAllLeavesAReducedAlternativeOnTheCompositorAlone()
    {
        var bar = ShowBar();
        Presto.Defaults.ReducedMotion = true;
        Presto.Defaults.ReduceAll = true;

        var animation = bar.AnimateOnCompositor(Presto.Build().X(20).WhenReduced(r => r.Opacity(0.5).Duration(300).Ease("linear")));
        Frames.Advance(150);

        Assert.False(animation.Completed);
        Assert.InRange(animation.Progress, 0.3, 0.7);
    }

    [AvaloniaFact]
    public void ACompositorLoopKeepsItsFormWhenTheFlagFlips()
    {
        var bar = ShowBar();
        var animation = bar.AnimateOnCompositor(Presto.Build().X(20).Duration(300).Ease("linear").Loop(true).WhenReduced(r => r.Opacity(0.5)));
        Assert.True(Pump(() => Extent(bar, Colors.Red)?.Left > 32), "the loop never started");

        Presto.Defaults.ReducedMotion = true;
        var moved = Extent(bar, Colors.Red);

        // Only the full form keeps the bar pure red while it moves.
        Assert.True(Pump(() => Extent(bar, Colors.Red) is { } now && now != moved), "the loop left its full form when the flag flipped");
        Assert.False(animation.Completed);
    }

    [AvaloniaFact]
    public void DictionaryParamsOnTheCompositorPlayTheirReducedForm()
    {
        var bar = ShowBar();
        var completed = false;
        Presto.Defaults.ReducedMotion = true;

        var animation = PrestoAvalonia.RunOnCompositor(bar, new AvaloniaAnimationParams
        {
            ["X"] = 20,
            Duration = 1000,
            OnComplete = _ => completed = true,
            WhenReduced = new() { [Visual.OpacityProperty] = 0.5, Duration = 300, Ease = "linear" },
        });
        Frames.Advance(150);
        Assert.InRange(animation.Progress, 0.3, 0.7);

        Frames.Advance(300);
        Assert.True(completed);
        Assert.Equal(0.5, bar.Opacity);
    }
}
