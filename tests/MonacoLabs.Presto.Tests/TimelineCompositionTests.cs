namespace MonacoLabs.Presto.Tests;

/// <summary>Port of the composition, loop and seek cases of anime.js tests/suites/timelines.test.js.</summary>
public sealed class TimelineCompositionTests : EngineTestBase
{
    [Fact]
    public void CorrectTweenOverridingWhenAddingAnAnimationBeforeMultipleKeyframeStartTimeOnTheSameProperty()
    {
        var target = new StyleTarget();
        var tl = Presto.CreateTimeline(new() { Autoplay = false, Defaults = new() { Duration = 100, Ease = "linear" } })
            .Add(target, new() { ["TranslateX"] = 100, ["Y"] = 100 })
            .Add(target, new()
            {
                ["TranslateX"] = new TweenParams[]
                {
                    new() { To = 250, Duration = 50, Delay = 50 },
                    new() { To = 350, Duration = 100 },
                    new() { To = 150, Duration = 150 },
                },
            })
            .Add(target, new() { ["TranslateX"] = 15, Duration = 200 }, "-=275")
            .Add(target, new() { ["Y"] = 25, Duration = 50 }, 0);

        Assert.Equal("0px", target.TranslateX);
        Assert.Equal(0, target.Y);
        tl.Seek(175);
        Assert.Equal("175px", target.TranslateX);
        Assert.Equal(25, target.Y);
        tl.Seek(275);
        Assert.Equal("95px", target.TranslateX);
        tl.Seek(375);
        Assert.Equal("15px", target.TranslateX);
        tl.Seek(tl.Duration);
        Assert.Equal("15px", target.TranslateX);
        tl.Seek(275);
        Assert.Equal("95px", target.TranslateX);
    }

    [Fact]
    public void CorrectTweenCompositionWithMultipleTimelines()
    {
        var target = new StyleTarget();
        var tl1 = Presto.CreateTimeline(new() { Autoplay = false, Defaults = new() { Duration = 100, Ease = "linear" } })
            .Add(target, new() { ["X"] = 100 })
            .Add(target, new() { ["X"] = 200 })
            .Init();

        tl1.Seek(200);

        var tl2 = Presto.CreateTimeline(new() { Autoplay = false, Defaults = new() { Duration = 100, Ease = "linear" } })
            .Add(target, new() { ["X"] = -100 })
            .Init();

        tl2.Seek(0);

        Assert.Equal(200, target.X);
    }

    [Fact]
    public void CorrectInitialTweenValueOnLoop()
    {
        var first = new StyleTarget { Opacity = 1 };
        var second = new StyleTarget { Opacity = 1 };
        var tl = Presto.CreateTimeline(new() { Autoplay = false, Loop = 2, Defaults = new() { Duration = 100, Ease = "linear" } })
            .Add(first, new() { ["TranslateX"] = 100 })
            .Add(first, new() { ["Opacity"] = 0 })
            .Add(second, new() { ["TranslateX"] = 100 })
            .Add(second, new() { ["Opacity"] = 0 });

        Assert.Equal("0px", first.TranslateX);
        Assert.Equal("0px", second.TranslateX);
        tl.Seek(50);
        Assert.Equal("50px", first.TranslateX);
        Assert.Equal(1, first.Opacity);
        Assert.Equal("0px", second.TranslateX);
        tl.Seek(100);
        Assert.Equal("100px", first.TranslateX);
        Assert.Equal(1, first.Opacity);
        Assert.Equal("0px", second.TranslateX);
        tl.Seek(150);
        Assert.Equal("100px", first.TranslateX);
        Assert.Equal(.5, first.Opacity);
        Assert.Equal("0px", second.TranslateX);
        tl.Seek(200);
        Assert.Equal(0, first.Opacity);
        Assert.Equal("0px", second.TranslateX);
        Assert.Equal(1, second.Opacity);
        tl.Seek(250);
        Assert.Equal(0, first.Opacity);
        Assert.Equal("50px", second.TranslateX);
        tl.Seek(300);
        Assert.Equal("100px", second.TranslateX);
        Assert.Equal(1, second.Opacity);
        tl.Seek(350);
        Assert.Equal(.5, second.Opacity);
        tl.Seek(400);
        Assert.Equal("0px", first.TranslateX);
        Assert.Equal(1, first.Opacity);
        Assert.Equal("0px", second.TranslateX);
        Assert.Equal(1, second.Opacity);
        tl.Seek(550);
        Assert.Equal("100px", first.TranslateX);
        Assert.Equal(.5, first.Opacity);
        Assert.Equal("0px", second.TranslateX);
        Assert.Equal(1, second.Opacity);
    }

