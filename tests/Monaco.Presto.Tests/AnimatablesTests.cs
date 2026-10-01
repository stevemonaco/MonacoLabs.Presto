namespace Monaco.Presto.Tests;

/// <summary>Port of anime.js tests/suites/animatables.test.js.</summary>
public sealed class AnimatablesTests : EngineTestBase
{
    [Fact]
    public void GetAndAnimateAnimatableValues()
    {
        var target = new StyleTarget();
        var animatable = Presto.CreateAnimatable(target, new()
        {
            ["TranslateX"] = 20,
            ["TranslateY"] = 30,
            ["Rotate"] = 40,
        });

        Assert.Equal(0, animatable.Get("TranslateX"));
        Assert.Equal(0, animatable.Get("TranslateY"));
        Assert.Equal(0, animatable.Get("Rotate"));

        animatable.Set("TranslateX", 100);
        animatable.Set("TranslateY", 150);
        animatable.Set("Rotate", 45);

        Assert.Equal(20, animatable.Animations["TranslateX"].Duration);
        Assert.Equal(30, animatable.Animations["TranslateY"].Duration);
        Assert.Equal(40, animatable.Animations["Rotate"].Duration);

        Frames.Advance(50, 16);

        Assert.Equal(100, animatable.Get("TranslateX"));
        Assert.Equal(150, animatable.Get("TranslateY"));
        Assert.Equal(45, animatable.Get("Rotate"));
    }

    /// <summary>
    /// Retargeting a zero duration property with a real duration, long after the engine started, still eases every frame:
    /// stretching the setter duration must not scale the animation's creation offset past what a double can resolve.
    /// </summary>
    [Fact]
    public void RetargetingAZeroDurationPropertyLongAfterTheEngineStartedStillEases()
    {
        var keepAlive = Presto.CreateTimer();
        Frames.Advance(62000, 16);
        var stuck = new List<string>();
        for (var i = 0; i < 40; i++)
        {
            Frames.Advance(7.3 * (i % 5 + 1));
            var target = new StyleTarget();
            var animatable = Presto.CreateAnimatable(target, new() { ["TranslateX"] = 0 });
            for (var drag = 0; drag < 5; drag++)
            {
                animatable.Set("TranslateX", drag * 10, 0);
                Frames.Advance(16);
            }
            animatable.Set("TranslateX", 100, 1480, Eases.None);
            Frames.Advance(736, 16);
            var value = animatable.Get("TranslateX");
            if (value is < 60 or > 80) stuck.Add($"{i}:{value:0.#}");
            animatable.Revert();
        }

        Assert.True(stuck.Count == 0, "midway values: " + string.Join(" ", stuck));
        keepAlive.Cancel();
    }

    [Fact]
    public void GetAndAnimateAnimatableComplexValues()
    {
        var target = new StyleTarget();
        var animatable = Presto.CreateAnimatable(target, new() { ["BackgroundColor"] = 20 });

        Assert.Equal<double[]>([255, 255, 255, 1], animatable.GetChannels("BackgroundColor"));

        animatable.Set("BackgroundColor", [100, 200, 0, 1]);

        Assert.Equal(20, animatable.Animations["BackgroundColor"].Duration);

        Frames.Advance(50, 16);

        Assert.Equal<double[]>([100, 200, 0, 1], animatable.GetChannels("BackgroundColor"));
    }

    [Fact]
    public void GetAndSetAnimatableValues()
    {
        var target = new StyleTarget();
        var animatable = Presto.CreateAnimatable(target, new()
        {
            ["TranslateX"] = 0,
            ["TranslateY"] = 0,
            ["Rotate"] = 0,
        });

        Assert.Equal(0, animatable.Get("TranslateX"));
        Assert.Equal(0, animatable.Get("TranslateY"));
        Assert.Equal(0, animatable.Get("Rotate"));

        animatable.Set("TranslateX", 100).Set("TranslateY", 150).Set("Rotate", 45);

        Assert.Equal(100, animatable.Get("TranslateX"));
        Assert.Equal(150, animatable.Get("TranslateY"));
        Assert.Equal(45, animatable.Get("Rotate"));
    }

