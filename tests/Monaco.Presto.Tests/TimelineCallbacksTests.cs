using Monaco.Presto.Core;

namespace Monaco.Presto.Tests;

/// <summary>Port of the label, <c>.set()</c>, <c>.call()</c> and <c>.sync()</c> cases of anime.js tests/suites/timelines.test.js.</summary>
public sealed class TimelineCallbacksTests : EngineTestBase
{
    private static StyleTarget[] StaggeredTargets() => [new(), new(), new()];

    /// <summary>The suite's mixed <c>.set()</c> and <c>.add()</c> timeline: every target holds the set value until its own delay elapses.</summary>
    private static Timeline SteppedOpacityTimeline(StyleTarget[] targets) =>
        Presto.CreateTimeline(new() { Loop = 2, Autoplay = false })
            .Set(targets, new() { ["Opacity"] = .5 }, 0)
            .Add(targets, new()
            {
                Duration = 500,
                ["Opacity"] = TweenValue.FromTo(1, .5),
                Delay = new FunctionValue((_, i, _) => i * 500),
                Ease = Eases.Steps(1),
            }, 0);

    [Fact]
    public void TimelineLabelsPositions()
    {
        var target = new StyleTarget();
        var tl = Presto.CreateTimeline(new() { Defaults = new() { Duration = 10 } })
            .Label("A starts", 50)
            .Label("B starts", 100)
            .Label("C")
            .Add(target, new() { ["TranslateX"] = 50 }, "A starts")
            .Label("A ends", "<")
            .Label("D")
            .Add(target, new() { ["TranslateX"] = -50 }, "B starts")
            .Add(target, new() { ["TranslateX"] = 0 }, "A ends")
            .Add(target, new() { ["TranslateX"] = 100 }, "C")
            .Add(target, new() { ["TranslateX"] = 150 }, "D");

        Assert.Equal(50, tl.Labels["A starts"]);
        Assert.Equal(100, tl.Labels["B starts"]);
        Assert.Equal(60, tl.Labels["A ends"]);
        Assert.Equal(0, tl.Labels["C"]);
        Assert.Equal(60, tl.Labels["D"]);
        Assert.Equal(tl.Labels["A starts"], TestHelpers.OffsetOf(tl, 0));
        Assert.Equal(tl.Labels["B starts"], TestHelpers.OffsetOf(tl, 1));
        Assert.Equal(tl.Labels["A ends"], TestHelpers.OffsetOf(tl, 2));
        Assert.Equal(tl.Labels["C"], TestHelpers.OffsetOf(tl, 3));
        Assert.Equal(tl.Labels["D"], TestHelpers.OffsetOf(tl, 4));
        Assert.Equal(110, tl.Duration);
    }

    [Fact]
    public void TimelineSet()
    {
        var target = new StyleTarget();
        Presto.CreateTimeline(new() { Autoplay = false })
            .Set(target, new() { ["Y"] = -300 }, 2000)
            .Seek(2000);

        Assert.Equal(-300, target.Y);
    }

    [Fact]
    public void TimelineMixSetAndAdd()
    {
        var target = new StyleTarget();
        var tl = Presto.CreateTimeline(new() { Autoplay = false, Defaults = new() { Ease = "linear" } })
            .Set(target, new() { ["TranslateX"] = 50 })
            .Add(target, new() { Duration = 200, ["TranslateX"] = 100 })
            .Set(target, new() { ["TranslateX"] = 200 })
            .Add(target, new() { Duration = 200, ["TranslateX"] = 400 })
            .Set(target, new() { ["TranslateX"] = -100 }, 800);

        tl.Seek(0);
        Assert.Equal("50px", target.TranslateX);
        tl.Seek(100);
        Assert.Equal("75px", target.TranslateX);
        tl.Seek(200);
        Assert.Equal("200px", target.TranslateX);
        tl.Seek(300);
        Assert.Equal("300px", target.TranslateX);
        tl.Seek(800);
        Assert.Equal("-100px", target.TranslateX);
    }

    [Fact]
    public void SetDoesNotMutateTheParametersItIsGiven()
    {
        var shared = new AnimationParams { ["TranslateX"] = 100, Duration = 200 };
        var tl = Presto.CreateTimeline(new() { Autoplay = false })
            .Set(new StyleTarget(), shared, 0)
            .Add(new StyleTarget(), shared, 0);

        Assert.Equal(Constants.MinValue, TestHelpers.GetChildAtIndex(tl, 0)!.Duration);
        Assert.Equal(200, TestHelpers.GetChildAtIndex(tl, 1)!.Duration);
    }

