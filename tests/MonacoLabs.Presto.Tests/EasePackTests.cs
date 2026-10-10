namespace MonacoLabs.Presto.Tests;

/// <summary>
/// <see cref="Eases.Wiggle"/>, <see cref="Eases.Rough"/>, <see cref="Eases.SlowMo"/> and <see cref="Eases.ExpoScale"/>, after
/// GSAP's CustomWiggle and EasePack; there is no anime.js upstream.
/// </summary>
public sealed class EasePackTests
{
    private static double[] Sample(Ease ease, int count = 2000)
    {
        var values = new double[count + 1];
        for (var i = 0; i <= count; i++) values[i] = ease((double)i / count);
        return values;
    }

    private static List<double> Peaks(double[] values)
    {
        var peaks = new List<double>();
        for (var i = 1; i < values.Length - 1; i++)
        {
            var v = Math.Abs(values[i]);
            if (v > Math.Abs(values[i - 1]) && v >= Math.Abs(values[i + 1])) peaks.Add(v);
        }
        return peaks;
    }

    [Theory]
    [InlineData(WiggleType.EaseOut)]
    [InlineData(WiggleType.EaseInOut)]
    [InlineData(WiggleType.Anticipate)]
    [InlineData(WiggleType.Uniform)]
    [InlineData(WiggleType.Random)]
    public void WiggleRestsAtBothEndsAndCrossesZeroTwicePerWiggle(WiggleType type)
    {
        var ease = Eases.Wiggle(6, type, new Random(3));
        var values = Sample(ease);
        var crossings = 0;
        for (var i = 2; i < values.Length - 1; i++)
        {
            if (Math.Sign(values[i]) != Math.Sign(values[i - 1])) crossings++;
        }

        Assert.Equal(0, ease(0));
        Assert.Equal(0, ease(1));
        Assert.InRange(crossings, 2 * 6 - 1, 2 * 6 + 1);
    }

    [Fact]
    public void EaseOutWigglePeaksDecay()
    {
        var peaks = Peaks(Sample(Eases.Wiggle(5)));

        Assert.Equal(10, peaks.Count);
        for (var i = 1; i < peaks.Count; i++) Assert.True(peaks[i] < peaks[i - 1]);
    }

    [Fact]
    public void UniformWigglePeaksAreEqual()
    {
        var ease = Eases.Wiggle(4, WiggleType.Uniform);

        for (var k = 0; k < 8; k++) Assert.Equal(1, Math.Abs(ease((k + .5) / 8)), 9);
    }

    [Fact]
    public void AnticipateSwingsAgainstTheValueFirst() => Assert.True(Eases.Wiggle(4, WiggleType.Anticipate)(1 / 16d) < -.5);

    [Theory]
    [InlineData(RoughTaper.None, true)]
    [InlineData(RoughTaper.In, true)]
    [InlineData(RoughTaper.Out, false)]
    [InlineData(RoughTaper.Both, false)]
    public void RoughIsExactAtTheEnds(RoughTaper taper, bool randomize)
    {
        var ease = Eases.Rough(taper: taper, randomize: randomize, random: new Random(5));

        Assert.Equal(0, ease(0));
        Assert.Equal(1, ease(1));
    }

    [Fact]
    public void RoughWithTheSameSeedIsIdentical()
        => Assert.Equal(Sample(Eases.Rough(30, 2, random: new Random(9))), Sample(Eases.Rough(30, 2, random: new Random(9))));

    [Fact]
    public void ClampedRoughStaysInTheUnitRange()
    {
        var values = Sample(Eases.Rough(40, 3, clamp: true, random: new Random(2)));

        Assert.All(values, v => Assert.InRange(v, 0, 1));
    }

    [Fact]
    public void RoughIsJagged() => Assert.Contains(Sample(Eases.Rough(random: new Random(4))).Zip(Sample(Eases.None)), p => Math.Abs(p.First - p.Second) > .05);

    [Fact]
    public void RoughFollowsItsTemplateWithZeroStrength()
    {
        var ease = Eases.Rough(10, 1e-12, randomize: false, template: Eases.InQuad);

        Assert.Equal(Eases.InQuad(.3), ease(.3), 9);
    }

    [Fact]
    public void SlowMoHasConstantSlopeOverItsLinearSection()
    {
        var ease = Eases.SlowMo(.5, .6);
        var slope = (ease(.3) - ease(.25)) / .05;

        Assert.Equal(1 - .6, slope, 9);
        Assert.Equal(slope, (ease(.75) - ease(.6)) / .15, 9);
        Assert.Equal(0, ease(0));
        Assert.Equal(1, ease(1), 12);
    }

    [Fact]
    public void YoyoSlowMoReturnsToZero()
    {
        var ease = Eases.SlowMo(.5, .7, yoyoMode: true);

        Assert.Equal(0, ease(0));
        Assert.Equal(1, ease(.5));
        Assert.Equal(0, ease(1));
    }

    [Fact]
    public void ExpoScaleMidpointIsTheGeometricMean()
    {
        var ease = Eases.ExpoScale(1, 10);

        Assert.Equal((Math.Sqrt(10) - 1) / 9, ease(.5), 12);
        Assert.Equal(0, ease(0), 12);
        Assert.Equal(1, ease(1), 12);
    }

    [Fact]
    public void ExpoScaleAppliesItsEaseToProgress()
        => Assert.Equal(Eases.ExpoScale(2, .5)(Eases.OutQuad(.3)), Eases.ExpoScale(2, .5, Eases.OutQuad)(.3), 12);

    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, -2)]
    [InlineData(3, 3)]
    public void ExpoScaleRejectsInvalidScales(double start, double end)
        => Assert.Throws<ArgumentOutOfRangeException>(() => Eases.ExpoScale(start, end));

    [Fact]
    public void InvokingDoesNotAllocate()
    {
        Ease[] eases =
        [
            Eases.Wiggle(), Eases.Wiggle(8, WiggleType.Random, new Random(1)), Eases.Rough(random: new Random(1)),
            Eases.SlowMo(), Eases.SlowMo(yoyoMode: true), Eases.ExpoScale(1, 4),
        ];
        var sum = 0d;
        foreach (var ease in eases) sum += Sample(ease, 10).Sum();

        var before = GC.GetAllocatedBytesForCurrentThread();
        foreach (var ease in eases)
        {
            for (var i = 0; i <= 1000; i++) sum += ease(i / 1000d);
        }
        var bytes = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.True(sum != 0);
        Assert.Equal(0, bytes);
    }
}