    [Fact]
    public void CorrectTweenPlaybackWithMultipleTimelines()
    {
        var target = new StyleTarget();
        var done = false;

        Presto.CreateTimer(new()
        {
            Duration = 50,
            OnComplete = _ => Presto.CreateTimeline(new() { Defaults = new() { Duration = 100, Ease = "linear" } })
                .Add(target, new() { ["X"] = 100 })
                .Init(),
        });

        Presto.CreateTimer(new()
        {
            Duration = 100,
            OnComplete = _ => Presto.CreateTimeline(new() { Defaults = new() { Duration = 100, Ease = "linear" } })
                .Add(target, new() { ["X"] = -100 })
                .Init(),
        });

        Presto.CreateTimer(new() { Duration = 150, OnComplete = _ => done = true });
        RunUntil(() => done);

        Assert.True(target.X < 0, $"Expected the second timeline to drive x below 0, got {target.X}.");
    }

    [Fact]
    public void AlternateDirectionTimelineChildrenShouldCorrectlyRenderOnSeekAfterTheAnimationEnd()
    {
        var target = new StyleTarget();
        var done = false;

        Presto.CreateTimeline(new()
        {
            Loop = 2,
            Alternate = true,
            OnComplete = self =>
            {
                self.Seek(40);
                Assert.Equal("175px", target.TranslateX);
                done = true;
            },
        })
        .Add(target, new() { ["TranslateX"] = -100, Duration = 10, Loop = 2, Alternate = true, Ease = "linear" })
        .Add(target, new() { ["TranslateX"] = 400, Duration = 10, Loop = 2, Alternate = true, Ease = "linear" }, "-=5");

        RunUntil(() => done);

        Assert.True(done);
    }

    [Fact]
    public void AlternateDirectionTimelineChildrenShouldCorrectlyRenderOnSeekMidway()
    {
        var target = new StyleTarget();
        var tl = Presto.CreateTimeline(new() { Loop = 1, Alternate = true, Autoplay = false })
            .Add(target, new() { ["TranslateX"] = 100, Duration = 10, Alternate = true, Ease = "linear" })
            .Add(target, new() { ["TranslateX"] = 200, ["TranslateY"] = 100, Duration = 10, Alternate = true, Ease = "linear" }, "-=5");

        tl.Seek(15);
        Assert.Equal("200px", target.TranslateX);
        Assert.Equal("100px", target.TranslateY);
        tl.Seek(16);
        Assert.Equal("185px", target.TranslateX);
        Assert.Equal("90px", target.TranslateY);
        tl.Seek(15);
        Assert.Equal("200px", target.TranslateX);
        Assert.Equal("100px", target.TranslateY);
        tl.Seek(14);
        Assert.Equal("185px", target.TranslateX);
        Assert.Equal("90px", target.TranslateY);
    }

    [Fact]
    public void PreviousTweenBeforeLastShouldNotRenderOnLoop()
    {
        var target = new StyleTarget();
        var done = false;

        Presto.CreateTimeline(new()
        {
            Loop = 3,
            OnLoop = self =>
            {
                self.Pause();
                Assert.True(target.Y > 39, $"Expected the last child value to hold on loop, got {target.Y}.");
                done = true;
            },
        })
        .Add(target, new() { ["Y"] = -100, Duration = 100 })
        .Add(target, new() { ["Y"] = 50, Duration = 100 });

        RunUntil(() => done);

        Assert.True(done);
    }

