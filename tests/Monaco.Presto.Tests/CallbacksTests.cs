namespace Monaco.Presto.Tests;

/// <summary>
/// Port of anime.js callbacks.test.js (timer subset; animation cases use the same Delay 10 / Duration 80 parameters).
/// Seek() excludes the delay, so Seek(5) on a Delay 10 timer sits at absolute 15.
/// Deferred: the timeline cases (Phase 4), the onRender cases, 'refresh() inside an onLoop' and 'onPause on ... tween override' (Phase 3).
/// </summary>
public sealed class CallbacksTests : EngineTestBase
{
    [Fact]
    public void OnBeginOnTimer()
    {
        var callbackCheck = 0;
        var timer = Presto.CreateTimer(new() { Autoplay = false, Delay = 10, Duration = 80, OnBegin = _ => callbackCheck++ });

        Assert.Equal(0, callbackCheck);
        Assert.False(timer.Began);
        timer.Seek(5);
        Assert.Equal(1, callbackCheck);
        Assert.True(timer.Began);
        timer.Seek(80);
        Assert.Equal(1, callbackCheck);
        Assert.True(timer.Began);
        timer.Seek(0);
        Assert.Equal(1, callbackCheck);
        Assert.False(timer.Began);
        timer.Seek(5);
        Assert.Equal(2, callbackCheck);
        Assert.True(timer.Began);
    }

    [Fact]
    public void OnBeginOnLoopedTimer()
    {
        var callbackCheck = 0;
        var timer = Presto.CreateTimer(new() { Loop = 2, Autoplay = false, Delay = 10, Duration = 80, OnBegin = _ => callbackCheck++ });

        Assert.Equal(0, callbackCheck);
        Assert.False(timer.Began);
        timer.Seek(5);
        Assert.Equal(1, callbackCheck);
        Assert.True(timer.Began);
        timer.Seek(80);
        Assert.Equal(1, callbackCheck);
        Assert.True(timer.Began);
        timer.Seek(85);
        Assert.Equal(1, callbackCheck);
        Assert.True(timer.Began);
        timer.Seek(240);
        Assert.True(timer.Began);
        Assert.Equal(1, callbackCheck);
        timer.Seek(100);
        Assert.True(timer.Began);
        Assert.Equal(1, callbackCheck);
        timer.Seek(0);
        Assert.False(timer.Began);
        Assert.Equal(1, callbackCheck);
        timer.Seek(5);
        Assert.True(timer.Began);
        Assert.Equal(2, callbackCheck);
    }

    [Fact]
    public void OnCompleteOnTimer()
    {
        var callbackCheck = 0;
        var timer = Presto.CreateTimer(new() { Autoplay = false, Delay = 10, Duration = 80, OnComplete = _ => callbackCheck++ });

        Assert.Equal(0, callbackCheck);
        Assert.False(timer.Completed);
        timer.Seek(50);
        Assert.Equal(0, callbackCheck);
        Assert.False(timer.Completed);
        timer.Seek(0);
        Assert.Equal(0, callbackCheck);
        Assert.False(timer.Completed);
        timer.Seek(100);
        Assert.Equal(1, callbackCheck);
        Assert.True(timer.Completed);
        timer.Seek(50);
        Assert.Equal(1, callbackCheck);
        Assert.False(timer.Completed);
        timer.Seek(100);
        Assert.Equal(2, callbackCheck);
        Assert.True(timer.Completed);
    }

    [Fact]
    public void OnCompleteOnLoopedTimer()
    {
        var callbackCheck = 0;
        var timer = Presto.CreateTimer(new() { Loop = 2, Autoplay = false, Delay = 10, Duration = 80, OnComplete = _ => callbackCheck++ });

        Assert.Equal(0, callbackCheck);
        Assert.False(timer.Completed);
        timer.Seek(10);
        Assert.Equal(0, callbackCheck);
        Assert.False(timer.Completed);
        timer.Seek(80);
        Assert.Equal(0, callbackCheck);
        Assert.False(timer.Completed);
        timer.Seek(240);
        Assert.Equal(1, callbackCheck);
        Assert.True(timer.Completed);
        timer.Seek(0);
        Assert.Equal(1, callbackCheck);
        Assert.False(timer.Completed);
        timer.Seek(80);
        Assert.Equal(1, callbackCheck);
        Assert.False(timer.Completed);
        timer.Seek(240);
        Assert.Equal(2, callbackCheck);
        Assert.True(timer.Completed);
    }

