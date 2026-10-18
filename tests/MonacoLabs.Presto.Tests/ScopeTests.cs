using MonacoLabs.Presto.Core;

namespace MonacoLabs.Presto.Tests;

/// <summary>Port of the portable half of anime.js tests/suites/scope.test.js, plus the registration behavior it leaves to the DOM suites.</summary>
public sealed class ScopeTests : EngineTestBase
{
    [Fact]
    public void KeepsTheRootItIsGiven()
    {
        var root = new StyleTarget();
        Assert.Null(Presto.CreateScope().Root);
        Assert.Same(root, Presto.CreateScope(new() { Root = root }).Root);
    }

    [Fact]
    public void RevertsEverythingCreatedInside()
    {
        var target = new StyleTarget();
        var scope = Presto.CreateScope();
        Animation? animation = null;
        Timer? timer = null;
        Animatable? animatable = null;

        scope.Add(_ =>
        {
            animation = Presto.Animate(target, new() { ["X"] = 100, Duration = 100 });
            timer = Presto.CreateTimer(new() { Duration = 100 });
            animatable = Presto.CreateAnimatable(target, new() { ["Y"] = 100 });
            animatable.Set("Y", 50);
        });

        Frames.Advance(200, 16);
        Assert.Equal(100, target.X);
        Assert.Equal(50, target.Y);

        scope.Revert();

        Assert.Equal(0, target.X);
        Assert.Equal(0, target.Y);
        Assert.True(animation!.Canceled);
        Assert.True(timer!.Canceled);
    }

    [Fact]
    public void ReplacedAnimationsDoNotAccumulateAndRevertStillRestoresTheOriginal()
    {
        var target = new StyleTarget { X = 10 };
        var scope = Presto.CreateScope();

        scope.Execute(_ =>
        {
            for (var i = 0; i < 1000; i++)
            {
                Presto.Animate(target, new() { ["X"] = 100 + i, Duration = 100 });
                Frames.Advance(16);
            }
        });

        Assert.InRange(scope.RevertibleCount, 1, 32);
        Assert.NotEqual(10, target.X);

        scope.Revert();

        Assert.Equal(10, target.X);
    }

    [Fact]
    public void CompletedAnimationsStayRegistered()
    {
        var targets = Enumerable.Range(0, 32).Select(_ => new StyleTarget()).ToArray();
        var scope = Presto.CreateScope();

        scope.Execute(_ =>
        {
            for (var i = 0; i < 31; i++) Presto.Animate(targets[i], new() { ["X"] = 100, Duration = 10 });
        });
        Frames.Advance(100, 16);
        // The 32nd registration triggers a prune, which must spare the completed (and engine-canceled) animations.
        scope.Execute(_ => Presto.Animate(targets[31], new() { ["X"] = 100, Duration = 10 }));

        Assert.Equal(32, scope.RevertibleCount);
        scope.Revert();
        Assert.All(targets, target => Assert.Equal(0, target.X));
    }

    [Fact]
    public void ReAnimatingATargetThatFinishesEachTimeStaysBounded()
    {
        var target = new StyleTarget { X = 10 };
        var scope = Presto.CreateScope();

        scope.Execute(_ =>
        {
            for (var i = 0; i < 1000; i++)
            {
                Presto.Animate(target, new() { ["X"] = 100 + i, Duration = 10 });
                Frames.Advance(50, 16);
            }
        });

        Assert.InRange(scope.RevertibleCount, 1, 32);
        scope.Revert();
        Assert.Equal(10, target.X);
    }

    [Fact]
    public void ReplayedTimelinesInsideAScopeStayBounded()
    {
        var target = new StyleTarget { X = 10, Y = 20 };
        var scope = Presto.CreateScope();

        scope.Execute(_ =>
        {
            for (var i = 0; i < 300; i++)
            {
                Presto.CreateTimeline()
                    .Add(target, new() { ["X"] = 100 + i, Duration = 10 })
                    .Add(target, new() { ["Y"] = 200 + i, Duration = 10 });
                Frames.Advance(100, 16);
            }
        });

        Assert.InRange(scope.RevertibleCount, 1, 32);
        scope.Revert();
        Assert.Equal(10, target.X);
        Assert.Equal(20, target.Y);
    }

