namespace MonacoLabs.Presto.Tests;

public sealed class HostHookTests : EngineTestBase
{
    private static readonly object Lifetime = new();
    private static readonly object ReducedLifetime = new();

    [Fact]
    public void HostLifetimeReachesTheTimer()
    {
        var animation = Presto.Animate(new StyleTarget(), new() { ["X"] = 100, HostLifetime = Lifetime });
        var timeline = Presto.CreateTimeline(new() { HostLifetime = Lifetime });
        timeline.Add(new StyleTarget(), new AnimationParams { ["X"] = 100, HostLifetime = ReducedLifetime });

        Assert.Same(Lifetime, animation._hostLifetime);
        Assert.Same(Lifetime, timeline._hostLifetime);
        Assert.Same(ReducedLifetime, timeline._head!._hostLifetime);
    }

    [Fact]
    public void HostLifetimeSurvivesBuilderCloneWithAndToParams()
    {
        var builder = Presto.Build().Property("X", 100).HostLifetime(Lifetime);

        Assert.Same(Lifetime, builder.ToParams().HostLifetime);
        Assert.Same(Lifetime, builder.Clone().ToParams().HostLifetime);
        Assert.Same(Lifetime, builder.With(b => b.Duration(200)).ToParams().HostLifetime);
    }

    [Fact]
    public void ReducedFormInheritsHostLifetimeUnlessItSetsItsOwn()
    {
        Presto.Defaults.ReducedMotion = true;
        var inherits = Presto.Build().Property("X", 100).HostLifetime(Lifetime).WhenReduced(r => r.Property("Opacity", 1));
        var overrides = Presto.Build().Property("X", 100).HostLifetime(Lifetime).WhenReduced(r => r.HostLifetime(ReducedLifetime));
        var reducedParams = new AnimationParams
        {
            ["X"] = 100,
            HostLifetime = Lifetime,
            WhenReduced = new() { ["Opacity"] = 1 },
        };

        Assert.Same(Lifetime, inherits.ToParams().HostLifetime);
        Assert.Same(ReducedLifetime, overrides.ToParams().HostLifetime);
        Assert.Same(Lifetime, reducedParams.Resolve(Presto.Engine).HostLifetime);
        Assert.Same(ReducedLifetime, (reducedParams with { WhenReduced = new() { HostLifetime = ReducedLifetime } }).Resolve(Presto.Engine).HostLifetime);
    }

    [Fact]
    public void TimerCreatedFiresOncePerRootAndPerTimelineChild()
    {
        var created = new List<Timer>();
        Presto.Engine.TimerCreated = created.Add;

        var timer = Presto.CreateTimer(new() { Duration = 100 });
        var animation = Presto.Animate(new StyleTarget(), new() { ["X"] = 100 });
        var timeline = Presto.CreateTimeline();
        timeline.Add(new StyleTarget(), new AnimationParams { ["X"] = 100 })
            .Add(new TimerParams { Duration = 50 })
            .Add(new[] { new StyleTarget(), new StyleTarget() }, new AnimationParams { ["X"] = 100 }, Presto.Stagger(10));

        Assert.Equal(7, created.Count);
        Assert.Same(timer, created[0]);
        Assert.Same(animation, created[1]);
        Assert.Same(timeline, created[2]);
        Assert.All(created.Skip(3), child => Assert.Same(timeline, child.Parent));
        var children = new List<Timer>();
        foreach (var child in timeline.Children) children.Add(child);
        Assert.Equal(children, created.Skip(3));
    }

    [Fact]
    public void TimerCreatedFiresAfterInit()
    {
        var running = false;
        Presto.Engine.TimerCreated = t => running = t.IsScheduled;

        Presto.Animate(new StyleTarget(), new() { ["X"] = 100 });

        Assert.True(running);
    }
}