    [Fact]
    public void AnimatingAChildTimelineProgressShouldNotCorruptParentRenderStateViaNestedTick()
    {
        var first = new StyleTarget();
        var second = new StyleTarget();

        var innerTL = Presto.CreateTimeline(new() { Autoplay = false, Defaults = new() { Duration = 100, Ease = "linear" } })
            .Add(first, new() { ["TranslateX"] = 200 });

        var call1Count = 0;
        var call2Count = 0;

        var parentTL = Presto.CreateTimeline(new() { Autoplay = false, Defaults = new() { Ease = "linear" } })
            .Call(_ => call1Count++, 0)
            .Add(innerTL, new() { ["Progress"] = 1, Duration = 200 })
            .Add(second, new() { ["TranslateY"] = 200, Duration = 200 }, 0)
            .Call(_ => call2Count++, 200);

        parentTL.Seek(100);
        Assert.Equal(1, call1Count);
        Assert.Equal("100px", first.TranslateX);
        Assert.Equal("100px", second.TranslateY);

        parentTL.Seek(200);
        Assert.Equal(1, call2Count);
        Assert.Equal("200px", first.TranslateX);
        Assert.Equal("200px", second.TranslateY);

        parentTL.Seek(100);
        Assert.Equal("100px", first.TranslateX);
        Assert.Equal("100px", second.TranslateY);
        Assert.Equal(2, call2Count);

        parentTL.Seek(0);
        Assert.Equal(2, call1Count);
    }

    [Fact]
    public void BackwardSeekWritesTheActiveChildValueNotTheStaleToValueOfAnEarlierPastEndSibling()
    {
        var target = new StyleTarget();
        var tl = Presto.CreateTimeline(new() { Autoplay = false, Loop = true, Defaults = new() { Duration = 1000, Ease = "linear" } })
            .Add(target, new() { ["X"] = 10 })
            .Add(target, new() { ["X"] = 20 })
            .Add(target, new() { ["X"] = 30 });

        // Forward seek to mid of the last segment so siblings 0/1 become past-end.
        tl.Seek(3000.01);
        Assert.Equal(.0001, target.X, 12);
        // Backward seek (still inside the last segment's range, but iteration order is now reversed). Without the fix, past-end
        // siblings render at progress 1 in backward iteration order and segment 0 writes its to-value last, masking the active one.
        tl.Seek(3000);
        Assert.Equal(0, target.X);
        tl.Seek(2999.99);
        Assert.Equal(29.9999, target.X, 12);
    }

    [Fact]
    public void SeekIntoTheGapBetweenATruncatedChildAndADelayedChildWritesTheHoldValue()
    {
        var target = new StyleTarget();
        var tl = Presto.CreateTimeline(new() { Autoplay = false })
            .Add(target, new() { ["X"] = new TweenParams { From = 0, To = 100, Duration = 1000 }, Ease = "linear" }, 0)
            .Add(target, new() { ["X"] = new TweenParams { To = 200, Duration = 500, Delay = 300 }, Ease = "linear" }, 500);

        tl.Seek(400);
        Assert.Equal(40, target.X);
        // First forward entry into the gap left by the truncated first child
        tl.Seek(600);
        Assert.Equal(50, target.X);
        tl.Seek(900);
        Assert.Equal(80, target.X);
        tl.Seek(600);
        Assert.Equal(50, target.X);
        tl.Seek(1300);
        Assert.Equal(200, target.X);
        tl.Seek(650);
        Assert.Equal(50, target.X);
        tl.Seek(400);
        Assert.Equal(40, target.X);
        tl.Seek(0);
        tl.Seek(700);
        Assert.Equal(50, target.X);
    }