    [Fact]
    public void DefinesCustomUnitsForAnimatableValues()
    {
        var target = new StyleTarget();
        var animatable = Presto.CreateAnimatable(target, new()
        {
            ["TranslateX"] = new() { Unit = "em" },
            ["TranslateY"] = new() { Unit = "rem" },
            ["Rotate"] = new() { Unit = "rad", Duration = 50 },
            Duration = 40,
        });

        Assert.Equal(0, animatable.Get("TranslateX"));
        Assert.Equal(0, animatable.Get("TranslateY"));
        Assert.Equal(0, animatable.Get("Rotate"));

        animatable.Set("TranslateX", 10).Set("TranslateY", 15).Set("Rotate", 1);

        Frames.Advance(60, 16);

        Assert.Equal(10, animatable.Get("TranslateX"));
        Assert.Equal(15, animatable.Get("TranslateY"));
        Assert.Equal(1, animatable.Get("Rotate"));
        Assert.Equal("10em", Presto.Get(target, "TranslateX"));
        Assert.Equal("15rem", Presto.Get(target, "TranslateY"));
        Assert.Equal("1rad", Presto.Get(target, "Rotate"));
    }

    [Fact]
    public void AnimatableOnBeginCallbacks()
    {
        var began = 0;
        var animatable = Presto.CreateAnimatable(new StyleTarget(), new()
        {
            ["TranslateX"] = 10,
            ["TranslateY"] = 10,
            ["Rotate"] = 5,
            OnBegin = _ => began++,
        });

        animatable.Set("TranslateX", 100).Set("TranslateY", 100).Set("Rotate", 100);
        Frames.Advance(20, 16);
        animatable.Set("TranslateX", -100).Set("TranslateY", -100).Set("Rotate", -100);
        Frames.Advance(20, 16);

        Assert.Equal(2, began);
    }

    [Fact]
    public void AnimatableOnCompleteCallbacks()
    {
        var completes = 0;
        var animatable = Presto.CreateAnimatable(new StyleTarget(), new()
        {
            ["TranslateX"] = 40,
            ["TranslateY"] = 40,
            ["Rotate"] = 5,
            OnComplete = _ => completes++,
        });

        animatable.Set("TranslateX", 100).Set("TranslateY", 100).Set("Rotate", 100);
        Frames.Advance(20, 16);
        animatable.Set("TranslateX", -100).Set("TranslateY", -100).Set("Rotate", -100);
        Frames.Advance(60, 16);
        animatable.Set("TranslateX", -50).Set("TranslateY", -50).Set("Rotate", -50);
        Frames.Advance(60, 16);

        Assert.Equal(2, completes);
    }

    [Fact]
    public void AnimatableOnUpdateCallbacks()
    {
        var updates = 0;
        var animatable = Presto.CreateAnimatable(new StyleTarget(), new()
        {
            ["TranslateX"] = 20,
            ["TranslateY"] = 20,
            ["Rotate"] = 5,
            OnUpdate = _ => updates++,
        });

        animatable.Set("TranslateX", 100).Set("TranslateY", 100).Set("Rotate", 100);
        Frames.Advance(20, 16);
        animatable.Set("TranslateX", -100).Set("TranslateY", -100).Set("Rotate", -100);
        Frames.Advance(20, 16);

        Assert.InRange(updates, 2, 5);
    }

    [Fact]
    public void StaggersAnimatableDurationsPerTarget()
    {
        IndexedTarget[] targets = [new(), new(), new()];
        var animatable = Presto.CreateAnimatable(targets, new() { ["TranslateX"] = new() { Duration = Presto.Stagger(10) } });

        var animation = animatable.Animations["TranslateX"];
        Assert.Equal(20, animation.Duration);
        Assert.Equal(10, TestHelpers.GetTweenAtIndex(animation, 1)!._changeDuration);
        Assert.Equal(20, TestHelpers.GetTweenAtIndex(animation, 2)!._changeDuration);
    }

