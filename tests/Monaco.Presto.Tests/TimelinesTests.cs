namespace Monaco.Presto.Tests;

/// <summary>Port of anime.js tests/suites/timelines.test.js.</summary>
public sealed class TimelinesTests : EngineTestBase
{
    private static double StartTimeOf(Timeline timeline, int index)
    {
        var child = TestHelpers.GetChildAtIndex(timeline, index)!;
        return child._offset + child._delay;
    }

    [Fact]
    public void ChildrenPositionShouldBeAddedToTheirDelay()
    {
        var target = new StyleTarget();
        var tl = Presto.CreateTimeline(new() { Autoplay = false, Defaults = new() { Duration = 50 } })
            .Add(target, new() { ["TranslateX"] = 100, Delay = 200 })
            .Add(target, new() { ["TranslateX"] = -100, Delay = 300 })
            .Add(target, new() { ["TranslateX"] = 0, Delay = 50 }, "+=150")
            .Add(target, new() { ["TranslateX"] = 100 }, "+=50");

        Assert.Equal(200, StartTimeOf(tl, 0));
        Assert.Equal(550, StartTimeOf(tl, 1));
        Assert.Equal(800, StartTimeOf(tl, 2));
        Assert.Equal(900, StartTimeOf(tl, 3));
    }

    [Fact]
    public void DurationIsTheSumOfEveryChildEndTimeTimesTheIterationCount()
    {
        StyleTarget[] targets = [new(), new(), new(), new()];
        var tl = Presto.CreateTimeline(new()
        {
            Id = "Test",
            Defaults = new()
            {
                Duration = 50,
                Ease = "outExpo",
                Delay = new FunctionValue((_, i, _) => 10 + (i * 20)),
            },
            Alternate = true,
            Loop = 1,
        })
        .Add(targets, new() { ["TranslateX"] = 250 })
        .Add(targets, new() { ["Opacity"] = .5, ["Scale"] = 2 })
        .Add(targets[0], new() { ["Rotate"] = 180 })
        .Add(targets, new() { ["TranslateX"] = 0, ["Scale"] = 1 });

        Assert.Equal(420 * 2, tl.Duration);
    }

    [Fact]
    public void DefaultParametersAreInheritedByChildren()
    {
        var target = new StyleTarget();
        var tl = Presto.CreateTimeline(new()
        {
            Loop = 1,
            Reversed = true,
            Defaults = new()
            {
                Duration = 10,
                Ease = "inOut",
                Alternate = true,
                Loop = 3,
                Composition = Composition.None,
            },
        })
        .Add(target, new() { ["TranslateX"] = 100, Duration = 15 })
        .Add(target, new() { ["TranslateX"] = 200, Reversed = false, Ease = "outExpo", Composition = Composition.Blend });

        var first = TestHelpers.GetChildAtIndex(tl, 0)!;
        var second = TestHelpers.GetChildAtIndex(tl, 1)!;

        Assert.False(first._reversed);
        Assert.False(second._reversed);
        // (( 15 * 4 ) + ( 10 * 4 )) * 2
        Assert.Equal(200, tl.Duration);
        Assert.Equal(Eases.InOut()(.75), first._headTween!._ease(.75));
        Assert.Equal(Eases.OutExpo(.75), second._headTween!._ease(.75));
        Assert.Equal(Composition.None, first._headTween!._composition);
        Assert.Equal(Composition.Blend, second._headTween!._composition);
    }

    [Fact]
    public void BasicTimePositions()
    {
        var target = new StyleTarget();
        var tl = Presto.CreateTimeline(new() { Defaults = new() { Duration = 10 } })
            .Add(target, new() { ["TranslateX"] = 100 })
            .Add(target, new() { ["TranslateX"] = 200 })
            .Add(target, new() { ["TranslateX"] = 300 });

        Assert.Equal(0, TestHelpers.OffsetOf(tl, 0));
        Assert.Equal(10, TestHelpers.OffsetOf(tl, 1));
        Assert.Equal(20, TestHelpers.OffsetOf(tl, 2));
        Assert.Equal(30, tl.Duration);
    }

    [Fact]
    public void AbsoluteTimePositions()
    {
        var target = new StyleTarget();
        var tl = Presto.CreateTimeline(new() { Defaults = new() { Duration = 10 } })
            .Add(target, new() { ["TranslateX"] = 100 }, 50)
            .Add(target, new() { ["TranslateX"] = 200 }, 25)
            .Add(target, new() { ["TranslateX"] = 300 }, 100);

        Assert.Equal(50, TestHelpers.OffsetOf(tl, 0));
        Assert.Equal(25, TestHelpers.OffsetOf(tl, 1));
        Assert.Equal(100, TestHelpers.OffsetOf(tl, 2));
        Assert.Equal(110, tl.Duration);
    }

