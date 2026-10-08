using Monaco.Presto.Core;

namespace Monaco.Presto.Tests;

public sealed class SettleTests : EngineTestBase
{
    [Fact]
    public void RelativeScrubKeepsTheCurrentProgressAndAppliesDeltas()
    {
        var animation = Presto.Animate(new TestObject(), new() { ["PlainValue"] = 110, Duration = 1000, Ease = "linear" });
        animation.Seek(400);
        var source = Presto.CreateMotionValue(.9);

        animation.Scrub(source, relative: true);
        Assert.True(animation.Paused);
        Assert.Equal(.4, animation.Progress);

        source.Set(1);
        Frames.Advance(16);
        Assert.Equal(.5, animation.Progress);
    }

    [Fact]
    public void RelativeScrubClampsToTheUnitRange()
    {
        var animation = Presto.Animate(new TestObject(), new() { ["PlainValue"] = 110, Duration = 1000, Ease = "linear" });
        animation.Seek(800);
        var source = Presto.CreateMotionValue(0);
        animation.Scrub(source, relative: true);

        source.Set(.5);
        Frames.Advance(16);
        Assert.Equal(1, animation.Progress);

        source.Set(.4);
        Frames.Advance(16);
        Assert.Equal(.9, animation.Progress);

        source.Set(-1);
        Frames.Advance(16);
        Assert.Equal(0, animation.Progress);

        source.Set(-.8);
        Frames.Advance(16);
        Assert.Equal(.2, animation.Progress);
    }

    [Fact]
    public void ChildTimerThrows()
    {
        var timeline = Presto.CreateTimeline().Add(new TestObject(), new() { ["PlainValue"] = 110, Duration = 500 });
        var child = timeline._head!;

        var scrub = Assert.Throws<InvalidOperationException>(() => child.Scrub(Presto.CreateMotionValue()));
        Assert.Contains("parent timeline", scrub.Message);
        var settle = Assert.Throws<InvalidOperationException>(() => { _ = child.Settle(); });
        Assert.Contains("parent timeline", settle.Message);
        child.Scrub(null);
    }

    [Fact]
    public void InvalidParamsThrow()
    {
        Assert.Throws<ArgumentException>(() => TimerSettle.Validate(new() { Points = [] }));
        Assert.Throws<ArgumentException>(() => TimerSettle.Validate(new() { Points = [0, double.NaN] }));
        Assert.Throws<ArgumentException>(() => TimerSettle.Validate(new() { Projection = -1 }));
        Assert.Throws<ArgumentException>(() => TimerSettle.Validate(new() { Projection = double.PositiveInfinity }));
        Assert.Throws<ArgumentException>(() => TimerSettle.Validate(new() { FlickVelocity = -.001 }));
        TimerSettle.Validate(new() { Points = [.5], Projection = 0, FlickVelocity = 0 });
        Assert.Throws<ArgumentException>(() => { _ = Card().Settle(new() { Points = [] }); });
    }

    [Fact]
    public void ProjectionPicksTheNearestPoint()
    {
        var defaults = new SettleParams();
        var animation = Presto.Animate(new TestObject(), new() { ["PlainValue"] = 110, Duration = 1000 });
        var points = TimerSettle.RestingPoints(animation, defaults);
        Assert.Equal([0, 1], points);
        Assert.Equal(350, defaults.Projection);
        Assert.Null(defaults.FlickVelocity);

        Assert.Equal(0, TimerSettle.Choose(.4, 0, points, defaults.Projection, null));
        Assert.Equal(1, TimerSettle.Choose(.4, .001, points, defaults.Projection, null));
        Assert.Equal(0, TimerSettle.Choose(.6, -.001, points, defaults.Projection, null));
        Assert.Equal(.5, TimerSettle.Choose(.2, .001, [0, .5, 1], defaults.Projection, null));
    }

