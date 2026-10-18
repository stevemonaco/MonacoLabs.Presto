namespace MonacoLabs.Presto.Tests;

public sealed class MotionValueDynamicsTests : EngineTestBase
{
    [Fact]
    public void ASpringSettlesExactlyOnItsTargetAndTheEngineSleeps()
    {
        var source = Presto.CreateMotionValue();
        var follower = source.Spring(new Spring(stiffness: 120, damping: 12));

        source.Set(100);
        Frames.Advance(160, 16);
        Assert.InRange(follower.Value, 1, 99);
        RunUntil(() => follower.Value == 100);
        Frames.Advance(16);
        Frames.Advance(16);

        Assert.Equal(100, follower.Value);
        Assert.False(Frames.IsRunning);
    }

    [Fact]
    public void ASpringOverADerivedValueFollowsItsSource()
    {
        var source = Presto.CreateMotionValue();
        var follower = source.Map(v => v * 2).Spring(new Spring(stiffness: 120, damping: 12));

        source.Set(50);
        RunUntil(() => follower.Value == 100);

        Assert.Equal(100, follower.Value);
    }

    [Fact]
    public void ASourceJumpMidFlightKeepsTheSpringsVelocity()
    {
        var source = Presto.CreateMotionValue();
        var follower = source.Spring(new Spring(stiffness: 120, damping: 12));
        source.Set(100);
        Frames.Advance(144, 16);
        var before = follower.Value;
        Frames.Advance(16);
        var turnedAt = follower.Value;

        source.Set(0);
        Frames.Advance(16);

        var stepBefore = turnedAt - before;
        var stepAfter = follower.Value - turnedAt;
        Assert.True(stepBefore > 1, $"moving {stepBefore} per frame before the jump");
        Assert.Equal(stepBefore, stepAfter, stepBefore * .5);
    }

    [Fact]
    public void ASpringIsFrameRateIndependent()
    {
        var slow = Follow(1000 / 30d);
        var fast = Follow(1000 / 120d);

        Assert.Equal(slow, fast, .01);

        double Follow(double step)
        {
            var source = Presto.CreateMotionValue();
            var follower = source.Spring(new Spring(stiffness: 120, damping: 8));
            source.Set(100);
            Frames.Advance(400, step);
            return follower.Value;
        }
    }

    [Fact]
    public void VelocityAndVelocityOfMatchAVelocityTrackerFedTheSameSamples()
    {
        var source = Presto.CreateMotionValue();
        Assert.Equal(0, source.Velocity);
        var velocity = source.VelocityOf();
        var reference = new VelocityTracker();
        reference.Add(0, Frames.Now);

        for (var i = 1; i <= 30; i++)
        {
            if (i <= 20) source.Set(i * i * .5);
            Frames.Advance(16);
            reference.Add(source.Value, Frames.Now);
            Assert.Equal(reference.Velocity, source.Velocity, 9);
            Assert.Equal(reference.Velocity, velocity.Value, 9);
        }
        Frames.Advance(16);

        Assert.Equal(0, source.Velocity);
        Assert.Equal(0, velocity.Value);
        Assert.False(Frames.IsRunning);
    }

    [Fact]
    public void ProgressValueTracksAPlayingTimer()
    {
        var timer = Presto.CreateTimer(new() { Duration = 1000 });
        var progress = timer.ProgressValue;

        Frames.Advance(480, 16);

        Assert.InRange(progress.Value, .4, .6);
        Assert.Equal(timer.Progress, progress.Value);
        RunUntil(() => timer.Completed);
        Assert.Equal(1, progress.Value);
    }

    [Fact]
    public void AScrubbedAnimationSeeksToValueTimesDuration()
    {
        var target = new TestObject();
        var animation = Presto.Animate(target, new() { ["PlainValue"] = 110, Duration = 1000, Ease = "linear" });
        var source = Presto.CreateMotionValue(.25);

        animation.Scrub(source);
        Assert.True(animation.Paused);
        Assert.Equal(250, animation.CurrentTime);

        source.Set(1);
        Frames.Advance(16);
        Frames.Advance(16);
        Assert.Equal(110, target.PlainValue);

        source.Set(.5);
        Frames.Advance(16);
        Assert.Equal(500, animation.CurrentTime);
        Assert.Equal(60, target.PlainValue);
    }

    [Fact]
    public void AScrubbedTimelineSeeksToValueTimesDuration()
    {
        var a = new TestObject();
        var b = new TestObject();
        var timeline = Presto.CreateTimeline()
            .Add(a, new() { ["PlainValue"] = 110, Duration = 500, Ease = "linear" })
            .Add(b, new() { ["PlainValue"] = 110, Duration = 500, Ease = "linear" });
        var source = Presto.CreateMotionValue();
        timeline.Scrub(source);

        source.Set(.75);
        Frames.Advance(16);

        Assert.Equal(750, timeline.CurrentTime);
        Assert.Equal(110, a.PlainValue);
        Assert.Equal(60, b.PlainValue);
    }