    [Fact]
    public void OnCompleteOnLoopedAlternateTimer()
    {
        var callbackCheck = 0;
        var timer = Presto.CreateTimer(new() { Loop = 1, Alternate = true, Autoplay = false, Delay = 10, Duration = 80, OnComplete = _ => callbackCheck++ });

        Assert.Equal(0, callbackCheck);
        Assert.False(timer.Completed);
        timer.Seek(10);
        Assert.Equal(0, callbackCheck);
        Assert.False(timer.Completed);
        timer.Seek(80);
        Assert.Equal(0, callbackCheck);
        Assert.False(timer.Completed);
        timer.Seek(140);
        Assert.Equal(0, callbackCheck);
        Assert.False(timer.Completed);
        timer.Seek(160);
        Assert.Equal(1, callbackCheck);
        Assert.True(timer.Completed);
    }

    [Fact]
    public void OnCompleteOnReversedTimer()
    {
        var callbackCheck = 0;
        var timer = Presto.CreateTimer(new() { Reversed = true, Autoplay = false, Delay = 10, Duration = 80, OnComplete = _ => callbackCheck++ });

        Assert.Equal(0, callbackCheck);
        Assert.False(timer.Completed);
        timer.Seek(10);
        Assert.Equal(0, callbackCheck);
        Assert.False(timer.Completed);
        timer.Seek(80);
        Assert.Equal(1, callbackCheck);
        Assert.True(timer.Completed);
    }

    [Fact]
    public void OnCompleteAfterCallingReverse()
    {
        var callbackCheck = 0;
        var timer = Presto.CreateTimer(new() { Reversed = true, Autoplay = false, Delay = 10, Duration = 80, OnComplete = _ => callbackCheck++ });

        Assert.Equal(0, callbackCheck);
        Assert.False(timer.Completed);
        timer.Seek(20);
        Assert.Equal(0, callbackCheck);
        Assert.False(timer.Completed);
        timer.Reverse();
        Assert.Equal(0, callbackCheck);
        Assert.False(timer.Completed);
        timer.Seek(80);
        Assert.Equal(1, callbackCheck);
        Assert.True(timer.Completed);
    }

    [Fact]
    public void OnCompleteOnLoopedReversedAlternateTimer()
    {
        var callbackCheck = 0;
        var timer = Presto.CreateTimer(new() { Loop = 1, Alternate = true, Reversed = true, Autoplay = false, Delay = 10, Duration = 80, OnComplete = _ => callbackCheck++ });

        Assert.Equal(0, callbackCheck);
        Assert.False(timer.Completed);
        timer.Seek(10);
        Assert.Equal(0, callbackCheck);
        Assert.False(timer.Completed);
        timer.Seek(80);
        Assert.Equal(0, callbackCheck);
        Assert.False(timer.Completed);
        timer.Seek(140);
        Assert.Equal(0, callbackCheck);
        Assert.False(timer.Completed);
        timer.Seek(160);
        Assert.Equal(1, callbackCheck);
        Assert.True(timer.Completed);
    }

    [Fact]
    public void OnBeforeUpdateOnTimer()
    {
        var callbackCheck = false;
        var ticks = 0;
        var timer = Presto.CreateTimer(new()
        {
            Autoplay = false,
            Delay = 10,
            Duration = 80,
            OnBeforeUpdate = _ =>
            {
                ticks++;
                callbackCheck = true;
            },
        });

        Assert.False(callbackCheck);
        timer.Seek(5);
        Assert.Equal(1, ticks);
        Assert.True(callbackCheck);
        timer.Seek(9);
        Assert.Equal(2, ticks);
        Assert.True(callbackCheck);
        timer.Seek(10);
        Assert.True(callbackCheck);
        Assert.Equal(3, ticks);
        timer.Seek(15);
        Assert.Equal(4, ticks);
    }