    [Fact]
    public void RevertedTimersArePrunedAndNotRevertedAgain()
    {
        var targets = Enumerable.Range(0, 20).Select(_ => new StyleTarget()).ToArray();
        var scope = Presto.CreateScope();

        scope.Execute(_ =>
        {
            foreach (var target in targets)
            {
                Presto.CreateTimer(new() { Duration = 100 }).Revert();
                var animation = Presto.Animate(target, new() { ["X"] = 100, Duration = 100 });
                Frames.Advance(50, 16);
                animation.Revert();
                target.X = 7;
            }
        });

        // The 32nd registration, the 16th animation, drops the 31 reverted entries before it.
        Assert.Equal(9, scope.RevertibleCount);
        scope.Revert();
        Assert.All(targets, target => Assert.Equal(7, target.X));
    }

    [Fact]
    public void RevertDoesNotRewriteAValueSetAfterTheAnimationsOwnRevert()
    {
        var target = new StyleTarget();
        var scope = Presto.CreateScope();
        var animation = scope.Execute(_ => Presto.Animate(target, new() { ["X"] = 100, Duration = 100 }));
        Frames.Advance(200, 16);

        animation.Revert();
        target.X = 42;
        scope.Revert();

        Assert.Equal(42, target.X);
    }

    [Fact]
    public void ARevivedPrunedTimerIsRevertedByItsScope()
    {
        var target = new StyleTarget();
        var scope = Presto.CreateScope();
        var animation = scope.Execute(_ => Presto.Animate(target, new() { ["X"] = 100, Duration = 100 }));
        Frames.Advance(200, 16);
        animation.Revert();

        scope.Execute(_ =>
        {
            for (var i = 0; i < 31; i++) Presto.CreateTimer(new() { Duration = 1000 });
        });
        Assert.Equal(31, scope.RevertibleCount);

        animation.Restart();
        Frames.Advance(200, 16);
        Assert.Equal(100, target.X);
        Assert.Equal(32, scope.RevertibleCount);

        scope.Revert();
        Assert.Equal(0, target.X);
    }

    [Fact]
    public void AReusedPrunedNestedScopeIsRevertedByItsParent()
    {
        var target = new StyleTarget();
        var parent = Presto.CreateScope();
        var nested = parent.Execute(_ => Presto.CreateScope());
        nested.Revert();

        parent.Execute(_ =>
        {
            for (var i = 0; i < 31; i++) Presto.CreateTimer(new() { Duration = 1000 });
        });
        Assert.Equal(31, parent.RevertibleCount);

        nested.Add(_ => Presto.Animate(target, new() { ["X"] = 100, Duration = 100 }));
        Frames.Advance(200, 16);
        Assert.Equal(100, target.X);
        Assert.Equal(32, parent.RevertibleCount);

        parent.Revert();
        Assert.Equal(0, target.X);
    }

    [Fact]
    public void ARevertibleFromOutsideTheLibraryIsNeverPruned()
    {
        var outside = new CountingRevertible();
        var scope = Presto.CreateScope();
        scope.Register(outside);
        outside.Revert();

        scope.Execute(_ =>
        {
            for (var i = 0; i < 31; i++) Presto.CreateTimer(new() { Duration = 100 }).Revert();
        });
        // The outside revertible and the last timer, whose registration ran the prune before its revert.
        Assert.Equal(2, scope.RevertibleCount);

        scope.Revert();
        Assert.Equal(2, outside.Reverts);
    }

    private sealed class CountingRevertible : IRevertible
    {
        public int Reverts { get; private set; }

        public void Revert() => Reverts++;
    }