    [Fact]
    public void OverrideAnAnimatableDurationAndEaseOnSet()
    {
        var target = new StyleTarget();
        var animatable = Presto.CreateAnimatable(target, new() { ["TranslateX"] = 20 });

        animatable.Set("TranslateX", 100, 60, "linear");

        Assert.Equal(60, animatable.Animations["TranslateX"].Duration);

        Frames.Advance(18); // Resume backdates the start time by 12 ms, so this frame lands on the tween's midpoint.

        Assert.Equal(50, animatable.Get("TranslateX"));
    }

    [Fact]
    public void ARetargetedSpringKeepsItsMomentumWhenVelocityIsPreserved()
    {
        var animatable = MomentumFollower(new StyleTarget(), preserveVelocity: true);

        animatable.Set("X", 100);
        Frames.Advance(160, 16);
        var before = Velocity(animatable);
        animatable.Set("X", 0);

        Assert.True(before > .1, $"moving at {before} per ms before the retarget");
        Assert.Equal(before, Velocity(animatable), before * .02);
    }

    [Fact]
    public void ARetargetedSpringStartsFromRestWithoutPreservedVelocity()
    {
        var animatable = MomentumFollower(new StyleTarget(), preserveVelocity: false);

        animatable.Set("X", 100);
        Frames.Advance(160, 16);
        var before = Velocity(animatable);
        animatable.Set("X", 0);

        var after = Velocity(animatable);
        Assert.True(Math.Abs(after) < before * .02, $"{after} per ms after the retarget against {before} before it");
    }

    [Fact]
    public void AMomentumSpringCarriesOnPastWhereItTurnedBeforeHeadingToItsNewTarget()
    {
        var target = new StyleTarget();
        var animatable = MomentumFollower(target, preserveVelocity: true);

        animatable.Set("X", 100);
        Frames.Advance(160, 16);
        var turnedAt = target.X;
        animatable.Set("X", 0);
        var peak = turnedAt;
        for (var i = 0; i < 10; i++)
        {
            Frames.Advance(16);
            peak = Math.Max(peak, target.X);
        }

        Assert.True(peak > turnedAt + 1, $"peaked at {peak} after turning at {turnedAt}");
        RunUntil(() => animatable.Animations["X"].Paused);
        Assert.Equal(0, target.X);
    }

    [Fact]
    public void AMomentumSpringSettlesOnItsTargetAndCompletesOnce()
    {
        var completes = 0;
        var target = new StyleTarget();
        var animatable = Presto.CreateAnimatable(target, new()
        {
            ["X"] = new(),
            Ease = new Spring(stiffness: 120, damping: 12),
            PreserveVelocity = true,
            OnComplete = _ => completes++,
        });

        animatable.Set("X", 100);
        Frames.Advance(80, 16);
        animatable.Set("X", -50);
        RunUntil(() => animatable.Animations["X"].Paused);
        Frames.Advance(32, 16);

        Assert.Equal(-50, target.X);
        Assert.Equal(1, completes);
    }

    [Fact]
    public void RetargetingASettledMomentumSpringStartsFromRest()
    {
        var animatable = MomentumFollower(new StyleTarget(), preserveVelocity: true);

        animatable.Set("X", 100);
        RunUntil(() => animatable.Animations["X"].Paused);
        animatable.Set("X", 0);

        Assert.Equal(0, Velocity(animatable), .01);
    }

    [Fact]
    public void RepeatedRetargetsWithinOneFrameKeepTheMomentum()
    {
        var animatable = MomentumFollower(new StyleTarget(), preserveVelocity: true);

        animatable.Set("X", 100);
        Frames.Advance(160, 16);
        var before = Velocity(animatable);
        animatable.Set("X", 10).Set("X", -20);

        Assert.Equal(before, Velocity(animatable), before * .02);
    }

