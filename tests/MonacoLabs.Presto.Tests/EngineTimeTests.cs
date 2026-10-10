namespace MonacoLabs.Presto.Tests;

public sealed class EngineTimeTests : EngineTestBase
{
    private static readonly Spring Bouncy = new(stiffness: 120, damping: 12);

    [Fact]
    public void AFrozenEngineIgnoresTheFrameSource()
    {
        var timer = Presto.CreateTimer(new() { Duration = 1000 });
        Frames.Advance(100, 16);
        var engine = Presto.Engine;
        var (time, now) = (timer.CurrentTime, engine.Now);

        engine.Freeze();
        Frames.Advance(500, 16);

        Assert.True(engine.Frozen);
        Assert.False(Frames.IsRunning);
        Assert.Equal(now, engine.Now);
        Assert.Equal(time, timer.CurrentTime);
    }

    [Fact]
    public void StepMovesATimerAndASpringByExactlyOneFrame()
    {
        var running = Presto.CreateMotionValue();
        var reference = running.Spring(Bouncy);
        running.Set(100);
        Frames.Advance(16);
        var oneFrame = reference.Value;

        var timer = Presto.CreateTimer(new() { Duration = 1000 });
        Frames.Advance(16);
        var before = timer.CurrentTime;

        Presto.Engine.Freeze();
        var frozen = Presto.CreateMotionValue();
        var follower = frozen.Spring(Bouncy);
        frozen.Set(100);
        Presto.Engine.Step(16);

        Assert.Equal(before + 16, timer.CurrentTime);
        Assert.Equal(oneFrame, follower.Value);
    }

    [Fact]
    public void HalfDilationHalvesTimerAndSpringProgress()
    {
        var full = Presto.CreateMotionValue();
        var reference = full.Spring(Bouncy);
        full.Set(100);
        Frames.Advance(80, 8);
        var expected = reference.Value;

        var timer = Presto.CreateTimer(new() { Duration = 1000 });
        Frames.Advance(16);
        var before = timer.CurrentTime;

        Presto.Engine.TimeDilation = .5;
        var slowed = Presto.CreateMotionValue();
        var follower = slowed.Spring(Bouncy);
        slowed.Set(100);
        Frames.Advance(160, 16);

        Assert.Equal(before + 80, timer.CurrentTime);
        Assert.Equal(expected, follower.Value, 1e-9);
    }

    [Fact]
    public void TimeDilationMustBePositive()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Presto.Engine.TimeDilation = 0);
    }

    [Fact]
    public void ThawingDoesNotJump()
    {
        var timer = Presto.CreateTimer(new() { Duration = 1000 });
        Frames.Advance(100, 16);
        var before = timer.CurrentTime;

        Presto.Engine.Freeze();
        Frames.Advance(1000);
        Presto.Engine.Thaw();
        Frames.Advance(16);

        Assert.Equal(before + 16, timer.CurrentTime);
    }

    [Fact]
    public void FreezingNeitherPausesNorFiresOnPause()
    {
        var pauses = 0;
        var timer = Presto.CreateTimer(new() { Duration = 1000, OnPause = _ => pauses++ });
        Presto.Engine.Resume();
        Frames.Advance(50, 16);

        Presto.Engine.Freeze();

        Assert.Equal(0, pauses);
        Assert.False(Presto.Engine.Paused);
        Assert.False(timer.Paused);
    }

    [Fact]
    public void ATimerCreatedWhileFrozenStartsFromFrozenTime()
    {
        Frames.Advance(100);
        Presto.Engine.Freeze();
        Frames.Advance(500);

        var timer = Presto.CreateTimer(new() { Duration = 1000 });
        Frames.Advance(500);
        Assert.Equal(0, timer.CurrentTime);

        // Resume backdates a timer by 12 ms so its first tick always advances it, as upstream does.
        Presto.Engine.Step(16);
        Assert.Equal(16 + 12, timer.CurrentTime);
    }

    [Fact]
    public void ASpringSurvivesPauseAndResumeWithoutSnappingToRest()
    {
        var source = Presto.CreateMotionValue();
        var follower = source.Spring(Bouncy);
        source.Set(100);
        Frames.Advance(48, 16);

        Presto.Engine.Pause();
        Frames.Advance(5000);
        Presto.Engine.Resume();
        Frames.Advance(16);

        Assert.InRange(follower.Value, 1, 99);
    }

    [Fact]
    public void DilationAppliesInSecondsMode()
    {
        Presto.Engine.TimeUnit = TimeUnit.Seconds;
        var timer = Presto.CreateTimer(new() { Duration = 10 });
        Frames.Advance(16);
        var before = timer.CurrentTime;

        Presto.Engine.TimeDilation = .5;
        Frames.Advance(1000, 16);

        Assert.Equal(before + .5, timer.CurrentTime, 1e-9);
    }
}
