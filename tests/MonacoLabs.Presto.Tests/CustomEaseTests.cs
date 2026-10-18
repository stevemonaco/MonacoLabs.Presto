namespace MonacoLabs.Presto.Tests;

/// <summary>
/// <see cref="Eases.FromPath"/>, after GSAP's CustomEase; there is no anime.js upstream.
/// </summary>
public sealed class CustomEaseTests
{
    private static void AssertClose(Ease expected, Ease actual, double tolerance)
    {
        for (var i = 0; i <= 100; i++)
        {
            var t = i / 100d;
            Assert.True(Math.Abs(expected(t) - actual(t)) <= tolerance, $"t={t}: {expected(t)} vs {actual(t)}");
        }
    }

    [Theory]
    [InlineData("M0,0 L1,1")]
    [InlineData("M0 0 L.5 .5 1 1")]
    public void StraightLineIsLinear(string path)
        => AssertClose(Eases.None, Eases.FromPath(path), 1e-6);

    [Theory]
    [InlineData(.42, 0, .58, 1)]
    [InlineData(.25, .1, .25, 1)]
    [InlineData(.68, -.55, .265, 1.55)]
    public void SingleCubicMatchesCubicBezier(double x1, double y1, double x2, double y2)
    {
        var path = FormattableString.Invariant($"M0,0 C{x1},{y1} {x2},{y2} 1,1");

        AssertClose(Eases.CubicBezier(x1, y1, x2, y2), Eases.FromPath(path), 1e-3);
    }

    [Fact]
    public void OvershootingPathExceedsOne()
    {
        var ease = Eases.FromPath("M0,0 C0.2,0 0.4,1.6 0.6,1.2 C0.8,0.9 0.9,1 1,1");

        Assert.True(ease(.6) > 1);
    }

    [Fact]
    public void EndpointsAreExact()
    {
        var ease = Eases.FromPath("M0,0 C0.3,-0.3 0.6,1.4 1,1");

        Assert.Equal(0, ease(0));
        Assert.Equal(1, ease(1));
    }

    [Fact]
    public void PathIsNormalizedToTheUnitSquare()
    {
        AssertClose(Eases.FromPath("M0,0 C0.2,0 0.4,1 1,1"), Eases.FromPath("M10,5 C30,5 50,105 110,105"), 1e-9);
    }

    [Fact]
    public void RelativeCommandsMatchAbsolute()
    {
        var absolute = Eases.FromPath("M0,0 C0.2,-0.3 0.3,0.6 0.5,0.6 C0.7,0.6 0.8,1 1,1 L1,1");
        var relative = Eases.FromPath("m0,0 c0.2-0.3 0.3,0.6 0.5,0.6 0.2,0 0.3,0.4 0.5,0.4 l0,0");

        AssertClose(absolute, relative, 1e-9);
    }

    [Fact]
    public void HorizontalAndVerticalCommandsDrawSteps()
    {
        var ease = Eases.FromPath("M0,0 H0.5 V1 H1");

        Assert.Equal(0, ease(.25), 9);
        Assert.Equal(1, ease(.75), 9);
    }

    [Fact]
    public void BackwardsXThrows() => Assert.Throws<ArgumentException>(() => Eases.FromPath("M0,0 L0.6,0.5 L0.4,0.7 L1,1"));

    [Theory]
    [InlineData("M0,0 Q0.5,0 1,1")]
    [InlineData("M0,0 L1,1 Z")]
    [InlineData("0,0 L1,1")]
    [InlineData("M0,0 L1")]
    public void MalformedPathThrows(string path) => Assert.Throws<FormatException>(() => Eases.FromPath(path));

    [Fact]
    public void InvokingDoesNotAllocate()
    {
        var ease = Eases.FromPath("M0,0 C0.2,0 0.4,1 1,1");
        var sum = 0d;
        for (var i = 0; i <= 100; i++) sum += ease(i / 100d);

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i <= 10000; i++) sum += ease(i / 10000d);
        var bytes = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.True(sum > 0);
        Assert.Equal(0, bytes);
    }

    [Fact]
    public void FlatPathIsOffsetWithoutScaling()
    {
        var flat = Eases.FromPath("M0,0 C0.3,1 0.7,1 1,0");
        var raised = Eases.FromPath("M0,2 C0.3,3 0.7,3 1,2");

        Assert.Equal(.75, flat(.5), 3);
        Assert.Equal(0, flat(1));
        AssertClose(flat, raised, 1e-9);
    }

    [Theory]
    [InlineData("")]
    [InlineData("M0,0")]
    [InlineData("M0,0 L0,1")]
    public void PathWithoutWidthThrows(string path) => Assert.Throws<ArgumentException>(() => Eases.FromPath(path));
}