    [Fact]
    public void PreservedVelocityIgnoresAPropertyThatIsNotSpringEased()
    {
        var target = new StyleTarget();
        var animatable = Presto.CreateAnimatable(target, new() { ["X"] = 100, Ease = "linear", PreserveVelocity = true });

        animatable.Set("X", 100);
        Frames.Advance(48, 16);
        animatable.Set("X", 0);

        Assert.Equal(100, animatable.Animations["X"].Duration);
        Frames.Advance(16);
        Assert.True(target.X < 48, "a linear retarget heads straight for the new target");
    }

    [Fact]
    public void ASpringPassedToSetCarriesTheMomentumOfTheDeclaredOne()
    {
        var animatable = MomentumFollower(new StyleTarget(), preserveVelocity: true);

        animatable.Set("X", 100);
        Frames.Advance(160, 16);
        var before = Velocity(animatable);
        animatable.Set("X", 0, ease: new Spring(stiffness: 300, damping: 20));

        Assert.Equal(before, Velocity(animatable), before * .02);
    }

    [Fact]
    public void AChannelRetargetKeepsEachComplexChannelsMomentum()
    {
        var animatable = ChannelFollower(new TestObject(), "MultiplePlainValues", preserveVelocity: true);

        animatable.Set("MultiplePlainValues", [116, 32, 14, 228]);
        Frames.Advance(160, 16);
        var before = ChannelVelocities(animatable, "MultiplePlainValues");
        animatable.Set("MultiplePlainValues", [0, 32, 100, 128]);
        var after = ChannelVelocities(animatable, "MultiplePlainValues");

        foreach (var i in new[] { 0, 2, 3 })
        {
            Assert.True(Math.Abs(before[i]) > .05, $"channel {i} moving at {before[i]} per ms before the retarget");
            Assert.Equal(before[i], after[i], Math.Abs(before[i]) * .02);
        }
        Assert.Equal(0, before[1]);
        Assert.Equal(0, after[1]);
    }

    [Fact]
    public void AChannelRetargetKeepsEachColourChannelsMomentum()
    {
        var animatable = ChannelFollower(new StyleTarget(), "BackgroundColor", preserveVelocity: true);

        animatable.Set("BackgroundColor", [55, 155, 255, 1]);
        Frames.Advance(160, 16);
        var before = ChannelVelocities(animatable, "BackgroundColor");
        animatable.Set("BackgroundColor", [200, 0, 255, 1]);
        var after = ChannelVelocities(animatable, "BackgroundColor");

        foreach (var i in new[] { 0, 1 })
        {
            Assert.True(Math.Abs(before[i]) > .05, $"channel {i} moving at {before[i]} per ms before the retarget");
            Assert.Equal(before[i], after[i], Math.Abs(before[i]) * .02);
        }
    }

    [Fact]
    public void ChannelsThatDoNotMoveStayExactlyAtRest()
    {
        var animatable = ChannelFollower(new StyleTarget(), "BackgroundColor", preserveVelocity: true);

        animatable.Set("BackgroundColor", [55, 155, 255, 1]);
        Frames.Advance(160, 16);
        animatable.Set("BackgroundColor", [200, 0, 255, 1]);
        while (!animatable.Animations["BackgroundColor"].Paused)
        {
            Frames.Advance(16);
            var channels = animatable.GetChannels("BackgroundColor");
            Assert.Equal(255, channels[2]);
            Assert.Equal(1, channels[3]);
        }
    }

    [Fact]
    public void ChannelMomentumSettlesOnItsTargetAndCompletesOnce()
    {
        var completes = 0;
        var animatable = Presto.CreateAnimatable(new StyleTarget(), new()
        {
            ["BackgroundColor"] = new(),
            Ease = new Spring(stiffness: 120, damping: 12),
            PreserveVelocity = true,
            OnComplete = _ => completes++,
        });

        animatable.Set("BackgroundColor", [55, 155, 255, 1]);
        Frames.Advance(80, 16);
        animatable.Set("BackgroundColor", [200, 0, 30, .5]);
        RunUntil(() => animatable.Animations["BackgroundColor"].Paused);
        Frames.Advance(32, 16);

        Assert.Equal<double[]>([200, 0, 30, .5], animatable.GetChannels("BackgroundColor"));
        Assert.Equal(1, completes);
    }

