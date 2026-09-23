using Monaco.Presto.Core;

namespace Monaco.Presto.Tests;

/// <summary>Port of the portable half of anime.js tests/suites/scope.test.js, plus the registration behaviour it leaves to the DOM suites.</summary>
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
        Assert.True(animation!.Cancelled);
        Assert.True(timer!.Cancelled);
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
        Assert.True(animation.Cancelled);
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
        Assert.True(first!.Cancelled);
        Assert.NotSame(first, timer);
        Assert.False(timer!.Cancelled);
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
        Assert.True(first.Cancelled);
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
        Assert.True(timer!.Cancelled);
    }
}
