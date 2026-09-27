using static Monaco.Presto.Tests.TestHelpers;

namespace Monaco.Presto.Tests;

/// <summary>
/// The acceptance suite for COMPOSITOR-PLAN §4.1: a second engine owns its own timers, defaults, scopes and blend state,
/// even when it is ticked from another thread. Upstream has no equivalent, since anime.js only ever has one engine.
/// </summary>
public sealed class EnginesTests : EngineTestBase
{
    [Fact]
    public void AdvancingOneEngineLeavesTheOtherWhereItWas()
    {
        var first = new IsolatedEngine();
        var second = new IsolatedEngine();
        var firstTarget = new StyleTarget();
        var secondTarget = new StyleTarget();

        Animation firstAnimation, secondAnimation;
        using (first.Engine.MakeCurrent()) firstAnimation = Presto.Animate(firstTarget, new() { ["X"] = 100, Duration = 100 });
        using (second.Engine.MakeCurrent()) secondAnimation = Presto.Animate(secondTarget, new() { ["X"] = 100, Duration = 100 });

        first.Frames.Advance(100, 16);

        Assert.Equal(100, firstTarget.X);
        Assert.Equal(100, firstAnimation.CurrentTime);
        Assert.Equal(0, secondTarget.X);
        Assert.Equal(0, secondAnimation.CurrentTime);

        second.Frames.Advance(100, 16);

        Assert.Equal(100, secondTarget.X);
        Assert.Equal(100, secondAnimation.CurrentTime);
    }

    [Fact]
    public void TimersCreatedUnderAnEngineRegisterWithItAndNotTheMainEngine()
    {
        var scene = new IsolatedEngine();

        using (scene.Engine.MakeCurrent())
        {
            var timer = Presto.CreateTimer(new() { Duration = 100 });

            Assert.Same(timer, GetChildAtIndex(scene.Engine, 0));
            Assert.Equal(1, GetChildLength(scene.Engine));
            Assert.Equal(0, GetChildLength(Presto.Engine));
        }
    }

    [Fact]
    public void ResumingAfterTheBlockEndsReRegistersWithTheEngineTheTimerWasCreatedUnder()
    {
        var scene = new IsolatedEngine();
        Timer timer;
        using (scene.Engine.MakeCurrent()) timer = Presto.CreateTimer(new() { Duration = 100 });

        timer.Pause();
        scene.Frames.Advance(16);
        Assert.Equal(0, GetChildLength(scene.Engine));

        timer.Resume();

        Assert.Same(scene.Engine, timer.OwnerEngine);
        Assert.Same(timer, GetChildAtIndex(scene.Engine, 0));
        Assert.Equal(0, GetChildLength(Presto.Engine));
    }

    [Fact]
    public void CurrentIsTheMainEngineUntilMakeCurrentNestsOne()
    {
        var outer = new IsolatedEngine();
        var inner = new IsolatedEngine();

        Assert.Same(Presto.Engine, Engine.Current);

        using (outer.Engine.MakeCurrent())
        {
            Assert.Same(outer.Engine, Engine.Current);
            using (inner.Engine.MakeCurrent()) Assert.Same(inner.Engine, Engine.Current);
            Assert.Same(outer.Engine, Engine.Current);
        }

        Assert.Same(Presto.Engine, Engine.Current);
    }

    [Fact]
    public void DefaultsStartAsACopyOfTheMainEnginesAndThenDiverge()
    {
        Presto.Defaults.Duration = 250;
        var scene = new IsolatedEngine();

        Assert.NotSame(Presto.Defaults, scene.Engine.Defaults);
        Assert.Equal(250, scene.Engine.Defaults.Duration.AsDouble);

        scene.Engine.Defaults.Duration = 500;
        Presto.Defaults.Delay = 20;

        Assert.Equal(250, Presto.Defaults.Duration.AsDouble);
        Assert.Equal(0, scene.Engine.Defaults.Delay.AsDouble);
    }

