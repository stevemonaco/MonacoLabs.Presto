using System.Globalization;
using Monaco.Presto.Values;

namespace Monaco.Presto.Tests;

/// <summary>
/// Port of anime.js tests/suites/units.test.js: the conversions at parser level, then the unit inheritance an animation
/// derives from a target's original values. The DOM-measured conversions have no core equivalent.
/// </summary>
public sealed class UnitsTests : EngineTestBase
{
    private static readonly string OneTurnInRadians = (Math.PI * 2).ToString(CultureInfo.InvariantCulture) + "rad";
    private static readonly string HalfTurnInRadians = Math.PI.ToString(CultureInfo.InvariantCulture) + "rad";

    public static TheoryData<string> ValidUnits => new(
        "cm", "mm", "in", "pc", "pt", "px", "em", "ex", "ch", "rem", "vw", "vh",
        "vmin", "vmax", "q", "lh", "rlh", "vb", "vi", "svw", "svh", "lvw", "lvh", "dvw", "dvh");

    public static TheoryData<string, string, double, double> AngleConversions => new()
    {
        { "360", ".5turn", 1, .5 },
        { "360deg", ".5turn", 1, .5 },
        { OneTurnInRadians, ".5turn", 1, .5 },
        { "360", HalfTurnInRadians, Math.PI * 2, Math.PI },
        { "360deg", HalfTurnInRadians, Math.PI * 2, Math.PI },
        { "1turn", HalfTurnInRadians, Math.PI * 2, Math.PI },
        { "360", "180deg", 360, 180 },
        { "1turn", "180deg", 360, 180 },
        { OneTurnInRadians, "180deg", 360, 180 },
    };

    private static DecomposedValue Convert(object raw, string unit)
    {
        var value = new DecomposedValue();
        ValueParser.Decompose(raw, value);
        UnitConverter.Convert(value, unit);
        return value;
    }

    [Theory]
    [InlineData("360deg", "turn", 1)]
    [InlineData("1turn", "deg", 360)]
    [InlineData("360deg", "rad", Math.PI * 2)]
    [InlineData("1turn", "rad", Math.PI * 2)]
    public void ConvertsBetweenAngleUnits(string raw, string unit, double expected)
    {
        var value = Convert(raw, unit);
        Assert.Equal(ValueKind.Unit, value.Kind);
        Assert.Equal(unit, value.Unit);
        Assert.Equal(expected, value.Number);
    }

    [Fact]
    public void RadToTurn()
    {
        var value = Convert((Math.PI * 2).ToString(CultureInfo.InvariantCulture) + "rad", "turn");
        Assert.Equal("turn", value.Unit);
        Assert.Equal(1, value.Number);
    }

    [Fact]
    public void PlainNumberAdoptsTargetUnit()
    {
        var value = Convert(360, "deg");
        Assert.Equal(ValueKind.Unit, value.Kind);
        Assert.Equal("deg", value.Unit);
        Assert.Equal(360, value.Number);
    }

    [Fact]
    public void SameUnitIsUnchanged()
    {
        var value = Convert("0.5turn", "turn");
        Assert.Equal("turn", value.Unit);
        Assert.Equal(.5, value.Number);
    }

    [Fact]
    public void NonAngleUnitsAdoptTargetUnitOneToOne()
    {
        var value = Convert("10px", "em");
        Assert.Equal(ValueKind.Unit, value.Kind);
        Assert.Equal("em", value.Unit);
        Assert.Equal(10, value.Number);
    }

    [Fact]
    public void SpecifiedUnitOnASimpleTween()
    {
        var animation = Presto.Animate(new StyleTarget(), new() { ["TranslateX"] = "100%", Duration = 10, Autoplay = false });

        Assert.Equal("%", TestHelpers.GetTweenAtIndex(animation, 0)!._unit);
    }

    [Fact]
    public void UnitsInheritanceOnFromToValues()
    {
        var animation = Presto.Animate(new StyleTarget(), new() { ["TranslateX"] = TweenValue.FromTo(-50, "50%"), Duration = 10, Autoplay = false });

        Assert.Equal("%", TestHelpers.GetTweenAtIndex(animation, 0)!._unit);
    }

    [Theory]
    [MemberData(nameof(ValidUnits))]
    public void ShouldMatchAnyUnitsFromOriginalValues(string unit)
    {
        var target = new Dictionary<string, object?> { ["Width"] = "99" + unit };
        var animation = Presto.Animate(target, new() { ["Width"] = 999, Duration = 10, Autoplay = false });

        Assert.Equal(unit, TestHelpers.GetTweenAtIndex(animation, 0)!._unit);
    }

    [Theory]
    [MemberData(nameof(ValidUnits))]
    public void ShouldMatchAnyUnitsSetInThePropertyValue(string unit)
    {
        var target = new Dictionary<string, object?> { ["Width"] = "99px" };
        var animation = Presto.Animate(target, new() { ["Width"] = "999" + unit, Duration = 10, Autoplay = false });

        Assert.Equal(unit, TestHelpers.GetTweenAtIndex(animation, 0)!._unit);
    }

    [Theory]
    [MemberData(nameof(ValidUnits))]
    public void ValuesSetWithUnitsShouldBeProperlyApplied(string unit)
    {
        var target = new Dictionary<string, object?>();

        Presto.Set(target, new()
        {
            ["Width"] = ".9" + unit,
            ["Left"] = "-.099" + unit,
            ["Top"] = "-1E37" + unit,
            ["Right"] = "+1e38" + unit,
            ["Bottom"] = "+0.099" + unit,
        });

        Assert.Equal("0.9" + unit, target["Width"]);
        Assert.Equal("-0.099" + unit, target["Left"]);
        Assert.Equal("-1e+37" + unit, target["Top"]);
        Assert.Equal("1e+38" + unit, target["Right"]);
        Assert.Equal("0.099" + unit, target["Bottom"]);
    }

    [Theory]
    [MemberData(nameof(ValidUnits))]
    public void ShouldMatchAnyUnitsFromComplexOriginalValues(string unit)
    {
        var target = new Dictionary<string, object?>();
        Presto.Set(target, new()
        {
            ["Width"] = ".9" + unit,
            ["Left"] = "-.099" + unit,
            ["Top"] = "-1E37" + unit,
            ["Right"] = "+1e38" + unit,
            ["Bottom"] = "+0.099" + unit,
        });

        var animation = Presto.Animate(target, new()
        {
            ["Width"] = .99,
            ["Left"] = -.0999,
            ["Top"] = double.NegativeInfinity,
            ["Right"] = double.PositiveInfinity,
            ["Bottom"] = .0999,
            Duration = 10,
            Autoplay = false,
        });

        double[] expected = [.99, -.0999, double.NegativeInfinity, double.PositiveInfinity, .0999];
        for (var i = 0; i < expected.Length; i++)
        {
            var tween = TestHelpers.GetTweenAtIndex(animation, i)!;
            Assert.Equal(unit, tween._unit);
            Assert.Equal(expected[i], tween._toNumber);
        }
    }

    [Theory]
    [MemberData(nameof(AngleConversions))]
    public void ConvertsAngleUnitsAgainstTheOriginalValue(string from, string to, double expectedFrom, double expectedTo)
    {
        var animation = Presto.Animate(new StyleTarget(), new() { ["Rotate"] = TweenValue.FromTo(from, to), Autoplay = false });

        var tween = TestHelpers.GetTweenAtIndex(animation, 0)!;
        Assert.Equal(expectedFrom, tween._fromNumber);
        Assert.Equal(expectedTo, tween._toNumber);
    }
}
