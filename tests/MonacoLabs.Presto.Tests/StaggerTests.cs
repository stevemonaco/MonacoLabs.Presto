namespace MonacoLabs.Presto.Tests;

/// <summary>Port of anime.js tests/suites/stagger.test.js.</summary>
public sealed class StaggerTests : EngineTestBase
{
    private static StyleTarget[] StyleTargets(int count)
    {
        var targets = new StyleTarget[count];
        for (var i = 0; i < count; i++) targets[i] = new StyleTarget();
        return targets;
    }

    private static TestObject[] PlainTargets(int count)
    {
        var targets = new TestObject[count];
        for (var i = 0; i < count; i++) targets[i] = new TestObject();
        return targets;
    }

    private static GridTarget[] GridTargets(double value)
    {
        var targets = new GridTarget[15];
        for (var row = 0; row < 3; row++)
        {
            for (var column = 0; column < 5; column++) targets[(row * 5) + column] = new GridTarget { X = column, Y = row, Val = value };
        }
        return targets;
    }

    private static double TweenDelay(Timer animation, int index) => TestHelpers.GetTweenAtIndex(animation, index)!._delay;

    private static double TweenTo(Timer animation, int index) => TestHelpers.GetTweenAtIndex(animation, index)!._toNumber;

    [Fact]
    public void StaggersDelaysByAFixedStepPerTarget()
    {
        var animation = Presto.Animate(StyleTargets(4), new()
        {
            ["TranslateX"] = 100,
            Duration = 10,
            Delay = Presto.Stagger(10),
            Autoplay = false,
        });

        Assert.Equal(0, TweenDelay(animation, 0));
        Assert.Equal(10, TweenDelay(animation, 1));
        Assert.Equal(20, TweenDelay(animation, 2));
        Assert.Equal(30, TweenDelay(animation, 3));
    }

    /// <summary>A deviation from upstream, which sizes a stagger by the first group it meets and leaves a larger one past its end.</summary>
    [Fact]
    public void AStaggerReusedOnALargerGroupStaggersEveryTarget()
    {
        var stagger = Presto.Stagger(10);
        Presto.Animate(StyleTargets(1), new() { ["TranslateX"] = 100, Duration = 10, Delay = stagger, Autoplay = false });

        var animation = Presto.Animate(StyleTargets(3), new() { ["TranslateX"] = 100, Duration = 10, Delay = stagger, Autoplay = false });

        Assert.Equal(20, TweenDelay(animation, 2));
    }

    [Fact]
    public void StaggersValuesWithAUnitString()
    {
        var targets = StyleTargets(5);
        var animation = Presto.Animate(targets, new() { ["TranslateX"] = Presto.Stagger("1rem"), Duration = 10, Autoplay = false });

        animation.Seek(animation.Duration);

        Assert.Equal("0rem", targets[0].TranslateX);
        Assert.Equal("1rem", targets[1].TranslateX);
        Assert.Equal("2rem", targets[2].TranslateX);
        Assert.Equal("3rem", targets[3].TranslateX);
        Assert.Equal("4rem", targets[4].TranslateX);
    }

    [Fact]
    public void OffsetsStaggerDelaysByTheStartOption()
    {
        var animation = Presto.Animate(StyleTargets(4), new()
        {
            ["TranslateX"] = 100,
            Duration = 10,
            Delay = Presto.Stagger(10, new() { Start = 5 }),
            Autoplay = false,
        });

        // The start offset lands on the animation delay, and the tween delays normalize back to the spacing.
        Assert.Equal(5, animation._delay);
        Assert.Equal(0, TweenDelay(animation, 0));
        Assert.Equal(10, TweenDelay(animation, 1));
        Assert.Equal(20, TweenDelay(animation, 2));
        Assert.Equal(30, TweenDelay(animation, 3));
    }