    [Fact]
    public void ScrubNullAndRevertEndTheScrub()
    {
        var target = new TestObject();
        var animation = Presto.Animate(target, new() { ["PlainValue"] = 110, Duration = 1000, Ease = "linear" });
        var source = Presto.CreateMotionValue(.5);
        animation.Scrub(source);

        animation.Scrub(null);
        source.Set(.8);
        Frames.Advance(16);
        Assert.Equal(500, animation.CurrentTime);

        animation.Scrub(source);
        Assert.Equal(800, animation.CurrentTime);
        animation.Revert();
        source.Set(.2);
        Frames.Advance(16);

        Assert.Equal(0, animation.CurrentTime);
        Assert.Equal(10, target.PlainValue);
    }

    [Fact]
    public void AnimatableValueOfFollowsSet()
    {
        var animatable = Presto.CreateAnimatable(new StyleTarget(), new() { ["X"] = 200, Ease = "linear" });
        var x = animatable.ValueOf("X");

        animatable.Set("X", 100);
        Frames.Advance(96, 16);

        Assert.InRange(x.Value, 1, 99);
        Assert.Equal(animatable.Get("X"), x.Value);
        RunUntil(() => animatable.Animations["X"].Paused);
        Assert.Equal(100, x.Value);
    }

    [Fact]
    public void AnimatableValueOfReturnsOneValuePerPropertySubscribedOnce()
    {
        var animatable = Presto.CreateAnimatable(new StyleTarget(), new() { ["X"] = 200, Ease = "linear" });
        var x = animatable.ValueOf("X");
        var subscribers = animatable.Animations["X"]._onRender!.GetInvocationList().Length;

        for (var i = 0; i < 10; i++) Assert.Same(x, animatable.ValueOf("X"));

        Assert.Equal(subscribers, animatable.Animations["X"]._onRender!.GetInvocationList().Length);
    }

    [Fact]
    public void ALagReplaysItsSourceDelayLateAtAnyFrameRate()
    {
        double[] expected = [150, 1000 / 3d - 100, 300];

        var at60 = Replay(1000 / 60d);
        var at144 = Replay(1000 / 144d);

        for (var i = 0; i < expected.Length; i++)
        {
            Assert.Equal(expected[i], at60[i], 1e-6);
            Assert.Equal(expected[i], at144[i], 1e-6);
        }

        double[] Replay(double step)
        {
            var start = Frames.Now;
            var source = Presto.CreateMotionValue();
            var lag = source.Lag(100);
            Presto.CreateTimer(new() { Duration = 400, OnUpdate = _ => source.Set(Math.Min(Presto.Engine.Now - start, 300)) });
            var readings = new double[expected.Length];
            double[] times = [250, 1000 / 3d, 500];
            for (var i = 0; i < times.Length; i++)
            {
                while (Frames.Now - start < times[i] - step / 2) Frames.Advance(step);
                readings[i] = lag.Value;
            }
            return readings;
        }
    }

    [Fact]
    public void ALagHoldsItsInitialValueForTheFirstDelay()
    {
        var source = Presto.CreateMotionValue(5);
        var lag = source.Lag(100);

        source.Set(50);
        for (var i = 0; i < 6; i++)
        {
            Frames.Advance(16);
            Assert.Equal(5, lag.Value);
        }
        RunUntil(() => lag.Value == 50, max: 200);

        Assert.Equal(50, lag.Value);
    }

    [Fact]
    public void ALagInterpolatesBetweenUnevenFrames()
    {
        var source = Presto.CreateMotionValue();
        var lag = source.Lag(50);
        Frames.Advance(10);
        source.Set(100);
        Frames.Advance(30);

        Frames.Advance(25);
        Assert.Equal(100 * 5 / 30d, lag.Value, 1e-9);
        Frames.Advance(13);
        Assert.Equal(60, lag.Value, 1e-9);
        source.Set(40);
        Frames.Advance(7);
        Assert.Equal(100 * 25 / 30d, lag.Value, 1e-9);
        Frames.Advance(40);
        Assert.Equal(100 - 60 * 35 / 45d, lag.Value, 1e-9);
    }

    [Fact]
    public void ALagAnimatesUntilCaughtUpThenSleepsAndTheEngineIdles()
    {
        var source = Presto.CreateMotionValue();
        var lag = source.Lag(100);
        Assert.False(lag.IsAnimating);

        source.Set(10);
        Frames.Advance(16);
        Assert.True(lag.IsAnimating);
        RunUntil(() => lag.Value == 10);
        Frames.Advance(16);

        Assert.False(lag.IsAnimating);
        Assert.Empty(Presto.Engine.Diagnostics.ActiveValues);
        Assert.False(Frames.IsRunning);
    }

