namespace Monaco.Presto.Tests;

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
}