    [Fact]
    public void CallCallbacks()
    {
        int first = 0, second = 0, third = 0;
        var completed = false;
        var tl = Presto.CreateTimeline(new() { OnComplete = _ => completed = true })
            .Call(_ => first++, 0)
            .Call(_ => second++, 20)
            .Call(_ => third++, 40);

        RunUntil(() => completed);

        Assert.True(completed);
        Assert.Equal(1, first);
        Assert.Equal(1, second);
        Assert.Equal(1, third);
        Assert.Equal(40, tl.Duration);
    }

    [Fact]
    public void CallCallbacksOnAZeroDurationTimeline()
    {
        int first = 0, second = 0, third = 0;
        var completed = false;
        var tl = Presto.CreateTimeline(new() { OnComplete = _ => completed = true })
            .Call(_ => first++, 0)
            .Call(_ => second++, 0)
            .Call(_ => third++, 0);

        RunUntil(() => completed);

        Assert.True(completed);
        Assert.Equal(1, first);
        Assert.Equal(1, second);
        Assert.Equal(1, third);
        Assert.Equal(Constants.MinValue, tl.Duration);
    }

    [Fact]
    public void CallCallbacksMultipleTimesViaSeek()
    {
        int first = 0, second = 0, third = 0;
        var tl = Presto.CreateTimeline(new() { Autoplay = false })
            .Call(_ => first++, 0)
            .Call(_ => second++, 1000)
            .Call(_ => third++, 2000);

        Assert.Equal(0, first);
        Assert.Equal(0, second);
        Assert.Equal(0, third);

        tl.Seek(500);
        Assert.Equal(1, first);
        Assert.Equal(0, second);
        Assert.Equal(0, third);

        tl.Seek(1000);
        Assert.Equal(1, first);
        Assert.Equal(1, second);
        Assert.Equal(0, third);

        tl.Seek(2000);
        Assert.Equal(1, first);
        Assert.Equal(1, second);
        Assert.Equal(1, third);

        tl.Seek(1000);
        Assert.Equal(1, first);
        Assert.Equal(2, second);
        Assert.Equal(2, third);

        tl.Seek(0);
        Assert.Equal(2, first);
        Assert.Equal(2, second);
        Assert.Equal(2, third);
    }

    [Fact]
    public void SeekDoesNotRefireACallItDoesNotCross()
    {
        int first = 0, second = 0, third = 0;
        var tl = Presto.CreateTimeline(new() { Autoplay = false })
            .Call(_ => first++, 0)
            .Call(_ => second++, 500)
            .Call(_ => third++, 1000);

        tl.Seek(600);
        Assert.Equal(1, first);
        Assert.Equal(1, second);
        Assert.Equal(0, third);

        tl.Seek(550);
        Assert.Equal(1, second);

        tl.Seek(540);
        Assert.Equal(1, second);

        tl.Seek(501);
        Assert.Equal(1, second);

        tl.Seek(800);
        Assert.Equal(1, second);
        Assert.Equal(0, third);

        tl.Seek(600);
        Assert.Equal(1, second);
        Assert.Equal(0, third);
    }

    [Fact]
    public void RepeatedSeekToTheSamePositionDoesNotRefireACall()
    {
        int first = 0, second = 0;
        var tl = Presto.CreateTimeline(new() { Autoplay = false })
            .Call(_ => first++, 0)
            .Call(_ => second++, 500);

        tl.Seek(600);
        Assert.Equal(1, first);
        Assert.Equal(1, second);

        tl.Seek(600);
        Assert.Equal(1, first);
        Assert.Equal(1, second);

        tl.Seek(600);
        Assert.Equal(1, first);
        Assert.Equal(1, second);
    }

    [Fact]
    public void SeekDirectionReversalPastACallInTheMiddleOfATimelineDoesNotRefireIt()
    {
        var called = 0;
        var tl = Presto.CreateTimeline(new() { Autoplay = false })
            .Add(new StyleTarget(), new() { ["X"] = 100, Duration = 1000 }, 0)
            .Call(_ => called++, 500);

        tl.Seek(600);
        Assert.Equal(1, called);

        tl.Seek(550);
        Assert.Equal(1, called);

        tl.Seek(580);
        Assert.Equal(1, called);

        tl.Seek(560);
        Assert.Equal(1, called);

        tl.Seek(700);
        Assert.Equal(1, called);
    }

    [Fact]
    public void CallCallbacksWithAlternateLoops()
    {
        int first = 0, second = 0, third = 0;
        var tl = Presto.CreateTimeline(new() { Autoplay = false, Alternate = true, Loop = 3 })
            .Call(_ => first++, 0)
            .Call(_ => second++, 1000)
            .Call(_ => third++, 2000);

        Assert.Equal(0, first);
        Assert.Equal(0, second);
        Assert.Equal(0, third);

        tl.Seek(500);
        Assert.Equal(1, first);
        Assert.Equal(0, second);
        Assert.Equal(0, third);

        tl.Seek(1500);
        Assert.Equal(1, first);
        Assert.Equal(1, second);
        Assert.Equal(0, third);

        tl.Seek(2000);
        Assert.Equal(1, first);
        Assert.Equal(1, second);
        Assert.Equal(1, third);

        tl.Seek(3000);
        Assert.Equal(1, first);
        Assert.Equal(2, second);
        Assert.Equal(1, third);

        tl.Seek(4000);
        Assert.Equal(2, first);
        Assert.Equal(2, second);
        Assert.Equal(1, third);
    }

