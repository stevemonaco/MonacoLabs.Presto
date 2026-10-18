using System.Diagnostics;

namespace MonacoLabs.Presto.Tests;

public sealed class MotionValueTests : EngineTestBase
{
    [Fact]
    public void CreateMotionValueReturnsANewSourceHoldingTheInitialValue()
    {
        var zero = Presto.CreateMotionValue();
        var five = Presto.CreateMotionValue(5);

        Assert.Equal(0, zero.Value);
        Assert.Equal(5, five.Value);
        Assert.NotSame(zero, Presto.CreateMotionValue());
        five.Set(7);
        Assert.Equal(7, five.Value);
        Assert.Equal(0, zero.Value);
    }

    [Theory]
    [InlineData(-50, 0)]
    [InlineData(25, 50)]
    [InlineData(100, 200)]
    [InlineData(150, 200)]
    public void MapClampsToTheOutputRange(double input, double expected)
    {
        var source = Presto.CreateMotionValue(input);

        Assert.Equal(expected, source.Map([0, 100], [0, 200]).Value);
    }

    [Theory]
    [InlineData(-50, -100)]
    [InlineData(150, 300)]
    public void UnclampedMapExtrapolatesTheEndSegments(double input, double expected)
    {
        var source = Presto.CreateMotionValue(input);

        Assert.Equal(expected, source.Map([0, 100], [0, 200], clamp: false).Value);
    }

    [Theory]
    [InlineData(50, 5)]
    [InlineData(100, 10)]
    [InlineData(150, 5)]
    [InlineData(200, 0)]
    public void MapInterpolatesAcrossSegments(double input, double expected)
    {
        var source = Presto.CreateMotionValue(input);

        Assert.Equal(expected, source.Map([0, 100, 200], [0, 10, 0]).Value);
    }

    [Fact]
    public void MapEasesEachSegment()
    {
        var source = Presto.CreateMotionValue(150);

        var mapped = source.Map([0, 100, 200], [0, 10, 20], Eases.InQuad);

        Assert.Equal(12.5, mapped.Value);
    }

    [Fact]
    public void MapFuncCombineAndSnapDerive()
    {
        var a = Presto.CreateMotionValue(3);
        var b = Presto.CreateMotionValue(4);

        var doubled = a.Map(v => v * 2);
        var sum = MotionValue.Combine(a, b, (x, y) => x + y);
        var product = MotionValue.Combine([a, b, doubled], values => values[0] * values[1] * values[2]);
        var snapped = sum.Snap(5);
        var pointed = sum.Snap([0, 6, 10]);

        Assert.Equal(6, doubled.Value);
        Assert.Equal(7, sum.Value);
        Assert.Equal(72, product.Value);
        Assert.Equal(5, snapped.Value);
        Assert.Equal(6, pointed.Value);
    }

    [Fact]
    public void ReadingADerivedValueBetweenFramesIsFresh()
    {
        var source = Presto.CreateMotionValue();
        var observed = source.Map(v => v + 1);
        observed.Changed += _ => { };
        var unobserved = source.Map(v => v * 10);

        source.Set(2);

        Assert.Equal(3, observed.Value);
        Assert.Equal(20, unobserved.Value);
    }

    [Fact]
    public void ADiamondEvaluatesOncePerChange()
    {
        var a = Presto.CreateMotionValue();
        var b = a.Map(v => v + 1);
        var c = a.Map(v => v * 2);
        var calls = 0;
        var d = MotionValue.Combine(b, c, (x, y) => { calls++; return x + y; });
        d.Changed += _ => { };
        calls = 0;

        a.Set(1);
        Frames.Advance(16);
        a.Set(2);
        Frames.Advance(16);

        Assert.Equal(2, calls);
        Assert.Equal(7, d.Value);
        Assert.Equal(2, calls);
    }

    [Fact]
    public void ASinkRaisesOnceWhenEverySourceChangesInOneFrame()
    {
        var x = Presto.CreateMotionValue();
        var y = Presto.CreateMotionValue();
        var z = Presto.CreateMotionValue();
        var sum = MotionValue.Combine([x, y, z], values => values[0] + values[1] + values[2]);
        var raised = new List<double>();
        sum.Changed += value => raised.Add(value.Value);

        x.Set(1);
        y.Set(2);
        z.Set(3);
        Frames.Advance(16);
        Frames.Advance(16);

        Assert.Equal([6], raised);
    }