    [Fact]
    public void RetargetingSettledChannelsStartsFromRest()
    {
        var animatable = ChannelFollower(new TestObject(), "MultiplePlainValues", preserveVelocity: true);

        animatable.Set("MultiplePlainValues", [116, 132, 14, 228]);
        RunUntil(() => animatable.Animations["MultiplePlainValues"].Paused);
        animatable.Set("MultiplePlainValues", [0, 0, 0, 0]);

        Assert.All(ChannelVelocities(animatable, "MultiplePlainValues"), v => Assert.Equal(0, v, .01));
    }

    [Fact]
    public void ChannelsStartFromRestWithoutPreservedVelocity()
    {
        var animatable = ChannelFollower(new TestObject(), "MultiplePlainValues", preserveVelocity: false);

        animatable.Set("MultiplePlainValues", [116, 132, 14, 228]);
        Frames.Advance(160, 16);
        var before = ChannelVelocities(animatable, "MultiplePlainValues");
        animatable.Set("MultiplePlainValues", [0, 0, 0, 0]);
        var after = ChannelVelocities(animatable, "MultiplePlainValues");

        Assert.True(before[0] > .05, $"moving at {before[0]} per ms before the retarget");
        Assert.All(after, v => Assert.True(Math.Abs(v) < before[0] * .02, $"{v} per ms after the retarget"));
    }

    [Fact]
    public void PreservedVelocityIgnoresChannelsThatAreNotSpringEased()
    {
        var animatable = Presto.CreateAnimatable(new StyleTarget(), new() { ["BackgroundColor"] = 100, Ease = "linear", PreserveVelocity = true });

        animatable.Set("BackgroundColor", [55, 155, 255, 1]);
        Frames.Advance(48, 16);
        animatable.Set("BackgroundColor", [200, 0, 255, 1]);

        Assert.Equal(100, animatable.Animations["BackgroundColor"].Duration);
        Assert.Null(animatable.Animations["BackgroundColor"]._headTween!._channelEases);
    }

    [Fact]
    public void AFlingWithoutAVelocityContinuesARunningSetAtItsSpeed()
    {
        var animatable = Presto.CreateAnimatable(new StyleTarget(), new() { ["X"] = 1000, Ease = "outQuad" });

        animatable.Set("X", 500);
        Frames.Advance(160, 16);
        var before = Velocity(animatable);
        animatable.Fling("X");

        Assert.True(before > .1, $"moving at {before} per ms before the fling");
        Assert.Equal(before, Velocity(animatable), before * .02);
    }

    [Fact]
    public void AFlingTravelsPowerTimesVelocityTimesTimeConstant()
    {
        var target = new StyleTarget();
        var animatable = Presto.CreateAnimatable(target, new() { ["X"] = 100 });

        animatable.Fling("X", .5, new Inertia(3) { Power = 2, TimeConstant = 200 });
        RunUntil(() => animatable.Animations["X"].Paused);

        Assert.Equal(200, target.X, 1e-9);
    }

    [Fact]
    public void AFlingPastABoundSettlesOnIt()
    {
        var target = new StyleTarget();
        var animatable = Presto.CreateAnimatable(target, new() { ["X"] = 100 });

        animatable.Fling("X", 2, new Inertia(0) { Min = -50, Max = 300 });
        var peak = 0d;
        while (!animatable.Animations["X"].Paused)
        {
            Frames.Advance(16);
            peak = Math.Max(peak, target.X);
        }

        Assert.True(peak > 300, $"peaked at {peak}");
        Assert.Equal(300, target.X, 1e-9);
    }

    [Fact]
    public void AFlingOutwardFromABoundGoesOutAndBack()
    {
        var target = new StyleTarget();
        var animatable = Presto.CreateAnimatable(target, new() { ["X"] = 100 });

        animatable.Fling("X", 1, new Inertia(0) { Max = 0 });
        var peak = 0d;
        while (!animatable.Animations["X"].Paused)
        {
            Frames.Advance(16);
            peak = Math.Max(peak, target.X);
        }

        Assert.True(peak > 1, $"peaked at {peak}");
        Assert.Equal(0, target.X, 1e-9);
    }

