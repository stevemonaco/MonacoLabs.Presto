using Avalonia;
using PrestoGallery.Showcase.Pages;

namespace PrestoGallery.Tests;

public sealed class MetaballTests
{
    private const double Reach = 30;

    [Fact]
    public void ANeckJoinsCirclesWithinReachAndNoneBeyondIt()
    {
        Assert.NotNull(Metaball.Neck(new(0, 0), 20, new(30, 0), 15, Reach));
        Assert.NotNull(Metaball.Neck(new(0, 0), 20, new(60, 0), 15, Reach));
        Assert.Null(Metaball.Neck(new(0, 0), 20, new(66, 0), 15, Reach));
    }

    [Fact]
    public void TheNeckIsSymmetricForEqualRadii()
    {
        var neck = Metaball.Neck(new(0, 0), 20, new(50, 0), 20, Reach)!;

        Assert.Equal(50 - neck[0].X, neck[3].X, 6);
        Assert.Equal(neck[0].Y, neck[3].Y, 6);
        Assert.Equal(neck[0].Y, -neck[7].Y, 6);
        Assert.Equal(neck[3].Y, -neck[4].Y, 6);
    }

    [Fact]
    public void TheNecksEndpointsLieOnTheTwoCircles()
    {
        Point c1 = new(10, 20), c2 = new(45, 60);
        var neck = Metaball.Neck(c1, 25, c2, 18, Reach)!;

        Assert.Equal(25, Point.Distance(neck[0], c1), 6);
        Assert.Equal(25, Point.Distance(neck[7], c1), 6);
        Assert.Equal(18, Point.Distance(neck[3], c2), 6);
        Assert.Equal(18, Point.Distance(neck[4], c2), 6);
    }
}