    [Fact]
    public void AChangeAfterALongSleepReplaysFromNow()
    {
        var source = Presto.CreateMotionValue();
        var lag = source.Lag(100);
        source.Set(10);
        RunUntil(() => !lag.IsAnimating);
        Frames.Advance(5000);

        source.Set(20);
        Frames.Advance(80, 16);

        Assert.Equal(10, lag.Value);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ALagContinuesWithoutAJumpAfterAPauseOrHide(bool hide)
    {
        var reference = Run(null);
        var interrupted = Run(() =>
        {
            if (hide) Presto.Engine.Hidden = true;
            else Presto.Engine.Pause();
            Frames.Advance(1000);
            if (hide) Presto.Engine.Hidden = false;
            else Presto.Engine.Resume();
        });

        for (var i = 0; i < reference.Length; i++) Assert.Equal(reference[i], interrupted[i], 1e-9);

        double[] Run(Action? interrupt)
        {
            var source = Presto.CreateMotionValue();
            var lag = source.Lag(50);
            var readings = new double[16];
            for (var i = 0; i < readings.Length; i++)
            {
                if (i < 8) source.Set((i + 1) * 10);
                if (i == 6) interrupt?.Invoke();
                Frames.Advance(16);
                readings[i] = lag.Value;
            }
            return readings;
        }
    }

    [Fact]
    public void FrozenStepMovesALagByExactlyOneFrame()
    {
        var source = Presto.CreateMotionValue();
        var lag = source.Lag(32);
        source.Set(100);
        Frames.Advance(16);
        Presto.Engine.Freeze();
        Frames.Advance(1000);
        Assert.Equal(0, lag.Value);

        Presto.Engine.Step(24);

        Assert.Equal(50, lag.Value);
    }

    [Fact]
    public void SecondsModeGivesALagTheSameTrajectoryScaledInTime()
    {
        var milliseconds = Trajectory(50);
        Presto.Engine.TimeUnit = TimeUnit.Seconds;
        var seconds = Trajectory(.05);

        for (var i = 0; i < milliseconds.Length; i++) Assert.Equal(milliseconds[i], seconds[i], 1e-9);

        double[] Trajectory(double delay)
        {
            var source = Presto.CreateMotionValue();
            var lag = source.Lag(delay);
            var samples = new double[12];
            for (var i = 0; i < samples.Length; i++)
            {
                if (i < 4) source.Set(i * i * 10);
                Frames.Advance(16);
                samples[i] = lag.Value;
            }
            return samples;
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void AnInvalidLagDelayThrows(double delay) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => Presto.CreateMotionValue().Lag(delay));

    [Fact]
    public void SettingALagThrows() =>
        Assert.Throws<InvalidOperationException>(() => Presto.CreateMotionValue().Lag(100).Set(1));

    [Fact]
    public void ALagTrailElementReplaysTheSourceOneDelayLaterThanThePrevious()
    {
        var start = Frames.Now;
        var source = Presto.CreateMotionValue();
        var trail = source.Trail(3, 48);
        Presto.CreateTimer(new() { Duration = 600, OnUpdate = _ => source.Set(Math.Min(Presto.Engine.Now - start, 300)) });

        for (var frame = 0; frame < 30; frame++)
        {
            Frames.Advance(16);
            var elapsed = Frames.Now - start;
            for (var i = 0; i < trail.Length; i++)
            {
                Assert.Equal(Math.Clamp(elapsed - (i + 1) * 48, 0, 300), trail[i].Value, 1e-6);
            }
        }
    }

    [Fact]
    public void ASpringTrailMatchesHandChainedSpringsFrameForFrame()
    {
        var spring = new Spring(stiffness: 120, damping: 12);
        var source = Presto.CreateMotionValue();
        var trail = source.Trail(3, spring);
        var chained = Presto.CreateMotionValue();
        var first = chained.Spring(spring);
        var second = first.Spring(spring);
        MotionValue[] expected = [first, second, second.Spring(spring)];

        source.Set(100);
        chained.Set(100);
        for (var frame = 0; frame < 40; frame++)
        {
            Frames.Advance(16);
            for (var i = 0; i < trail.Length; i++) Assert.Equal(expected[i].Value, trail[i].Value);
        }
        Assert.NotEqual(0, trail[2].Value);
    }

    [Fact]
    public void ATrailOfFewerThanOneThrows()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Presto.CreateMotionValue().Trail(0, new Spring()));
        Assert.Throws<ArgumentOutOfRangeException>(() => Presto.CreateMotionValue().Trail(0, 100));
    }

    [Fact]
    public void VelocityRebasesToNowWhenAMeasuredValueStartsMovingAgain()
    {
        var source = Presto.CreateMotionValue();
        Assert.Equal(0, source.Velocity);
        Frames.Advance(5000);

        source.Set(16);
        Frames.Advance(20);

        Assert.Equal(.8, source.Velocity, 9);
    }

    [Fact]
    public void ANewScrubReplacesThePreviousOne()
    {
        var animation = Presto.Animate(new TestObject(), new() { ["PlainValue"] = 110, Duration = 1000, Ease = "linear" });
        var first = Presto.CreateMotionValue(.2);
        var second = Presto.CreateMotionValue(.4);

        animation.Scrub(first);
        animation.Scrub(second);
        Assert.Equal(400, animation.CurrentTime);

        first.Set(.9);
        Frames.Advance(16);
        Assert.Equal(400, animation.CurrentTime);

        second.Set(.6);
        Frames.Advance(16);
        Assert.Equal(600, animation.CurrentTime);
    }
}
