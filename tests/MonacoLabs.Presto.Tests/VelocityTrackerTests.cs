namespace MonacoLabs.Presto.Tests;

/// <summary>The velocity sampler extracted from <c>computeVelocity</c> in draggable.js, per INERTIA-PLAN §5.</summary>
public sealed class VelocityTrackerTests
{
    [Fact]
    public void ConstantRateFeedGivesTheRate()
    {
        var tracker = new VelocityTracker();

        for (var t = 0; t <= 200; t += 20) tracker.Add(t * 1.5, t);

        Assert.Equal(1.5, tracker.Velocity, 9);
    }

    [Fact]
    public void SamplesCloserThanTheIntervalAreIgnored()
    {
        var tracker = new VelocityTracker();
        tracker.Add(0, 0);

        tracker.Add(100, 10);
        Assert.Equal(0, tracker.Velocity);

        tracker.Add(100, 20);
        Assert.Equal(5, tracker.Velocity, 9);
    }

    [Fact]
    public void IsTheStrongestOfTheLastSamples()
    {
        var tracker = new VelocityTracker(interval: 10, samples: 3);
        tracker.Add(0, 0);

        tracker.Add(50, 10);
        tracker.Add(60, 20);
        tracker.Add(70, 30);
        Assert.Equal(5, tracker.Velocity, 9);

        tracker.Add(80, 40);
        Assert.Equal(1, tracker.Velocity, 9);
    }

    [Fact]
    public void KeepsTheSign()
    {
        var tracker = new VelocityTracker();
        tracker.Add(0, 0);

        tracker.Add(-40, 20);
        tracker.Add(-50, 40);

        Assert.Equal(-2, tracker.Velocity, 9);
    }

    [Fact]
    public void ResetZeroesAndStartsANewBaseline()
    {
        var tracker = new VelocityTracker();
        tracker.Add(0, 0);
        tracker.Add(100, 20);

        tracker.Reset();
        Assert.Equal(0, tracker.Velocity);

        tracker.Add(500, 40);
        Assert.Equal(0, tracker.Velocity);
        tracker.Add(520, 60);
        Assert.Equal(1, tracker.Velocity, 9);
    }

    [Fact]
    public void ReproducesTheDraggableSampler()
    {
        const double multiplier = 1.3, min = .2;
        var max = 3.0;
        var tracker = new VelocityTracker();
        var replica = new UpstreamDraggableSampler(start: 0);
        tracker.AddDisplacement(0, 0);
        double[] times = [5, 17, 30, 34, 60, 61, 90, 130, 131, 150, 300, 317, 318, 340];
        double[] dxs = [3, 12, -4, 8, 90, 1, 0, .5, 7, -20, 2, 0, 6, 1];
        double[] dys = [1, -5, 2, 0, 40, 1, 0, .1, -3, 10, 0, 0, 2, 1];
        var velocity = 0.0;

        for (var i = 0; i < times.Length; i++)
        {
            // Earlier samples keep the limits they were clamped to.
            if (i == 6) max = .5;
            if (i == 10)
            {
                replica.Clear();
                tracker.Reset();
                velocity = 0;
            }
            if (tracker.AddDisplacement(Math.Sqrt(dxs[i] * dxs[i] + dys[i] * dys[i]), times[i], multiplier, min, max))
                velocity = tracker.Velocity;

            Assert.Equal(replica.Compute(dxs[i], dys[i], times[i], multiplier, min, max), velocity);
        }
    }

    [Fact]
    public void TheDraggableSamplerTakesTheStrongestSampleWhenTheyAreNegative()
    {
        var tracker = new VelocityTracker();
        tracker.AddDisplacement(0, 0);

        tracker.AddDisplacement(10, 20, -1, -3, 3);
        tracker.AddDisplacement(40, 40, -1, -3, 3);

        Assert.Equal(-2, tracker.Velocity);
    }

    [Fact]
    public void AddDoesNotAllocate()
    {
        var tracker = new VelocityTracker();
        for (var i = 0; i < 10; i++) tracker.Add(i, i * 20);

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 10; i < 1000; i++) tracker.Add(i * (i % 3), i * 10);
        var bytes = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.Equal(0, bytes);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-17)]
    public void ANonPositiveIntervalThrows(double interval) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => new VelocityTracker(interval));

    [Fact]
    public void FewerThanOneSampleThrows() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => new VelocityTracker(samples: 0));

    /// <summary><c>computeVelocity</c> in draggable.js, line for line.</summary>
    private sealed class UpstreamDraggableSampler(double start)
    {
        private readonly double[] _stack = new double[3];
        private double _time = start;
        private int _index;
        private double _velocity;

        public double Compute(double dx, double dy, double time, double multiplier, double min, double max)
        {
            var elapsed = time - _time;
            if (elapsed < 17) return _velocity;
            _time = time;
            _stack[_index] = Utils.Round(Utils.Clamp(Math.Sqrt(dx * dx + dy * dy) / elapsed * multiplier, min, max), 5);
            _index = (_index + 1) % 3;
            return _velocity = Math.Max(_stack[0], Math.Max(_stack[1], _stack[2]));
        }

        public void Clear()
        {
            Array.Clear(_stack);
            _index = 0;
            _velocity = 0;
        }
    }
}
