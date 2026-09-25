using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Monaco.Presto.AvaloniaBridge.Compositor;

namespace Monaco.Presto.AvaloniaBridge.Tests;

/// <summary>
/// Covers the compile half of COMPOSITOR-PLAN §4.3. tests/suites/waapi.test.js is DOM and WAAPI only and has no portable
/// counterpart, so these assert the same behaviour against the compiled key frame tracks instead.
/// </summary>
public sealed class CompositorCompileTests : BridgeTestBase
{
    [AvaloniaFact]
    public void XAndYCompileIntoOneSampledTranslationTrack()
    {
        var track = Assert.Single(Compile(new Border(), new() { ["X"] = 100, ["Y"] = 200, Duration = 400, Ease = Eases.InOutQuad }));

        Assert.Equal(CompositorTrackKind.Translation, track.Kind);
        foreach (var progress in new[] { 0, .25, .5, 1 })
        {
            var frame = (int)(progress * (track.Keys.Length - 1));
            Assert.Equal(progress, track.Keys[frame], 1e-3);
            Assert.Equal(100 * Eases.InOutQuad(progress), track.Values[frame][0], 1e-3);
            Assert.Equal(200 * Eases.InOutQuad(progress), track.Values[frame][1], 1e-3);
            Assert.Equal(0d, track.Values[frame][2]);
        }
    }

    [AvaloniaFact]
    public void ASinglePlainTweenCompilesToTwoExactKeyFrames()
    {
        var track = Assert.Single(Compile(new Border(), new() { ["X"] = 100, Duration = 400, Ease = Eases.InOutQuad }));

        Assert.Equal(2, track.Keys.Length);
        Assert.Equal(0f, track.Keys[0]);
        Assert.Equal(1f, track.Keys[1]);
        Assert.Equal(0d, track.Values[0][0]);
        Assert.Equal(100d, track.Values[1][0]);
        Assert.IsType<LinearEasing>(track.Easings[0]);
        var easing = Assert.IsType<PrestoEasing>(track.Easings[1]);
        Assert.Equal(Eases.InOutQuad(.3), easing.Ease(.3), 1e-9);
    }

    [AvaloniaFact]
    public void ASpringEaseCompilesWithoutTheSamplingCompletingIt()
    {
        var spring = new Spring();

        var track = Assert.Single(Compile(new Border(), new() { ["X"] = 100, Ease = spring }));

        Assert.Equal(2, track.Keys.Length);
        var easing = Assert.IsType<PrestoEasing>(track.Easings[1]);
        for (var i = 0; i < 200; i++) easing.Ease(i / 199d);
        Assert.False(spring.Completed);
    }

    [AvaloniaTheory]
    [InlineData(0, 1)]
    [InlineData(1, 0)]
    public void AnOvershootingEaseCompilesOpacityInsideZeroToOne(double from, double to)
    {
        var track = Assert.Single(Compile(new Border { Opacity = from }, new() { ["Opacity"] = to, Duration = 400, Ease = Eases.OutBack() }));

        Assert.All(track.Values, value => Assert.InRange(value[0], 0, 1));
        Assert.Equal(to, track.Values[^1][0]);
        // An eased segment would overshoot between its two key frames on the server.
        Assert.All(track.Easings, easing => Assert.IsType<LinearEasing>(easing));
    }

    [AvaloniaFact]
    public void RotateCompilesToRadians()
    {
        var track = Assert.Single(Compile(new Border(), new() { ["Rotate"] = 360, Duration = 400 }));

        Assert.Equal(CompositorTrackKind.RotationAngle, track.Kind);
        Assert.Equal(2 * Math.PI, track.Values[^1][0], 1e-9);
    }

    [AvaloniaFact]
    public void AnAngleUnitConvertsTheSameWayItDoesOnTheUIThread()
    {
        var track = Assert.Single(Compile(new Border(), new() { ["Rotate"] = "1turn", Duration = 400 }));

        Assert.Equal(2 * Math.PI, track.Values[^1][0], 1e-9);
    }

    [AvaloniaFact]
    public void TheUniformScaleMultipliesTheAxisScales()
    {
        var track = Assert.Single(Compile(new Border(), new() { ["Scale"] = 2, ["ScaleX"] = 3, Duration = 400 }));

        Assert.Equal(CompositorTrackKind.Scale, track.Kind);
        Assert.Equal(new double[] { 6, 2, 1 }, track.Values[^1]);
    }

