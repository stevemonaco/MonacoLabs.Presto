using MonacoLabs.Presto.Core;
using MonacoLabs.Presto.Values;

namespace MonacoLabs.Presto.Tests;

/// <summary>
/// Port of anime.js tests/suites/values.test.js: the parser-level cases, plus the animate-driven cases.
/// </summary>
public sealed class ValuesTests : EngineTestBase
{
    public static TheoryData<string> ShouldNotMatch => new()
    {
        "range", "range1", "range13.134", "10 10", "10px 10px", "10.1px 10.2px", "1.12E0px 1.12E0px",
        "1", "1234", ".1234", "0.1234", "1234.1234",
        "+1234.1234", "+.1234", "-1234.1234", "-.1234",
        "1e+100", "1e-100", "1234e+100", "1234e-100",
        ".1234e+100", ".1234e-100", "1234.1234e+100", "1234.1234e-100",
        "-1234.1234e+100", "+1234.1234e-100", "0.1234e+100", "0.1234e-100",
        "-.1234e+100", "+.1234e-100",
    };

    public static TheoryData<string> ShouldMatch => new()
    {
        "1px", "1em", "1e", "1E", "1e+100px", "1e-100em", "1e+100e", "1e-100E", "1E+100e", "1E-100E",
        "1234px", "1234em", "1234e", "1234E", "1234e+100px", "1234e-100em", "1234e+100e", "1234e-100E", "1234E+100e", "1234E-100E",
        ".1234px", ".1234em", ".1234e", ".1234E", ".1234e+100px", ".1234e-100em", ".1234e+100e", ".1234e-100E", ".1234E+100e", ".1234E-100E",
        "0.1234px", "0.1234em", "0.1234e", "0.1234E", "0.1234e+100px", "0.1234e-100em", "0.1234e+100e", "0.1234e-100E", "0.1234E+100e", "0.1234E-100E",
        "1234.1234px", "1234.1234em", "1234.1234e", "1234.1234E", "1234.1234e+100px", "1234.1234e-100em", "1234.1234e+100e", "1234.1234e-100E", "1234.1234E+100e", "1234.1234E-100E",
        "-1234.1234px", "+1234.1234em", "-1234.1234e", "+1234.1234E", "-1234.1234e+100px", "+1234.1234e-100em", "-1234.1234e+100e", "+1234.1234e-100E", "-1234.1234E+100e", "+1234.1234E-100E",
        "-.1234px", "+.1234em", "-.1234e", "+.1234E", "-.1234e+100px", "+.1234e-100em", "-.1234e+100e", "+.1234e-100E", "-.1234E+100e", "+.1234E-100E",
    };

    private static DecomposedValue Decompose(object? raw)
    {
        var value = new DecomposedValue();
        ValueParser.Decompose(raw, value);
        return value;
    }

    [Theory]
    [MemberData(nameof(ShouldNotMatch))]
    public void UnitParsingShouldNotMatch(string value) => Assert.False(ValueParser.TryParseUnit(value, out _, out _));

    [Theory]
    [MemberData(nameof(ShouldMatch))]
    public void UnitParsingShouldMatch(string value) => Assert.True(ValueParser.TryParseUnit(value, out _, out _));

    [Theory]
    [InlineData(42, 42)]
    [InlineData("42", 42)]
    [InlineData(1.23456e-5, 1.23456e-5)]
    [InlineData("1.23456e-5", 1.23456e-5)]
    [InlineData(1.10E+10, 1.10E+10)]
    [InlineData("-2.5e+100", -2.5e+100)]
    public void NumberTypeValuesFromNumbersAndStrings(object raw, double expected)
    {
        var value = Decompose(raw);
        Assert.Equal(ValueKind.Number, value.Kind);
        Assert.Equal(expected, value.Number);
        Assert.Null(value.Unit);
        Assert.Equal('\0', value.Operator);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(0)]
    [InlineData(false)]
    public void FalsyValuesDecomposeToZero(object? raw)
    {
        var value = Decompose(raw);
        Assert.Equal(ValueKind.Number, value.Kind);
        Assert.Equal(0, value.Number);
    }