    [Fact]
    public void FlickAdvancesToTheNextPointInTheVelocityDirection()
    {
        double[] points = [0, .5, 1];
        Assert.Equal(.5, TimerSettle.Choose(.1, .0001, points, 350, .0001));
        Assert.Equal(0, TimerSettle.Choose(.1, .0001, points, 350, .001));
        Assert.Equal(.5, TimerSettle.Choose(.9, -.0001, points, 350, .0001));
        Assert.Equal(1, TimerSettle.Choose(.5, .0001, points, 350, .0001));
        Assert.Equal(1, TimerSettle.Choose(1, .0001, points, 350, .0001));
        Assert.Equal(0, TimerSettle.Choose(0, -.0001, points, 350, .0001));
        Assert.Equal(.5, TimerSettle.Choose(.6, 0, points, 350, 0));
    }

    [Fact]
    public void LabelsBecomeRestingPointsOnATimeline()
    {
        var timeline = Presto.CreateTimeline()
            .Add(new TestObject(), new() { ["PlainValue"] = 110, Duration = 1000 })
            .Label("half", 250);

        Assert.Equal([0, 1], TimerSettle.RestingPoints(timeline, new()));
        Assert.Equal([0, .25, 1], TimerSettle.RestingPoints(timeline, new() { Labels = true }));
        Assert.Equal(.25, TimerSettle.Choose(.3, 0, TimerSettle.RestingPoints(timeline, new() { Labels = true }), 350, null));
    }

    [Fact]
    public void PointsAreClampedToTheUnitRange()
    {
        var animation = Presto.Animate(new TestObject(), new() { ["PlainValue"] = 110, Duration = 1000 });

        Assert.Equal([0, .5, 1], TimerSettle.RestingPoints(animation, new() { Points = [-2, .5, 3, 1] }));
    }

    [Fact]
    public void ReleaseVelocityCarriesIntoTheSpring()
    {
        var animation = Card(200);

        _ = animation.Settle(new() { Velocity = .002, Spring = new Spring(stiffness: 1, damping: 1) });
        Frames.Advance(16);

        Assert.Equal(.002, (animation.Progress - .2) / 16, .0001);
    }

    [Fact]
    public async Task LandsExactlyOnTheTargetAndCompletesTheTask()
    {
        var animation = Card(300);

        var task = animation.Settle(new() { Points = [0, 1 / 3d, 1] });
        Assert.False(task.IsCompleted);
        RunUntil(() => task.IsCompleted);

        Assert.Equal(1 / 3d, await task);
        Assert.Equal(1000 / 3d, animation.CurrentTime, 3);
    }

    [Fact]
    public void TrajectoryIsFrameRateIndependent()
    {
        var slow = Settle(16);
        var fast = Settle(8);

        for (var i = 0; i < slow.Length; i++) Assert.Equal(slow[i], fast[i], .001);

        double[] Settle(double step)
        {
            var animation = Card(300);
            _ = animation.Settle(new() { Velocity = .001 });
            var samples = new double[5];
            for (var i = 0; i < samples.Length; i++)
            {
                Frames.Advance(48, step);
                samples[i] = animation.Progress;
            }
            return samples;
        }
    }

    [Fact]
    public async Task ProgressIsClampedWhenTheSpringOvershoots()
    {
        var animation = Card(800);
        var task = animation.Settle(new() { Velocity = .004, Spring = new Spring(stiffness: 300, damping: 4) });
        var reachedEnd = false;
        var bouncedBack = false;

        while (!task.IsCompleted)
        {
            Frames.Advance(16);
            Assert.InRange(animation.Progress, 0, 1);
            if (animation.Progress == 1) reachedEnd = true;
            else if (reachedEnd) bouncedBack = true;
        }

        Assert.True(bouncedBack);
        Assert.Equal(1, await task);
    }

