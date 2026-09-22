using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.RegularExpressions;
using Monaco.Presto.Core;
using Monaco.Presto.Targets;

namespace Monaco.Presto.Values;

/// <summary>
/// Decomposes raw tween values (numbers, unit strings, relative <c>+=</c>/<c>-=</c>/<c>*=</c> values, colors and complex
/// strings) into a <see cref="DecomposedValue"/>, porting upstream <c>decomposeRawValue</c>.
/// </summary>
public static partial class ValueParser
{
    [GeneratedRegex(@"^([-+]?\d*\.?\d+(?:e[-+]?\d+)?)([a-z]+|%)$", RegexOptions.ECMAScript | RegexOptions.IgnoreCase)]
    private static partial Regex UnitsExec();

    // Upstream matches a single exponent digit; "1e10" splits into "1e1" and "0".
    [GeneratedRegex(@"[-+]?\d*\.?\d+(?:e[-+]?\d)?", RegexOptions.ECMAScript | RegexOptions.IgnoreCase)]
    private static partial Regex DigitWithExponent();

    [GeneratedRegex(@"\)\s*[-.\d]", RegexOptions.ECMAScript)]
    private static partial Regex ValidRgbHsl();

    /// <summary>
    /// Resets <paramref name="into"/> and fills it from <paramref name="raw"/>: null, empty, 0 and false are the number 0;
    /// numeric primitives are numbers; a <see cref="PrestoColor"/> is a color; a <c>double[]</c> is a space-separated complex
    /// value; strings are parsed as upstream does. Any other type falls back to <paramref name="codec"/>, and throws
    /// <see cref="NotSupportedException"/> when there is none.
    /// </summary>
    public static void Decompose(object? raw, DecomposedValue into, IValueCodec? codec = null)
    {
        into.Reset();
        switch (raw)
        {
            case null:
                return;
            case string s:
                DecomposeString(s, into);
                return;
            case double or float or int or long or short or byte or decimal or bool:
                var n = Convert.ToDouble(raw, CultureInfo.InvariantCulture);
                into.Number = double.IsNaN(n) ? 0 : n;
                return;
            case PrestoColor color:
                into.Kind = ValueKind.Color;
                into.Numbers = color.ToChannels();
                return;
            case double[] numbers:
                into.Kind = ValueKind.Complex;
                into.Numbers = Helpers.CloneNumbers(numbers);
                into.Strings = new string[numbers.Length + 1];
                for (var i = 0; i <= numbers.Length; i++) into.Strings[i] = i == 0 || i == numbers.Length ? "" : " ";
                return;
            default:
                if (codec != null && codec.TryDecode(raw, into)) return;
                throw new NotSupportedException($"Cannot decompose a value of type {raw.GetType()}.");
        }
    }

    /// <summary>Copies a tween's end value into <paramref name="into"/>, porting upstream <c>decomposeTweenValue</c>.</summary>
    public static void DecomposeTween(Tween tween, DecomposedValue into)
    {
        into.Kind = tween._valueType;
        into.Number = tween._toNumber;
        into.Unit = tween._unit;
        into.Operator = '\0';
        into.Numbers = Helpers.CloneNumbers(tween._toNumbers);
        into.Strings = (string[])tween._strings.Clone();
    }

    private static void DecomposeString(string str, DecomposedValue into)
    {
        var num = JsNumber.ToNumber(str);
        if (!double.IsNaN(num))
        {
            into.Number = num;
            return;
        }
        if (str.Length > 1 && str[1] == '=')
        {
            into.Operator = str[0];
            str = str[2..];
        }
        if (!str.Contains(' ') && TryParseUnit(str, out var number, out var unit))
        {
            into.Kind = ValueKind.Unit;
            into.Number = number;
            into.Unit = unit;
        }
        else if (into.Operator != '\0')
        {
            into.Number = JsNumber.ToNumber(str);
        }
        else if (IsColor(str))
        {
            into.Kind = ValueKind.Color;
            into.Numbers = PrestoColor.Parse(str).ToChannels();
        }
        else
        {
            var matches = DigitWithExponent().Matches(str);
            var numbers = new double[matches.Count];
            for (var i = 0; i < numbers.Length; i++) numbers[i] = JsNumber.ToNumber(matches[i].ValueSpan);
            into.Kind = ValueKind.Complex;
            into.Numbers = numbers;
            into.Strings = DigitWithExponent().Split(str);
        }
    }

    /// <summary>Matches upstream <c>unitsExecRgx</c>: a number immediately followed by letters or <c>%</c>.</summary>
    public static bool TryParseUnit(string s, out double number, [NotNullWhen(true)] out string? unit)
    {
        var m = UnitsExec().Match(s);
        if (!m.Success)
        {
            number = 0;
            unit = null;
            return false;
        }
        number = JsNumber.ToNumber(m.Groups[1].ValueSpan);
        unit = m.Groups[2].Value;
        return true;
    }

    public static double GetRelativeValue(double x, double y, char op) =>
        op == '-' ? x - y :
        op == '+' ? x + y :
        x * y;

    /// <summary>Upstream <c>isCol</c>: a hex color, or an <c>rgb</c>/<c>hsl</c> string that is not followed by more values.</summary>
    public static bool IsColor(string s) =>
        PrestoColor.IsHex(s) ||
        ((s.StartsWith("rgb", StringComparison.Ordinal) || s.StartsWith("hsl", StringComparison.Ordinal)) &&
         (s[^1] == ')' || !ValidRgbHsl().IsMatch(s)));
}