    [Fact]
    public void ASetAfterAFlingUsesTheDeclaredEaseAndDurationAgain()
    {
        var animatable = Presto.CreateAnimatable(new StyleTarget(), new() { ["X"] = 100, Ease = "linear" });

        animatable.Fling("X", 1);
        Frames.Advance(48, 16);
        animatable.Set("X", 0);

        Assert.Equal(100, animatable.Animations["X"].Duration);
        Assert.Equal(.25, animatable.Animations["X"]._headTween!._ease(.25));
    }

    [Fact]
    public void RevertAFlungAnimatable()
    {
        var target = new StyleTarget { X = 10 };
        var animatable = Presto.CreateAnimatable(target, new() { ["X"] = 100 });

        animatable.Fling("X", 1);
        Frames.Advance(48, 16);
        animatable.Revert();
        animatable.Fling("X", 1);
        Frames.Advance(48, 16);

        Assert.Equal(10, target.X);
    }

    private static Animatable MomentumFollower(StyleTarget target, bool preserveVelocity) =>
        Presto.CreateAnimatable(target, new() { ["X"] = new(), Ease = new Spring(stiffness: 120, damping: 12), PreserveVelocity = preserveVelocity });

    /// <summary>How fast the X tween moves at its current time, in units per ms, from a central difference of its ease.</summary>
    private static double Velocity(Animatable animatable)
    {
        const double step = 1e-4;
        var tween = animatable.Animations["X"]._headTween!;
        var progress = tween._currentTime / tween._updateDuration;
        var before = Math.Max(0, progress - step);
        var after = Math.Min(1, progress + step);
        return (tween._ease(after) - tween._ease(before)) / ((after - before) * tween._updateDuration) * (tween._toNumber - tween._fromNumber);
    }

    private static Animatable ChannelFollower(object target, string property, bool preserveVelocity) =>
        Presto.CreateAnimatable(target, new() { [property] = new(), Ease = new Spring(stiffness: 120, damping: 12), PreserveVelocity = preserveVelocity });

    /// <summary>How fast each displayed channel of the property moves at its current time, in units per ms, blended as the renderer blends it.</summary>
    private static double[] ChannelVelocities(Animatable animatable, string property)
    {
        const double step = 1e-4;
        var tween = animatable.Animations[property]._headTween!;
        var progress = tween._currentTime / tween._updateDuration;
        var before = Math.Max(0, progress - step);
        var after = Math.Min(1, progress + step);
        var velocities = new double[tween._numbers.Length];
        for (var i = 0; i < velocities.Length; i++)
        {
            velocities[i] = (Channel(tween, i, after) - Channel(tween, i, before)) / ((after - before) * tween._updateDuration);
        }
        return velocities;
    }

    private static double Channel(Tween tween, int i, double time)
    {
        var progress = (tween._channelEases?[i] ?? tween._ease)(time);
        var (from, to) = (tween._fromNumbers[i], tween._toNumbers[i]);
        return tween._valueType == Values.ValueKind.Color && i < 3
            ? Math.Sqrt(from * from * (1 - progress) + to * to * progress)
            : from + (to - from) * progress;
    }

    [Fact]
    public void RevertAnAnimatable()
    {
        var target = new StyleTarget();
        var animatable = Presto.CreateAnimatable(target, new()
        {
            ["TranslateX"] = new() { Unit = "em" },
            ["TranslateY"] = new() { Unit = "rem" },
            ["Rotate"] = new() { Unit = "rad", Duration = 50 },
            Duration = 40,
        });

        Assert.Single(animatable.Targets);

        animatable.Set("TranslateX", 10);
        animatable.Revert();

        Assert.Empty(animatable.Targets);
        Assert.Empty(animatable.Animations);
        Assert.Equal("0px", target.TranslateX);
    }
}
