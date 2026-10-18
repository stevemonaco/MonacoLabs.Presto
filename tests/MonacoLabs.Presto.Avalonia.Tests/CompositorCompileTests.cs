using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using MonacoLabs.Presto.Compositor;

namespace MonacoLabs.Presto.Tests;

/// <summary>
/// Covers the compile half of COMPOSITOR-PLAN §4.3. tests/suites/waapi.test.js is DOM and WAAPI only and has no portable
/// counterpart, so these assert the same behavior against the compiled key frame tracks instead.
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

    [AvaloniaFact]
    public void AnInertiaCompilesToTheCurveTheUIThreadPlays()
    {
        var inertia = new Inertia(1) { Max = 100 };
        var track = Assert.Single(Compile(new Border(), new() { ["X"] = inertia }));
        var border = new Border();
        var animation = Presto.Animate(border, new AnimationParams { ["X"] = inertia, Autoplay = false });

        Assert.Equal(animation.Duration, track.Duration.TotalMilliseconds, 1e-3);
        for (var i = 0; i <= 50; i++)
        {
            var progress = i / 50d;
            animation.Seek(progress * animation.Duration);
            var k = Math.Max(1, Array.FindIndex(track.Keys, key => key >= progress));
            var local = (progress - track.Keys[k - 1]) / (track.Keys[k] - track.Keys[k - 1]);
            var sampled = track.Values[k - 1][0] + ((track.Values[k][0] - track.Values[k - 1][0]) * track.Easings[k].Ease(local));
            Assert.Equal((double)PrestoAvalonia.Get(border, "X")!, sampled, 1e-6);
        }
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
    public void AReversedRunCompilesMirroredKeyFramesPlayedForward()
    {
        var exact = Assert.Single(Compile(new Border(), new() { ["X"] = 100, Duration = 400, Ease = Eases.InOutQuad, Reversed = true }));
        var sampled = Assert.Single(Compile(new Border(), new() { ["X"] = 100, ["Y"] = 50, Duration = 400, Ease = "linear", Reversed = true }));
        var alternating = Assert.Single(Compile(new Border(), new() { ["X"] = 100, Duration = 400, Loop = 3, Alternate = true, Reversed = true }));

        Assert.Equal(PlaybackDirection.Normal, exact.Direction);
        Assert.Equal(new[] { 0f, 1f }, exact.Keys);
        Assert.Equal(100d, exact.Values[0][0]);
        Assert.Equal(0d, exact.Values[1][0]);
        Assert.Equal(1 - Eases.InOutQuad(.7), Assert.IsType<PrestoEasing>(exact.Easings[1]).Ease(.3), 1e-9);

        Assert.Equal(PlaybackDirection.Normal, sampled.Direction);
        Assert.Equal(sampled.Keys.Order(), sampled.Keys);
        Assert.Equal(100, sampled.Values[0][0], 1e-6);
        Assert.Equal(25, sampled.Values[sampled.Values.Length / 2][1], 1e-6);
        Assert.Equal(0, sampled.Values[^1][0], 1e-6);

        Assert.Equal(PlaybackDirection.Alternate, alternating.Direction);
        Assert.Equal(100, alternating.Values[0][0], 1e-6);
        Assert.Equal(0, alternating.Values[^1][0], 1e-6);
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

    [AvaloniaTheory]
    [InlineData(2, 1600, 100)]
    [InlineData(1, 1000, 0)]
    public void AFiniteAlternatingLoopDelayUnrollsIntoOneTrackEndingWithTheMirror(int loop, double duration, double end)
    {
        var (mirror, tracks) = CompositorCompiler.Compile(0, [new Border()], new() { ["X"] = 100, Duration = 400, Loop = loop, LoopDelay = 200, Alternate = true });
        var track = Assert.Single(tracks);

        Assert.Equal(duration, mirror.Duration);
        Assert.Equal(TimeSpan.FromMilliseconds(duration), track.Duration);
        Assert.Equal(1, track.IterationCount);
        Assert.False(track.Forever);
        Assert.Equal(PlaybackDirection.Normal, track.Direction);
        Assert.Equal(100, track.Values[(int)Math.Round(500 / duration * (track.Keys.Length - 1))][0], 1e-6);
        Assert.Equal(end, track.Values[^1][0], 1e-6);
    }

    [AvaloniaFact]
    public void AnAlternatingLoopDelayForeverPlaysForwardAndBackCycles()
    {
        var track = Assert.Single(Compile(new Border(), new() { ["X"] = 100, Duration = 400, Loop = true, LoopDelay = 200, Alternate = true }));

        Assert.Equal(TimeSpan.FromMilliseconds(1200), track.Duration);
        Assert.True(track.Forever);
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

    [AvaloniaFact]
    public void UniformTargetsSeededAlikeShareTheirSampledTracks()
    {
        object[] targets = [new Border(), new Border(), new Border()];
        var parameters = new CompositorAnimationParams
        {
            ["X"] = new TweenValue[] { 50, 100 }, ["Opacity"] = .5, Duration = 400, Delay = Presto.Stagger(100),
        };

        var compiled = CompositorCompiler.Compile(targets, parameters);

        for (var i = 0; i < targets.Length; i++)
        {
            Assert.Same(compiled[0].Tracks[0].Values, compiled[i].Tracks[0].Values);
            Assert.Same(compiled[0].Tracks[0].Keys, compiled[i].Tracks[0].Keys);
            Assert.All(compiled[i].Tracks, track => Assert.Equal(TimeSpan.FromMilliseconds(100 * i), track.Delay));
        }
    }

    [AvaloniaFact]
    public void ATargetSeededDifferentlyGetsItsOwnTracks()
    {
        object[] targets = [new Border(), new Border { Opacity = .2 }, new Border()];
        var parameters = new CompositorAnimationParams { ["X"] = new TweenValue[] { 50, 100 }, ["Opacity"] = 1, Duration = 400 };

        var compiled = CompositorCompiler.Compile(targets, parameters);
        var alone = CompositorCompiler.Compile(1, targets, parameters).Tracks;

        Assert.Same(compiled[0].Tracks[1].Values, compiled[2].Tracks[1].Values);
        Assert.Equal(alone[1].Values, compiled[1].Tracks[1].Values);
        Assert.NotEqual(compiled[0].Tracks[1].Values, compiled[1].Tracks[1].Values);
    }

    [AvaloniaFact]
    public void PerTargetValuesCompileEachTargetOnItsOwn()
    {
        object[] targets = [new Border(), new Border(), new Border()];
        CompositorAnimationParams[] cases =
        [
            new() { ["X"] = Presto.Stagger(50), Duration = 400, Ease = Eases.OutBack() },
            new() { ["X"] = (FunctionValue)((_, i, _) => 100 * (i + 1)), Duration = 400, Ease = Eases.OutBack() },
            new() { ["X"] = new TweenParams { To = 100, Duration = Presto.Stagger(100, new() { Start = 200 }) }, Ease = Eases.OutBack() },
        ];

        foreach (var parameters in cases)
        {
            var compiled = CompositorCompiler.Compile(targets, parameters);

            Assert.NotSame(compiled[0].Tracks[0].Values, compiled[1].Tracks[0].Values);
            for (var i = 0; i < targets.Length; i++)
                Assert.Equal(CompositorCompiler.Compile(i, targets, parameters).Tracks[0].Values, compiled[i].Tracks[0].Values);
        }
    }

    [AvaloniaTheory]
    [InlineData(0, false)]
    [InlineData(2, false)]
    [InlineData(-1, false)]
    [InlineData(0, true)]
    [InlineData(2, true)]
    public void ASharedTrackLeavesItsMirrorWhereSamplingWould(int loop, bool reversed)
    {
        object[] targets = [new Border(), new Border()];
        var parameters = new CompositorAnimationParams
        {
            ["X"] = new TweenValue[] { 50, 100 },
            Duration = 400,
            Loop = loop < 0 ? true : (LoopCount)loop,
            Reversed = reversed,
        };

        var compiled = CompositorCompiler.Compile(targets, parameters);

        Assert.Same(compiled[0].Tracks[0].Values, compiled[1].Tracks[0].Values);
        Assert.Equal(compiled[0].Mirror.CurrentTime, compiled[1].Mirror.CurrentTime);
        Assert.Equal(compiled[0].Mirror.Completed, compiled[1].Mirror.Completed);
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

    [AvaloniaFact]
    public void EachTargetStartsFromItsControlsShorthandsAndOpacity()
    {
        var border = new Border { Opacity = .4 };
        PrestoAvalonia.Set(border, new() { ["X"] = 30, ["Rotate"] = 90 });

        var tracks = Compile(border, new() { ["X"] = 100, ["Rotate"] = 0, ["Opacity"] = 1, Duration = 400, Ease = "linear" });

        Assert.Equal(30, tracks.Single(t => t.Kind == CompositorTrackKind.Translation).Values[0][0], 1e-6);
        Assert.Equal(Math.PI / 2, tracks.Single(t => t.Kind == CompositorTrackKind.RotationAngle).Values[0][0], 1e-6);
        Assert.Equal(.4, tracks.Single(t => t.Kind == CompositorTrackKind.Opacity).Values[0][0], 1e-6);
    }

    [AvaloniaFact]
    public void TrackTimesAreWallClockScaledByThePlaybackRateAndTheEngineSpeed()
    {
        Presto.Engine.Speed = 2;

        var track = Assert.Single(Compile(new Border(), new() { ["X"] = 100, Duration = 800, Delay = 400, PlaybackRate = 2, Ease = "linear" }));

        Assert.Equal(TimeSpan.FromMilliseconds(200), track.Duration);
        Assert.Equal(TimeSpan.FromMilliseconds(100), track.Delay);
    }

    [AvaloniaFact]
    public void UnsetDurationDelayAndEaseComeFromTheEngineDefaults()
    {
        Presto.Defaults.Duration = 250;
        Presto.Defaults.Delay = 50;
        Presto.Defaults.Ease = Eases.InQuad;

        var track = Assert.Single(Compile(new Border(), new() { ["X"] = 100 }));

        Assert.Equal(TimeSpan.FromMilliseconds(250), track.Duration);
        Assert.Equal(TimeSpan.FromMilliseconds(50), track.Delay);
        Assert.Equal(Eases.InQuad(.3), Assert.IsType<PrestoEasing>(track.Easings[1]).Ease(.3), 1e-9);
    }

    private static CompiledTrack[] Compile(Visual visual, CompositorAnimationParams parameters) =>
        CompositorCompiler.Compile(0, [visual], parameters).Tracks;
}