    [Fact]
    public async Task UsesTheScrubSourceVelocityWhenNoneIsGiven()
    {
        var source = Presto.CreateMotionValue();
        var scrubbed = Card().Scrub(source);
        for (var i = 1; i <= 3; i++)
        {
            source.Set(i * .1);
            Frames.Advance(16);
        }
        var velocity = source.Velocity;
        Assert.True(velocity > 0);
        var given = Card(300);

        var task = scrubbed.Settle();
        _ = given.Settle(new() { Velocity = velocity });
        Frames.Advance(16);

        Assert.Equal(given.Progress, scrubbed.Progress);

        source.Set(0);
        Frames.Advance(16);
        Assert.Equal(given.Progress, scrubbed.Progress);
        RunUntil(() => task.IsCompleted);
        Assert.Equal(1, await task);
    }

    [Fact]
    public async Task UnscrubbedTimerSettlesWithZeroVelocity()
    {
        var animation = Card(400);

        var task = animation.Settle();
        Frames.Advance(16);
        Assert.InRange(animation.Progress, 0, .4);
        RunUntil(() => task.IsCompleted);

        Assert.Equal(0, await task);
    }

    [Fact]
    public void LandingOnOneCompletesAndResolvesThen()
    {
        var completions = 0;
        var animation = Presto.Animate(new TestObject(), new() { ["PlainValue"] = 110, Duration = 1000, OnComplete = _ => completions++ });
        animation.CurrentTime = 800;
        var then = ((Timer)animation).Then();

        var task = animation.Settle();
        RunUntil(() => task.IsCompleted);

        Assert.True(animation.Completed);
        Assert.Equal(1, completions);
        Assert.True(then.IsCompleted);
    }

    [Fact]
    public async Task LandingOnZeroFiresNoCompletion()
    {
        var completions = 0;
        var animation = Presto.Animate(new TestObject(), new() { ["PlainValue"] = 110, Duration = 1000, OnComplete = _ => completions++ });
        animation.CurrentTime = 200;
        var then = ((Timer)animation).Then();

        var task = animation.Settle();
        RunUntil(() => task.IsCompleted);

        Assert.Equal(0, await task);
        Assert.False(animation.Completed);
        Assert.Equal(0, completions);
        Assert.False(then.IsCompleted);
    }

    [Fact]
    public async Task ASecondSettleReplacesTheFirst()
    {
        var animation = Card(400);
        var first = animation.Settle();
        Frames.Advance(48, 16);
        var progress = animation.Progress;

        var second = animation.Settle(new() { Points = [.5] });

        Assert.True(first.IsCompleted);
        Assert.Equal(progress, await first);
        RunUntil(() => second.IsCompleted);
        Assert.Equal(.5, await second);
        Assert.Equal(.5, animation.Progress);
    }

    [Fact]
    public void InfiniteTimerThrows()
    {
        var timer = Presto.CreateTimer(new());

        Assert.Throws<InvalidOperationException>(() => { _ = timer.Settle(); });
    }

    [Fact]
    public void EngineSleepsOnceTheSettleRests()
    {
        var animation = Card(300);

        var task = animation.Settle();
        Frames.Advance(16);
        Assert.True(Frames.IsRunning);
        RunUntil(() => task.IsCompleted);
        Frames.Advance(16);
        Frames.Advance(16);

        Assert.False(Frames.IsRunning);
    }

    [Theory]
    [InlineData("Scrub")]
    [InlineData("ScrubNull")]
    [InlineData("Seek")]
    [InlineData("Play")]
    [InlineData("Resume")]
    [InlineData("Reverse")]
    [InlineData("Restart")]
    [InlineData("Pause")]
    [InlineData("Cancel")]
    [InlineData("Revert")]
    [InlineData("Complete")]
    public async Task APlayheadControlEndsTheSettleWithTheCurrentProgress(string control)
    {
        var animation = Card(400);
        var task = animation.Settle();
        Frames.Advance(48, 16);
        var progress = animation.Progress;

        switch (control)
        {
            case "Scrub": _ = animation.Scrub(Presto.CreateMotionValue(.5)); break;
            case "ScrubNull": _ = animation.Scrub(null); break;
            case "Seek": _ = animation.Seek(500); break;
            case "Play": _ = animation.Play(); break;
            case "Resume": _ = animation.Resume(); break;
            case "Reverse": _ = animation.Reverse(); break;
            case "Restart": _ = animation.Restart(); break;
            case "Pause": _ = animation.Pause(); break;
            case "Cancel": _ = animation.Cancel(); break;
            case "Revert": _ = animation.Revert(); break;
            case "Complete": _ = animation.Complete(); break;
        }

        Assert.True(task.IsCompleted);
        Assert.Equal(progress, await task);
        _ = animation.Pause();
        var after = animation.Progress;
        Frames.Advance(160, 16);
        Assert.Equal(after, animation.Progress);
    }