    [Fact]
    public void DistributesValuesEvenlyAcrossANumericRange()
    {
        var targets = StyleTargets(5);
        var animation = Presto.Animate(targets, new()
        {
            ["TranslateX"] = Presto.Stagger(new double[] { -10, 10 }),
            Duration = 10,
            Autoplay = false,
        });

        animation.Seek(animation.Duration);

        Assert.Equal(-10, TweenTo(animation, 0));
        Assert.Equal(-5, TweenTo(animation, 1));
        Assert.Equal(0, TweenTo(animation, 2));
        Assert.Equal(5, TweenTo(animation, 3));
        Assert.Equal(10, TweenTo(animation, 4));

        Assert.Equal("-10px", targets[0].TranslateX);
        Assert.Equal("-5px", targets[1].TranslateX);
        Assert.Equal("0px", targets[2].TranslateX);
        Assert.Equal("5px", targets[3].TranslateX);
        Assert.Equal("10px", targets[4].TranslateX);
    }

    [Fact]
    public void DistributesValuesAcrossAUnitRange()
    {
        var targets = StyleTargets(5);
        var animation = Presto.Animate(targets, new()
        {
            ["TranslateX"] = Presto.Stagger(new[] { "-10rem", "10rem" }),
            Duration = 10,
            Autoplay = false,
        });

        animation.Seek(animation.Duration);

        Assert.Equal("-10rem", targets[0].TranslateX);
        Assert.Equal("-5rem", targets[1].TranslateX);
        Assert.Equal("0rem", targets[2].TranslateX);
        Assert.Equal("5rem", targets[3].TranslateX);
        Assert.Equal("10rem", targets[4].TranslateX);
    }

    [Fact]
    public void StaggersFromTheCenter()
    {
        var animation = Presto.Animate(StyleTargets(5), new()
        {
            ["TranslateX"] = 10,
            Delay = Presto.Stagger(10, new() { From = StaggerFrom.Center }),
            Autoplay = false,
        });

        Assert.Equal(20, TweenDelay(animation, 0));
        Assert.Equal(10, TweenDelay(animation, 1));
        Assert.Equal(0, TweenDelay(animation, 2));
        Assert.Equal(10, TweenDelay(animation, 3));
        Assert.Equal(20, TweenDelay(animation, 4));
    }

    [Fact]
    public void StaggersFromTheLastElement()
    {
        var animation = Presto.Animate(StyleTargets(5), new()
        {
            ["TranslateX"] = 10,
            Delay = Presto.Stagger(10, new() { From = StaggerFrom.Last }),
            Autoplay = false,
        });

        Assert.Equal(40, TweenDelay(animation, 0));
        Assert.Equal(30, TweenDelay(animation, 1));
        Assert.Equal(20, TweenDelay(animation, 2));
        Assert.Equal(10, TweenDelay(animation, 3));
        Assert.Equal(0, TweenDelay(animation, 4));
    }

    [Fact]
    public void StaggersFromASpecificIndex()
    {
        var animation = Presto.Animate(StyleTargets(5), new()
        {
            ["TranslateX"] = 10,
            Delay = Presto.Stagger(10, new() { From = 1 }),
            Autoplay = false,
        });

        Assert.Equal(10, TweenDelay(animation, 0));
        Assert.Equal(0, TweenDelay(animation, 1));
        Assert.Equal(10, TweenDelay(animation, 2));
        Assert.Equal(20, TweenDelay(animation, 3));
        Assert.Equal(30, TweenDelay(animation, 4));
    }

    [Fact]
    public void ReversesTheStaggerOrder()
    {
        var animation = Presto.Animate(StyleTargets(5), new()
        {
            ["TranslateX"] = 10,
            Delay = Presto.Stagger(10, new() { From = 1, Reversed = true }),
            Autoplay = false,
        });

        Assert.Equal(20, TweenDelay(animation, 0));
        Assert.Equal(30, TweenDelay(animation, 1));
        Assert.Equal(20, TweenDelay(animation, 2));
        Assert.Equal(10, TweenDelay(animation, 3));
        Assert.Equal(0, TweenDelay(animation, 4));
    }