    [Fact]
    public void MixedCallAndAdd()
    {
        int first = 0, second = 0, third = 0;
        var targets = StaggeredTargets();
        var tl = SteppedOpacityTimeline(targets)
            .Call(_ => first++, 0)
            .Call(_ => second++, 500)
            .Call(_ => third++, 1000)
            .Init();

        Assert.Equal(0, first);
        Assert.Equal(0, second);
        Assert.Equal(0, third);

        tl.Seek(50);
        Assert.Equal(.5, targets[2].Opacity);
        Assert.Equal(1, first);
        Assert.Equal(0, second);
        Assert.Equal(0, third);

        tl.Seek(600);
        Assert.Equal(.5, targets[2].Opacity);
        Assert.Equal(1, first);
        Assert.Equal(1, second);
        Assert.Equal(0, third);

        tl.Seek(1000);
        Assert.Equal(1, first);
        Assert.Equal(1, second);
        Assert.Equal(1, third);
        Assert.Equal(1, targets[2].Opacity);
    }

    [Fact]
    public void CallWithOddFloatingPointValuesA()
    {
        int first = 0, second = 0, third = 0, fourth = 0;
        var completed = false;
        Presto.CreateTimeline(new() { OnComplete = _ => completed = true })
            .Call(_ => first++, 0)
            .Call(_ => second++, 800)
            .Call(_ => third++, 8700)
            .Call(_ => fourth++, 8650)
            .Seek(8650);

        RunUntil(() => completed);

        Assert.True(completed);
        Assert.Equal(1, first);
        Assert.Equal(1, second);
        Assert.Equal(1, third);
        Assert.Equal(1, fourth);
    }

    [Fact]
    public void CallWithOddFloatingPointValuesB()
    {
        int first = 0, second = 0, third = 0, fourth = 0;
        var completed = false;
        Presto.CreateTimeline(new() { OnComplete = _ => completed = true })
            .Call(_ => first++, 0)
            .Call(_ => second++, 800)
            .Call(_ => third++, 8000)
            .Call(_ => fourth++, 8450)
            .Seek(8400);

        RunUntil(() => completed);

        Assert.True(completed);
        Assert.Equal(1, first);
        Assert.Equal(1, second);
        Assert.Equal(1, third);
        Assert.Equal(1, fourth);
    }

    [Fact]
    public void SetValuesUpdateOnLoop()
    {
        var targets = StaggeredTargets();
        var tl = SteppedOpacityTimeline(targets).Init();

        tl.Seek(1250);
        Assert.Equal(1, targets[2].Opacity);
        tl.Seek(1750);
        Assert.Equal(.5, targets[2].Opacity);
    }

    [Fact]
    public void NestedTimelines()
    {
        var target = new StyleTarget();
        var timerUpdated = false;
        var completed = false;

        var timer = Presto.CreateTimer(new() { Duration = 30, OnUpdate = _ => timerUpdated = true });
        var animation = Presto.Animate(target, new() { ["X"] = 100, Duration = 30 });

        var tl = Presto.CreateTimeline(new() { OnComplete = _ => completed = true })
            .Sync(timer)
            .Sync(animation, 20);

        Assert.False(timerUpdated);
        Assert.Equal(0, target.X);

        RunUntil(() => completed);

        Assert.True(completed);
        Assert.Equal(50, tl.Duration);
        Assert.True(timerUpdated);
        Assert.Equal(100, target.X);
    }

    [Fact]
    public void RemoveNestedAnimations()
    {
        var target = new StyleTarget();
        var timerUpdated = false;
        var completed = false;

        var timer = Presto.CreateTimer(new() { Duration = 30, OnUpdate = _ => timerUpdated = true });
        var animation = Presto.Animate(target, new() { ["X"] = 100, Duration = 30 });

        var tl = Presto.CreateTimeline(new() { OnComplete = _ => completed = true })
            .Sync(timer)
            .Sync(animation, 20);

        tl.Remove(animation);

        // The removal recomputes the iteration duration from the surviving children, but leaves the total duration alone.
        Assert.Equal(30, tl.IterationDuration);

        RunUntil(() => completed);

        Assert.True(completed);
        Assert.Equal(50, tl.Duration);
        Assert.True(timerUpdated);
        Assert.Equal(0, target.X);
    }
}