    [Fact]
    public void ASinkDoesNotRaiseWhenTheFrameEndsUnchanged()
    {
        var source = Presto.CreateMotionValue(5);
        var raised = 0;
        source.Map(v => v * 2).Changed += _ => raised++;

        source.Set(8);
        source.Set(5);
        Frames.Advance(16);

        Assert.Equal(0, raised);
    }

    [Fact]
    public void SettingASourceWakesTheEngineUntilTheFlush()
    {
        var source = Presto.CreateMotionValue();
        var raised = 0;
        source.Changed += _ => raised++;
        Assert.False(Frames.IsRunning);

        source.Set(1);

        Assert.True(Frames.IsRunning);
        Frames.Advance(16);
        Assert.Equal(1, raised);
        Frames.Advance(16);
        Assert.False(Frames.IsRunning);
        Assert.False(Presto.Engine.IsRunning);
    }

    [Fact]
    public void ASetFromAHandlerFlushesOnTheNextFrame()
    {
        var source = Presto.CreateMotionValue();
        var raised = new List<double>();
        source.Changed += value =>
        {
            raised.Add(value.Value);
            if (value.Value < 3) source.Set(value.Value + 1);
        };

        source.Set(1);
        Frames.Advance(16);
        Assert.Equal([1], raised);
        Frames.Advance(16);
        Frames.Advance(16);

        Assert.Equal([1, 2, 3], raised);
    }

    [Fact]
    public void UnsubscribingDequeuesTheSink()
    {
        var source = Presto.CreateMotionValue();
        var raised = 0;
        Action<MotionValue> handler = _ => raised++;
        var mapped = source.Map(v => v + 1);
        mapped.Changed += handler;

        source.Set(1);
        mapped.Changed -= handler;
        Frames.Advance(16);
        source.Set(2);

        Assert.Equal(0, raised);
        Assert.Equal(3, mapped.Value);
    }

    [Fact]
    public void SettingADerivedValueThrows()
    {
        var mapped = Presto.CreateMotionValue().Map(v => v);

        Assert.Throws<InvalidOperationException>(() => mapped.Set(1));
    }

    [Theory]
    [InlineData(50, .8, 50)]
    [InlineData(0, .8, 0)]
    [InlineData(100, .8, 100)]
    [InlineData(150, .8, 110)]
    [InlineData(-50, .8, -10)]
    [InlineData(150, .5, 125)]
    [InlineData(-50, .5, -25)]
    [InlineData(150, 1, 100)]
    [InlineData(-50, 1, 0)]
    [InlineData(150, 0, 150)]
    [InlineData(-50, 0, -50)]
    [InlineData(150, 2, 100)]
    [InlineData(150, -1, 150)]
    public void ResistScalesTheExcessPastEitherBound(double input, double friction, double expected)
    {
        var source = Presto.CreateMotionValue(input);

        Assert.Equal(expected, source.Resist(0, 100, friction).Value, 1e-9);
    }

    [Fact]
    public void ResistDefaultsToAFrictionOfPointEight()
    {
        var source = Presto.CreateMotionValue(150);

        Assert.Equal(110, source.Resist(0, 100).Value, 1e-9);
    }

    [Fact]
    public void ResistWithMinAboveMaxThrows() =>
        Assert.Throws<ArgumentException>(() => Presto.CreateMotionValue().Resist(1, 0));

    [Fact]
    public void InvalidMapRangesThrow()
    {
        var source = Presto.CreateMotionValue();

        Assert.Throws<ArgumentException>(() => source.Map([0, 1], [0, 1, 2]));
        Assert.Throws<ArgumentException>(() => source.Map([0], [0]));
        Assert.Throws<ArgumentException>(() => source.Map([0, 2, 1], [0, 1, 2]));
        Assert.Throws<ArgumentException>(() => source.Map([0, 0], [0, 1]));
    }