    [AvaloniaFact]
    public void LoopMapsToTheIterationCount()
    {
        var counted = Assert.Single(Compile(new Border(), new() { ["X"] = 100, Duration = 400, Loop = 2 }));
        var forever = Assert.Single(Compile(new Border(), new() { ["X"] = 100, Duration = 400, Loop = true }));

        Assert.Equal(3, counted.IterationCount);
        Assert.False(counted.Forever);
        Assert.True(forever.Forever);
    }

    [AvaloniaFact]
    public void AlternateAndReversedMapToThePlaybackDirection()
    {
        var track = Assert.Single(Compile(new Border(), new() { ["X"] = 100, Duration = 400, Loop = 3, Alternate = true, Reversed = true }));

        Assert.Equal(PlaybackDirection.AlternateReverse, track.Direction);
        Assert.Equal(0, track.Values[0][0], 1e-6);
        Assert.Equal(100, track.Values[^1][0], 1e-6);
    }

    [AvaloniaFact]
    public void ALoopDelayHoldsTheEndValueForTheRestOfTheCycle()
    {
        var track = Assert.Single(Compile(new Border(), new() { ["X"] = 100, Duration = 400, Loop = 2, LoopDelay = 200 }));

        Assert.Equal(TimeSpan.FromMilliseconds(600), track.Duration);
        Assert.Equal(400d / 600, track.Keys[^2], 1e-6);
        Assert.Equal(1f, track.Keys[^1]);
        Assert.Equal(track.Values[^2], track.Values[^1]);
    }

    [AvaloniaFact]
    public void AnAlternatingLoopDelayExpandsIntoATwoIterationCycle()
    {
        var track = Assert.Single(Compile(new Border(), new() { ["X"] = 100, Duration = 400, Loop = 3, LoopDelay = 200, Alternate = true }));

        Assert.Equal(TimeSpan.FromMilliseconds(1200), track.Duration);
        Assert.Equal(2, track.IterationCount);
        Assert.Equal(PlaybackDirection.Normal, track.Direction);
        Assert.Equal(.5, track.Keys[track.Keys.Length / 2], 1e-6);
        Assert.Equal(100, track.Values[track.Keys.Length / 2][0], 1e-6);
        Assert.Equal(0, track.Values[^1][0], 1e-6);
    }

    [AvaloniaFact]
    public void DelayBecomesTheTrackDelayAndLeavesTheFromValueOnKeyFrameZero()
    {
        var track = Assert.Single(Compile(new Border(), new() { ["X"] = new TweenParams { From = 20, To = 100 }, Duration = 400, Delay = 200 }));

        Assert.Equal(TimeSpan.FromMilliseconds(200), track.Delay);
        Assert.Equal(0f, track.Keys[0]);
        Assert.Equal(20d, track.Values[0][0]);
    }

    [AvaloniaFact]
    public void AStaggeredDelayResolvesPerTarget()
    {
        object[] targets = [new Border(), new Border()];
        var parameters = new CompositorAnimationParams { ["X"] = 100, Duration = 400, Delay = Presto.Stagger(100) };

        var first = CompositorCompiler.Compile(0, targets, parameters).Tracks;
        var second = CompositorCompiler.Compile(1, targets, parameters).Tracks;

        Assert.Equal(TimeSpan.Zero, Assert.Single(first).Delay);
        Assert.Equal(TimeSpan.FromMilliseconds(100), Assert.Single(second).Delay);
    }

    [AvaloniaTheory]
    [InlineData("Width")]
    [InlineData("Background")]
    [InlineData("SkewX")]
    public void APropertyWithNoCompositionCounterpartThrows(string property)
    {
        var exception = Assert.Throws<NotSupportedException>(() => Compile(new Border(), new() { [property] = 10, Duration = 400 }));

        Assert.Contains("X, Y, Rotate, Scale, ScaleX, ScaleY and Opacity", exception.Message);
    }

    private static CompiledTrack[] Compile(Visual visual, CompositorAnimationParams parameters) =>
        CompositorCompiler.Compile(0, [visual], parameters).Tracks;
}