    [Fact]
    public void AppliesAnEaseFunctionToStaggerValues()
    {
        var animation = Presto.Animate(StyleTargets(5), new()
        {
            ["TranslateX"] = 10,
            Delay = Presto.Stagger(10, new() { Ease = "inOutQuad" }),
            Autoplay = false,
        });

        Assert.Equal(0, TweenDelay(animation, 0));
        Assert.Equal(5, TweenDelay(animation, 1));
        Assert.Equal(20, TweenDelay(animation, 2));
        Assert.Equal(35, TweenDelay(animation, 3));
        Assert.Equal(40, TweenDelay(animation, 4));
    }

    [Fact]
    public void ReversesEasedStaggerValues()
    {
        var animation = Presto.Animate(StyleTargets(5), new()
        {
            ["TranslateX"] = 10,
            Delay = Presto.Stagger(10, new() { Ease = "inOutQuad", Reversed = true }),
            Autoplay = false,
        });

        Assert.Equal(40, TweenDelay(animation, 0));
        Assert.Equal(35, TweenDelay(animation, 1));
        Assert.Equal(20, TweenDelay(animation, 2));
        Assert.Equal(5, TweenDelay(animation, 3));
        Assert.Equal(0, TweenDelay(animation, 4));
    }

    [Fact]
    public void ReproducesJitterDelaysForAGivenSeed()
    {
        var first = Presto.Animate(StyleTargets(5), new()
        {
            ["TranslateX"] = 10,
            Delay = Presto.Stagger(10, new() { Jitter = [2], Seed = 42 }),
            Autoplay = false,
        });
        var second = Presto.Animate(StyleTargets(5), new()
        {
            ["TranslateX"] = 10,
            Delay = Presto.Stagger(10, new() { Jitter = [2], Seed = 42 }),
            Autoplay = false,
        });

        for (var i = 0; i < 5; i++) Assert.Equal(TweenDelay(first, i), TweenDelay(second, i));
    }

    [Fact]
    public void RampsJitterMagnitudeAcrossOrder()
    {
        var animation = Presto.Animate(StyleTargets(5), new()
        {
            ["TranslateX"] = 10,
            Delay = Presto.Stagger(10, new() { Jitter = [0, 4], Seed = 0 }),
            Autoplay = false,
        });

        // The first target's progress is 0, so it takes no jitter at all; the last one takes the full magnitude.
        Assert.Equal(0, TweenDelay(animation, 0));
        Assert.InRange(TweenDelay(animation, 4), 36, 44);
    }

    [Fact]
    public void HandlesUseReturningOutOfRangeIndex()
    {
        var animation = Presto.Animate(StyleTargets(5), new()
        {
            ["TranslateX"] = 10,
            Delay = Presto.Stagger(10, new() { Use = (_, i, _) => i + 100 }),
            Autoplay = false,
        });

        // Every target advertises a distinct out of range index, so the delays must still be distinct.
        var delays = new HashSet<double>();
        for (var i = 0; i < 5; i++) delays.Add(TweenDelay(animation, i));
        Assert.Equal(5, delays.Count);
    }

    [Fact]
    public void TotalSpacesARangeOverMoreTargetsThanAreAnimated()
    {
        var animation = Presto.Animate(StyleTargets(3), new()
        {
            ["TranslateX"] = 10,
            Delay = Presto.Stagger(TweenValue.FromTo(0, 100), new() { Total = 5 }),
            Autoplay = false,
        });

        Assert.Equal(0, TweenDelay(animation, 0));
        Assert.Equal(25, TweenDelay(animation, 1));
        Assert.Equal(50, TweenDelay(animation, 2));
    }

    [Fact]
    public void TotalBelowTheTargetCountLeavesTheRemainingTargetsAtTheStartValue()
    {
        var animation = Presto.Animate(StyleTargets(4), new()
        {
            ["TranslateX"] = 10,
            Delay = Presto.Stagger(10, new() { Total = 2 }),
            Autoplay = false,
        });

        Assert.Equal(0, TweenDelay(animation, 0));
        Assert.Equal(10, TweenDelay(animation, 1));
        Assert.Equal(0, TweenDelay(animation, 2));
        Assert.Equal(0, TweenDelay(animation, 3));
    }

