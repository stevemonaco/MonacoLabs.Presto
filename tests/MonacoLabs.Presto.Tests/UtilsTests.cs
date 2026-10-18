using System.Globalization;

namespace MonacoLabs.Presto.Tests;

/// <summary>Port of anime.js tests/suites/utils.test.js.</summary>
public sealed class UtilsTests : EngineTestBase
{
    [Fact]
    public void SnapToAnIncrement()
    {
        Assert.Equal(0, Utils.Snap(10, 25));
        Assert.Equal(25, Utils.Snap(20, 25));
        Assert.Equal(50, Utils.Snap(50, 25));
        Assert.Equal(50, Utils.Snap(60, 25));
        Assert.Equal(75, Utils.Snap(70, 25));
    }

    [Fact]
    public void DampEndsAtZeroAndOneAndPropagatesANaNFactor()
    {
        Assert.Equal(10, Utils.Damp(10, 20, 16, 0));
        Assert.Equal(20, Utils.Damp(10, 20, 16, 1));
        Assert.Equal(10 + 10 * (1 - Math.Exp(-.5 * 16 * .1)), Utils.Damp(10, 20, 16, .5), 12);
        Assert.True(double.IsNaN(Utils.Damp(10, 20, 16, double.NaN)));
    }

    [Fact]
    public void SnapToTheClosestPoint()
    {
        double[] points = [25, 100, 400];

        Assert.Equal(25, Utils.Snap(10, points));
        Assert.Equal(25, Utils.Snap(20, points));
        Assert.Equal(25, Utils.Snap(50, points));
        Assert.Equal(100, Utils.Snap(63, points));
        Assert.Equal(100, Utils.Snap(75, points));
        Assert.Equal(100, Utils.Snap(200, points));
        Assert.Equal(400, Utils.Snap(300, points));
        Assert.Equal(400, Utils.Snap(1000, points));
    }

    [Fact]
    public void ShuffleReordersWithoutLosingItems()
    {
        double[] original = [0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15];
        var shuffled = new List<double>(original);

        for (var i = 0; i < 100; i++)
        {
            Assert.Same(shuffled, Utils.Shuffle(shuffled));
            Assert.Equal(original.Length, shuffled.Count);
            Assert.Equal(120, shuffled.Sum());
            Assert.NotEqual(original, shuffled);
        }
    }

    [Fact]
    public void SeededRandomReproducesTheJavaScriptSequence()
    {
        var rng = Utils.CreateSeededRandom(1234);
        var same = Utils.CreateSeededRandom(1234);

        // Values from the vendored tools/anime createSeededRandom(1234) under node.
        double[] expected = [7.329, 94.863, 43.739, 21.539, 23.7];
        foreach (var value in expected)
        {
            Assert.Equal(value, rng(0, 100, 3));
            Assert.Equal(value, same(0, 100, 3));
        }
    }

    [Fact]
    public void ChainedUtilityFunctions()
    {
        var chain = Utils.MapRange(0, 100, 0, 1000).Clamp(0, 100);

        Assert.Equal(10, chain.Invoke(1));
        Assert.Equal(50, chain.Invoke(5));
        Assert.Equal(100, chain.Invoke(50));

        Func<double, double> modifier = chain;
        Assert.Equal(10, modifier(1));
    }

    [Fact]
    public void ADefaultChainPassesValuesThrough()
    {
        var chain = default(ChainedUtil);

        Assert.Equal(7.5, chain.Invoke(7.5));
        Assert.Equal(5, chain.Clamp(0, 5).Invoke(7.5));
        Assert.Equal("7.50", chain.RoundPad(2)(7.5));
    }

    [Fact]
    public void WrapCyclesPastEitherEnd()
    {
        Assert.Equal(10, Utils.Wrap(370, 0, 360));
        Assert.Equal(350, Utils.Wrap(-10, 0, 360));
        Assert.Equal(0, Utils.Wrap(360, 0, 360));
        Assert.Equal(15, Utils.Wrap(5, 10, 20));
        Assert.Equal(19, Utils.Wrap(-1, 10, 20));
    }

    [Fact]
    public void LerpIsExactAtItsEndsAndDoesNotClamp()
    {
        Assert.Equal(1, Utils.Lerp(1e17, 1, 1));
        Assert.Equal(5, Utils.Lerp(5, double.PositiveInfinity, 0));
        Assert.Equal(15, Utils.Lerp(10, 20, .5));
        Assert.Equal(30, Utils.Lerp(10, 20, 2));
        Assert.Equal(5, Utils.Lerp(10, 20, -.5));
    }