    [Theory]
    [InlineData("1e-100px", 1e-100, "px")]
    [InlineData("42px", 42, "px")]
    [InlineData("+1.5e-10em", 1.5e-10, "em")]
    [InlineData("-2.5E+100em", -2.5e+100, "em")]
    [InlineData("100%", 100, "%")]
    public void UnitTypeValues(string raw, double number, string unit)
    {
        var value = Decompose(raw);
        Assert.Equal(ValueKind.Unit, value.Kind);
        Assert.Equal(number, value.Number);
        Assert.Equal(unit, value.Unit);
    }

    [Theory]
    [InlineData("+=42", '+', ValueKind.Number, 42, null)]
    [InlineData("*=2.5", '*', ValueKind.Number, 2.5, null)]
    [InlineData("-=20px", '-', ValueKind.Unit, 20, "px")]
    public void RelativeValueOperators(string raw, char op, ValueKind kind, double number, string? unit)
    {
        var value = Decompose(raw);
        Assert.Equal(kind, value.Kind);
        Assert.Equal(op, value.Operator);
        Assert.Equal(number, value.Number);
        Assert.Equal(unit, value.Unit);
    }

    [Fact]
    public void ComplexWhiteSpaceValues()
    {
        var value = Decompose("42 42 42 42");
        Assert.Equal(ValueKind.Complex, value.Kind);
        Assert.Equal(new double[] { 42, 42, 42, 42 }, value.Numbers);
        Assert.Equal(new[] { "", " ", " ", " ", "" }, value.Strings);
    }

    [Fact]
    public void ComplexMixedTypeValues()
    {
        var value = Decompose("auto 42px auto 42rem");
        Assert.Equal(ValueKind.Complex, value.Kind);
        Assert.Equal(new double[] { 42, 42 }, value.Numbers);
        Assert.Equal(new[] { "auto ", "px auto ", "rem" }, value.Strings);
    }

    [Fact]
    public void ComplexCssFilterValues()
    {
        var value = Decompose("blur(42px) contrast(42)");
        Assert.Equal(ValueKind.Complex, value.Kind);
        Assert.Equal(new double[] { 42, 42 }, value.Numbers);
        Assert.Equal(new[] { "blur(", "px) contrast(", ")" }, value.Strings);
    }

    [Fact]
    public void ComplexValueWithoutNumbersKeepsTheWholeString()
    {
        var value = Decompose("auto none");
        Assert.Equal(ValueKind.Complex, value.Kind);
        Assert.Empty(value.Numbers!);
        Assert.Equal(new[] { "auto none" }, value.Strings);
    }

    [Fact]
    public void ComplexExponentsKeepUpstreamSingleDigitQuirk()
    {
        var value = Decompose("1e10 2");
        Assert.Equal(new double[] { 10, 0, 2 }, value.Numbers);
        Assert.Equal(new[] { "", "", " ", "" }, value.Strings);
    }

    [Fact]
    public void DoubleArrayDecomposesToSpaceSeparatedComplex()
    {
        double[] source = [1, 2, 3];
        var value = Decompose(source);
        Assert.Equal(ValueKind.Complex, value.Kind);
        Assert.Equal(source, value.Numbers);
        Assert.NotSame(source, value.Numbers);
        Assert.Equal(new[] { "", " ", " ", "" }, value.Strings);
    }

    [Fact]
    public void PrestoColorDecomposesToColor()
    {
        var value = Decompose(new PrestoColor(1, 2, 3, .5));
        Assert.Equal(ValueKind.Color, value.Kind);
        Assert.Equal(new double[] { 1, 2, 3, .5 }, value.Numbers);
    }

    [Fact]
    public void UnsupportedTypesThrow() => Assert.Throws<NotSupportedException>(() => Decompose(new object()));

