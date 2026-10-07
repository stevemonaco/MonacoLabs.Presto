using Monaco.Presto.Values;

namespace Monaco.Presto.Tests;

/// <summary>
/// Per-frame allocation budget of the tick path. Each bound is the figure measured on a warm loop, so an allocation put
/// back into a render shows up here instead of as frame rate noise.
/// </summary>
public sealed class AllocationTests : EngineTestBase
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void LoopingTimerTicksWithoutAllocating(bool diagnostics)
    {
        Presto.Engine.Diagnostics.Enabled = diagnostics;
        var updates = 0;
        Presto.CreateTimer(new() { Duration = 400, Loop = true, OnUpdate = _ => updates++ });

        var bytes = BytesPerTick();

        Assert.True(updates >= 200, $"the timer stopped after {updates} updates");
        Assert.True(bytes <= 0, $"{bytes:0.##} bytes per tick");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void NumberTweenTicksWithoutAllocating(bool diagnostics)
    {
        Presto.Engine.Diagnostics.Enabled = diagnostics;
        var targets = new TestObject[10];
        for (var i = 0; i < targets.Length; i++) targets[i] = new TestObject();
        Presto.Animate(targets, new() { ["PlainValue"] = 100, Delay = Presto.Stagger(10), Duration = 400, Loop = true, Alternate = true });

        var bytes = BytesPerTick();

        Assert.True(bytes <= 0, $"{bytes:0.##} bytes per tick");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DiscreteTweenTicksWithoutAllocating(bool diagnostics)
    {
        Presto.Engine.Diagnostics.Enabled = diagnostics;
        var target = new ToggleTarget();
        Presto.Animate(target, new() { ["On"] = TweenValue.Of(true), Duration = 400, Loop = true, Alternate = true });

        var bytes = BytesPerTick();

        Assert.True(bytes <= 0, $"{bytes:0.##} bytes per tick");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void BouncingInertiaTweenTicksWithoutAllocating(bool diagnostics)
    {
        Presto.Engine.Diagnostics.Enabled = diagnostics;
        Presto.Animate(new StyleTarget(), new() { ["X"] = new Inertia(2) { Max = 300 }, Loop = true });

        var bytes = BytesPerTick();

        Assert.True(bytes <= 0, $"{bytes:0.##} bytes per tick");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ColorTweenTicksWithoutAllocating(bool diagnostics)
    {
        Presto.Engine.Diagnostics.Enabled = diagnostics;
        var target = new ColorTarget();
        Presto.Animate(target, new() { ["Background"] = "#FF4B4B", Duration = 400, Loop = true, Alternate = true });

        var bytes = BytesPerTick();

        Assert.True(bytes <= 0, $"{bytes:0.##} bytes per tick");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ChannelMomentumTicksWithoutAllocating(bool diagnostics)
    {
        Presto.Engine.Diagnostics.Enabled = diagnostics;
        // A barely damped spring, so the animatable is still moving for the whole measurement.
        var animatable = Presto.CreateAnimatable(new ColorTarget(), new()
        {
            ["Background"] = new(), Ease = new Spring(stiffness: 5, damping: .2), PreserveVelocity = true,
        });
        animatable.Set("Background", [255, 75, 75, 1]);
        Frames.Advance(160, 16);
        animatable.Set("Background", [0, 0, 255, 1]);

        var bytes = BytesPerTick();

        Assert.False(animatable.Animations["Background"].Paused);
        Assert.True(bytes <= 0, $"{bytes:0.##} bytes per tick");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ParticleSystemStepsWithoutAllocating(bool diagnostics)
    {
        Presto.Engine.Diagnostics.Enabled = diagnostics;
        var system = new ParticleSystem(new()
        {
            Burst = 500, Rate = 5000, Lifetime = (100, 200), Gravity = .001, Drag = 1, Spin = (-1, 1), Flutter = 2,
            Size = new(4, 8, "outQuad"), ColorMix = new(0, 1), EndColor = new(0, 0, 0), Seed = 1,
        });
        system.Start();
        for (var i = 0; i < 10; i++) system.Step(16);

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 200; i++) system.Step(16);
        var bytes = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.True(system.Live > 400, $"{system.Live} live");
        Assert.Equal(0, bytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MotionValueGraphFlushesWithoutAllocating(bool diagnostics)
    {
        Presto.Engine.Diagnostics.Enabled = diagnostics;
        var source = Presto.CreateMotionValue();
        var other = Presto.CreateMotionValue(1);
        var mapped = source.Map([0, 250, 500], [0, 1, 0], Eases.OutQuad);
        var raised = 0;
        MotionValue.Combine(mapped, other, (a, b) => a * b).Changed += _ => raised++;
        Presto.CreateTimer(new() { Duration = 400, Loop = true, OnUpdate = _ => source.Set(source.Value + 1) });

        var bytes = BytesPerTick();

        Assert.True(raised >= 200, $"the sink raised {raised} times");
        Assert.True(bytes <= 0, $"{bytes:0.##} bytes per tick");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SettlingSpringAndVelocityOfStepWithoutAllocating(bool diagnostics)
    {
        Presto.Engine.Diagnostics.Enabled = diagnostics;
        var source = Presto.CreateMotionValue();
        var raised = 0;
        source.Spring(new Spring(stiffness: 120, damping: 12)).VelocityOf().Changed += _ => raised++;
        Presto.CreateTimer(new() { Duration = 400, Loop = true, OnLoop = _ => source.Set(source.Value == 0 ? 100 : 0) });
        source.Set(100);

        var bytes = BytesPerTick();

        Assert.True(raised >= 50, $"the velocity raised {raised} times");
        Assert.True(bytes <= 0, $"{bytes:0.##} bytes per tick");
    }

    [Fact]
    public void ColorMixAndShiftWithoutAllocating()
    {
        var from = new PrestoColor(37, 99, 235);
        var to = new PrestoColor(245, 158, 11);
        PrestoColor.Mix(from, to, .5, ColorSpace.Oklch, HueInterpolation.Longer);
        from.ShiftOklch(dc: .2);

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 1; i < 100; i++)
        {
            PrestoColor.Mix(from, to, i / 100.0, ColorSpace.Oklch, HueInterpolation.Longer);
            from.ShiftOklch(dc: .2, dh: i);
        }
        var bytes = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.Equal(0, bytes);
    }

    [Fact]
    public void CreatingATimerWithDiagnosticsOffNeverTouchesTheRegistry()
    {
        TimerParams parameters = new() { Autoplay = false };
        Presto.CreateTimer(parameters);

        var before = GC.GetAllocatedBytesForCurrentThread();
        var untracked = Presto.CreateTimer(parameters);
        var offBytes = GC.GetAllocatedBytesForCurrentThread() - before;
        Presto.Engine.Diagnostics.Enabled = true;
        before = GC.GetAllocatedBytesForCurrentThread();
        Presto.CreateTimer(parameters);
        var onBytes = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.Null(untracked._diagnostics);
        Assert.DoesNotContain(untracked, Presto.Engine.Diagnostics.Roots);
        Assert.True(offBytes < onBytes, $"{offBytes} bytes off, {onBytes} on");
    }

    [Fact]
    public void EaseFactoriesAndParserAllocateNoMoreWithDiagnosticsInactive()
    {
        Eases.Out(2);
        EaseParser.Parse("out(1.5)");

        var before = GC.GetAllocatedBytesForCurrentThread();
        Eases.Out(3);
        var factoryBytes = GC.GetAllocatedBytesForCurrentThread() - before;
        // The minimum of three, so a cache resize during one parse does not count against the budget.
        var parserBytes = long.MaxValue;
        foreach (var ease in new[] { "out(3.25)", "out(3.5)", "out(3.75)" })
        {
            before = GC.GetAllocatedBytesForCurrentThread();
            EaseParser.Parse(ease);
            parserBytes = Math.Min(parserBytes, GC.GetAllocatedBytesForCurrentThread() - before);
        }

        // measured before ease names existed: 176 and 352.
        Assert.True(factoryBytes <= 176, $"{factoryBytes} bytes per factory call");
        Assert.True(parserBytes <= 352, $"{parserBytes} bytes per parse");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void TimelineTicksWithinItsLoopBoundaryBudget(bool diagnostics)
    {
        Presto.Engine.Diagnostics.Enabled = diagnostics;
        var targets = new[] { new TestObject(), new TestObject(), new TestObject() };
        Presto.CreateTimeline(new() { Loop = true, Defaults = new() { Duration = 200 } })
            .Add(targets[0], new() { ["PlainValue"] = 100 })
            .Add(targets[1], new() { ["PlainValue"] = 100 }, "-=100")
            .Add(targets[2], new() { ["PlainValue"] = 100 }, "+=50");

        var bytes = BytesPerTick();

        // measured .72: each loop boundary re-renders its children internally, and an internal render composes Tween._value.
        Assert.True(bytes <= 2, $"{bytes:0.##} bytes per tick");
    }

    private sealed class ToggleTarget
    {
        public bool On { get; set; }
    }
}