    [Fact]
    public void ScopeExecuteSwapsOnlyItsOwnEnginesDefaults()
    {
        var scene = new IsolatedEngine();
        var mainDefaults = Presto.Defaults;
        var sceneDefaults = scene.Engine.Defaults;
        var target = new StyleTarget();

        using (scene.Engine.MakeCurrent())
        {
            var scope = Presto.CreateScope(new() { Defaults = defaults => defaults.Duration = 500 });
            var inside = scope.Execute(_ =>
            {
                Assert.Same(scope, scene.Engine.CurrentScope);
                Assert.Null(Presto.Engine.CurrentScope);
                Assert.Same(mainDefaults, Presto.Defaults);
                return Presto.Animate(target, new() { ["X"] = 100 });
            });

            Assert.Equal(500, inside.Duration);
            Assert.Same(sceneDefaults, scene.Engine.Defaults);
            Assert.Null(scene.Engine.CurrentScope);

            scene.Frames.Advance(600, 16);
            Assert.Equal(100, target.X);

            scope.Revert();

            Assert.Equal(0, target.X);
            Assert.True(inside.Cancelled);
        }

        Assert.Same(mainDefaults, Presto.Defaults);
    }

    [Fact]
    public void ScopeExecuteMakesItsOwnEngineAmbientOutsideAMakeCurrentBlock()
    {
        var scene = new IsolatedEngine();
        var target = new StyleTarget();
        Scope scope;
        using (scene.Engine.MakeCurrent()) scope = Presto.CreateScope(new() { Defaults = defaults => defaults.Duration = 500 });

        var animation = scope.Execute(_ => Presto.Animate(target, new() { ["X"] = 100 }));

        Assert.Same(scene.Engine, animation.OwnerEngine);
        Assert.Equal(500, animation.Duration);
        Assert.Equal(0, GetChildLength(Presto.Engine));

        scene.Frames.Advance(600, 16);
        Assert.Equal(100, target.X);

        scope.Revert();

        Assert.Equal(0, target.X);
        Assert.True(animation.Cancelled);
    }

    [Fact]
    public void AnEngineCreatedInSecondsModeRunsAtTheSameSpeedAsTheMainOne()
    {
        Presto.Engine.TimeUnit = TimeUnit.Seconds;
        var scene = new IsolatedEngine();
        var target = new StyleTarget();

        using (scene.Engine.MakeCurrent()) Presto.Animate(target, new() { ["X"] = 100, Duration = 1, Ease = "linear" });

        Assert.Equal(1, scene.Engine.Speed);

        scene.Frames.Advance(500, 16);
        Assert.True(target.X is > 40 and < 60, $"expected roughly half way after 500 ms, was {target.X}");

        scene.Frames.Advance(500, 16);
        Assert.Equal(100, target.X);
    }

    [Fact]
    public void BlendedTweensAreInvisibleToAnotherEngine()
    {
        var first = new IsolatedEngine();
        var second = new IsolatedEngine();
        var target = new StyleTarget();

        using (first.Engine.MakeCurrent())
        {
            Presto.Animate(target, new() { ["X"] = 100, Duration = 100, Composition = Composition.Blend });
        }

        Assert.Single(first.Engine.Additive.Lookups);
        Assert.Empty(second.Engine.Additive.Lookups);
        Assert.Null(second.Engine.Additive.Animation);

        second.Frames.Advance(100, 16);
        second.Engine.Update();
        Assert.Equal(0, target.X);

        first.Frames.Advance(100, 16);
        Assert.Equal(100, target.X);
    }

    [Fact]
    public async Task ATimerTickedOnAWorkerThreadStaysOnItsOwnEngine()
    {
        var (sceneEngine, timer) = await Task.Run(() =>
        {
            var scene = new IsolatedEngine();
            using (scene.Engine.MakeCurrent())
            {
                var sceneTimer = Presto.CreateTimer(new() { Duration = 100 });
                scene.Frames.Advance(100, 16);
                return (scene.Engine, sceneTimer);
            }
        });

        Assert.Same(sceneEngine, timer.OwnerEngine);
        Assert.True(timer.Completed);
        Assert.Equal(100, timer.CurrentTime);
        Assert.Equal(0, GetChildLength(Presto.Engine));
    }

    /// <summary>An engine of its own with its own frame source, for the tests that need a second engine beside <see cref="Presto.Engine"/>.</summary>
    private sealed class IsolatedEngine
    {
        public IsolatedEngine()
        {
            Frames = new ManualFrameSource();
            Engine = new Engine(Frames);
        }

        public Engine Engine { get; }

        public ManualFrameSource Frames { get; }
    }
}
