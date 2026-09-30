namespace Monaco.Presto.Tests;

/// <summary>
/// Per-frame allocation budget of the tick path. Each bound is the figure measured on a warm loop, so an allocation put
/// back into a render shows up here instead of as frame rate noise.
/// </summary>
public sealed class AllocationTests : EngineTestBase
{
    [Fact]
    public void LoopingTimerTicksWithoutAllocating()
    {
        var updates = 0;
        Presto.CreateTimer(new() { Duration = 400, Loop = true, OnUpdate = _ => updates++ });

        var bytes = BytesPerTick();

        Assert.True(updates >= 200, $"the timer stopped after {updates} updates");
        Assert.True(bytes <= 0, $"{bytes:0.##} bytes per tick");
    }

    [Fact]
    public void NumberTweenTicksWithoutAllocating()
    {
        var targets = new TestObject[10];
        for (var i = 0; i < targets.Length; i++) targets[i] = new TestObject();
        Presto.Animate(targets, new() { ["PlainValue"] = 100, Delay = Presto.Stagger(10), Duration = 400, Loop = true, Alternate = true });

        var bytes = BytesPerTick();

        Assert.True(bytes <= 0, $"{bytes:0.##} bytes per tick");
    }

    [Fact]
    public void BouncingInertiaTweenTicksWithoutAllocating()
    {
        Presto.Animate(new StyleTarget(), new() { ["X"] = new Inertia(2) { Max = 300 }, Loop = true });

        var bytes = BytesPerTick();

        Assert.True(bytes <= 0, $"{bytes:0.##} bytes per tick");
    }

    [Fact]
    public void ColorTweenTicksWithoutAllocating()
    {
        var target = new ColorTarget();
        Presto.Animate(target, new() { ["Background"] = "#FF4B4B", Duration = 400, Loop = true, Alternate = true });

        var bytes = BytesPerTick();

        Assert.True(bytes <= 0, $"{bytes:0.##} bytes per tick");
    }

    [Fact]
    public void TimelineTicksWithinItsLoopBoundaryBudget()
    {
        var targets = new[] { new TestObject(), new TestObject(), new TestObject() };
        Presto.CreateTimeline(new() { Loop = true, Defaults = new() { Duration = 200 } })
            .Add(targets[0], new() { ["PlainValue"] = 100 })
            .Add(targets[1], new() { ["PlainValue"] = 100 }, "-=100")
            .Add(targets[2], new() { ["PlainValue"] = 100 }, "+=50");

        var bytes = BytesPerTick();

        // measured .72: each loop boundary re-renders its children internally, and an internal render composes Tween._value.
        Assert.True(bytes <= 2, $"{bytes:0.##} bytes per tick");
    }
}