    [Fact]
    public void DecomposeResetsPreviousState()
    {
        var value = Decompose("-=20px");
        ValueParser.Decompose(42, value);
        Assert.Equal(ValueKind.Number, value.Kind);
        Assert.Equal(42, value.Number);
        Assert.Null(value.Unit);
        Assert.Equal('\0', value.Operator);
        Assert.Null(value.Numbers);
        Assert.Null(value.Strings);
    }

    [Theory]
    [InlineData('-', 5)]
    [InlineData('+', 15)]
    [InlineData('*', 50)]
    public void GetRelativeValueAppliesOperator(char op, double expected) =>
        Assert.Equal(expected, ValueParser.GetRelativeValue(10, 5, op));

    [Theory]
    [InlineData(1E+38, "1e+38")]
    [InlineData(-1E37, "-1e+37")]
    [InlineData(2E-100, "2e-100")]
    [InlineData(1e-7, "1e-7")]
    [InlineData(0.099, "0.099")]
    [InlineData(-0.099, "-0.099")]
    [InlineData(42, "42")]
    [InlineData(0.1 + 0.2, "0.30000000000000004")]
    [InlineData(double.NaN, "NaN")]
    [InlineData(double.PositiveInfinity, "Infinity")]
    [InlineData(double.NegativeInfinity, "-Infinity")]
    public void JsNumberFormatMatchesJavaScript(double value, string expected) => Assert.Equal(expected, JsNumber.Format(value));

    [Fact]
    public void JsNumberFormatsNegativeZeroAsZero() => Assert.Equal("0", JsNumber.Format(double.NegativeZero));

    [Fact]
    public void LerpReturnsEndpointsExactly()
    {
        Assert.Equal(0.1, Helpers.Lerp(0.1, 0.3, 0));
        Assert.Equal(0.3, Helpers.Lerp(0.1, 0.3, 1));
        Assert.Equal(5, Helpers.Lerp(0, 10, .5));
    }

    [Theory]
    [InlineData(42d)]
    [InlineData("42")]
    public void NumberTypeTweenValuesFromNumbersAndStrings(object raw)
    {
        var value = TweenValue.Of(raw);
        var target = new Dictionary<string, object?>
        {
            ["Number"] = 1d,
            ["Decimals"] = 1.2,
            ["Exponent"] = 1.23456e-5,
            ["Func"] = 1337d,
            ["NumberString"] = "1",
            ["DecimalsString"] = "1.2",
            ["ExponentString"] = "1.23456e-5",
            ["FuncString"] = "1337",
        };

        var animation = Presto.Animate(target, new()
        {
            ["Number"] = value,
            ["Decimals"] = value,
            ["Exponent"] = value,
            ["Func"] = new FunctionValue((_, _, _) => value),
            ["NumberString"] = value,
            ["DecimalsString"] = value,
            ["ExponentString"] = value,
            ["FuncString"] = new FunctionValue((_, _, _) => value),
            Autoplay = false,
        });

        Assert.Equal(8, TestHelpers.GetTweenCount(animation));
        for (var i = 0; i < 8; i++) Assert.Equal(ValueKind.Number, TestHelpers.GetTweenAtIndex(animation, i)!._valueType);
    }

    [Fact]
    public void UnitTypeTweenValues()
    {
        var target = new Dictionary<string, object?>
        {
            ["Number"] = 1d,
            ["Decimals"] = 1.2,
            ["Exponent"] = 1.23456e-5,
            ["Func"] = 1337d,
            ["NumberUnit"] = "1px",
            ["DecimalsUnit"] = "1.2px",
            ["ExponentUnit"] = "1.23456e-5px",
            ["FuncUnit"] = "1337px",
        };

        var animation = Presto.Animate(target, new()
        {
            ["Number"] = "42px",
            ["Decimals"] = "42px",
            ["Exponent"] = "42px",
            ["Func"] = new FunctionValue((_, _, _) => "42px"),
            ["NumberUnit"] = 42,
            ["DecimalsUnit"] = 42,
            ["ExponentUnit"] = 42,
            ["FuncUnit"] = new FunctionValue((_, _, _) => 42),
            Autoplay = false,
        });

        Assert.Equal(8, TestHelpers.GetTweenCount(animation));
        for (var i = 0; i < 8; i++)
        {
            var tween = TestHelpers.GetTweenAtIndex(animation, i)!;
            Assert.Equal(ValueKind.Unit, tween._valueType);
            Assert.Equal(42, tween._toNumber);
            Assert.Equal("px", tween._unit);
        }
    }