    [Theory]
    [InlineData(-50, "rgba(255,0,0,1)")]
    [InlineData(50, "rgba(128,128,0,1)")]
    [InlineData(100, "rgba(0,255,0,1)")]
    [InlineData(150, "rgba(0,128,128,1)")]
    [InlineData(250, "rgba(0,0,255,1)")]
    public void MapInterpolatesColorStringsAcrossStopsAndClamps(double input, string expected)
    {
        var source = Presto.CreateMotionValue(input);

        Assert.Equal(expected, source.Map([0, 100, 200], ["#ff0000", "rgb(0,255,0)", "#0000ff"]).Value);
    }

    [Fact]
    public void ObjectMapEasesEachSegment()
    {
        var source = Presto.CreateMotionValue(150);

        var mapped = source.Map([0, 100, 200], ["#ff0000", "#00ff00", "#0000ff"], Eases.InQuad);

        Assert.Equal("rgba(0,191,64,1)", mapped.Value);
    }

    [Fact]
    public void ObjectMapRaisesOnlyWhenTheOutputMoves()
    {
        var source = Presto.CreateMotionValue();
        var mapped = source.Map([0, 100], ["0px", "100px"]);
        var raised = new List<object>();
        mapped.Changed += value => raised.Add(value.Value);

        source.Set(50);
        Frames.Advance(16);
        source.Set(150);
        Frames.Advance(16);
        source.Set(200);
        Frames.Advance(16);

        Assert.Equal(["50px", "100px"], raised);
    }

    [Fact]
    public void ObjectMapOutputsOfDifferentKindsThrow()
    {
        var source = Presto.CreateMotionValue();

        Assert.Throws<ArgumentException>(() => source.Map([0, 1], ["#ff0000", "10px"]));
        Assert.Throws<ArgumentException>(() => source.Map([0, 1], ["10px", "50%"]));
    }

    [Fact]
    public void SettingFromAnotherThreadFailsTheDebugAssertionOnceTheEngineIsPinned()
    {
#if !DEBUG
        Assert.Skip("The thread assertion exists only in Debug builds.");
#endif
        var source = Presto.CreateMotionValue();
        var listener = new FailureListener();
        var listeners = Trace.Listeners.Cast<TraceListener>().ToArray();
        Trace.Listeners.Clear();
        Trace.Listeners.Add(listener);
        try
        {
            OnAnotherThread(() => source.Set(1));
            Assert.Equal(0, listener.Failures);

            Presto.Engine.Update();
            OnAnotherThread(() => source.Set(2));
            Assert.Equal(1, listener.Failures);
        }
        finally
        {
            Trace.Listeners.Clear();
            Trace.Listeners.AddRange(listeners);
        }
    }

    private static void OnAnotherThread(Action action)
    {
        var thread = new Thread(() => action());
        thread.Start();
        thread.Join();
    }

    private sealed class FailureListener : TraceListener
    {
        public int Failures { get; private set; }

        public override void Fail(string? message, string? detailMessage) => Failures++;

        public override void Write(string? message) { }

        public override void WriteLine(string? message) { }
    }

    [Fact]
    public void AnObjectMapEncodesAgainOnlyWhenItsOutputMoves()
    {
        var source = Presto.CreateMotionValue(150);
        var mapped = source.Map([0, 100], ["0px", "100px"]);

        var first = mapped.Value;
        Assert.Same(first, mapped.Value);
        source.Set(200);
        Assert.Same(first, mapped.Value);

        source.Set(50);
        var moved = mapped.Value;
        Assert.Equal("50px", moved);
        Assert.NotSame(first, moved);
    }

    [Fact]
    public void AnObjectMapSubscribesToItsSourceOnlyWhileItHasAHandler()
    {
        var source = Presto.CreateMotionValue();
        var mapped = source.Map([0, 100], ["0px", "100px"]);
        Action<MotionObjectValue> handler = _ => { };

        source.Set(10);
        Assert.False(source._queued);

        mapped.Changed += handler;
        source.Set(20);
        Assert.True(source._queued);
        Frames.Advance(16);

        mapped.Changed -= handler;
        source.Set(30);
        Assert.False(source._queued);
    }

    [Fact]
    public void CreateMotionValueDefaultsToZero() => Assert.Equal(0, Presto.CreateMotionValue().Value);
}