    [Fact]
    public void ModifierPostProcessesEveryStaggeredValue()
    {
        var animation = Presto.Animate(StyleTargets(4), new()
        {
            ["TranslateX"] = 10,
            Delay = Presto.Stagger(10, new() { Modifier = v => v * 2 }),
            Autoplay = false,
        });

        Assert.Equal(0, TweenDelay(animation, 0));
        Assert.Equal(20, TweenDelay(animation, 1));
        Assert.Equal(40, TweenDelay(animation, 2));
        Assert.Equal(60, TweenDelay(animation, 3));
    }

    /// <summary>The property name form of <c>use</c>, which the upstream suite only covers as a function.</summary>
    [Fact]
    public void ReadsTheStaggerOrderFromATargetProperty()
    {
        IndexedTarget[] targets = [new() { Index = 3 }, new() { Index = 2 }, new() { Index = 1 }, new() { Index = 0 }];
        var animation = Presto.Animate(targets, new()
        {
            ["TranslateX"] = 10,
            Delay = Presto.Stagger(10, new() { UseProperty = "Index" }),
            Autoplay = false,
        });

        Assert.Equal(30, TweenDelay(animation, 0));
        Assert.Equal(20, TweenDelay(animation, 1));
        Assert.Equal(10, TweenDelay(animation, 2));
        Assert.Equal(0, TweenDelay(animation, 3));
    }

    [Fact]
    public void AppliesStaggerToZeroDurationAnimations()
    {
        var targets = StyleTargets(15);
        foreach (var target in targets) target.Opacity = 1;
        var animation = Presto.Animate(targets, new()
        {
            ["Opacity"] = 0,
            Duration = 0,
            Delay = Presto.Stagger(100),
            Autoplay = false,
        });

        animation.Seek(animation.Duration);

        foreach (var target in targets) Assert.Equal(0, target.Opacity);
    }

    [Fact]
    public void StaggersAcrossA2DGrid()
    {
        var animation = Presto.Animate(StyleTargets(15), new()
        {
            ["Scale"] = TweenValue.FromTo(1, 0),
            Delay = Presto.Stagger(10, new() { Grid = [5, 3], From = StaggerFrom.Center }),
            Autoplay = false,
        });

        AssertCenteredGridDelays(animation);
    }

    [Fact]
    public void StaggersAcrossA2DGridAlongAnAxis()
    {
        var animation = Presto.Animate(StyleTargets(15), new()
        {
            ["TranslateX"] = Presto.Stagger(10, new() { Grid = [5, 3], From = StaggerFrom.Center, Axis = StaggerAxis.X }),
            ["TranslateY"] = Presto.Stagger(10, new() { Grid = [5, 3], From = StaggerFrom.Center, Axis = StaggerAxis.Y }),
            Autoplay = false,
        });

        Assert.Equal(-20, TweenTo(animation, 0));
        Assert.Equal(-10, TweenTo(animation, 2));
        Assert.Equal(0, TweenTo(animation, 4));
        Assert.Equal(10, TweenTo(animation, 6));
        Assert.Equal(20, TweenTo(animation, 8));

        Assert.Equal(-20, TweenTo(animation, 10));
        Assert.Equal(-10, TweenTo(animation, 12));
        Assert.Equal(0, TweenTo(animation, 14));
        Assert.Equal(10, TweenTo(animation, 16));
        Assert.Equal(20, TweenTo(animation, 18));

        Assert.Equal(-20, TweenTo(animation, 20));
        Assert.Equal(-10, TweenTo(animation, 22));
        Assert.Equal(0, TweenTo(animation, 24));
        Assert.Equal(10, TweenTo(animation, 26));
        Assert.Equal(20, TweenTo(animation, 28));

        for (var i = 1; i < 10; i += 2) Assert.Equal(-10, TweenTo(animation, i));
        for (var i = 11; i < 20; i += 2) Assert.Equal(0, TweenTo(animation, i));
        for (var i = 21; i < 30; i += 2) Assert.Equal(10, TweenTo(animation, i));
    }

    [Fact]
    public void MirrorsStaggerOrderWhenReversedAlongAnAxis() => AssertAxisReverseMirrors(StaggerFrom.First);