    [Fact]
    public void ColorTypeValues()
    {
        var target = new Dictionary<string, object?>
        {
            ["HEX3"] = "#f99",
            ["HEX6"] = "#ff9999",
            ["RGB"] = "rgb(255, 153, 153)",
            ["HSL"] = "hsl(0, 100%, 80%)",
            ["HEX3A"] = "#f999",
            ["HEX6A"] = "#ff999999",
            ["RGBA"] = "rgba(255, 153, 153, .6)",
            ["HSLA"] = "hsla(0, 100%, 80%, .6)",
            ["Func"] = "#f99",
        };

        const string to = "hsla(180, 100%, 50%, .8)";
        var animation = Presto.Animate(target, new()
        {
            ["HEX3"] = to,
            ["HEX6"] = to,
            ["RGB"] = to,
            ["HSL"] = to,
            ["HEX3A"] = to,
            ["HEX6A"] = to,
            ["RGBA"] = to,
            ["HSLA"] = to,
            ["Func"] = new FunctionValue((_, _, _) => to),
            Autoplay = false,
        });

        Assert.Equal(9, TestHelpers.GetTweenCount(animation));
        for (var i = 0; i < 9; i++)
        {
            var tween = TestHelpers.GetTweenAtIndex(animation, i)!;
            Assert.Equal(ValueKind.Color, tween._valueType);
            Assert.Equal([0, 255, 255, .8], tween._toNumbers);
        }
    }

    [Fact]
    public void ComplexTypeValues()
    {
        var target = new Dictionary<string, object?>
        {
            ["WhiteSpace"] = "0 1 2 1.234",
            ["MixedTypes"] = "auto 20px auto 2rem",
            ["CssFilter"] = "blur(100px) contrast(200)",
            ["Func"] = "blur(100px) contrast(200)",
            ["WhiteSpaceFromNumber"] = 10d,
            ["MixedTypesFromNumber"] = 10d,
            ["CssFilterFromNumber"] = 10d,
            ["FuncFromNumber"] = 10d,
        };

        var animation = Presto.Animate(target, new()
        {
            ["WhiteSpace"] = "42 42 42 42",
            ["MixedTypes"] = "auto 42px auto 42rem",
            ["CssFilter"] = "blur(42px) contrast(42)",
            ["Func"] = new FunctionValue((_, _, _) => "blur(42px) contrast(42)"),
            ["WhiteSpaceFromNumber"] = "42 42 42 42",
            ["MixedTypesFromNumber"] = "auto 42px auto 42rem",
            ["CssFilterFromNumber"] = "blur(42px) contrast(42)",
            ["FuncFromNumber"] = new FunctionValue((_, _, _) => "blur(42px) contrast(42)"),
            Autoplay = false,
        });

        Assert.Equal(8, TestHelpers.GetTweenCount(animation));
        for (var i = 0; i < 8; i++)
        {
            var tween = TestHelpers.GetTweenAtIndex(animation, i)!;
            Assert.Equal(ValueKind.Complex, tween._valueType);
            Assert.Equal(tween._toNumbers.Length == 4 ? [42, 42, 42, 42] : new double[] { 42, 42 }, tween._toNumbers);
        }
    }

    [Fact]
    public void ValuesWithWhiteSpace()
    {
        var target = new Dictionary<string, object?>();
        var animation = Presto.Animate(target, new()
        {
            ["BackgroundSize"] = TweenValue.FromTo("auto 100%", "auto 200%"),
            Duration = 100,
            Ease = "linear",
            Autoplay = false,
        });

        var tween = TestHelpers.GetTweenAtIndex(animation, 0)!;
        Assert.Equal(ValueKind.Complex, tween._valueType);
        Assert.Equal(100, tween._fromNumbers[0]);
        Assert.Equal("auto ", tween._strings[0]);
        Assert.Equal("%", tween._strings[1]);
        Assert.Equal(200, tween._toNumbers[0]);

        Assert.Equal("auto 100%", target["BackgroundSize"]);
        animation.Pause().Seek(animation.Duration);
        Assert.Equal("auto 200%", target["BackgroundSize"]);
    }

