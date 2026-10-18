namespace MonacoLabs.Presto.Tests;

/// <summary>
/// Port of anime.js callbacks.test.js.
/// Seek() excludes the delay, so Seek(5) on a Delay 10 animation sits at absolute 15.
/// 'then() on animation' and 'then() on timeline' are covered by <see cref="PromisesTests"/>.
/// </summary>
public sealed class CallbacksTests : EngineTestBase
{
    /// <summary>The suite's <c>setupAnimationCallBack</c> parameters; Autoplay false holds the animation at time 0, so callbacks can be assigned afterwards.</summary>
    private static Animation SetupAnimationCallback(LoopCount? loop = null, bool alternate = false, bool reversed = false) =>
        Presto.Animate(new StyleTarget(), new()
        {
            ["X"] = 100,
            Autoplay = false,
            Delay = 10,
            Duration = 80,
            Loop = loop,
            Alternate = alternate,
            Reversed = reversed,
        });

    /// <summary><c>setupAnimationCallBack</c> again, as a timeline and as the children the timeline cases add to it.</summary>
    private static TimelineParams TimelineCallbackParameters => new() { Autoplay = false, Delay = 10, Duration = 80 };

    /// <inheritdoc cref="TimelineCallbackParameters"/>
    private static AnimationParams ChildCallbackParameters => new() { ["TranslateX"] = 100, Autoplay = false, Delay = 10, Duration = 80 };

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
        Assert.True(timer.Canceled);
        Assert.Equal(0, paused);
    }

    [Fact]
    public void CompleteFiresOnCompleteAndNotOnPause()
    {
        int paused = 0, completed = 0;
        var timer = Presto.CreateTimer(new() { Duration = 100, OnPause = _ => paused++, OnComplete = _ => completed++ });
        var animation = Presto.Animate(new StyleTarget(), new() { ["X"] = 100, Duration = 100, OnPause = _ => paused++, OnComplete = _ => completed++ });
        Frames.Advance(16);

        timer.Complete();
        animation.Complete();

        Assert.Equal(2, completed);
        Assert.Equal(0, paused);
        Assert.True(timer.Canceled);
        Assert.True(animation.Canceled);
    }

    [Fact]
    public void OnBeginOnAnimation()
    {
        var callbackCheck = 0;
        var animation = SetupAnimationCallback();
        animation.OnBegin = _ => callbackCheck++;

        Assert.Equal(0, callbackCheck);
        Assert.False(animation.Began);
        animation.Seek(5);
        Assert.Equal(1, callbackCheck);
        Assert.True(animation.Began);
        animation.Seek(80);
        Assert.Equal(1, callbackCheck);
        Assert.True(animation.Began);
        animation.Seek(0);
        Assert.Equal(1, callbackCheck);
        Assert.False(animation.Began);
        animation.Seek(5);
        Assert.Equal(2, callbackCheck);
        Assert.True(animation.Began);
    }

    [Fact]
    public void OnBeginOnLoopedAnimation()
    {
        var callbackCheck = 0;
        var animation = SetupAnimationCallback(loop: 2);
        animation.OnBegin = _ => callbackCheck++;

        Assert.Equal(0, callbackCheck);
        Assert.False(animation.Began);
        animation.Seek(5);
        Assert.Equal(1, callbackCheck);
        Assert.True(animation.Began);
        animation.Seek(80);
        Assert.Equal(1, callbackCheck);
        Assert.True(animation.Began);
        animation.Seek(85);
        Assert.Equal(1, callbackCheck);
        Assert.True(animation.Began);
        animation.Seek(240);
        Assert.True(animation.Began);
        Assert.Equal(1, callbackCheck);
        animation.Seek(100);
        Assert.True(animation.Began);
        Assert.Equal(1, callbackCheck);
        animation.Seek(0);
        Assert.False(animation.Began);
        Assert.Equal(1, callbackCheck);
        animation.Seek(5);
        Assert.True(animation.Began);
        Assert.Equal(2, callbackCheck);
    }

    [Fact]
    public void OnCompleteOnAnimation()
    {
        var callbackCheck = 0;
        var animation = SetupAnimationCallback();
        animation.OnComplete = _ => callbackCheck++;

        Assert.Equal(0, callbackCheck);
        Assert.False(animation.Completed);
        animation.Seek(50);
        Assert.Equal(0, callbackCheck);
        Assert.False(animation.Completed);
        animation.Seek(0);
        Assert.Equal(0, callbackCheck);
        Assert.False(animation.Completed);
        animation.Seek(100);
        Assert.Equal(1, callbackCheck);
        Assert.True(animation.Completed);
        animation.Seek(50);
        Assert.Equal(1, callbackCheck);
        Assert.False(animation.Completed);
        animation.Seek(100);
        Assert.Equal(2, callbackCheck);
        Assert.True(animation.Completed);
    }

    [Fact]
    public void OnCompleteOnLoopedAnimation()
    {
        var callbackCheck = 0;
        var animation = SetupAnimationCallback(loop: 2);
        animation.OnComplete = _ => callbackCheck++;

        Assert.Equal(0, callbackCheck);
        Assert.False(animation.Completed);
        animation.Seek(10);
        Assert.Equal(0, callbackCheck);
        Assert.False(animation.Completed);
        animation.Seek(80);
        Assert.Equal(0, callbackCheck);
        Assert.False(animation.Completed);
        animation.Seek(240);
        Assert.Equal(1, callbackCheck);
        Assert.True(animation.Completed);
        animation.Seek(0);
        Assert.Equal(1, callbackCheck);
        Assert.False(animation.Completed);
        animation.Seek(80);
        Assert.Equal(1, callbackCheck);
        Assert.False(animation.Completed);
        animation.Seek(240);
        Assert.Equal(2, callbackCheck);
        Assert.True(animation.Completed);
    }

    [Fact]
    public void OnCompleteOnLoopedAlternateAnimation()
    {
        var callbackCheck = 0;
        var animation = SetupAnimationCallback(loop: 1, alternate: true);
        animation.OnComplete = _ => callbackCheck++;

        Assert.Equal(0, callbackCheck);
        Assert.False(animation.Completed);
        animation.Seek(10);
        Assert.Equal(0, callbackCheck);
        Assert.False(animation.Completed);
        animation.Seek(80);
        Assert.Equal(0, callbackCheck);
        Assert.False(animation.Completed);
        animation.Seek(140);
        Assert.Equal(0, callbackCheck);
        Assert.False(animation.Completed);
        animation.Seek(160);
        Assert.Equal(1, callbackCheck);
        Assert.True(animation.Completed);
    }

    [Fact]
    public void OnCompleteOnReversedAnimation()
    {
        var callbackCheck = 0;
        var animation = SetupAnimationCallback(reversed: true);
        animation.OnComplete = _ => callbackCheck++;

        Assert.Equal(0, callbackCheck);
        Assert.False(animation.Completed);
        animation.Seek(10);
        Assert.Equal(0, callbackCheck);
        Assert.False(animation.Completed);
        animation.Seek(80);
        Assert.Equal(1, callbackCheck);
        Assert.True(animation.Completed);
    }

    [Fact]
    public void OnCompleteAfterCallingAnimationReverse()
    {
        var callbackCheck = 0;
        var animation = SetupAnimationCallback(reversed: true);
        animation.OnComplete = _ => callbackCheck++;

        Assert.Equal(0, callbackCheck);
        Assert.False(animation.Completed);
        animation.Seek(20);
        Assert.Equal(0, callbackCheck);
        Assert.False(animation.Completed);
        animation.Reverse();
        Assert.Equal(0, callbackCheck);
        Assert.False(animation.Completed);
        animation.Seek(80);
        Assert.Equal(1, callbackCheck);
        Assert.True(animation.Completed);
    }

    [Fact]
    public void OnCompleteOnLoopedReversedAlternateAnimation()
    {
        var callbackCheck = 0;
        var animation = SetupAnimationCallback(loop: 1, alternate: true, reversed: true);
        animation.OnComplete = _ => callbackCheck++;

        Assert.Equal(0, callbackCheck);
        Assert.False(animation.Completed);
        animation.Seek(10);
        Assert.Equal(0, callbackCheck);
        Assert.False(animation.Completed);
        animation.Seek(80);
        Assert.Equal(0, callbackCheck);
        Assert.False(animation.Completed);
        animation.Seek(140);
        Assert.Equal(0, callbackCheck);
        Assert.False(animation.Completed);
        animation.Seek(160);
        Assert.Equal(1, callbackCheck);
        Assert.True(animation.Completed);
    }

    [Fact]
    public void OnBeforeUpdateOnAnimation()
    {
        var ticks = 0;
        var animation = SetupAnimationCallback();
        animation.OnBeforeUpdate = _ => ticks++;

        animation.Seek(5);
        Assert.Equal(1, ticks);
        animation.Seek(9);
        Assert.Equal(2, ticks);
        animation.Seek(10);
        Assert.Equal(3, ticks);
        animation.Seek(15);
        Assert.Equal(4, ticks);
    }

    [Fact]
    public void OnUpdateOnAnimation()
    {
        var ticks = 0;
        var animation = SetupAnimationCallback();
        animation.OnUpdate = _ => ticks++;

        animation.Seek(5);
        Assert.Equal(1, ticks);
        animation.Seek(9);
        Assert.Equal(2, ticks);
        animation.Seek(10);
        Assert.Equal(3, ticks);
        animation.Seek(15);
        Assert.Equal(4, ticks);
    }

    [Fact]
    public void OnLoopOnAnimation()
    {
        var loops = 0;
        var animation = Presto.Animate(new StyleTarget(), new()
        {
            ["X"] = 100,
            Duration = 80,
            Loop = 2,
            LoopDelay = 20,
            Autoplay = false,
            OnLoop = _ => loops++,
        });

        animation.Seek(5);
        Assert.Equal(0, loops);
        animation.Seek(80);
        Assert.Equal(0, loops);
        animation.Seek(100);
        Assert.Equal(1, loops);
        animation.Seek(180);
        Assert.Equal(1, loops);
        animation.Seek(200);
        Assert.Equal(2, loops);
        animation.Seek(280);
        Assert.Equal(2, loops);
    }

    [Fact]
    public void RefreshInsideAnOnLoopOnAnimation()
    {
        var loops = 0;
        var data = new Dictionary<string, object?> { ["X"] = 0d };
        var animation = Presto.Animate(data, new()
        {
            ["X"] = new FunctionValue((_, _, _) => loops * 10),
            Duration = 80,
            Loop = 2,
            Ease = "linear",
            LoopDelay = 20,
            Autoplay = false,
        });
        animation.OnLoop = _ =>
        {
            loops++;
            animation.Refresh();
        };

        animation.Seek(5);
        Assert.Equal(0, loops);
        Assert.Equal(0d, data["X"]);
        animation.Seek(100);
        Assert.Equal(1, loops);
        Assert.Equal(0d, data["X"]);
        animation.Seek(140);
        Assert.Equal(1, loops);
        Assert.Equal(5d, data["X"]);
        animation.Seek(199);
        Assert.Equal(1, loops);
        Assert.Equal(10d, data["X"]);
        animation.Seek(200);
        Assert.Equal(2, loops);
        Assert.Equal(10d, data["X"]);
        animation.Seek(240);
        Assert.Equal(2, loops);
        Assert.Equal(15d, data["X"]);
    }

    [Fact]
    public void OnPauseOnAnimationTweenOverride()
    {
        var target = new StyleTarget();
        var paused = 0;
        Presto.Animate(target, new() { ["Y"] = 200, Duration = 30, OnPause = _ => paused++ });

        var completed = false;
        Presto.Animate(target, new() { ["Y"] = 100, Duration = 30, Delay = 10, OnComplete = _ => completed = true });
        RunUntil(() => completed);

        Assert.Equal(1, paused);
    }

    [Fact]
    public void OnRenderWithAutoplayFalse()
    {
        var target = new StyleTarget();
        var renders = 0;

        Presto.Animate(target, new()
        {
            ["Opacity"] = TweenValue.FromTo(.5, 1),
            ["X"] = 100,
            ["Scale"] = 2,
            Autoplay = false,
            OnRender = _ => renders++,
        });

        Assert.Equal(.5, target.Opacity);
        Assert.Equal(1, renders);
    }

    [Fact]
    public void OnRenderOnAnimation()
    {
        var renders = 0;
        var animation = Presto.Animate(new StyleTarget(), new()
        {
            ["TranslateX"] = 100,
            Autoplay = false,
            Delay = 10,
            Duration = 80,
            OnRender = _ => renders++,
        });

        Assert.Equal(0, renders);
        animation.Seek(5);
        Assert.Equal(1, renders);
        animation.Seek(9);
        Assert.Equal(2, renders);
        animation.Seek(10);
        Assert.Equal(3, renders);
        animation.Seek(15);
        Assert.Equal(4, renders);
    }

    [Fact]
    public void CallbacksOrderExecutionOnAnimation()
    {
        var value = 0d;
        var animation = Presto.Animate(new StyleTarget(), new()
        {
            ["TranslateX"] = 100,
            Autoplay = false,
            OnBegin = _ => value = 2,
            OnBeforeUpdate = _ => value *= 3,
            OnRender = _ => value += 4,
            OnUpdate = _ => value += 10,
            OnComplete = _ => value /= 20,
        });
        Assert.Equal(0, value);

        animation.Seek(animation.Duration);

        Assert.Equal(1, value);
    }

    [Fact]
    public void AllCallbacksButOnLoopTriggerOnZeroDurationAnimation()
    {
        var onBeginCheck = false;
        var onBeforeUpdateCheck = false;
        var onUpdateCheck = false;
        var onRenderCheck = false;
        var onLoopCheck = false;
        var onCompleteCheck = false;

        var animation = Presto.Animate(new StyleTarget(), new()
        {
            ["TranslateX"] = 100,
            Duration = 0,
            OnBegin = _ => onBeginCheck = true,
            OnBeforeUpdate = _ => onBeforeUpdateCheck = true,
            OnUpdate = _ => onUpdateCheck = true,
            OnRender = _ => onRenderCheck = true,
            OnLoop = _ => onLoopCheck = true,
            OnComplete = _ => onCompleteCheck = true,
        });

        Assert.True(onBeginCheck);
        Assert.True(onBeforeUpdateCheck);
        Assert.True(onUpdateCheck);
        Assert.True(onRenderCheck);
        Assert.False(onLoopCheck);
        Assert.True(onCompleteCheck);
        Assert.True(animation.Began);
        Assert.True(animation.Completed);
    }

    [Fact]
    public void OnBeginOnTimeline()
    {
        var target = new StyleTarget();
        int tlCallbackCheck = 0, tlAnim1CallbackCheck = 0, tlAnim2CallbackCheck = 0;
        var tl = Presto.CreateTimeline(TimelineCallbackParameters with { OnBegin = _ => tlCallbackCheck++ })
            .Add(target, ChildCallbackParameters with { OnBegin = _ => tlAnim1CallbackCheck++ })
            .Add(target, ChildCallbackParameters with { OnBegin = _ => tlAnim2CallbackCheck++ });

        Assert.Equal(0, tlCallbackCheck);
        Assert.Equal(0, tlAnim1CallbackCheck);
        Assert.Equal(0, tlAnim2CallbackCheck);
        tl.Seek(5);
        Assert.Equal(1, tlCallbackCheck);
        Assert.Equal(0, tlAnim1CallbackCheck);
        Assert.Equal(0, tlAnim2CallbackCheck);
        tl.Seek(10);
        Assert.Equal(1, tlCallbackCheck);
        Assert.Equal(0, tlAnim1CallbackCheck);
        Assert.Equal(0, tlAnim2CallbackCheck);
        tl.Seek(11);
        Assert.Equal(1, tlCallbackCheck);
        Assert.Equal(1, tlAnim1CallbackCheck);
        Assert.Equal(0, tlAnim2CallbackCheck);
        tl.Seek(100);
        Assert.Equal(1, tlCallbackCheck);
        Assert.Equal(1, tlAnim1CallbackCheck);
        Assert.Equal(0, tlAnim2CallbackCheck);
        tl.Seek(101);
        Assert.Equal(1, tlCallbackCheck);
        Assert.Equal(1, tlAnim1CallbackCheck);
        Assert.Equal(1, tlAnim2CallbackCheck);
        tl.Seek(95);
        Assert.Equal(1, tlCallbackCheck);
        Assert.Equal(1, tlAnim1CallbackCheck);
        Assert.Equal(1, tlAnim2CallbackCheck);
        tl.Seek(0);
        Assert.Equal(1, tlCallbackCheck);
        Assert.Equal(1, tlAnim1CallbackCheck);
        Assert.Equal(1, tlAnim2CallbackCheck);
        tl.Seek(5);
        Assert.Equal(2, tlCallbackCheck);
        Assert.Equal(1, tlAnim1CallbackCheck);
        Assert.Equal(1, tlAnim2CallbackCheck);
        tl.Seek(11);
        Assert.Equal(2, tlCallbackCheck);
        Assert.Equal(2, tlAnim1CallbackCheck);
        Assert.Equal(1, tlAnim2CallbackCheck);
        tl.Seek(101);
        Assert.Equal(2, tlCallbackCheck);
        Assert.Equal(2, tlAnim1CallbackCheck);
        Assert.Equal(2, tlAnim2CallbackCheck);
    }

    [Fact]
    public void OnBeginOnLoopedTimeline()
    {
        var target = new StyleTarget();
        int tlCallbackCheck = 0, tlAnim1CallbackCheck = 0, tlAnim2CallbackCheck = 0;
        var tl = Presto.CreateTimeline(TimelineCallbackParameters with { Loop = 2, OnBegin = _ => tlCallbackCheck++ })
            .Add(target, ChildCallbackParameters with { OnBegin = _ => tlAnim1CallbackCheck++ })
            .Add(target, ChildCallbackParameters with { OnBegin = _ => tlAnim2CallbackCheck++ })
            .Init();

        Assert.Equal(0, tlCallbackCheck);
        Assert.Equal(0, tlAnim1CallbackCheck);
        Assert.Equal(0, tlAnim2CallbackCheck);
        tl.Seek(5);
        Assert.Equal(1, tlCallbackCheck);
        Assert.Equal(0, tlAnim1CallbackCheck);
        Assert.Equal(0, tlAnim2CallbackCheck);
        tl.Seek(10);
        Assert.Equal(0, tlAnim1CallbackCheck);
        Assert.Equal(0, tlAnim2CallbackCheck);
        tl.Seek(11);
        Assert.Equal(1, tlAnim1CallbackCheck);
        Assert.Equal(0, tlAnim2CallbackCheck);
        tl.Seek(101);
        Assert.Equal(1, tlAnim1CallbackCheck);
        Assert.Equal(1, tlAnim2CallbackCheck);
        tl.Seek(191);
        Assert.Equal(2, tlAnim1CallbackCheck);
        Assert.Equal(1, tlAnim2CallbackCheck);
        tl.Seek(160);
        Assert.Equal(2, tlAnim1CallbackCheck);
        Assert.Equal(1, tlAnim2CallbackCheck);
        tl.Seek(10);
        Assert.Equal(2, tlAnim1CallbackCheck);
        Assert.Equal(1, tlAnim2CallbackCheck);
        tl.Seek(11);
        Assert.Equal(3, tlAnim1CallbackCheck);
        Assert.Equal(1, tlAnim2CallbackCheck);
    }

    [Fact]
    public void OnBeginAndOnCompleteOnDelayedTimeline()
    {
        int tlBeginCheck = 0, tlCompleteCheck = 0, tlAnim1BeginCheck = 0, tlAnim1CompleteCheck = 0;
        var tl = Presto.CreateTimeline(new()
        {
            Delay = 1000,
            Loop = 1,
            Autoplay = false,
            OnBegin = _ => tlBeginCheck++,
            OnComplete = _ => tlCompleteCheck++,
        })
        .Add(new StyleTarget(), new()
        {
            ["X"] = 100,
            Duration = 0,
            OnBegin = _ => tlAnim1BeginCheck++,
            OnComplete = _ => tlAnim1CompleteCheck++,
        })
        .Init();

        tl.Seek(-75);

        Assert.Equal(0, tlBeginCheck);
        Assert.Equal(0, tlCompleteCheck);
        Assert.Equal(0, tlAnim1BeginCheck);
        Assert.Equal(0, tlAnim1CompleteCheck);

        var startTime = Frames.Now;
        tl.Play();
        RunUntil(() => tl.Completed);

        // The playhead starts 75ms before the timeline, so completing it costs that much elapsed time and no more.
        Assert.InRange(Frames.Now - startTime, 50, 100);
        Assert.Equal(1, tlBeginCheck);
        Assert.Equal(1, tlCompleteCheck);
        Assert.Equal(1, tlAnim1BeginCheck);
        Assert.Equal(1, tlAnim1CompleteCheck);
    }

    [Fact]
    public void OnCompleteOnTimeline()
    {
        var target = new StyleTarget();
        int tlCallbackCheck = 0, tlAnim1CallbackCheck = 0, tlAnim2CallbackCheck = 0;
        var tl = Presto.CreateTimeline(TimelineCallbackParameters with { OnComplete = _ => tlCallbackCheck++ })
            .Add(target, ChildCallbackParameters with { OnComplete = _ => tlAnim1CallbackCheck++ })
            .Add(target, ChildCallbackParameters with { OnComplete = _ => tlAnim2CallbackCheck++ })
            .Init();

        Assert.Equal(0, tlCallbackCheck);
        Assert.Equal(0, tlAnim1CallbackCheck);
        Assert.Equal(0, tlAnim2CallbackCheck);
        tl.Seek(50);
        Assert.Equal(0, tlCallbackCheck);
        Assert.Equal(0, tlAnim1CallbackCheck);
        Assert.Equal(0, tlAnim2CallbackCheck);
        tl.Seek(150);
        Assert.Equal(0, tlCallbackCheck);
        Assert.Equal(1, tlAnim1CallbackCheck);
        Assert.Equal(0, tlAnim2CallbackCheck);
        tl.Seek(200);
        Assert.Equal(1, tlCallbackCheck);
        Assert.Equal(1, tlAnim1CallbackCheck);
        Assert.Equal(1, tlAnim2CallbackCheck);
        tl.Seek(0);
        Assert.Equal(1, tlCallbackCheck);
        Assert.Equal(1, tlAnim1CallbackCheck);
        Assert.Equal(1, tlAnim2CallbackCheck);
        tl.Seek(150);
        Assert.Equal(1, tlCallbackCheck);
        Assert.Equal(2, tlAnim1CallbackCheck);
        Assert.Equal(1, tlAnim2CallbackCheck);
        tl.Seek(200);
        Assert.Equal(2, tlCallbackCheck);
        Assert.Equal(2, tlAnim1CallbackCheck);
        Assert.Equal(2, tlAnim2CallbackCheck);
    }

    [Fact]
    public void OnCompleteOnLoopedTimeline()
    {
        var target = new StyleTarget();
        int tlCallbackCheck = 0, tlAnim1CallbackCheck = 0, tlAnim2CallbackCheck = 0;
        var tl = Presto.CreateTimeline(TimelineCallbackParameters with { Loop = 2, OnComplete = _ => tlCallbackCheck++ })
            .Add(target, ChildCallbackParameters with { OnComplete = _ => tlAnim1CallbackCheck++ })
            .Add(target, ChildCallbackParameters with { OnComplete = _ => tlAnim2CallbackCheck++ })
            .Init();

        Assert.Equal(0, tlCallbackCheck);
        Assert.Equal(0, tlAnim1CallbackCheck);
        Assert.Equal(0, tlAnim2CallbackCheck);
        tl.Seek(90);
        Assert.Equal(1, tlAnim1CallbackCheck);
        Assert.Equal(0, tlAnim2CallbackCheck);
        tl.Seek(180);
        Assert.Equal(1, tlAnim1CallbackCheck);
        Assert.Equal(1, tlAnim2CallbackCheck);
        tl.Seek(200);
        Assert.Equal(1, tlAnim1CallbackCheck);
        Assert.Equal(1, tlAnim2CallbackCheck);
        tl.Seek(160);
        Assert.Equal(1, tlAnim1CallbackCheck);
        Assert.Equal(1, tlAnim2CallbackCheck);
        tl.Seek(90);
        Assert.Equal(1, tlAnim1CallbackCheck);
        Assert.Equal(1, tlAnim2CallbackCheck);
        tl.Seek(0);
        Assert.Equal(1, tlAnim1CallbackCheck);
        Assert.Equal(1, tlAnim2CallbackCheck);
        tl.Seek(90);
        Assert.Equal(2, tlAnim1CallbackCheck);
        Assert.Equal(1, tlAnim2CallbackCheck);
        tl.Seek(180);
        Assert.Equal(2, tlAnim1CallbackCheck);
        Assert.Equal(2, tlAnim2CallbackCheck);
        tl.Seek(540);
        Assert.Equal(1, tlCallbackCheck);
        Assert.Equal(4, tlAnim1CallbackCheck);
        Assert.Equal(4, tlAnim2CallbackCheck);
    }

    [Fact]
    public void OnBeginAndOnCompleteOnLoopedTimeline()
    {
        int tlOnBeginCheck = 0, tlOnCompleteCheck = 0, childOnBeginCheck = 0, childOnCompleteCheck = 0;
        var tl = Presto.CreateTimeline(new()
        {
            Loop = 2,
            LoopDelay = 10,
            OnBegin = _ => tlOnBeginCheck++,
            OnComplete = _ => tlOnCompleteCheck++,
        })
        .Add(new TimerParams
        {
            Delay = 10,
            Duration = 80,
            OnBegin = _ => childOnBeginCheck++,
            OnComplete = _ => childOnCompleteCheck++,
        });

        Assert.Equal(0, tlOnBeginCheck);
        Assert.Equal(0, tlOnCompleteCheck);
        Assert.Equal(0, childOnBeginCheck);
        Assert.Equal(0, childOnCompleteCheck);
        tl.Seek(5);
        Assert.Equal(1, tlOnBeginCheck);
        Assert.Equal(0, tlOnCompleteCheck);
        Assert.Equal(0, childOnBeginCheck);
        Assert.Equal(0, childOnCompleteCheck);
        tl.Seek(11);
        Assert.Equal(1, tlOnBeginCheck);
        Assert.Equal(0, tlOnCompleteCheck);
        Assert.Equal(1, childOnBeginCheck);
        Assert.Equal(0, childOnCompleteCheck);
        tl.Seek(100);
        Assert.Equal(1, tlOnBeginCheck);
        Assert.Equal(0, tlOnCompleteCheck);
        Assert.Equal(1, childOnBeginCheck);
        Assert.Equal(1, childOnCompleteCheck);
        tl.Seek(200);
        Assert.Equal(1, tlOnBeginCheck);
        Assert.Equal(0, tlOnCompleteCheck);
        Assert.Equal(2, childOnBeginCheck);
        Assert.Equal(2, childOnCompleteCheck);
        tl.Seek(300);
        Assert.Equal(1, tlOnBeginCheck);
        Assert.Equal(1, tlOnCompleteCheck);
        Assert.Equal(3, childOnBeginCheck);
        Assert.Equal(3, childOnCompleteCheck);
    }

    [Fact]
    public void OnBeginAndOnCompleteOnAlternateTimeline()
    {
        int tlOnBeginCheck = 0, tlOnCompleteCheck = 0;
        int child1OnBeginCheck = 0, child1OnCompleteCheck = 0, child2OnBeginCheck = 0, child2OnCompleteCheck = 0;
        var tl = Presto.CreateTimeline(new()
        {
            Loop = 2,
            Alternate = true,
            OnBegin = _ => tlOnBeginCheck++,
            OnComplete = _ => tlOnCompleteCheck++,
        })
        .Add(new TimerParams { Duration = 100, OnBegin = _ => child1OnBeginCheck++, OnComplete = _ => child1OnCompleteCheck++ })
        .Add(new TimerParams { Duration = 100, OnBegin = _ => child2OnBeginCheck++, OnComplete = _ => child2OnCompleteCheck++ });

        Assert.Equal(0, tlOnBeginCheck);
        Assert.Equal(0, tlOnCompleteCheck);
        Assert.Equal(0, child1OnBeginCheck);
        Assert.Equal(0, child1OnCompleteCheck);
        Assert.Equal(0, child2OnBeginCheck);
        Assert.Equal(0, child2OnCompleteCheck);
        tl.Seek(5);
        Assert.Equal(1, tlOnBeginCheck);
        Assert.Equal(0, tlOnCompleteCheck);
        Assert.Equal(1, child1OnBeginCheck);
        Assert.Equal(0, child1OnCompleteCheck);
        Assert.Equal(0, child2OnBeginCheck);
        Assert.Equal(0, child2OnCompleteCheck);
        tl.Seek(10);
        Assert.Equal(1, tlOnBeginCheck);
        Assert.Equal(0, tlOnCompleteCheck);
        Assert.Equal(1, child1OnBeginCheck);
        Assert.Equal(0, child1OnCompleteCheck);
        Assert.Equal(0, child2OnBeginCheck);
        Assert.Equal(0, child2OnCompleteCheck);
        tl.Seek(100);
        Assert.Equal(1, tlOnBeginCheck);
        Assert.Equal(0, tlOnCompleteCheck);
        Assert.Equal(1, child1OnBeginCheck);
        Assert.Equal(1, child1OnCompleteCheck);
        Assert.Equal(0, child2OnBeginCheck);
        Assert.Equal(0, child2OnCompleteCheck);
        tl.Seek(110);
        Assert.Equal(1, tlOnBeginCheck);
        Assert.Equal(0, tlOnCompleteCheck);
        Assert.Equal(1, child1OnBeginCheck);
        Assert.Equal(1, child1OnCompleteCheck);
        Assert.Equal(1, child2OnBeginCheck);
        Assert.Equal(0, child2OnCompleteCheck);
        tl.Seek(200);
        Assert.Equal(1, tlOnBeginCheck);
        Assert.Equal(0, tlOnCompleteCheck);
        Assert.Equal(1, child1OnBeginCheck);
        Assert.Equal(1, child1OnCompleteCheck);
        Assert.Equal(1, child2OnBeginCheck);
        // Upstream edge case: the last child's onComplete does not fire before an alternate loop.
        Assert.Equal(0, child2OnCompleteCheck);
        tl.Seek(210);
        Assert.Equal(1, tlOnBeginCheck);
        Assert.Equal(0, tlOnCompleteCheck);
        Assert.Equal(1, child1OnBeginCheck);
        Assert.Equal(1, child1OnCompleteCheck);
        Assert.Equal(1, child2OnBeginCheck);
        Assert.Equal(0, child2OnCompleteCheck);
        tl.Seek(400);
        Assert.Equal(1, tlOnBeginCheck);
        Assert.Equal(0, tlOnCompleteCheck);
        Assert.Equal(1, child1OnBeginCheck);
        Assert.Equal(1, child1OnCompleteCheck);
        Assert.Equal(1, child2OnBeginCheck);
        Assert.Equal(0, child2OnCompleteCheck);
        tl.Seek(410);
        Assert.Equal(1, tlOnBeginCheck);
        Assert.Equal(0, tlOnCompleteCheck);
        Assert.Equal(2, child1OnBeginCheck);
        Assert.Equal(1, child1OnCompleteCheck);
        Assert.Equal(1, child2OnBeginCheck);
        Assert.Equal(0, child2OnCompleteCheck);
        tl.Seek(600);
        Assert.Equal(1, tlOnBeginCheck);
        Assert.Equal(1, tlOnCompleteCheck);
        Assert.Equal(2, child1OnBeginCheck);
        Assert.Equal(2, child1OnCompleteCheck);
        Assert.Equal(2, child2OnBeginCheck);
        Assert.Equal(1, child2OnCompleteCheck);
    }

    [Fact]
    public void OnCompleteOnReversedTimelineShouldCompleteAndIgnoreChildren()
    {
        int tlOnBeginCheck = 0, tlOnCompleteCheck = 0;
        int child1OnBeginCheck = 0, child1OnCompleteCheck = 0, child2OnBeginCheck = 0, child2OnCompleteCheck = 0;
        var tl = Presto.CreateTimeline(new()
        {
            Reversed = true,
            Defaults = new() { Ease = "linear", Duration = 100 },
            OnBegin = _ => tlOnBeginCheck++,
            OnComplete = _ => tlOnCompleteCheck++,
        })
        .Add(new StyleTarget(), new() { ["Y"] = 100, OnBegin = _ => child1OnBeginCheck++, OnComplete = _ => child1OnCompleteCheck++ })
        .Add(new StyleTarget(), new() { ["Y"] = 100, OnBegin = _ => child2OnBeginCheck++, OnComplete = _ => child2OnCompleteCheck++ });

        Assert.Equal(0, tlOnBeginCheck);
        Assert.Equal(0, tlOnCompleteCheck);
        tl.Seek(5);
        Assert.Equal(1, tlOnBeginCheck);
        Assert.Equal(0, tlOnCompleteCheck);
        tl.Seek(10);
        Assert.Equal(1, tlOnBeginCheck);
        Assert.Equal(0, tlOnCompleteCheck);
        tl.Seek(100);
        Assert.Equal(1, tlOnBeginCheck);
        Assert.Equal(0, tlOnCompleteCheck);
        tl.Seek(110);
        Assert.Equal(1, tlOnBeginCheck);
        Assert.Equal(0, tlOnCompleteCheck);
        tl.Seek(200);
        Assert.Equal(1, tlOnBeginCheck);
        Assert.Equal(1, tlOnCompleteCheck);
        Assert.Equal(0, child1OnBeginCheck);
        Assert.Equal(0, child1OnCompleteCheck);
        Assert.Equal(0, child2OnBeginCheck);
        Assert.Equal(0, child2OnCompleteCheck);
    }

    [Fact]
    public void OnCompleteOnTimelineAfterCallingReverse()
    {
        int tlOnBeginCheck = 0, tlOnCompleteCheck = 0;
        int child1OnBeginCheck = 0, child1OnCompleteCheck = 0, child2OnBeginCheck = 0, child2OnCompleteCheck = 0;
        var tl = Presto.CreateTimeline(new()
        {
            Defaults = new() { Ease = "linear", Duration = 100 },
            OnBegin = _ => tlOnBeginCheck++,
            OnComplete = _ => tlOnCompleteCheck++,
        })
        .Add(new StyleTarget(), new() { ["Y"] = 100, OnBegin = _ => child1OnBeginCheck++, OnComplete = _ => child1OnCompleteCheck++ })
        .Add(new StyleTarget(), new() { ["Y"] = 100, OnBegin = _ => child2OnBeginCheck++, OnComplete = _ => child2OnCompleteCheck++ });

        Assert.Equal(0, tlOnBeginCheck);
        Assert.Equal(0, tlOnCompleteCheck);
        Assert.Equal(0, child1OnBeginCheck);
        Assert.Equal(0, child1OnCompleteCheck);
        Assert.Equal(0, child2OnBeginCheck);
        Assert.Equal(0, child2OnCompleteCheck);
        tl.Seek(5);
        Assert.Equal(1, tlOnBeginCheck);
        Assert.Equal(0, tlOnCompleteCheck);
        Assert.Equal(1, child1OnBeginCheck);
        Assert.Equal(0, child1OnCompleteCheck);
        Assert.Equal(0, child2OnBeginCheck);
        Assert.Equal(0, child2OnCompleteCheck);
        tl.Seek(10);
        Assert.Equal(1, tlOnBeginCheck);
        Assert.Equal(0, tlOnCompleteCheck);
        Assert.Equal(1, child1OnBeginCheck);
        Assert.Equal(0, child1OnCompleteCheck);
        Assert.Equal(0, child2OnBeginCheck);
        Assert.Equal(0, child2OnCompleteCheck);
        tl.Seek(100);
        Assert.Equal(1, tlOnBeginCheck);
        Assert.Equal(0, tlOnCompleteCheck);
        Assert.Equal(1, child1OnBeginCheck);
        Assert.Equal(1, child1OnCompleteCheck);
        Assert.Equal(0, child2OnBeginCheck);
        Assert.Equal(0, child2OnCompleteCheck);
        tl.Seek(110);
        Assert.Equal(1, tlOnBeginCheck);
        Assert.Equal(0, tlOnCompleteCheck);
        Assert.Equal(1, child1OnBeginCheck);
        Assert.Equal(1, child1OnCompleteCheck);
        Assert.Equal(1, child2OnBeginCheck);
        Assert.Equal(0, child2OnCompleteCheck);
        tl.Reverse();
        tl.Seek(200);
        Assert.Equal(1, tlOnBeginCheck);
        Assert.Equal(1, tlOnCompleteCheck);
        Assert.Equal(1, child1OnBeginCheck);
        Assert.Equal(1, child1OnCompleteCheck);
        Assert.Equal(1, child2OnBeginCheck);
        Assert.Equal(0, child2OnCompleteCheck);
    }

    [Fact]
    public void OnBeginAndOnCompleteOnAlternateTimelineWithReversedChildren()
    {
        int tlOnBeginCheck = 0, tlOnCompleteCheck = 0;
        int child1OnBeginCheck = 0, child1OnCompleteCheck = 0, child2OnBeginCheck = 0, child2OnCompleteCheck = 0;
        var tl = Presto.CreateTimeline(new()
        {
            Loop = 2,
            Alternate = true,
            Defaults = new() { Ease = "linear", Reversed = true, Duration = 100 },
            OnBegin = _ => tlOnBeginCheck++,
            OnComplete = _ => tlOnCompleteCheck++,
        })
        .Add(new StyleTarget(), new() { ["Y"] = 100, OnBegin = _ => child1OnBeginCheck++, OnComplete = _ => child1OnCompleteCheck++ })
        .Add(new StyleTarget(), new() { ["Y"] = 100, OnBegin = _ => child2OnBeginCheck++, OnComplete = _ => child2OnCompleteCheck++ });

        Assert.Equal(0, tlOnBeginCheck);
        Assert.Equal(0, tlOnCompleteCheck);
        tl.Seek(5);
        Assert.Equal(1, tlOnBeginCheck);
        Assert.Equal(0, tlOnCompleteCheck);
        tl.Seek(10);
        Assert.Equal(1, tlOnBeginCheck);
        Assert.Equal(0, tlOnCompleteCheck);
        tl.Seek(100);
        Assert.Equal(1, tlOnBeginCheck);
        Assert.Equal(0, tlOnCompleteCheck);
        tl.Seek(110);
        Assert.Equal(1, tlOnBeginCheck);
        Assert.Equal(0, tlOnCompleteCheck);
        tl.Seek(210);
        Assert.Equal(1, tlOnBeginCheck);
        Assert.Equal(0, tlOnCompleteCheck);
        tl.Seek(380);
        Assert.Equal(1, tlOnBeginCheck);
        Assert.Equal(0, tlOnCompleteCheck);
        tl.Seek(600);
        Assert.Equal(1, tlOnBeginCheck);
        Assert.Equal(1, tlOnCompleteCheck);
        Assert.Equal(0, child1OnBeginCheck);
        Assert.Equal(0, child1OnCompleteCheck);
        Assert.Equal(0, child2OnBeginCheck);
        Assert.Equal(0, child2OnCompleteCheck);
    }

    [Fact]
    public async Task OnCompleteOnTimelineWithAnimationsOfTheSameDurationAsTheTimeline()
    {
        var target = new Dictionary<string, object?> { ["Value"] = 1d };
        int tlCallbackCheck = 0, a1 = 0, a2 = 0, a3 = 0, a4 = 0;
        var tl = Presto.CreateTimeline(new() { OnComplete = _ => tlCallbackCheck++ })
            .Add(target, new() { ["Value"] = 0, Duration = 1.8, OnComplete = _ => a1++ }, 0)
            .Add(target, new() { ["Value"] = 1, Duration = 1.4, OnComplete = _ => a2++ }, .4)
            .Add(target, new() { ["Value"] = 2, Duration = 1, OnComplete = _ => a3++ }, .8)
            .Add(target, new() { ["Value"] = 3, Duration = 1, OnComplete = _ => a4++ }, .8);

        var task = tl.Then();
        RunUntil(() => task.IsCompleted);
        await task;

        Assert.Equal(1, tlCallbackCheck);
        Assert.Equal(1, a1);
        Assert.Equal(1, a2);
        Assert.Equal(1, a3);
        Assert.Equal(1, a4);
        Assert.True(tl.Completed);
    }

    [Fact]
    public void OnBeforeUpdateOnTimeline()
    {
        var target = new StyleTarget();
        var ticks = 0;
        bool tlCallbackCheck = false, tlAnim1CallbackCheck = false, tlAnim2CallbackCheck = false;
        var tl = Presto.CreateTimeline(TimelineCallbackParameters with { OnBeforeUpdate = _ => { ticks++; tlCallbackCheck = true; } })
            .Add(target, ChildCallbackParameters with { OnBeforeUpdate = _ => { ticks++; tlAnim1CallbackCheck = true; } })
            .Add(target, ChildCallbackParameters with { OnBeforeUpdate = _ => { ticks++; tlAnim2CallbackCheck = true; } })
            .Init();

        Assert.False(tlCallbackCheck);
        Assert.False(tlAnim1CallbackCheck);
        Assert.False(tlAnim2CallbackCheck);
        tl.Seek(5);
        Assert.Equal(1, ticks);
        Assert.True(tlCallbackCheck);
        Assert.False(tlAnim1CallbackCheck);
        Assert.False(tlAnim2CallbackCheck);
        tl.Seek(9);
        Assert.Equal(2, ticks);
        Assert.False(tlAnim1CallbackCheck);
        Assert.False(tlAnim2CallbackCheck);
        tl.Seek(10);
        Assert.Equal(3, ticks);
        Assert.False(tlAnim1CallbackCheck);
        Assert.False(tlAnim2CallbackCheck);
        tl.Seek(11);
        Assert.Equal(5, ticks);
        Assert.True(tlAnim1CallbackCheck);
        Assert.False(tlAnim2CallbackCheck);
        tl.Seek(150);
        Assert.Equal(8, ticks);
        Assert.True(tlAnim2CallbackCheck);
        tl.Seek(250);
        Assert.Equal(10, ticks);
    }

    [Fact]
    public void OnUpdateOnTimeline()
    {
        var target = new StyleTarget();
        var ticks = 0;
        bool tlCallbackCheck = false, tlAnim1CallbackCheck = false, tlAnim2CallbackCheck = false;
        var tl = Presto.CreateTimeline(TimelineCallbackParameters with { OnUpdate = _ => { ticks++; tlCallbackCheck = true; } })
            .Add(target, ChildCallbackParameters with { OnUpdate = _ => { ticks++; tlAnim1CallbackCheck = true; } })
            .Add(target, ChildCallbackParameters with { OnUpdate = _ => { ticks++; tlAnim2CallbackCheck = true; } })
            .Init();

        Assert.False(tlCallbackCheck);
        Assert.False(tlAnim1CallbackCheck);
        Assert.False(tlAnim2CallbackCheck);
        tl.Seek(5);
        Assert.Equal(1, ticks);
        Assert.True(tlCallbackCheck);
        Assert.False(tlAnim1CallbackCheck);
        Assert.False(tlAnim2CallbackCheck);
        tl.Seek(9);
        Assert.Equal(2, ticks);
        Assert.False(tlAnim1CallbackCheck);
        Assert.False(tlAnim2CallbackCheck);
        tl.Seek(10);
        Assert.Equal(3, ticks);
        Assert.False(tlAnim1CallbackCheck);
        Assert.False(tlAnim2CallbackCheck);
        tl.Seek(11);
        Assert.Equal(5, ticks);
        Assert.True(tlAnim1CallbackCheck);
        Assert.False(tlAnim2CallbackCheck);
        tl.Seek(150);
        Assert.Equal(8, ticks);
        Assert.True(tlAnim2CallbackCheck);
        tl.Seek(250);
        Assert.Equal(10, ticks);
    }

    [Fact]
    public void OnRenderOnTimeline()
    {
        var target = new StyleTarget();
        bool tlCallbackCheck = false, tlAnim1CallbackCheck = false, tlAnim2CallbackCheck = false;
        var tl = Presto.CreateTimeline(TimelineCallbackParameters with { OnRender = _ => tlCallbackCheck = true })
            .Add(target, ChildCallbackParameters with { OnRender = _ => tlAnim1CallbackCheck = true })
            .Add(target, ChildCallbackParameters with { OnRender = _ => tlAnim2CallbackCheck = true })
            .Init();

        Assert.False(tlCallbackCheck);
        Assert.False(tlAnim1CallbackCheck);
        Assert.False(tlAnim2CallbackCheck);
        tl.Seek(5);
        Assert.False(tlCallbackCheck);
        Assert.False(tlAnim1CallbackCheck);
        Assert.False(tlAnim2CallbackCheck);
        tl.Seek(9);
        Assert.False(tlCallbackCheck);
        Assert.False(tlAnim1CallbackCheck);
        Assert.False(tlAnim2CallbackCheck);
        tl.Seek(10);
        Assert.False(tlCallbackCheck);
        Assert.False(tlAnim1CallbackCheck);
        Assert.False(tlAnim2CallbackCheck);
        tl.Seek(50);
        Assert.True(tlCallbackCheck);
        Assert.True(tlAnim1CallbackCheck);
        Assert.False(tlAnim2CallbackCheck);
        tl.Seek(150);
        Assert.True(tlCallbackCheck);
        Assert.True(tlAnim1CallbackCheck);
        Assert.True(tlAnim2CallbackCheck);
    }

    [Fact]
    public void OnLoopOnTimeline()
    {
        var loops = 0;
        var tl = Presto.CreateTimeline(new()
        {
            Loop = 1,
            LoopDelay = 20,
            Autoplay = false,
            OnLoop = _ => loops++,
        })
        .Add(new StyleTarget(), new()
        {
            ["X"] = 100,
            Duration = 80,
            Loop = 1,
            LoopDelay = 20,
            OnLoop = _ => loops++,
        })
        .Init();

        tl.Seek(5);
        Assert.Equal(0, loops);
        tl.Seek(100);
        Assert.Equal(1, loops);
        tl.Seek(180);
        Assert.Equal(1, loops);
        tl.Seek(200);
        Assert.Equal(2, loops);
        tl.Seek(300);
        Assert.Equal(3, loops);
        tl.Seek(380);
        Assert.Equal(3, loops);
        tl.Seek(400);
        Assert.Equal(3, loops);
        tl.Seek(401);
        Assert.Equal(3, loops);
    }

    [Fact]
    public async Task OnPauseOnTimelineTweenOverride()
    {
        var target = new StyleTarget();
        var paused = 0;
        _ = Presto.CreateTimeline(new() { OnPause = _ => paused++ })
            .Add(target, new() { ["X"] = 200, Duration = 30 }, 0)
            .Add(target, new() { ["Y"] = 200, Duration = 30 }, 0);

        var task = Presto.CreateTimeline(new() { Delay = 10 })
            .Add(target, new() { ["X"] = 100, Duration = 30 }, 0)
            .Add(target, new() { ["Y"] = 100, Duration = 30 }, 0)
            .Then();
        RunUntil(() => task.IsCompleted);
        await task;

        Assert.Equal(1, paused);
    }

    [Fact]
    public async Task AllCallbacksButOnLoopTriggerOnZeroDurationTimeline()
    {
        var target = new StyleTarget();
        int onBeginCheck = 0, onBeforeUpdateCheck = 0, onUpdateCheck = 0, onRenderCheck = 0, onLoopCheck = 0, onCompleteCheck = 0;
        int a1Begin = 0, a1BeforeUpdate = 0, a1Update = 0, a1Render = 0, a1Loop = 0, a1Complete = 0;
        int a2Begin = 0, a2BeforeUpdate = 0, a2Update = 0, a2Render = 0, a2Loop = 0, a2Complete = 0;

        var tl = Presto.CreateTimeline(new()
        {
            Id = "TL",
            Defaults = new() { Duration = 0 },
            OnBegin = _ => onBeginCheck++,
            OnBeforeUpdate = _ => onBeforeUpdateCheck++,
            OnUpdate = _ => onUpdateCheck++,
            OnRender = _ => onRenderCheck++,
            OnLoop = _ => onLoopCheck++,
            OnComplete = _ => onCompleteCheck++,
        })
        .Add(target, new()
        {
            ["TranslateX"] = 100,
            Id = "A1",
            OnBegin = _ => a1Begin++,
            OnBeforeUpdate = _ => a1BeforeUpdate++,
            OnUpdate = _ => a1Update++,
            OnRender = _ => a1Render++,
            OnLoop = _ => a1Loop++,
            OnComplete = _ => a1Complete++,
        })
        .Add(target, new()
        {
            ["TranslateY"] = 100,
            Id = "A2",
            OnBegin = _ => a2Begin++,
            OnBeforeUpdate = _ => a2BeforeUpdate++,
            OnUpdate = _ => a2Update++,
            OnRender = _ => a2Render++,
            OnLoop = _ => a2Loop++,
            OnComplete = _ => a2Complete++,
        });

        var task = tl.Then();
        RunUntil(() => task.IsCompleted);
        await task;

        Assert.Equal(1, onBeginCheck);
        Assert.Equal(1, onBeforeUpdateCheck);
        Assert.Equal(1, onUpdateCheck);
        Assert.Equal(1, onRenderCheck);
        Assert.Equal(0, onLoopCheck);
        Assert.Equal(1, onCompleteCheck);
        Assert.True(tl.Began);
        Assert.True(tl.Completed);

        Assert.Equal(1, a1Begin);
        Assert.Equal(1, a1BeforeUpdate);
        Assert.Equal(1, a1Update);
        Assert.Equal(1, a1Render);
        Assert.Equal(0, a1Loop);
        Assert.Equal(1, a1Complete);

        Assert.Equal(1, a2Begin);
        Assert.Equal(1, a2BeforeUpdate);
        Assert.Equal(1, a2Update);
        Assert.Equal(1, a2Render);
        Assert.Equal(0, a2Loop);
        Assert.Equal(1, a2Complete);
    }

    [Fact]
    public void OnUpdateAssignedAfterCreationFiresOnTheNextTick()
    {
        var ticks = 0;
        var timer = Presto.CreateTimer(new() { Duration = 1000 });
        Frames.Advance(16);

        timer.OnUpdate = _ => ticks++;
        Frames.Advance(16);

        Assert.Equal(1, ticks);
    }

    [Fact]
    public void OnCompleteReplacedAfterCreationFiresTheNewCallback()
    {
        var first = 0;
        var second = 0;
        var timer = Presto.CreateTimer(new() { Duration = 50, OnComplete = _ => first++ });
        Frames.Advance(16);

        timer.OnComplete = _ => second++;
        RunUntil(() => timer.Completed);

        Assert.Equal(0, first);
        Assert.Equal(1, second);
    }

    [Fact]
    public void CallbacksComeFromTheParametersOrTheInheritedDefaultsAsTheyAreAtConstruction()
    {
        var log = new List<string>();
        Action<Timer> engineBegin = _ => log.Add("engine begin");
        Action<Timer> engineComplete = _ => log.Add("engine complete");
        Action<Timer> ownComplete = _ => log.Add("own complete");
        Action<Timer> timelineBegin = _ => log.Add("timeline begin");
        Presto.Defaults.OnBegin = engineBegin;
        Presto.Defaults.OnComplete = engineComplete;

        var timer = Presto.CreateTimer(new() { Duration = 30, Autoplay = false, OnComplete = ownComplete });
        var tl = Presto.CreateTimeline(new() { Autoplay = false, Defaults = new() { OnBegin = timelineBegin } })
            .Add(new TimerParams { Duration = 30 });
        var child = TestHelpers.GetChildAtIndex(tl, 0)!;
        Presto.Defaults.OnBegin = null;
        Presto.Defaults.OnComplete = null;

        Assert.Same(engineBegin, timer.OnBegin);
        Assert.Same(ownComplete, timer.OnComplete);
        Assert.Same(timelineBegin, child.OnBegin);
        Assert.Same(engineComplete, child.OnComplete);

        timer.Play();
        RunUntil(() => timer.Completed);
        Assert.Equal(["engine begin", "own complete"], log);
    }

    [Fact]
    public void ATimelineFiresItsOwnUpdateCallbacksBeforeItsChildrenRender()
    {
        var log = new List<string>();
        var target = new StyleTarget();
        double? xInTimelineUpdate = null;
        Presto.CreateTimeline(new()
        {
            OnBeforeUpdate = _ => log.Add("timeline before"),
            OnUpdate = _ =>
            {
                log.Add("timeline update");
                xInTimelineUpdate = target.X;
            },
        })
        .Add(target, new()
        {
            ["X"] = 100,
            Duration = 100,
            Ease = "linear",
            OnBeforeUpdate = _ => log.Add("child before"),
            OnUpdate = _ => log.Add("child update"),
        });
        Frames.Advance(48, 16);
        var previousX = target.X;

        log.Clear();
        Frames.Advance(16);

        Assert.Equal(["timeline before", "timeline update", "child before", "child update"], log);
        Assert.Equal(previousX, xInTimelineUpdate);
        Assert.True(target.X > previousX);
    }

    [Fact]
    public void OnRenderIsFixedAtConstruction()
    {
        Assert.All(new[] { typeof(Timer), typeof(Animation), typeof(Timeline) }, type =>
            Assert.DoesNotContain(type.GetProperties(), p => p.Name == "OnRender" && p.SetMethod?.IsPublic == true));

        var early = 0;
        var late = 0;
        Presto.Defaults.OnRender = _ => early++;
        var withDefault = Presto.Animate(new StyleTarget(), new() { ["X"] = 100, Duration = 100 });
        Presto.Defaults.OnRender = null;
        var withoutDefault = Presto.Animate(new StyleTarget(), new() { ["X"] = 100, Duration = 100 });
        Presto.Defaults.OnRender = _ => late++;

        Frames.Advance(48, 16);

        Assert.True(early > 0);
        Assert.Equal(0, late);
        Assert.True(withDefault.CurrentTime > 0);
        Assert.True(withoutDefault.CurrentTime > 0);
    }

    [Fact]
    public void MutedRendersFireNoCallbacksButStillUpdateBeganAndCompleted()
    {
        var fired = new List<string>();
        var animation = Presto.Animate(new StyleTarget(), new()
        {
            ["X"] = 100,
            Duration = 100,
            Loop = 1,
            Autoplay = false,
            OnBegin = _ => fired.Add("begin"),
            OnBeforeUpdate = _ => fired.Add("before update"),
            OnRender = _ => fired.Add("render"),
            OnUpdate = _ => fired.Add("update"),
            OnLoop = _ => fired.Add("loop"),
            OnComplete = _ => fired.Add("complete"),
        });

        animation.Seek(150, muteCallbacks: true);
        Assert.True(animation.Began);
        Assert.False(animation.Completed);

        animation.Seek(200, muteCallbacks: true);
        Assert.True(animation.Completed);

        animation.Reset();
        Assert.False(animation.Began);
        Assert.False(animation.Completed);

        animation.Complete(muteCallbacks: true);
        Assert.True(animation.Completed);

        animation.Seek(50, muteCallbacks: true);
        animation.Revert();

        Assert.Empty(fired);
    }

    [Fact]
    public void CallbacksRunSynchronouslyOnTheThreadThatTicksTheEngine()
    {
        int? workerThread = null;
        int? callbackThread = null;
        var firedDuringAdvance = false;
        var worker = new Thread(() =>
        {
            workerThread = Environment.CurrentManagedThreadId;
            var source = new ManualFrameSource();
            var engine = new Engine(source);
            using (engine.MakeCurrent())
            {
                Presto.CreateTimer(new() { Duration = 100, OnUpdate = _ => callbackThread = Environment.CurrentManagedThreadId });
            }
            source.Advance(16);
            firedDuringAdvance = callbackThread != null;
        });

        worker.Start();
        worker.Join();

        Assert.True(firedDuringAdvance);
        Assert.NotNull(workerThread);
        Assert.Equal(workerThread, callbackThread);
    }

    [Fact]
    public void UnsetCallbacksFireNothing()
    {
        var timer = Presto.CreateTimer(new() { Duration = 30 });

        Assert.Null(timer.OnBegin);
        Assert.Null(timer.OnBeforeUpdate);
        Assert.Null(timer.OnUpdate);
        Assert.Null(timer.OnLoop);
        Assert.Null(timer.OnPause);
        Assert.Null(timer.OnComplete);

        RunUntil(() => timer.Completed);
        Assert.True(timer.Completed);
    }
}