    [Fact]
    public void MirrorsStaggerOrderWhenReversedAlongAnAxisFromLast() => AssertAxisReverseMirrors(StaggerFrom.Last);

    [Fact]
    public void StaggersFromAnXYOriginInAGrid()
    {
        var animation = Presto.Animate(StyleTargets(15), new()
        {
            ["Scale"] = TweenValue.FromTo(1, 0),
            Delay = Presto.Stagger(10, new() { Grid = [5, 3], From = StaggerFrom.At(.5, .5) }),
            Autoplay = false,
        });

        AssertCenteredGridDelays(animation);
    }

    // The suite's 'Computes auto grid from DOM element positions' case needs laid out elements, so it lives in the bridge's
    // StaggerLayoutTests; the object coordinate cases below cover the same algorithm.

    [Fact]
    public void ComputesAutoGridFromObjectCoordinates()
    {
        var animation = Presto.Animate(GridTargets(1), new()
        {
            ["Val"] = 0,
            Delay = Presto.Stagger(10, new() { AutoGrid = true, From = StaggerFrom.Center }),
            Autoplay = false,
        });

        AssertCenteredGridDelays(animation);
    }

    [Fact]
    public void ComputesAutoGridFromMeasuredCoordinates()
    {
        var targets = PlainTargets(15);

        var animation = Presto.Animate(targets, new()
        {
            ["PlainValue"] = 0,
            Delay = Presto.Stagger(10, new()
            {
                AutoGrid = true,
                From = StaggerFrom.Center,
                Coordinates = target =>
                {
                    var index = Array.IndexOf(targets, target);
                    return [index % 5, index / 5];
                },
            }),
            Autoplay = false,
        });

        AssertCenteredGridDelays(animation);
    }

    [Fact]
    public void ReadsTheXYPropertiesWhenMeasuringCoordinatesReturnsNull()
    {
        var animation = Presto.Animate(GridTargets(1), new()
        {
            ["Val"] = 0,
            Delay = Presto.Stagger(10, new() { AutoGrid = true, From = StaggerFrom.Center, Coordinates = static _ => null }),
            Autoplay = false,
        });

        AssertCenteredGridDelays(animation);
    }

    [Fact]
    public void ComputesAutoGridFromAnXYOrigin()
    {
        var animation = Presto.Animate(GridTargets(1), new()
        {
            ["Val"] = 0,
            Delay = Presto.Stagger(10, new() { AutoGrid = true, From = StaggerFrom.At(1, 1) }),
            Autoplay = false,
        });

        Assert.Equal(0, TweenDelay(animation, 14));
        Assert.Equal(44.7, TweenDelay(animation, 0), 2);
        Assert.Equal(20, TweenDelay(animation, 4));
        Assert.Equal(40, TweenDelay(animation, 10));
    }

    [Fact]
    public void ComputesAutoGridAlongASingleAxis()
    {
        var animation = Presto.Animate(GridTargets(0), new()
        {
            ["Val"] = Presto.Stagger(10, new() { AutoGrid = true, From = StaggerFrom.Center, Axis = StaggerAxis.X }),
            Autoplay = false,
        });

        Assert.Equal(-20, TweenTo(animation, 0));
        Assert.Equal(-10, TweenTo(animation, 1));
        Assert.Equal(0, TweenTo(animation, 2));
        Assert.Equal(10, TweenTo(animation, 3));
        Assert.Equal(20, TweenTo(animation, 4));
        Assert.Equal(-20, TweenTo(animation, 5));
        Assert.Equal(-20, TweenTo(animation, 10));
    }

    [Fact]
    public void FallsBackTo1DAutoGridWithoutSpatialData()
    {
        var animation = Presto.Animate(PlainTargets(4), new()
        {
            ["PlainValue"] = 0,
            Delay = Presto.Stagger(10, new() { AutoGrid = true }),
            Autoplay = false,
        });

        Assert.Equal(0, TweenDelay(animation, 0));
        Assert.Equal(10, TweenDelay(animation, 1));
        Assert.Equal(20, TweenDelay(animation, 2));
        Assert.Equal(30, TweenDelay(animation, 3));
    }