    [Fact]
    public void RelativeTimePositions()
    {
        var target = new StyleTarget();
        var tl = Presto.CreateTimeline(new() { Defaults = new() { Duration = 10 } })
            .Add(target, new() { ["TranslateX"] = 100 }, "+=20")  // 0 + 20 = 20
            .Add(target, new() { ["TranslateX"] = 200 }, "*=2")   // (20 + 10) * 2 = 60
            .Add(target, new() { ["TranslateX"] = 300 }, "-=50"); // (60 + 10) - 50 = 20

        Assert.Equal(20, TestHelpers.OffsetOf(tl, 0));
        Assert.Equal(60, TestHelpers.OffsetOf(tl, 1));
        Assert.Equal(20, TestHelpers.OffsetOf(tl, 2));
        Assert.Equal(70, tl.Duration);
    }

    [Fact]
    public void PreviousOperatorWithRelativeValues()
    {
        var target = new StyleTarget();
        var tl = Presto.CreateTimeline(new() { Defaults = new() { Duration = 10 } })
            .Add(target, new() { ["TranslateX"] = 100 })
            .Add(target, new() { ["Rotate"] = 100 }, "<<+=100");

        Assert.Equal(100, TestHelpers.OffsetOf(tl, 1));
    }

    [Fact]
    public void PreviousAnimationEndPosition()
    {
        var target = new StyleTarget();
        var tl = Presto.CreateTimeline(new() { Defaults = new() { Duration = 10 } })
            .Add(target, new() { ["TranslateX"] = 100 }, "+=40")
            .Add(target, new() { ["TranslateX"] = 200 }, "-=30") // 40 + 10 - 30 = 20
            .Add(target, new() { ["TranslateX"] = 300 }, "<");   // 20 + 10 = 30

        Assert.Equal(40, TestHelpers.OffsetOf(tl, 0));
        Assert.Equal(20, TestHelpers.OffsetOf(tl, 1));
        Assert.Equal(30, TestHelpers.OffsetOf(tl, 2));
        Assert.Equal(50, tl.Duration);
    }

    [Fact]
    public void PreviousAnimationEndPositionWithRelativeValue()
    {
        var target = new StyleTarget();
        var tl = Presto.CreateTimeline(new() { Defaults = new() { Duration = 10 } })
            .Add(target, new() { ["TranslateX"] = 100 }, "+=40")
            .Add(target, new() { ["TranslateX"] = 200 }, "-=30")  // 40 + 10 - 30 = 20
            .Add(target, new() { ["TranslateX"] = 300 }, "<+=5"); // 20 + 10 + 5 = 35

        Assert.Equal(35, TestHelpers.OffsetOf(tl, 2));
    }

    [Fact]
    public void PreviousAnimationStartPosition()
    {
        var target = new StyleTarget();
        var tl = Presto.CreateTimeline(new() { Defaults = new() { Duration = 10 } })
            .Add(target, new() { ["TranslateX"] = 100 }, "+=40")
            .Add(target, new() { ["TranslateX"] = 200 }, "-=30") // 40 + 10 - 30 = 20
            .Add(target, new() { ["TranslateX"] = 300 }, "<<");  // 20

        Assert.Equal(40, TestHelpers.OffsetOf(tl, 0));
        Assert.Equal(20, TestHelpers.OffsetOf(tl, 1));
        Assert.Equal(20, TestHelpers.OffsetOf(tl, 2));
        Assert.Equal(50, tl.Duration);
    }

    [Fact]
    public void PreviousAnimationStartPositionWithRelativeValues()
    {
        var target = new StyleTarget();
        var tl = Presto.CreateTimeline(new() { Defaults = new() { Duration = 10 } })
            .Add(target, new() { ["TranslateX"] = 100 }, "+=40")
            .Add(target, new() { ["TranslateX"] = 200 }, "-=30")   // 40 + 10 - 30 = 20
            .Add(target, new() { ["TranslateX"] = 300 }, "<<+=5"); // 20 + 5 = 25

        Assert.Equal(40, TestHelpers.OffsetOf(tl, 0));
        Assert.Equal(20, TestHelpers.OffsetOf(tl, 1));
        Assert.Equal(25, TestHelpers.OffsetOf(tl, 2));
        Assert.Equal(50, tl.Duration);
    }

    [Fact]
    public void MixedTimePositionTypes()
    {
        var target = new StyleTarget();
        var tl = Presto.CreateTimeline(new() { Defaults = new() { Duration = 10 } })
            .Add(target, new() { ["TranslateX"] = 100 }, 50)
            .Add(target, new() { ["TranslateX"] = 200 }, "-=20") // (50 + 10) - 20 = 40
            .Add(target, new() { ["TranslateX"] = 300 }, 0);

        Assert.Equal(50, TestHelpers.OffsetOf(tl, 0));
        Assert.Equal(40, TestHelpers.OffsetOf(tl, 1));
        Assert.Equal(0, TestHelpers.OffsetOf(tl, 2));
        Assert.Equal(60, tl.Duration);
    }

