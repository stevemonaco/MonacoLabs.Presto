using Monaco.Presto.Core;

namespace Monaco.Presto.Tests;

public sealed class ReducedMotionTests : EngineTestBase
{
    [Fact]
    public void ReduceAllWritesTheEndValueOnTheFirstFrameAndCompletes()
    {
        var target = new StyleTarget();
        var calls = new List<string>();
        Presto.Defaults.ReduceAll = true;

        var animation = Presto.Animate(target, new()
        {
            ["X"] = 100,
            Duration = 1000,
            Delay = 300,
            Loop = 3,
            OnBegin = _ => calls.Add("begin"),
            OnComplete = _ => calls.Add("complete"),
        });
        Frames.Advance(16);

        Assert.Equal(100, target.X);
        Assert.Equal(Constants.MinValue, animation.Duration);
        Assert.True(animation.Completed);
        Assert.Equal(["begin", "complete"], calls);
    }

    [Fact]
    public void ReduceAllEndsAKeyframedPropertyOnItsLastKeyframe()
    {
        var target = new StyleTarget();
        Presto.Defaults.ReduceAll = true;

        Presto.Animate(target, new()
        {
            ["X"] = new TweenParams[] { new() { To = 50, Duration = 400 }, new() { To = 20, Duration = 400, Delay = 100 } },
            Keyframes = new Keyframe[] { new() { ["Y"] = 80 }, new() { ["Y"] = 30 } },
            Duration = 1000,
        });
        Frames.Advance(16);

        Assert.Equal(20, target.X);
        Assert.Equal(30, target.Y);
    }

    [Fact]
    public void ReduceAllCollapsesTimelineChildren()
    {
        var target = new StyleTarget();
        Presto.Defaults.ReduceAll = true;

        Presto.CreateTimeline()
            .Add(target, new() { ["X"] = 100, Duration = 1000 })
            .Add(target, new() { ["Y"] = 50, Duration = 1000 });
        Frames.Advance(16);

        Assert.Equal(100, target.X);
        Assert.Equal(50, target.Y);
    }

    [Fact]
    public void ReduceAllLeavesAReducedAlternativeAndAPlainTimerAlone()
    {
        Presto.Defaults.ReduceAll = true;
        Presto.Defaults.ReducedMotion = true;

        var animation = Presto.Build()
            .Property("X", 100)
            .Duration(1000)
            .WhenReduced(b => b.Property("Opacity", 1).Duration(200))
            .Animate(new StyleTarget());
        var timer = Presto.CreateTimer(new() { Duration = 1000, Loop = 1 });

        Assert.Equal(200, animation.Duration);
        Assert.Equal(2000, timer.Duration);
    }

    [Fact]
    public void AScopeKeepsItsOwnFlags()
    {
        var scope = Presto.CreateScope(new() { Defaults = d => { d.ReduceAll = true; d.ReducedMotion = true; } });
        var builder = Presto.Build().Property("X", 100).WhenReduced(b => b.Property("Opacity", 1));

        var inside = scope.Execute(_ => Presto.Animate(new StyleTarget(), new() { ["X"] = 100, Duration = 1000 }));
        var insideParams = scope.Execute(_ => builder.ToParams());
        var outside = Presto.Animate(new StyleTarget(), new() { ["X"] = 100, Duration = 1000 });

        Assert.Equal(Constants.MinValue, inside.Duration);
        Assert.Equal(["Opacity"], insideParams.Properties.Keys);
        Assert.Equal(1000, outside.Duration);
        Assert.Equal(["X"], builder.ToParams().Properties.Keys);
    }
}