    [Fact]
    public void DegToRadAndRadToDegConvert()
    {
        Assert.Equal(Math.PI, Utils.DegToRad(180));
        Assert.Equal(Math.PI / 2, Utils.DegToRad(90), 15);
        Assert.Equal(180, Utils.RadToDeg(Math.PI));
        Assert.Equal(-90, Utils.RadToDeg(-Math.PI / 2), 12);
    }

    [Fact]
    public void RoundPadFormatsFixedDecimalsInTheInvariantCulture()
    {
        var culture = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");
        try
        {
            Assert.Equal("1.500", Utils.RoundPad(1.5, 3));
            Assert.Equal("1234.50", Utils.RoundPad(1234.5, 2));
            Assert.Equal("3", Utils.RoundPad(2.75, 0));
        }
        finally
        {
            CultureInfo.CurrentCulture = culture;
        }
    }

    [Fact]
    public void ChainableLerpAndDampTakeTheChainedValueAsTheFactor()
    {
        Assert.Equal(15, Utils.Lerp(10, 20).Invoke(.5));
        Assert.Equal(30, Utils.Lerp(10, 20).Invoke(2));
        Assert.Equal(10, Utils.Damp(10, 20, 16).Invoke(0));
        Assert.Equal(20, Utils.Damp(10, 20, 16).Invoke(1));
        Assert.Equal(Utils.Damp(10, 20, 16, .5), Utils.Damp(10, 20, 16).Invoke(.5));
    }

    [Fact]
    public void ChainingLeavesTheOriginalChainUnchanged()
    {
        var clamp = Utils.Clamp(0, 10);
        var scaled = clamp.MapRange(0, 10, 0, 100);
        var wrapped = clamp.Wrap(0, 4);

        Assert.Equal(10, clamp.Invoke(20));
        Assert.Equal(100, scaled.Invoke(20));
        Assert.Equal(2, wrapped.Invoke(20));
    }

    [Fact]
    public void ChainedRoundPadFormatsTheChainsResult()
    {
        var format = Utils.MapRange(0, 1, 0, 10).RoundPad(2);
        var culture = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");
        try
        {
            Assert.Equal("5.00", format(.5));
            Assert.Equal("12.50", format(1.25));
        }
        finally
        {
            CultureInfo.CurrentCulture = culture;
        }
    }

    [Fact]
    public void RandomReachesBothEndsAtItsPrecision()
    {
        var integers = new HashSet<double>();
        var hundredths = new HashSet<double>();
        for (var i = 0; i < 20000; i++)
        {
            integers.Add(Utils.Random(-2, 2));
            var value = Utils.Random(0, 1, 2);
            Assert.InRange(value, 0, 1);
            Assert.Equal(value, Math.Round(value, 2));
            hundredths.Add(value);
        }

        Assert.Equal([-2, -1, 0, 1, 2], integers.Order());
        Assert.Contains(0, hundredths);
        Assert.Contains(1, hundredths);
        Assert.Equal(101, hundredths.Count);
    }

    [Fact]
    public void RandomDefaultsToZeroToOneWithNoDecimals()
    {
        var coin = new HashSet<double>();
        var upToThree = new HashSet<double>();
        var tenths = new HashSet<double>();
        for (var i = 0; i < 2000; i++)
        {
            coin.Add(Utils.Random());
            upToThree.Add(Utils.Random(max: 3));
            tenths.Add(Utils.Random(decimals: 1));
        }

        Assert.Equal([0, 1], coin.Order());
        Assert.Equal([0, 1, 2, 3], upToThree.Order());
        Assert.Equal(11, tenths.Count);
        Assert.All(tenths, value => Assert.InRange(value, 0, 1));
    }

    [Fact]
    public void ASeededGeneratorDefaultsToZeroToOneWithNoDecimals()
    {
        var rng = Utils.CreateSeededRandom(77);
        var same = Utils.CreateSeededRandom(77);

        for (var i = 0; i < 20; i++)
        {
            Assert.Equal(same(0, 1, 0), rng());
            Assert.Equal(same(0, 10, 0), rng(max: 10));
            Assert.Equal(same(0, 1, 3), rng(decimals: 3));
        }
    }