    [Fact]
    public void TimelineValues()
    {
        var target = new StyleTarget();
        var tl = Presto.CreateTimeline(new() { Autoplay = false, Defaults = new() { Duration = 100, Ease = "linear" } })
            .Add(target, new() { ["TranslateX"] = 100 })
            .Add(target, new() { ["TranslateX"] = 200 }, "-=50")
            .Add(target, new() { ["TranslateX"] = 300 }, "-=50")
            .Add(target, new()
            {
                ["TranslateX"] = new TweenParams[]
                {
                    new() { To = 250, Duration = 50, Delay = 50 },
                    new() { To = 350, Duration = 100 },
                    new() { To = 150, Duration = 150 },
                },
            });

        Assert.Equal("0px", target.TranslateX);
        tl.Seek(25);
        Assert.Equal("25px", target.TranslateX);
        tl.Seek(100);
        Assert.Equal("125px", target.TranslateX);
        tl.Seek(150);
        Assert.Equal("212.5px", target.TranslateX);
        tl.Seek(tl.Duration);
        Assert.Equal("150px", target.TranslateX);
    }

    [Fact]
    public void AddAnimationsAndTimersAsTargets()
    {
        var target = new StyleTarget();
        var timerUpdated = false;
        var completed = false;

        var timer = Presto.CreateTimer(new() { OnUpdate = _ => timerUpdated = true });
        var animation = Presto.Animate(target, new() { ["X"] = 100 });

        var tl = Presto.CreateTimeline(new() { OnComplete = _ => completed = true })
            .Add(timer, new() { ["CurrentTime"] = timer.Duration, Duration = 30 })
            .Add(animation, new() { ["CurrentTime"] = animation.Duration, Duration = 30 }, 20);

        Assert.False(timerUpdated);
        Assert.Equal(0, target.X);

        RunUntil(() => completed);

        Assert.True(completed);
        Assert.Equal(50, tl.Duration);
        Assert.True(timerUpdated);
        Assert.Equal(100, target.X);
    }

    [Fact]
    public void AddTimers()
    {
        var firstUpdated = false;
        var secondUpdated = false;
        var completed = false;

        Presto.CreateTimeline(new() { OnComplete = _ => completed = true })
            .Add(new TimerParams { Duration = 30, OnUpdate = _ => firstUpdated = true })
            .Add(new TimerParams { Duration = 30, OnUpdate = _ => secondUpdated = true }, 20);

        RunUntil(() => completed);

        Assert.True(completed);
        Assert.True(firstUpdated);
        Assert.True(secondUpdated);
    }

    [Fact]
    public void AddZeroDurationAnimation()
    {
        var target = new StyleTarget();
        var tl = Presto.CreateTimeline(new() { Autoplay = false })
            .Add(target, new() { ["Y"] = -100, Duration = 0 }, 2000);

        tl.Seek(2000);

        Assert.Equal(-100, target.Y);
    }

    [Fact]
    public void RaisingAChildLoopGrowsTheTimelineToFitIt()
    {
        var tl = Presto.CreateTimeline().Add(new TimerParams { Duration = 100 });
        var child = tl._head!;
        Frames.Advance(48);

        child.Loop = 2;
        Assert.Equal(300, tl.Duration);
        RunUntil(() => tl.Completed);

        Assert.True(child.Completed);
        Assert.Equal(300, tl.CurrentTime);
    }

    [Fact]
    public void LoweringAChildLoopKeepsTheTimelineDuration()
    {
        var tl = Presto.CreateTimeline().Add(new TimerParams { Duration = 100, Loop = 2 });
        var child = tl._head!;
        Frames.Advance(48);

        child.Loop = 0;
        Assert.Equal(100, child.Duration);
        Assert.Equal(300, tl.Duration);
        RunUntil(() => tl.Completed);

        Assert.True(child.Completed);
        Assert.Equal(300, tl.CurrentTime);
    }

    [Fact]
    public void RemovingTheNextChildsTargetFromAChildCallbackKeepsTheRestRendering()
    {
        StyleTarget a = new(), b = new(), c = new();
        var removed = false;
        var cUpdatesSinceRemoval = 0;
        var tl = Presto.CreateTimeline(new() { Defaults = new() { Duration = 200 } })
            .Add(a, new()
            {
                ["TranslateX"] = 100,
                OnUpdate = self =>
                {
                    if (removed || self.CurrentTime < 50) return;
                    removed = true;
                    Presto.Remove(b);
                },
            })
            .Add(b, new() { ["TranslateX"] = 100 }, 0)
            .Add(c, new() { ["TranslateX"] = 100, OnUpdate = _ => { if (removed) cUpdatesSinceRemoval++; } }, 0);

        RunUntil(() => removed);
        Assert.Equal(1, cUpdatesSinceRemoval);
        Frames.Advance(16);

        Assert.Equal(2, cUpdatesSinceRemoval);
        Assert.Equal(2, TestHelpers.GetChildLength(tl));
    }
}
