namespace Monaco.Presto.Tests;

/// <summary>Port of anime.js leaks.test.js: the engine holds no tickable once everything it ran has completed.</summary>
public sealed class LeaksTests : EngineTestBase
{
    [Fact]
    public void EngineShouldNotContainAnyActiveTickable()
    {
        var target = new StyleTarget();
        Presto.Animate(target, new() { ["X"] = 100, Duration = 20 });
        Presto.Animate(target, new() { ["Opacity"] = TweenValue.FromTo(0, 1), Duration = 30, Delay = 10 });
        Presto.Set(target, new() { ["Y"] = 50 });
        Presto.CreateTimer(new() { Duration = 15 });
        var last = Presto.CreateTimer(new() { Duration = 40 });

        RunUntil(() => last.Completed);
        Frames.Advance(48, 16);

        Assert.Null(Presto.Engine._tail);
        Assert.False(Presto.Engine._hasChildren);
    }
}