    [Fact]
    public void AppliesScopedDefaultsToChildrenCreatedInside()
    {
        var scope = Presto.CreateScope(new() { Defaults = defaults => defaults.Duration = 500 });

        var inside = scope.Execute(_ => Presto.Animate(new StyleTarget(), new() { ["X"] = 100 }));
        var outside = Presto.Animate(new StyleTarget(), new() { ["X"] = 100 });

        Assert.Equal(500, inside.Duration);
        Assert.Equal(Constants.K, outside.Duration);
        Assert.Equal(Constants.K, Presto.Engine.Defaults.Duration.AsDouble);
    }

    [Fact]
    public void RestoresTheGlobalsWhenTheCallbackThrows()
    {
        var defaults = Presto.Engine.Defaults;
        var scope = Presto.CreateScope(new() { Defaults = scoped => scoped.Duration = 500 });

        Assert.Throws<InvalidOperationException>(() => scope.Execute(_ => { throw new InvalidOperationException(); }));

        Assert.Same(defaults, Presto.Engine.Defaults);
        Assert.Null(Presto.Engine.CurrentScope);
    }

    [Fact]
    public void RunsMethodsInsideTheScope()
    {
        var scope = Presto.CreateScope(new() { Defaults = defaults => defaults.Duration = 500 });
        Animation? animation = null;

        scope.Add("start", () => animation = Presto.Animate(new StyleTarget(), new() { ["X"] = 100 }));
        scope.Methods["start"]();

        Assert.Equal(500, animation!.Duration);
        scope.Revert();
        Assert.True(animation.Canceled);
    }

    [Fact]
    public void RefreshRevertsThenReRunsTheConstructors()
    {
        var runs = 0;
        var cleanups = 0;
        var scope = Presto.CreateScope();
        Timer? timer = null;

        scope.Add(_ =>
        {
            runs++;
            timer = Presto.CreateTimer(new() { Duration = 100 });
            return () => cleanups++;
        });

        var first = timer;
        scope.Refresh();

        Assert.Equal(2, runs);
        Assert.Equal(1, cleanups);
        Assert.True(first!.Canceled);
        Assert.NotSame(first, timer);
        Assert.False(timer!.Canceled);
    }

    [Fact]
    public void AddOnceRunsOnceAcrossRefreshes()
    {
        var runs = 0;
        var onceRuns = 0;
        var scope = Presto.CreateScope();

        scope.Add(inner =>
        {
            runs++;
            inner.AddOnce(_ => onceRuns++);
        });

        scope.Refresh();
        scope.Refresh();

        Assert.Equal(3, runs);
        Assert.Equal(1, onceRuns);
    }

    [Fact]
    public void KeepTimeKeepsThePlaybackPositionAcrossARefresh()
    {
        var scope = Presto.CreateScope();
        Timer? timer = null;

        scope.Add(inner => timer = inner.KeepTime(_ => Presto.CreateTimer(new() { Duration = 100, Loop = 3 })));

        timer!.Seek(150);
        var first = timer;
        scope.Refresh();

        Assert.NotSame(first, timer);
        Assert.True(first.Canceled);
        Assert.Equal(1, timer!.CurrentIteration);
        Assert.Equal(.5, timer.IterationProgress, 6);
    }

    [Fact]
    public void RevertsNestedScopes()
    {
        var target = new StyleTarget();
        var parent = Presto.CreateScope();
        Timer? timer = null;

        parent.Add(_ => Presto.CreateScope().Add(__ => timer = Presto.Animate(target, new() { ["X"] = 100, Duration = 100 })));

        Frames.Advance(200, 16);
        Assert.Equal(100, target.X);

        parent.Revert();

        Assert.Equal(0, target.X);
        Assert.True(timer!.Canceled);
    }

    [Fact]
    public void APlainTimerTakesNoDurationAndNoFunctionBasedDelayFromTheDefaults()
    {
        Presto.Defaults.Duration = 500;
        Presto.Defaults.Delay = 200;
        var numeric = Presto.CreateTimer();
        Presto.Defaults.Delay = Presto.Stagger(100);
        var functionBased = Presto.CreateTimer();

        Assert.Equal(double.PositiveInfinity, numeric.IterationDuration);
        Assert.Equal(200, numeric.Delay);
        Assert.Equal(0, functionBased.Delay);
    }

