using static Monaco.Presto.Tests.TestHelpers;

namespace Monaco.Presto.Tests;

/// <summary>Port of anime.js engine.test.js, adapted to timers driven by a manual frame source.</summary>
public sealed class EngineTests : EngineTestBase
{
    [Fact]
    public void InitialOffsetPositionShouldBeProperlyCalculatedOnColdStart()
    {
        Timer? timer2 = null;

        Frames.Advance(55);
        var timer1 = Presto.CreateTimer(new()
        {
            Duration = 20,
            OnComplete = _ => timer2 = Presto.CreateTimer(new() { Duration = 20 }),
        });
        RunUntil(() => timer2 != null);

        Assert.NotNull(timer2);
        Assert.True(timer1._offset > 50);
        Assert.True(timer1._offset < timer2._offset);
    }

    [Fact]
    public void SetUseDefaultMainLoopToFalseShouldPreventTimersFromRunning()
    {
        Presto.Engine.UseDefaultMainLoop = false;
        var updates = 0;

        var timer = Presto.CreateTimer(new() { Duration = 20, OnUpdate = _ => updates++ });
        Frames.Advance(70, 16);

        Assert.False(timer.Began);
        Assert.Equal(0, timer.CurrentTime);
        Assert.Equal(0, updates);
    }

    [Fact]
    public void ManuallyTickTheEngineWithAnExternalLoop()
    {
        Presto.Engine.UseDefaultMainLoop = false;
        var updates = 0;

        var timer = Presto.CreateTimer(new() { Duration = 50, OnUpdate = _ => updates++ });
        for (var i = 0; i < 5; i++)
        {
            Frames.Advance(16);
            Presto.Engine.Update();
        }

        Assert.True(timer.Began);
        Assert.True(timer.Completed);
        Assert.Equal(50, timer.CurrentTime);
        Assert.True(updates > 2);
        Assert.Equal(0, GetChildLength(Presto.Engine));
    }

    [Fact]
    public void PauseAndResumeTheEngine()
    {
        var updates = 0;
        var timer = Presto.CreateTimer(new() { Duration = 50, OnUpdate = _ => updates++ });

        Presto.Engine.Pause();
        Frames.Advance(50, 16);

        Assert.False(timer.Began);
        Assert.False(timer.Completed);
        Assert.Equal(0, timer.CurrentTime);
        Assert.Equal(0, updates);
        Assert.Same(timer, GetChildAtIndex(Presto.Engine, 0));

        Presto.Engine.Resume();
        Frames.Advance(100, 16);

        Assert.True(timer.Began);
        Assert.True(timer.Completed);
        Assert.Equal(50, timer.CurrentTime);
        Assert.True(updates > 2);
    }

    [Fact]
    public void DefaultPrecisionShouldBe4()
    {
        Assert.Equal(4, Presto.Engine.Precision);

        var timer = Presto.CreateTimer(new() { Duration = 500, Autoplay = false });
        timer.Seek(0.123456);

        Assert.Equal(0.1235, timer.CurrentTime);
    }

    [Fact]
    public void ChangingPrecisionAffectsReportedTime()
    {
        Presto.Engine.Precision = 1;

        var timer = Presto.CreateTimer(new() { Duration = 500, Autoplay = false });
        timer.Seek(0.123456);

        Assert.Equal(0.1, timer.CurrentTime);
    }

    [Fact]
    public void ChangingTheTimeUnitShouldAffectDurationValues()
    {
        Presto.Engine.TimeUnit = TimeUnit.Seconds;
        Assert.Equal(1, Presto.Defaults.Duration);

        var timer = Presto.CreateTimer(new() { Duration = .75 });
        Frames.Advance(150, 16);

        Assert.True(timer.CurrentTime > .1);
        Assert.True(timer.CurrentTime < .75);

        Presto.Engine.TimeUnit = TimeUnit.Milliseconds;
        Assert.Equal(1000, Presto.Defaults.Duration);
    }

    [Fact]
    public void EngineSpeedReads1InSecondsMode()
    {
        Presto.Engine.TimeUnit = TimeUnit.Seconds;

        Assert.Equal(1, Presto.Engine.Speed);
        Assert.Equal(.001, Presto.Engine._speed);
    }
}