    [Fact]
    public void SwitchesAutoGridTo3DWhenTargetsExposeZ()
    {
        var targets = new CubeTarget[8];
        for (var i = 0; i < 8; i++) targets[i] = new CubeTarget { X = i % 2, Y = (i / 2) % 2, Z = i / 4, Val = 1 };
        var animation = Presto.Animate(targets, new()
        {
            ["Val"] = 0,
            Delay = Presto.Stagger(10, new() { AutoGrid = true, From = StaggerFrom.Center }),
            Autoplay = false,
        });

        // Every corner of a 2x2x2 cube is the same distance from its center.
        var corner = TweenDelay(animation, 0);
        for (var i = 1; i < 8; i++) Assert.Equal(corner, TweenDelay(animation, i), 2);
    }

    [Fact]
    public void ComputesAutoGridAlongTheZAxis()
    {
        var targets = new CubeTarget[12];
        for (var i = 0; i < 12; i++) targets[i] = new CubeTarget { X = i % 2, Y = (i / 2) % 2, Z = i / 4 };
        var animation = Presto.Animate(targets, new()
        {
            ["Val"] = Presto.Stagger(10, new() { AutoGrid = true, From = StaggerFrom.Center, Axis = StaggerAxis.Z }),
            Autoplay = false,
        });

        Assert.Equal(-10, TweenTo(animation, 0));
        Assert.Equal(-10, TweenTo(animation, 3));
        Assert.Equal(0, TweenTo(animation, 4));
        Assert.Equal(0, TweenTo(animation, 7));
        Assert.Equal(10, TweenTo(animation, 8));
        Assert.Equal(10, TweenTo(animation, 11));
    }

    [Fact]
    public void StaggersAcrossA3DGrid()
    {
        var animation = Presto.Animate(PlainTargets(8), new()
        {
            ["PlainValue"] = 0,
            Delay = Presto.Stagger(10, new() { Grid = [2, 2, 2], From = StaggerFrom.Center }),
            Autoplay = false,
        });

        // The center of a 2x2x2 grid is the midpoint between all eight cells.
        var first = TweenDelay(animation, 0);
        for (var i = 1; i < 8; i++) Assert.Equal(first, TweenDelay(animation, i), 2);
    }

    [Fact]
    public void StaggersFromAnXYZOriginInA3DGrid()
    {
        var animation = Presto.Animate(PlainTargets(8), new()
        {
            ["PlainValue"] = 0,
            Delay = Presto.Stagger(10, new() { Grid = [2, 2, 2], From = StaggerFrom.At(0, 0, 0) }),
            Autoplay = false,
        });

        Assert.Equal(0, TweenDelay(animation, 0));
        Assert.Equal(17.32, TweenDelay(animation, 7), 1);
    }

    [Fact]
    public void StaggersTimelinePositions()
    {
        var tl = Presto.CreateTimeline(new() { Defaults = new() { Duration = 10 }, Autoplay = false })
            .Add(StyleTargets(4), new() { Id = "staggered", ["TranslateX"] = 50 }, Presto.Stagger(100));

        AssertStaggeredChildren(tl, 0);
        Assert.Equal(310, tl.Duration);
    }

    [Fact]
    public void StaggersTimelinePositionsWithANumericStart()
    {
        var tl = Presto.CreateTimeline(new() { Defaults = new() { Duration = 10 }, Autoplay = false })
            .Add(StyleTargets(4), new() { Id = "staggered", ["TranslateX"] = 50 }, Presto.Stagger(100, new() { Start = 100 }));

        AssertStaggeredChildren(tl, 100);
        Assert.Equal(410, tl.Duration);
    }

    [Fact]
    public void StaggersTimelinePositionsStartingAtALabel()
    {
        var tl = Presto.CreateTimeline(new() { Defaults = new() { Duration = 10 }, Autoplay = false })
            .Label("LABEL", 100)
            .Add(StyleTargets(4), new() { Id = "staggered", ["TranslateX"] = 50 }, Presto.Stagger(100, new() { Start = "LABEL" }));

        AssertStaggeredChildren(tl, 100);
        Assert.Equal(410, tl.Duration);
    }