    [Fact]
    public void ChangingADefaultAffectsOnlyTimersCreatedAfterwards()
    {
        var before = Presto.CreateTimer(new() { Duration = 100 });
        Presto.Defaults.Loop = 2;
        Presto.Defaults.PlaybackRate = 3;
        var after = Presto.CreateTimer(new() { Duration = 100 });

        Assert.Equal(1, before.IterationCount);
        Assert.Equal(1, before.Speed);
        Assert.Equal(3, after.IterationCount);
        Assert.Equal(3, after.Speed);
    }

    [Fact]
    public void ATimelineWithoutDefaultsReadsTheEngineDefaultsAsEachChildIsAdded()
    {
        var timeline = Presto.CreateTimeline(new() { Autoplay = false });
        timeline.Add(new StyleTarget(), new AnimationParams { ["X"] = 100 });
        Presto.Defaults.Duration = 300;
        timeline.Add(new StyleTarget(), new AnimationParams { ["X"] = 100 });

        Assert.Same(Presto.Defaults, timeline.ChildDefaults);
        Assert.Equal(Constants.K, TestHelpers.GetChildAtIndex(timeline, 0)!.Duration);
        Assert.Equal(300, TestHelpers.GetChildAtIndex(timeline, 1)!.Duration);
    }

    [Fact]
    public void TimelineDefaultsCarryNoReducedMotionFlagsAndKeepTheEngines()
    {
        Presto.Defaults.ReducedMotion = true;
        Presto.Defaults.ReduceAll = true;

        var timeline = Presto.CreateTimeline(new() { Autoplay = false, Defaults = new() { Duration = 50 } });

        Assert.Null(typeof(DefaultsParams).GetProperty(nameof(PrestoDefaults.ReducedMotion)));
        Assert.Null(typeof(DefaultsParams).GetProperty(nameof(PrestoDefaults.ReduceAll)));
        Assert.NotSame(Presto.Defaults, timeline.ChildDefaults);
        Assert.True(timeline.ChildDefaults.ReducedMotion);
        Assert.True(timeline.ChildDefaults.ReduceAll);
    }

    [Fact]
    public void AScopeWithoutDefaultsSharesTheEngineInstance()
    {
        var scope = Presto.CreateScope();
        scope.Defaults.Duration = 400;

        Assert.Same(Presto.Engine.Defaults, scope.Defaults);
        Assert.Equal(400, Presto.Defaults.Duration.AsDouble);
    }

    [Fact]
    public void PrestoDefaultsReadsTheExecutingScopesDefaults()
    {
        var scope = Presto.CreateScope(new() { Defaults = defaults => defaults.Duration = 500 });

        var inside = scope.Execute(_ => Presto.Defaults);

        Assert.Same(scope.Defaults, inside);
        Assert.NotSame(scope.Defaults, Presto.Defaults);
    }

    [Fact]
    public void ANestedExecuteRestoresTheOuterScope()
    {
        var outer = Presto.CreateScope(new() { Defaults = defaults => defaults.Duration = 500 });
        var inner = Presto.CreateScope(new() { Defaults = defaults => defaults.Duration = 700 });
        Scope? current = null;
        PrestoDefaults? defaults = null;

        outer.Execute(_ =>
        {
            inner.Execute(_ => { });
            current = Presto.Engine.CurrentScope;
            defaults = Presto.Engine.Defaults;
        });

        Assert.Same(outer, current);
        Assert.Same(outer.Defaults, defaults);
    }

    [Fact]
    public void RegistrationsAfterAddOnceSurviveRefreshUntilTheNextAdd()
    {
        var scope = Presto.CreateScope();
        scope.AddOnce(_ => { });
        var once = scope.Execute(_ => Presto.CreateTimer(new() { Duration = 100 }));
        scope.Add(_ => { });
        var refreshed = scope.Execute(_ => Presto.CreateTimer(new() { Duration = 100 }));

        scope.Refresh();

        Assert.False(once.Canceled);
        Assert.True(refreshed.Canceled);
        scope.Revert();
        Assert.True(once.Canceled);
    }

