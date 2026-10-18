namespace MonacoLabs.Presto.Tests;

/// <summary>Engine.Hidden and PauseOnHidden, the port of upstream's <c>pauseOnDocumentHidden</c> visibilitychange handler in engine.js.</summary>
public sealed class EngineHiddenTests : EngineTestBase
{
    private static readonly Spring Bouncy = new(stiffness: 120, damping: 12);

    [Fact]
    public void HidingPausesAndShowingResumesWithoutAJump()
    {
        var timer = Presto.CreateTimer(new() { Duration = 1000 });
        Frames.Advance(100, 16);
        var before = timer.CurrentTime;

        Presto.Engine.Hidden = true;
        Frames.Advance(60000, 16);

        Assert.True(Presto.Engine.Paused);
        Assert.False(Frames.IsRunning);
        Assert.Equal(before, timer.CurrentTime);

        Presto.Engine.Hidden = false;
        Frames.Advance(16);

        Assert.True(Frames.IsRunning);
        Assert.Equal(before + 16, timer.CurrentTime);
    }

    [Fact]
    public void ShowingResumesAManualPauseButNotAFreeze()
    {
        var timer = Presto.CreateTimer(new() { Duration = 1000 });
        Frames.Advance(100, 16);

        Presto.Engine.Pause();
        Presto.Engine.Hidden = true;
        Presto.Engine.Hidden = false;
        Assert.False(Presto.Engine.Paused);
        Assert.True(Frames.IsRunning);

        Presto.Engine.Freeze();
        var frozenAt = timer.CurrentTime;
        Presto.Engine.Hidden = true;
        Presto.Engine.Hidden = false;
        Frames.Advance(100, 16);
        Assert.True(Presto.Engine.Frozen);
        Assert.Equal(frozenAt, timer.CurrentTime);
    }

    [Fact]
    public void HiddenIsANoOpWithoutPauseOnHidden()
    {
        Presto.Engine.PauseOnHidden = false;
        var timer = Presto.CreateTimer(new() { Duration = 1000 });
        Frames.Advance(100, 16);
        var before = timer.CurrentTime;

        Presto.Engine.Hidden = true;
        Frames.Advance(16);

        Assert.True(Frames.IsRunning);
        Assert.Equal(before + 16, timer.CurrentTime);
    }

    [Fact]
    public void TurningPauseOnHiddenOffWhileHiddenResumes()
    {
        var timer = Presto.CreateTimer(new() { Duration = 1000 });
        Frames.Advance(100, 16);
        var before = timer.CurrentTime;

        Presto.Engine.Hidden = true;
        Frames.Advance(500);
        Presto.Engine.PauseOnHidden = false;
        Frames.Advance(16);

        Assert.Equal(before + 16, timer.CurrentTime);
    }

    [Fact]
    public void ATimerCreatedWhileHiddenWaitsForTheShow()
    {
        Presto.Engine.Hidden = true;
        var timer = Presto.CreateTimer(new() { Duration = 1000 });
        Frames.Advance(500, 16);

        Assert.False(Frames.IsRunning);
        Assert.Equal(0, timer.CurrentTime);

        Presto.Engine.Hidden = false;
        Frames.Advance(16);

        Assert.True(timer.Began);
        Assert.InRange(timer.CurrentTime, 1, 16 + 12);
    }

    [Fact]
    public async Task AnAnimationStartedBeforeHidingCompletesAfterShowing()
    {
        var animation = Presto.Animate(new StyleTarget(), new() { ["Y"] = 100, Duration = 100 });
        var task = animation.Then();
        Frames.Advance(48, 16);

        Presto.Engine.Hidden = true;
        Frames.Advance(1000, 16);
        Assert.False(task.IsCompleted);

        Presto.Engine.Hidden = false;
        RunUntil(() => task.IsCompleted);

        Assert.Same(animation, await task);
        Assert.Equal(100, animation.CurrentTime);
    }

    [Fact]
    public void AMovingValueResumesWithoutAJump()
    {
        var reference = Presto.CreateMotionValue();
        var expected = reference.Spring(Bouncy);
        reference.Set(100);
        Frames.Advance(64, 16);
        var expectedValue = expected.Value;

        var source = Presto.CreateMotionValue();
        var follower = source.Spring(Bouncy);
        source.Set(100);
        Frames.Advance(48, 16);
        Presto.Engine.Hidden = true;
        Frames.Advance(5000);
        Presto.Engine.Hidden = false;
        Frames.Advance(16);

        Assert.Equal(expectedValue, follower.Value, 1e-9);
    }

    [Fact]
    public void ThawingWhileHiddenDoesNotWake()
    {
        var timer = Presto.CreateTimer(new() { Duration = 1000 });
        Frames.Advance(100, 16);
        var before = timer.CurrentTime;

        Presto.Engine.Freeze();
        Presto.Engine.Hidden = true;
        Presto.Engine.Thaw();
        Frames.Advance(500, 16);

        Assert.False(Frames.IsRunning);
        Assert.Equal(before, timer.CurrentTime);

        Presto.Engine.Hidden = false;
        Frames.Advance(16);

        Assert.Equal(before + 16, timer.CurrentTime);
    }

    [Fact]
    public void ThawingAfterAFrozenShowWakes()
    {
        var timer = Presto.CreateTimer(new() { Duration = 1000 });
        Frames.Advance(100, 16);
        var before = timer.CurrentTime;

        Presto.Engine.Hidden = true;
        Presto.Engine.Freeze();
        Presto.Engine.Hidden = false;
        Frames.Advance(500, 16);
        Assert.False(Frames.IsRunning);

        Presto.Engine.Thaw();
        Frames.Advance(16);

        Assert.True(Frames.IsRunning);
        Assert.Equal(before + 16, timer.CurrentTime);
    }

    [Fact]
    public void HidingDoesNotJumpInSecondsMode()
    {
        Presto.Engine.TimeUnit = TimeUnit.Seconds;
        var timer = Presto.CreateTimer(new() { Duration = 10 });
        Frames.Advance(100, 16);
        var before = timer.CurrentTime;

        Presto.Engine.Hidden = true;
        Frames.Advance(5000);
        Presto.Engine.Hidden = false;
        Frames.Advance(16);

        Assert.Equal(before + .016, timer.CurrentTime, 1e-9);
    }

    [Fact]
    public void TurningPauseOnHiddenOnWhileHiddenPauses()
    {
        Presto.Engine.PauseOnHidden = false;
        var timer = Presto.CreateTimer(new() { Duration = 1000 });
        Frames.Advance(100, 16);
        Presto.Engine.Hidden = true;
        Frames.Advance(16);
        var before = timer.CurrentTime;

        Presto.Engine.PauseOnHidden = true;
        Frames.Advance(500, 16);

        Assert.True(Presto.Engine.Paused);
        Assert.False(Frames.IsRunning);
        Assert.Equal(before, timer.CurrentTime);
    }

    [Fact]
    public void SettingHiddenOrPauseOnHiddenToItsCurrentValueChangesNothing()
    {
        Presto.CreateTimer(new() { Duration = 1000 });
        Frames.Advance(100, 16);
        Presto.Engine.Pause();

        Presto.Engine.Hidden = false;
        Assert.True(Presto.Engine.Paused);
        Assert.False(Frames.IsRunning);

        Presto.Engine.PauseOnHidden = false;
        Presto.Engine.Hidden = true;
        Presto.Engine.PauseOnHidden = false;
        Assert.True(Presto.Engine.Paused);
        Assert.False(Frames.IsRunning);
    }
}