    [Fact]
    public void NonNumericStringsStayComplexAndWriteTheEndStringFromTheFirstFrame()
    {
        var target = new TextTarget { Text = "left" };
        var animation = Presto.Animate(target, new() { ["Text"] = "right", Duration = 100, Ease = "linear", Autoplay = false });

        var tween = TestHelpers.GetTweenAtIndex(animation, 0)!;
        Assert.Null(tween.Accessor.Codec);
        Assert.Equal(ValueKind.Complex, tween._valueType);
        Assert.Empty(tween._toNumbers);
        animation.Seek(1);
        Assert.Equal("right", target.Text);
        animation.Seek(100);
        Assert.Equal("right", target.Text);
    }

    [Fact]
    public void NumberTypeValuesFromRelativeValuesOperators()
    {
        var target = new Dictionary<string, object?>
        {
            ["Number"] = 1d,
            ["Decimals"] = 1.2,
            ["Exponent"] = 1.23456e-5,
            ["Func"] = 1337d,
            ["NumberString"] = "1",
            ["DecimalsString"] = "1.2",
            ["ExponentString"] = "1.23456e-5",
            ["FuncString"] = "1337",
        };
        var expected = new Dictionary<string, double>();
        foreach (var (property, value) in target) expected[property] = (value is string text ? JsNumber.ToNumber(text) : (double)value!) + 42;

        var animation = Presto.Animate(target, new()
        {
            ["Number"] = "+=42",
            ["Decimals"] = "+=42",
            ["Exponent"] = "+=42",
            ["Func"] = new FunctionValue((_, _, _) => "+=42"),
            ["NumberString"] = "+=42",
            ["DecimalsString"] = "+=42",
            ["ExponentString"] = "+=42",
            ["FuncString"] = new FunctionValue((_, _, _) => "+=42"),
            Duration = 10,
        });

        RunUntil(() => animation.Completed);

        Assert.True(animation.Completed);
        foreach (var (property, value) in expected) Assert.Equal(value, target[property]);
    }

    [Fact]
    public void TweenEndValueTypes()
    {
        var to = new Dictionary<string, object?>
        {
            ["Number"] = 2d,
            ["Decimals"] = 2.2,
            ["Exponent"] = 2.10E+10,
            ["Exponent2"] = 2.5e-10,
            ["NumberUnit"] = "2px",
            ["DecimalsUnit"] = "2.2px",
            ["ExponentUnit"] = "2e-100px",
            ["ExponentUnit2"] = "2.5e-10em",
            ["Prefix1"] = "2.5e-10em",
            ["Prefix2"] = "-2.5e+100em",
        };
        var target = new Dictionary<string, object?>
        {
            ["Number"] = 1d,
            ["Decimals"] = 1.2,
            ["Exponent"] = 1.10E+10,
            ["Exponent2"] = 1.5e-10,
            ["NumberUnit"] = "1px",
            ["DecimalsUnit"] = "1.2px",
            ["ExponentUnit"] = "1e-100px",
            ["ExponentUnit2"] = "1.5E-10em",
            ["Prefix1"] = "+1.5e-10em",
            ["Prefix2"] = "-1.5E+100em",
        };

        var animation = Presto.Animate(target, new()
        {
            ["Number"] = TweenValue.Of(to["Number"]),
            ["Decimals"] = TweenValue.Of(to["Decimals"]),
            ["Exponent"] = TweenValue.Of(to["Exponent"]),
            ["Exponent2"] = TweenValue.Of(to["Exponent2"]),
            ["NumberUnit"] = TweenValue.Of(to["NumberUnit"]),
            ["DecimalsUnit"] = TweenValue.Of(to["DecimalsUnit"]),
            ["ExponentUnit"] = TweenValue.Of(to["ExponentUnit"]),
            ["ExponentUnit2"] = TweenValue.Of(to["ExponentUnit2"]),
            ["Prefix1"] = TweenValue.Of(to["Prefix1"]),
            ["Prefix2"] = TweenValue.Of(to["Prefix2"]),
            Duration = 10,
        });

        RunUntil(() => animation.Completed);

        Assert.True(animation.Completed);
        foreach (var (property, value) in to) Assert.Equal(value, target[property]);
    }