    [Fact]
    public void OnUpdateOnTimer()
    {
        var callbackCheck = false;
        var ticks = 0;
        var timer = Presto.CreateTimer(new()
        {
            Autoplay = false,
            Delay = 10,
            Duration = 80,
            OnUpdate = _ =>
            {
                ticks++;
                callbackCheck = true;
            },
        });

        Assert.False(callbackCheck);
        timer.Seek(5);
        Assert.Equal(1, ticks);
        Assert.True(callbackCheck);
        timer.Seek(9);
        Assert.Equal(2, ticks);
        Assert.True(callbackCheck);
        timer.Seek(10);
        Assert.True(callbackCheck);
        Assert.Equal(3, ticks);
        timer.Seek(15);
        Assert.Equal(4, ticks);
    }

    [Fact]
    public void OnLoopOnTimer()
    {
        var loops = 0;
        var timer = Presto.CreateTimer(new()
        {
            Duration = 80,
            Loop = 2,
            LoopDelay = 20,
            Autoplay = false,
            OnLoop = _ => loops++,
        });

        timer.Seek(5);
        Assert.Equal(0, loops);
        timer.Seek(80);
        Assert.Equal(0, loops);
        timer.Seek(100);
        Assert.Equal(1, loops);
        timer.Seek(180);
        Assert.Equal(1, loops);
        timer.Seek(200);
        Assert.Equal(2, loops);
        timer.Seek(280);
        Assert.Equal(2, loops);
    }

    [Fact]
    public void OnPauseOnTimer()
    {
        var paused = 0;
        var timer = Presto.CreateTimer(new() { Duration = 80, OnPause = _ => paused++ });
        Frames.Advance(16);

        timer.Pause();
        Assert.Equal(1, paused);
        timer.Pause();
        Assert.Equal(1, paused);
    }

    [Fact]
    public void OnPauseDoesNotFireWhenATimerCompletesOnItsOwn()
    {
        var paused = 0;
        var timer = Presto.CreateTimer(new() { Duration = 30, OnPause = _ => paused++ });

        RunUntil(() => timer.Completed);
        Frames.Advance(16);

        Assert.True(timer.Paused);
        Assert.True(timer.Cancelled);
        Assert.Equal(0, paused);
    }

    [Fact]
    public void OnBeginOnBeforeUpdateOnUpdateOnCompleteShouldTriggerOn0DurationTimer()
    {
        var onBeginCheck = false;
        var onBeforeUpdateCheck = false;
        var onUpdateCheck = false;
        var onLoopCheck = false;
        var onCompleteCheck = false;

        var timer = Presto.CreateTimer(new()
        {
            Duration = 0,
            OnBegin = _ => onBeginCheck = true,
            OnBeforeUpdate = _ => onBeforeUpdateCheck = true,
            OnUpdate = _ => onUpdateCheck = true,
            OnLoop = _ => onLoopCheck = true,
            OnComplete = _ => onCompleteCheck = true,
        });

        Assert.True(onBeginCheck);
        Assert.True(onBeforeUpdateCheck);
        Assert.True(onUpdateCheck);
        Assert.False(onLoopCheck);
        Assert.True(onCompleteCheck);
        Assert.True(timer.Began);
        Assert.True(timer.Completed);
    }

    [Fact]
    public void CallbacksOrderExecution()
    {
        var value = 0d;
        var timer = Presto.CreateTimer(new()
        {
            Autoplay = false,
            OnBegin = _ => value = 2,
            OnBeforeUpdate = _ => value *= 3,
            OnUpdate = _ => value += 10,
            OnComplete = _ => value /= 16,
        });
        Assert.Equal(0, value);

        timer.Seek(timer.Duration);

        Assert.Equal(1, value);
    }
}
