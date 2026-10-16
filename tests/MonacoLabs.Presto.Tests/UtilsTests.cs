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