    [Fact]
    public void ComplexCssValues()
    {
        var target = new Dictionary<string, object?> { ["Filter"] = "blur(0px) contrast(0)", ["ZIndex"] = 0d };
        var animation = Presto.Animate(target, new()
        {
            ["Filter"] = "blur(10px) contrast(200)",
            ["ZIndex"] = new TweenParams { To = 10, Modifier = v => Math.Round(v) },
            Duration = 100,
            Ease = "linear",
            Autoplay = false,
        });

        Assert.Equal("blur(0px) contrast(0)", target["Filter"]);
        Assert.Equal(0d, target["ZIndex"]);

        animation.Pause().Seek(animation.Duration);

        Assert.Equal("blur(10px) contrast(200)", target["Filter"]);
        Assert.Equal(10d, target["ZIndex"]);
        Assert.Equal([10, 200], TestHelpers.GetTweenAtIndex(animation, 0)!._toNumbers);
    }

    [Theory]
    [InlineData(50d, null, 50d, 100d)]
    [InlineData(50d, 150d, 50d, 150d)]
    [InlineData(50d, 0d, 50d, 0d)]
    public void FromAndFromToValues(double from, double? to, double expectedStart, double expectedEnd)
    {
        var target = new Dictionary<string, object?> { ["TranslateX"] = 100d };
        var animation = Presto.Animate(target, new()
        {
            ["TranslateX"] = new TweenParams { From = from, To = to is { } value ? (TweenValue?)value : null },
            Duration = 100,
            Ease = "linear",
            Autoplay = false,
        });

        Assert.Equal(expectedStart, target["TranslateX"]);
        animation.Pause().Seek(animation.Duration);
        Assert.Equal(expectedEnd, target["TranslateX"]);
    }

    [Fact]
    public void FromToValuesShorthand()
    {
        var target = new Dictionary<string, object?> { ["TranslateX"] = 100d };
        var animation = Presto.Animate(target, new()
        {
            ["TranslateX"] = TweenValue.FromTo(50, 150),
            Duration = 100,
            Ease = "linear",
            Autoplay = false,
        });

        Assert.Equal(50d, target["TranslateX"]);
        animation.Pause().Seek(animation.Duration);
        Assert.Equal(150d, target["TranslateX"]);
    }

    [Fact]
    public void RelativeValuesWithOperators()
    {
        var target = new Dictionary<string, object?> { ["TranslateX"] = 100d, ["Width"] = "28px", ["Rotate"] = "0turn" };
        var animation = Presto.Animate(target, new()
        {
            ["TranslateX"] = "*=2.5",
            ["Width"] = "-=20px",
            ["Rotate"] = "+=2turn",
            Duration = 100,
            Ease = "linear",
            Autoplay = false,
        });

        animation.Pause().Seek(0);
        Assert.Equal(100d, target["TranslateX"]);
        Assert.Equal("28px", target["Width"]);
        Assert.Equal("0turn", target["Rotate"]);

        animation.Seek(animation.Duration);
        Assert.Equal(250d, target["TranslateX"]);
        Assert.Equal("8px", target["Width"]);
        Assert.Equal("2turn", target["Rotate"]);
    }