    [Fact]
    public void RegrabMidSettleWithARelativeScrubDoesNotJump()
    {
        var animation = Card(400);
        _ = animation.Settle();
        Frames.Advance(48, 16);
        var progress = animation.Progress;
        var drag = Presto.CreateMotionValue(.9);

        animation.Scrub(drag, relative: true);
        Frames.Advance(48, 16);
        Assert.Equal(progress, animation.Progress);

        drag.Set(1);
        Frames.Advance(16);
        Assert.Equal(progress + .1, animation.Progress, 1e-9);
    }

    [Fact]
    public void ResumesWithoutAJumpAfterAPause()
    {
        var reference = Card(300);
        _ = reference.Settle(new() { Velocity = .001 });
        Frames.Advance(64, 16);
        var expected = reference.Progress;

        var animation = Card(300);
        _ = animation.Settle(new() { Velocity = .001 });
        Frames.Advance(48, 16);
        Presto.Engine.Hidden = true;
        Frames.Advance(5000);
        Presto.Engine.Hidden = false;
        Frames.Advance(16);

        Assert.Equal(expected, animation.Progress, 1e-9);
    }

    [Fact]
    public void StepMovesTheSettleByExactlyOneFrame()
    {
        var reference = Card(300);
        _ = reference.Settle(new() { Velocity = .001 });
        Frames.Advance(16);
        var expected = reference.Progress;

        Presto.Engine.Freeze();
        var animation = Card(300);
        _ = animation.Settle(new() { Velocity = .001 });
        Presto.Engine.Step(16);

        Assert.Equal(expected, animation.Progress, 1e-9);
    }

    [Fact]
    public void SecondsModeGivesTheSameTrajectoryScaledInTime()
    {
        var milliseconds = Trajectory(1000, .001);
        Presto.Engine.TimeUnit = TimeUnit.Seconds;
        var seconds = Trajectory(1, 1);

        for (var i = 0; i < milliseconds.Length; i++) Assert.Equal(milliseconds[i], seconds[i], 1e-6);

        double[] Trajectory(double duration, double velocity)
        {
            var animation = Presto.Animate(new TestObject(), new() { ["PlainValue"] = 110, Duration = duration, Ease = "linear" });
            animation.Seek(duration * .3);
            // Springs.Gentle's 500 is read in the current time unit, so the default spring is not unit-neutral.
            _ = animation.Settle(new() { Velocity = velocity, Spring = new Spring(stiffness: 100, damping: 20) });
            var samples = new double[5];
            for (var i = 0; i < samples.Length; i++)
            {
                Frames.Advance(48, 16);
                samples[i] = animation.Progress;
            }
            return samples;
        }
    }

    [Fact]
    public async Task ReducedMotionLandsOnTheChosenTargetAtOnce()
    {
        Presto.Defaults.ReducedMotion = true;
        var animation = Card(100);

        var task = animation.Settle(new() { Points = [0, .5, 1], Velocity = .0001, FlickVelocity = .0001 });

        Assert.True(task.IsCompleted);
        Assert.Equal(.5, await task);
        Assert.Equal(.5, animation.Progress);
        Frames.Advance(160, 16);
        Assert.Equal(.5, animation.Progress);
    }

    private static Animation Card(double time = 0)
    {
        var animation = Presto.Animate(new TestObject(), new() { ["PlainValue"] = 110, Duration = 1000, Ease = "linear" });
        animation.Seek(time);
        return animation;
    }
}
