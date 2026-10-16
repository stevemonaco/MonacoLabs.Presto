using MonacoLabs.Presto.Platform;

namespace MonacoLabs.Presto.Tests;

public sealed class FramePacerTests
{
    private static (double Now, double Frame)[] Pace(FramePacer pacer, int count, params double[] intervals)
    {
        var frames = new (double, double)[count];
        var now = 0.0;
        for (var i = 0; i < count; i++)
        {
            if (i > 0) now += intervals[(i - 1) % intervals.Length];
            frames[i] = (now, pacer.Frame(now));
        }
        return frames;
    }

    private static double[] Deltas((double Now, double Frame)[] frames, int from) =>
        [.. frames.Skip(from).Zip(frames.Skip(from + 1), (a, b) => b.Frame - a.Frame)];

    [Fact]
    public void AJitteredCadenceSettlesToEvenDeltasOfItsMean()
    {
        var frames = Pace(new FramePacer(), 300, 4.3, 5.6, 6.8);

        // Pacing starts at frame 8, once 8 intervals have been seen.
        Assert.All(Deltas(frames, 8 + 16), delta => Assert.InRange(delta, 5.567 - 0.05, 5.567 + 0.05));
    }

    [Fact]
    public void ASkippedCallbackAdvancesTwoIntervals()
    {
        var pacer = new FramePacer();
        var (now, before) = Pace(pacer, 40, 5.56)[^1];
        var interval = pacer.Interval;

        var after = pacer.Frame(now + 2 * 5.56);

        Assert.Equal(interval, pacer.Interval);
        Assert.Equal(2 * interval, after - before, 1e-9);
    }

    [Fact]
    public void AGapRestartsAtTheWallClock()
    {
        var pacer = new FramePacer();
        var now = Pace(pacer, 40, 4.3, 5.6, 6.8)[^1].Now;

        Assert.Equal(now + 100, pacer.Frame(now + 100));
    }

    [Fact]
    public void ASlowerSustainedCadenceIsAdoptedWithin32Frames()
    {
        var pacer = new FramePacer();
        var now = Pace(pacer, 40, 5.56)[^1].Now;

        var frames = new double[80];
        for (var i = 0; i < frames.Length; i++) frames[i] = pacer.Frame(now += 16.67);

        for (var i = 32; i < frames.Length - 1; i++) Assert.InRange(frames[i + 1] - frames[i], 16.67 - 0.05, 16.67 + 0.05);
    }

    [Fact]
    public void AFasterCadenceNeverLeadsTheWallClockByMoreThanAnInterval()
    {
        var pacer = new FramePacer();
        var now = Pace(pacer, 40, 16.67)[^1].Now;

        var frames = new double[80];
        for (var i = 0; i < frames.Length; i++)
        {
            frames[i] = pacer.Frame(now += 5.56);
            Assert.InRange(frames[i] - now, 0, 16.67);
        }

        for (var i = 40; i < frames.Length - 1; i++) Assert.InRange(frames[i + 1] - frames[i], 5.56 - 0.05, 5.56 + 0.05);
    }

    [Fact]
    public void FrameTimesNeverPrecedeTheWallClockOrDecrease()
    {
        var random = new Random(7);
        var pacer = new FramePacer();
        var now = 0.0;
        var last = double.NegativeInfinity;

        for (var i = 0; i < 5000; i++)
        {
            if (random.NextDouble() < 0.01) pacer.Restart();
            now += random.NextDouble() switch
            {
                < 0.02 => random.NextDouble() * 200,
                < 0.05 => 0,
                < 0.5 => 5.56 + random.NextDouble() * 2 - 1,
                _ => 16.67 + random.NextDouble() * 4 - 2,
            };
            var frame = pacer.Frame(now);

            Assert.True(frame >= now, $"frame {i}: {frame} before the wall clock {now}");
            Assert.True(frame >= last, $"frame {i}: {frame} before the previous {last}");
            last = frame;
        }
    }
}