    [Fact]
    public void UnseededGeneratorsTakeSuccessiveSeedsFromTheCounter()
    {
        var first = Utils.CreateSeededRandom();
        var second = Utils.CreateSeededRandom();
        double[] firstDraws = [first(0, 1, 9), first(0, 1, 9), first(0, 1, 9)];
        double[] secondDraws = [second(0, 1, 9), second(0, 1, 9), second(0, 1, 9)];

        var seed = Enumerable.Range(0, 1_000_000).First(s =>
        {
            var candidate = Utils.CreateSeededRandom(s);
            return candidate(0, 1, 9) == firstDraws[0] && candidate(0, 1, 9) == firstDraws[1] && candidate(0, 1, 9) == firstDraws[2];
        });
        var next = Utils.CreateSeededRandom(seed + 1);

        double[] nextDraws = [next(0, 1, 9), next(0, 1, 9), next(0, 1, 9)];
        Assert.Equal(nextDraws, secondDraws);
    }

    [Fact]
    public void RandomPickReturnsEveryElementAndFailsOnAnEmptyList()
    {
        string[] items = ["a", "b", "c"];
        var picked = new HashSet<string>();
        for (var i = 0; i < 1000; i++) picked.Add(Utils.RandomPick(items));

        Assert.Equal(items, picked.Order());
        Assert.Equal("only", Utils.RandomPick(["only"]));
        Assert.Throws<ArgumentOutOfRangeException>(() => Utils.RandomPick(new List<int>()));
    }

    [Fact]
    public void ShuffleWithASeededGeneratorIsDeterminedByTheSeed()
    {
        List<int> Shuffled(int seed) => (List<int>)Utils.Shuffle(Enumerable.Range(0, 16).ToList(), Utils.CreateSeededRandom(seed));

        Assert.Equal(Shuffled(42), Shuffled(42));
        Assert.NotEqual(Shuffled(42), Shuffled(43));
        Assert.NotEqual(Enumerable.Range(0, 16), Shuffled(42));
    }

    [Fact]
    public void SyncWithoutACallbackStillCompletes()
    {
        var timer = Utils.Sync();
        Assert.False(timer.Completed);

        Frames.Advance(16);

        Assert.True(timer.Completed);
    }

    [Fact]
    public void AKeepTimeWrapperTracksOnlyItsOwnTimers()
    {
        var first = new StyleTarget();
        var second = new StyleTarget();
        Func<double, Animation> Keeper(StyleTarget target) => Utils.KeepTime<double, Animation>(duration => Presto.Animate(target, new()
        {
            ["X"] = 100,
            Ease = "linear",
            Duration = duration,
            Autoplay = false,
        }));
        var keepFirst = Keeper(first);
        var keepSecond = Keeper(second);

        keepFirst(1000).Seek(500);
        var other = keepSecond(1000).Seek(200);
        var replaced = keepFirst(1000);

        Assert.Equal(500, replaced.CurrentTime);
        Assert.Equal(200, other.CurrentTime);
        Assert.Equal(20, second.X);
    }

    [Fact]
    public void SyncRunsTheCallbackOnTheNextTick()
    {
        var ran = false;
        Utils.Sync(_ => ran = true);
        Assert.False(ran);

        Frames.Advance(16);

        Assert.True(ran);
    }

    [Fact]
    public void TrackInstanceLoopAlternateProgressWithATimekeeper()
    {
        var target = new StyleTarget();
        var timekeeper = Utils.KeepTime<double, Animation>(duration => Presto.Animate(target, new()
        {
            ["X"] = 100,
            Ease = "linear",
            Duration = duration,
            Loop = true,
            Alternate = true,
        }));

        var animation = timekeeper(1000);
        Assert.Equal(0, animation.CurrentTime);

        animation.Seek(500);
        Assert.Equal(500, animation.CurrentTime);

        animation = timekeeper(2000);
        Assert.Equal(1000, animation.CurrentTime);

        animation.Seek(500);
        Assert.Equal(500, animation.CurrentTime);

        animation = timekeeper(500);
        Assert.Equal(125, animation.CurrentTime);

        animation.Seek(875);
        Assert.Equal(875, animation.CurrentTime);
        Assert.Equal(.25, animation.IterationProgress);

        animation.Seek(1375);
        Assert.Equal(1375, animation.CurrentTime);
        Assert.Equal(.75, animation.IterationProgress);

        animation.Pause();
    }
}
