using Monaco.Presto.Core;

namespace Monaco.Presto.Tests;

/// <summary>An <see cref="Inertia"/> to-value resolved through real animations (INERTIA-PLAN §5), with no upstream equivalent.</summary>
public sealed class InertiaTests : EngineTestBase
{
    private List<double> Record(Inertia inertia)
    {
        var target = new StyleTarget();
        var animation = Presto.Animate(target, new() { ["X"] = inertia });
        var values = new List<double>();
        for (var i = 0; i < 1000 && !animation.Completed; i++)
        {
            Frames.Advance(16);
            values.Add(target.X);
        }
        return values;
    }

    [Fact]
    public void FrameByFrameValuesAreIdenticalAcrossRuns()
    {
        var inertia = new Inertia(2) { Max = 300 };
        Assert.Equal(Record(inertia), Record(inertia));
    }

    [Fact]
    public async Task ThenCompletesAtTheResolvedDuration()
    {
        var inertia = new Inertia(1.5);
        var target = new StyleTarget();
        var animation = Presto.Animate(target, new() { ["X"] = inertia, Duration = 100 });
        var task = animation.Then();

        RunUntil(() => task.IsCompleted, max: 5000);
        await task;

        var plan = InertiaSolver.Resolve(0, inertia.Velocity, inertia);
        Assert.Equal(plan.Duration, animation.Duration, 1e-9);
        Assert.Equal(plan.Duration, animation.CurrentTime, 1e-9);
        Assert.Equal(plan.To, target.X);
    }

    [Fact]
    public void RevertRestoresTheStart()
    {
        var target = new StyleTarget { X = 20 };
        var animation = Presto.Animate(target, new() { ["X"] = new Inertia(1) });
        for (var i = 0; i < 5; i++) Frames.Advance(16);
        Assert.NotEqual(20, target.X);

        animation.Revert();

        Assert.Equal(20, target.X);
    }

    [Theory]
    [InlineData(100)]
    [InlineData(600)]
    [InlineData(1200)]
    public void SeekMidFlightReproducesTheTrajectory(double time)
    {
        var inertia = new Inertia(2) { Max = 400, ModifyTarget = Utils.Snap(100) };
        var target = new StyleTarget { X = 50 };
        var animation = Presto.Animate(target, new() { ["X"] = inertia, Autoplay = false });

        animation.Seek(time);

        var plan = InertiaSolver.Resolve(50, inertia.Velocity, inertia);
        Assert.Equal(50 + (plan.To - 50) * plan.Ease(time / plan.Duration), target.X, 1e-3);
    }

    [Fact]
    public void SecondsModeGivesTheSameTrajectoryScaled()
    {
        double[] times = [100, 400, 900, 1500];
        var millis = new StyleTarget();
        var inMillis = Presto.Animate(millis, new() { ["X"] = new Inertia(2) { Max = 300 }, Autoplay = false });
        var expected = times.Select(t => { inMillis.Seek(t); return millis.X; }).ToArray();

        Presto.Engine.TimeUnit = TimeUnit.Seconds;
        var seconds = new StyleTarget();
        var inSeconds = Presto.Animate(seconds, new() { ["X"] = new Inertia(2000) { Max = 300, TimeConstant = .35 }, Autoplay = false });
        var actual = times.Select(t => { inSeconds.Seek(t / 1000); return seconds.X; }).ToArray();

        Assert.Equal(inMillis.Duration / 1000, inSeconds.Duration, 1e-9);
        for (var i = 0; i < times.Length; i++) Assert.Equal(expected[i], actual[i], 1e-3);
    }

    [Fact]
    public void AFunctionValueReturningPerTargetInertiasStaggers()
    {
        var targets = new[] { new StyleTarget(), new StyleTarget(), new StyleTarget() };
        var animation = Presto.Animate(targets, new()
        {
            ["X"] = (FunctionValue)((_, i, _) => new Inertia(i + 1)),
            Delay = Presto.Stagger(100),
            Autoplay = false,
        });

        for (var i = 0; i < targets.Length; i++)
        {
            var tween = TestHelpers.GetTweenAtIndex(animation, i)!;
            var plan = InertiaSolver.Resolve(0, i + 1, new Inertia(i + 1));
            Assert.Equal(plan.To, tween._toNumber);
            Assert.Equal(i * 100, tween._startTime);
            Assert.Equal(plan.Duration, tween._updateDuration, 1e-9);
        }

        animation.Complete();
        for (var i = 0; i < targets.Length; i++) Assert.Equal((i + 1) * 350, targets[i].X, 1e-9);
    }

    [Fact]
    public void ReduceAllLandsOnTheSnappedClampedTargetInOneFrame()
    {
        Presto.Defaults.ReduceAll = true;
        var target = new StyleTarget();

        var animation = Presto.Animate(target, new() { ["X"] = new Inertia(1.3) { ModifyTarget = Utils.Snap(100), Max = 450 } });
        Frames.Advance(16);

        Assert.Equal(450, target.X);
        Assert.Equal(Constants.MinValue, animation.Duration);
        Assert.True(animation.Completed);
    }

    [Fact]
    public void AColorPropertyThrows() =>
        Assert.Throws<ArgumentException>(() => Presto.Animate(new StyleTarget(), new() { ["BackgroundColor"] = new Inertia(1) }));

    [Fact]
    public void AnInertiaInAKeyframeArrayThrows()
    {
        Assert.Throws<ArgumentException>(() => Presto.Animate(new StyleTarget(), new() { ["X"] = TweenValue.Keyframes(0, new Inertia(1), 50) }));
        Assert.Throws<ArgumentException>(() => Presto.Animate(new StyleTarget(), new()
        {
            ["X"] = new TweenParams[] { new() { To = 50 }, new() { To = new Inertia(1) } },
        }));
    }

    [Fact]
    public void BoundsSettleOnTheBound()
    {
        var target = new StyleTarget();
        var animation = Presto.Animate(target, new() { ["X"] = new Inertia(2) { Max = 300 } });
        var peak = 0.0;

        RunUntil(() =>
        {
            peak = Math.Max(peak, target.X);
            return animation.Completed;
        });

        Assert.True(peak > 300);
        Assert.Equal(300, target.X);
        Assert.Equal(300, TestHelpers.GetTweenAtIndex(animation, 0)!._toNumber);
    }

    [Fact]
    public void AReleaseOnABoundMovingOutwardGoesOutAndBack()
    {
        var target = new StyleTarget { X = 300 };
        var animation = Presto.Animate(target, new() { ["X"] = new Inertia(1) { Max = 300 } });
        var peak = 0.0;

        RunUntil(() =>
        {
            peak = Math.Max(peak, target.X);
            return animation.Completed;
        });

        Assert.Equal(2, TestHelpers.GetTweenCount(animation));
        Assert.True(peak > 300);
        Assert.Equal(300, target.X);
    }
}