    [Fact]
    public void StaggersATweenValueInATimeline()
    {
        var tl = Presto.CreateTimeline(new() { Defaults = new() { Duration = 10 }, Autoplay = false })
            .Add(
                StyleTargets(4),
                new() { Id = "staggered", ["TranslateX"] = Presto.Stagger(100, new() { From = StaggerFrom.Last }) },
                Presto.Stagger(100));

        Assert.Equal(300, TweenTo(TestHelpers.GetChildAtIndex(tl, 0)!, 0));
        Assert.Equal(200, TweenTo(TestHelpers.GetChildAtIndex(tl, 1)!, 0));
        Assert.Equal(100, TweenTo(TestHelpers.GetChildAtIndex(tl, 2)!, 0));
        Assert.Equal(0, TweenTo(TestHelpers.GetChildAtIndex(tl, 3)!, 0));
    }

    [Fact]
    public void AStaggerReusedAsAPositionOnAnotherTimelineUsesThatTimelinesEnd()
    {
        var stagger = Presto.Stagger(100);
        var first = Presto.CreateTimeline(new() { Defaults = new() { Duration = 10 }, Autoplay = false })
            .Add(StyleTargets(4), new() { Id = "staggered", ["TranslateX"] = 50 }, stagger);
        var second = Presto.CreateTimeline(new() { Defaults = new() { Duration = 10 }, Autoplay = false })
            .Add(new StyleTarget(), new() { ["TranslateX"] = 50, Duration = 500 })
            .Add(StyleTargets(4), new() { ["TranslateX"] = 50 }, stagger);

        AssertStaggeredChildren(first, 0);
        for (var i = 0; i < 4; i++) Assert.Equal(500 + (i * 100), TestHelpers.OffsetOf(second, i + 1));
    }

    [Fact]
    public void AStaggerReusedAfterItsTimelineGrowsUsesTheNewEnd()
    {
        var stagger = Presto.Stagger(100);
        var tl = Presto.CreateTimeline(new() { Defaults = new() { Duration = 10 }, Autoplay = false })
            .Add(StyleTargets(4), new() { ["TranslateX"] = 50 }, stagger)
            .Add(StyleTargets(4), new() { ["TranslateX"] = 50 }, stagger);

        for (var i = 0; i < 4; i++) Assert.Equal(310 + (i * 100), TestHelpers.OffsetOf(tl, i + 4));
    }

    private static void AssertAxisReverseMirrors(StaggerFrom from)
    {
        var forward = Presto.Animate(StyleTargets(5), new()
        {
            ["TranslateX"] = 10,
            Delay = Presto.Stagger(10, new() { Grid = [5, 1], Axis = StaggerAxis.X, From = from }),
            Autoplay = false,
        });
        var reverse = Presto.Animate(StyleTargets(5), new()
        {
            ["TranslateX"] = 10,
            Delay = Presto.Stagger(10, new() { Grid = [5, 1], Axis = StaggerAxis.X, From = from, Reversed = true }),
            Autoplay = false,
        });

        for (var i = 0; i < 5; i++) Assert.Equal(TweenDelay(forward, i), TweenDelay(reverse, 4 - i));
    }

    private static void AssertStaggeredChildren(Timeline tl, double start)
    {
        for (var i = 0; i < 4; i++)
        {
            Assert.Equal(start + (i * 100), TestHelpers.OffsetOf(tl, i));
            Assert.Equal($"staggered-{i}", TestHelpers.GetChildAtIndex(tl, i)!.Id);
        }
    }

    private static void AssertCenteredGridDelays(Timer animation)
    {
        double[] expected = [22.4, 14.1, 10, 14.1, 22.4, 20, 10, 0, 10, 20, 22.4, 14.1, 10, 14.1, 22.4];
        for (var i = 0; i < expected.Length; i++) Assert.Equal(expected[i], TweenDelay(animation, i), 2);
    }
}