    [Fact]
    public void BackwardSeekOutOfAnExplicitFromChildIntoTheGapWritesThePreviousChildHold()
    {
        var target = new StyleTarget();
        var tl = Presto.CreateTimeline(new() { Autoplay = false })
            .Add(target, new() { ["X"] = new TweenParams { To = 50, Duration = 200 }, Ease = "linear" }, 0)
            .Add(target, new() { ["X"] = new TweenParams { From = 0, To = 100, Duration = 500 }, Ease = "linear" }, 1000);

        tl.Seek(1500);
        tl.Seek(1100);
        Assert.Equal(20, target.X);
        tl.Seek(900);
        Assert.Equal(50, target.X);
    }

    [Fact]
    public void PlayingBackwardOutOfAFadeIntoTheGapWritesALaterAddedSetAtZero()
    {
        var target = new StyleTarget { Opacity = 1 };
        var tl = Presto.CreateTimeline()
            .Add(target, new() { ["Opacity"] = new TweenParams { From = 0 }, Duration = 900, Ease = Eases.OutQuart }, 1000)
            .Set(target, new() { ["Opacity"] = 0 }, 0)
            .Add(new TimerParams { Duration = 800 }, "+=400");
        RunUntil(() => tl.Completed);
        Assert.Equal(1, target.Opacity);

        tl.Reverse();
        RunUntil(() => tl.IterationCurrentTime < 900);

        Assert.Equal(0, target.Opacity);
    }

    [Theory]
    [InlineData(new double[] { 0 })]
    [InlineData(new double[] { })]
    [InlineData(new double[] { 0, 1500 })]
    public void AForwardJumpPastALaterAddedSetAtZeroEndsOnTheLaterFade(double[] before)
    {
        var target = new StyleTarget { Opacity = 1 };
        var tl = Presto.CreateTimeline(new() { Autoplay = false })
            .Add(target, new() { ["Opacity"] = new TweenParams { From = 0 }, Duration = 900 }, 1000)
            .Set(target, new() { ["Opacity"] = 0 }, 0);

        foreach (var time in before) tl.Seek(time);
        tl.Seek(tl.Duration);

        Assert.Equal(1, target.Opacity);
    }

    [Fact]
    public void BackwardSeekBeforeANegativeDelaySiblingStartRestoresThePreviousChildEndValue()
    {
        StyleTarget[] targets = [new(), new(), new()];
        var tl = Presto.CreateTimeline(new() { Autoplay = false })
            .Add(targets, new() { ["Y"] = new TweenParams { To = 1, Duration = 100 }, Ease = "linear" }, 0)
            .Add(targets, new()
            {
                ["Y"] = new TweenParams { To = 2, Duration = 100, Delay = new FunctionValue((_, i, _) => new double[] { -50, 0, 300 }[i]) },
                Ease = "linear",
            }, 200);

        Assert.Equal(600, tl.Duration);
        tl.Seek(600);
        Assert.Equal(2, targets[0].Y);
        Assert.Equal(2, targets[1].Y);
        Assert.Equal(2, targets[2].Y);
        // The last target tween spans 500 to 600, seeking back before its start must restore the first child end value
        tl.Seek(400);
        Assert.Equal(2, targets[0].Y);
        Assert.Equal(2, targets[1].Y);
        Assert.Equal(1, targets[2].Y);
        tl.Seek(175);
        Assert.Equal(1.25, targets[0].Y);
        Assert.Equal(1, targets[1].Y);
        Assert.Equal(1, targets[2].Y);
        tl.Seek(50);
        Assert.Equal(.5, targets[0].Y);
        Assert.Equal(.5, targets[1].Y);
        Assert.Equal(.5, targets[2].Y);
        tl.Seek(550);
        Assert.Equal(2, targets[0].Y);
        Assert.Equal(2, targets[1].Y);
        Assert.Equal(1.5, targets[2].Y);
    }