    [Fact]
    public void RegistrationsAfterKeepTimeSurviveRefresh()
    {
        var scope = Presto.CreateScope();
        scope.KeepTime(_ => Presto.CreateTimer(new() { Duration = 100 }));
        var once = scope.Execute(_ => Presto.CreateTimer(new() { Duration = 100 }));

        scope.Refresh();

        Assert.False(once.Canceled);
    }

    [Fact]
    public void AddOnceCleanupsAndTimersAreLeftByRefreshAndUndoneByRevert()
    {
        var cleanups = 0;
        Timer? timer = null;
        var scope = Presto.CreateScope();
        scope.AddOnce(_ =>
        {
            timer = Presto.CreateTimer(new() { Duration = 100 });
            return (Action)(() => cleanups++);
        });

        scope.Refresh();

        Assert.Equal(0, cleanups);
        Assert.False(timer!.Canceled);
        scope.Revert();
        Assert.Equal(1, cleanups);
        Assert.True(timer.Canceled);
    }

    [Fact]
    public void RevertClearsTheConstructorsMethodsAndData()
    {
        var runs = 0;
        var onceRuns = 0;
        var scope = Presto.CreateScope();
        scope.Add(_ => { runs++; });
        scope.AddOnce(_ => { onceRuns++; });
        scope.Add("method", () => { });
        scope.Data["key"] = 1;

        scope.Revert();
        scope.Refresh();
        scope.AddOnce(_ => { onceRuns++; });

        Assert.Equal(1, runs);
        Assert.Equal(2, onceRuns);
        Assert.Empty(scope.Methods);
        Assert.Empty(scope.Data);
    }

    [Fact]
    public void AScopeStaysUsableAfterRevert()
    {
        var scope = Presto.CreateScope();
        scope.Add(_ => { Presto.CreateTimer(new() { Duration = 100 }); });
        scope.Revert();
        var runs = 0;
        Timer? timer = null;

        scope.Add(_ =>
        {
            runs++;
            timer = Presto.CreateTimer(new() { Duration = 100 });
        });

        Assert.Equal(1, runs);
        Assert.Equal(1, scope.RevertibleCount);
        scope.Revert();
        Assert.True(timer!.Canceled);
    }

    [Fact]
    public void RevertsAnyRegisteredRevertible()
    {
        var scope = Presto.CreateScope();
        var revertible = new CountingRevertible();
        scope.Register(revertible);

        scope.Revert();

        Assert.Equal(1, revertible.Reverts);
    }

    [Fact]
    public void ANewPrestoDefaultsHoldsThePlainTimerChoices()
    {
        var defaults = new PrestoDefaults();

        Assert.Equal(1, defaults.PlaybackRate);
        Assert.Equal(0, defaults.Loop.Value);
        Assert.False(defaults.Reversed);
        Assert.False(defaults.Alternate);
        Assert.True(defaults.Autoplay);
        Assert.Equal(0, defaults.Delay.AsDouble);
        Assert.Equal(0, defaults.LoopDelay);
        Assert.Null(defaults.PlaybackEase);
        Assert.Equal(12.345, defaults.Modifier(12.345));
        Assert.Equal(-3, defaults.Modifier(-3));
        Assert.Null(defaults.OnBegin);
        Assert.Null(defaults.OnBeforeUpdate);
        Assert.Null(defaults.OnUpdate);
        Assert.Null(defaults.OnLoop);
        Assert.Null(defaults.OnPause);
        Assert.Null(defaults.OnComplete);
        Assert.Null(defaults.OnRender);
    }

    [Fact]
    public void AScopeWithoutParametersBehavesAsWithEmptyParams()
    {
        var parent = Presto.CreateScope();

        var (bare, empty) = parent.Execute(_ => (new Scope(), new Scope(new ScopeParams())));

        Assert.Null(bare.Root);
        Assert.Same(Presto.Defaults, bare.Defaults);
        Assert.Same(empty.Defaults, bare.Defaults);
        Assert.Equal(2, parent.RevertibleCount);
    }
}