    [Fact]
    public void RelativeFromValues()
    {
        var target = new Dictionary<string, object?> { ["TranslateX"] = 100d, ["Width"] = "28px", ["Rotate"] = "2turn" };
        var animation = Presto.Animate(target, new()
        {
            ["TranslateX"] = new TweenParams { From = "*=2.5" },
            ["Width"] = new TweenParams { From = "-=20px" },
            ["Rotate"] = new TweenParams { From = "+=2turn" },
            Duration = 100,
            Ease = "linear",
            Autoplay = false,
        });

        Assert.Equal(250d, target["TranslateX"]);
        Assert.Equal("8px", target["Width"]);
        Assert.Equal("4turn", target["Rotate"]);

        animation.Pause().Seek(animation.Duration);
        Assert.Equal(100d, target["TranslateX"]);
        Assert.Equal("28px", target["Width"]);
        Assert.Equal("2turn", target["Rotate"]);
    }

    [Fact]
    public void RelativeFromToValues()
    {
        var target = new Dictionary<string, object?> { ["TranslateX"] = 100d, ["Width"] = "28px", ["Rotate"] = "2turn" };
        var animation = Presto.Animate(target, new()
        {
            ["TranslateX"] = TweenValue.FromTo("*=2.5", 10),
            ["Width"] = TweenValue.FromTo(100, "-=20px"),
            ["Rotate"] = TweenValue.FromTo("+=2turn", "-=1turn"),
            Duration = 100,
            Ease = "linear",
            Autoplay = false,
        });

        Assert.Equal(250d, target["TranslateX"]);
        Assert.Equal("100px", target["Width"]);
        Assert.Equal("4turn", target["Rotate"]);

        animation.Pause().Seek(animation.Duration);
        Assert.Equal(10d, target["TranslateX"]);
        Assert.Equal("80px", target["Width"]);
        Assert.Equal("3turn", target["Rotate"]);
    }

    [Fact]
    public void RelativeValuesInsideKeyframes()
    {
        var target = new Dictionary<string, object?> { ["TranslateX"] = 100d, ["Rotate"] = "2turn" };
        var animation = Presto.Animate(target, new()
        {
            ["TranslateX"] = TweenValue.Keyframes(new TweenParams { To = "+=10" }, new TweenParams { To = "-=10" }),
            ["Rotate"] = TweenValue.Keyframes(
                new TweenParams { From = "+=2turn", To = "-=1turn" },
                new TweenParams { From = "+=5turn", To = "-=2turn" }),
            Duration = 100,
            Ease = "linear",
            Autoplay = false,
        });

        Assert.Equal(100d, target["TranslateX"]);
        Assert.Equal("4turn", target["Rotate"]);

        animation.Seek(animation.Duration * .25);
        Assert.Equal(105d, target["TranslateX"]);
        Assert.Equal("3.5turn", target["Rotate"]);

        animation.Seek(animation.Duration * .5);
        Assert.Equal(110d, target["TranslateX"]);
        Assert.Equal("8turn", target["Rotate"]);

        animation.Pause().Seek(animation.Duration);
        Assert.Equal(100d, target["TranslateX"]);
        Assert.Equal("6turn", target["Rotate"]);
    }

    [Fact]
    public void RendersNextKeyframeAtExactBoundaryWhenOffsetHasFloatRoundingDrift()
    {
        var target = new Dictionary<string, object?> { ["Rotate"] = "2turn" };
        var animation = Presto.Animate(target, new()
        {
            ["Rotate"] = TweenValue.Keyframes(
                new TweenParams { From = "+=2turn", To = "-=1turn" },
                new TweenParams { From = "+=5turn", To = "-=2turn" }),
            Duration = 100,
            Ease = "linear",
            Autoplay = false,
        });

        // An engine-timed run can leave an _offset whose rounded value is strictly above the float itself; the
        // boundary check then has to survive absoluteTime landing just under the rounded tween start.
        const double drifted = 0.49999999999999;
        animation._offset = drifted;
        for (var tween = animation._headTween; tween != null; tween = tween._next)
        {
            tween._absoluteStartTime = Helpers.Round(drifted + tween._startTime, 12);
        }

        animation.Seek(animation.Duration * .5);

        Assert.Equal("8turn", target["Rotate"]);
    }
}