    [Fact]
    public void BackwardSeekReEnteringAnEarlierChildRangeKeepsTheActiveChildValue()
    {
        var target = new StyleTarget();
        var tl = Presto.CreateTimeline(new() { Autoplay = false })
            .Add(target, new()
            {
                ["X"] = new TweenParams[] { new() { To = 1, Duration = 100 }, new() { To = 2, Duration = 100 }, new() { To = 3, Duration = 200 } },
                Ease = "linear",
            }, 0)
            .Add(target, new() { ["X"] = new TweenParams { To = 4.5, Duration = 300 }, Ease = "linear" }, 300);

        Assert.Equal(600, tl.Duration);
        tl.Seek(600);
        Assert.Equal(4.5, target.X);
        // The first child re-renders on backward re-entry, its keyframes must yield to the second child write
        tl.Seek(360);
        Assert.Equal(2.9, target.X, 12);
        tl.Seek(250);
        Assert.Equal(2.25, target.X);
        tl.Seek(150);
        Assert.Equal(1.5, target.X);
    }

    /// <summary>
    /// The dot pop child animates to 14.5 by 1250, then holds it until the outro at 1600, so y is 14.5 across [1250,1600].
    /// The expand child has delay 300 greater than its 200 duration, so once past its end its iteration time equals its
    /// duration, which is below its delay.
    /// </summary>
    [Fact]
    public void APastEndChildWhoseDelayExceedsItsDurationMustNotOverwriteALaterSiblingOnAForwardSeek()
    {
        var target = new StyleTarget();
        var tl = Presto.CreateTimeline(new() { Autoplay = false })
            .Add(target, new()
            {
                ["Y"] = new TweenParams[] { new() { To = -2.5, Duration = 150 }, new() { To = -2.53, Duration = 150 } },
                Ease = "linear",
            }, 500)
            .Add(target, new() { ["Y"] = new TweenParams { To = .75, Duration = 200, Delay = 300 }, Ease = "linear" }, 550)
            .Add(target, new() { ["Y"] = new TweenParams { To = 14.5, Duration = 150 }, Ease = "linear" }, 1100)
            .Add(target, new() { ["Y"] = 5.5, Duration = 200, Ease = "linear" }, 1600);

        // Seek forward into the hold so the previous time is set, then step forward again across it.
        tl.Seek(1250);
        Assert.Equal(14.5, target.Y);
        tl.Seek(1300);
        Assert.Equal(14.5, target.Y);
        tl.Seek(1450);
        Assert.Equal(14.5, target.Y);
    }

    /// <summary>
    /// The dot pop child leaves y at 14.5 before the outro, so the outro's bare to value must start from 14.5.
    /// expand squeeze-overrides recover's last keyframe in place, so that keyframe sits overridden mid-chain before dot pop.
    /// The from walk must skip the overridden keyframe and reach dot pop, not stop at it and chain from the stale -2.5.
    /// </summary>
    [Fact]
    public void AnImplicitFromChainsFromTheLatestLiveSiblingSkippingAnOverriddenMidChainKeyframe()
    {
        var target = new StyleTarget();
        var tl = Presto.CreateTimeline(new() { Autoplay = false })
            .Add(target, new()
            {
                ["Y"] = new TweenParams[] { new() { To = -2.5, Duration = 200 }, new() { To = -2.53, Duration = 200 } },
                Ease = "linear",
            }, 1000)
            .Add(target, new() { ["Y"] = new TweenParams { To = .75, Duration = 2000, Delay = 700 }, Ease = "linear" }, 1100)
            .Add(target, new() { ["Y"] = new TweenParams { To = 14.5, Duration = 200 }, Ease = "linear" }, 5000)
            .Add(target, new() { ["Y"] = 5.5, Duration = 200, Ease = "linear" }, 8000)
            .Init();

        tl.Seek(5200);
        Assert.Equal(14.5, target.Y); // dot pop landed, the live value before the outro
        tl.Seek(8000);
        Assert.Equal(14.5, target.Y); // outro starts continuous
        tl.Seek(8100);
        Assert.Equal(10, target.Y); // midpoint of 14.5 down to 5.5
    }
}
