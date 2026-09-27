using System.Globalization;
using System.Text.RegularExpressions;
using Monaco.Presto.Core;

namespace Monaco.Presto.Values;

/// <summary>
/// An RGBA color with 0-255 channels and 0-1 alpha (upstream <c>ColorArray</c>), parsed from <c>#rgb</c>, <c>#rgba</c>,
/// <c>#rrggbb</c>, <c>#rrggbbaa</c>, <c>rgb()</c>, <c>rgba()</c>, <c>hsl()</c> and <c>hsla()</c> strings.
/// </summary>
public readonly partial record struct PrestoColor(double R, double G, double B, double A = 1)
{
    [GeneratedRegex(@"(^#([\da-f]{3}){1,2}$)|(^#([\da-f]{4}){1,2}$)", RegexOptions.ECMAScript | RegexOptions.IgnoreCase)]
    private static partial Regex HexTest();

    [GeneratedRegex(@"rgb\(\s*(\d+)\s*,\s*(\d+)\s*,\s*(\d+)\s*\)", RegexOptions.ECMAScript | RegexOptions.IgnoreCase)]
    private static partial Regex RgbExec();

    [GeneratedRegex(@"rgba\(\s*(\d+)\s*,\s*(\d+)\s*,\s*(\d+)\s*,\s*(-?\d+|-?\d*.\d+)\s*\)", RegexOptions.ECMAScript | RegexOptions.IgnoreCase)]
    private static partial Regex RgbaExec();

    [GeneratedRegex(@"hsl\(\s*(-?\d+|-?\d*.\d+)\s*,\s*(-?\d+|-?\d*.\d+)%\s*,\s*(-?\d+|-?\d*.\d+)%\s*\)", RegexOptions.ECMAScript | RegexOptions.IgnoreCase)]
    private static partial Regex HslExec();

    [GeneratedRegex(@"hsla\(\s*(-?\d+|-?\d*.\d+)\s*,\s*(-?\d+|-?\d*.\d+)%\s*,\s*(-?\d+|-?\d*.\d+)%\s*,\s*(-?\d+|-?\d*.\d+)\s*\)", RegexOptions.ECMAScript | RegexOptions.IgnoreCase)]
    private static partial Regex HslaExec();

    internal static bool IsHex(string s) => HexTest().IsMatch(s);

    /// <summary>Parses a color string; anything unrecognised is opaque black, as upstream.</summary>
    public static PrestoColor Parse(string s) => TryParse(s, out var color) ? color : new(0, 0, 0);

    /// <summary>Parses a color string, returning false for anything that is not one of the recognised hex, <c>rgb</c> or <c>hsl</c> forms.</summary>
    public static bool TryParse(string s, out PrestoColor color)
    {
        if (s.StartsWith("rgb", StringComparison.Ordinal)) return TryParseRgb(s, out color);
        if (IsHex(s))
        {
            color = ParseHex(s);
            return true;
        }
        if (s.StartsWith("hsl", StringComparison.Ordinal)) return TryParseHsl(s, out color);
        color = default;
        return false;
    }

    /// <summary>Rebuilds a color from the four channels a color tween interpolates.</summary>
    public static PrestoColor FromChannels(ReadOnlySpan<double> channels) => new(channels[0], channels[1], channels[2], channels[3]);

    /// <summary>The four channels in the order a color tween interpolates them.</summary>
    public double[] ToChannels() => [R, G, B, A];

    /// <summary>The <c>rgba(r,g,b,a)</c> string upstream writes to non-DOM targets: channels rounded, alpha as is, no spaces.</summary>
    public static string FormatRgba(ReadOnlySpan<double> channels) =>
        $"rgba({JsNumber.Format(Helpers.Round(channels[0], 0))},{JsNumber.Format(Helpers.Round(channels[1], 0))},{JsNumber.Format(Helpers.Round(channels[2], 0))},{JsNumber.Format(channels[3])})";

    /// <summary>The color in the <see cref="FormatRgba"/> form, which is also what a color tween writes.</summary>
    public override string ToString() => FormatRgba([R, G, B, A]);

    private static bool TryParseRgb(string s, out PrestoColor color)
    {
        var m = RgbExec().Match(s);
        if (!m.Success) m = RgbaExec().Match(s);
        if (!m.Success)
        {
            color = default;
            return false;
        }
        color = new(Num(m.Groups[1]), Num(m.Groups[2]), Num(m.Groups[3]), m.Groups[4].Success ? Num(m.Groups[4]) : 1);
        return true;
    }

    private static PrestoColor ParseHex(string s)
    {
        var isShort = s.Length is 4 or 5;
        var r = HexPair(s[1], s[isShort ? 1 : 2]);
        var g = HexPair(s[isShort ? 2 : 3], s[isShort ? 2 : 4]);
        var b = HexPair(s[isShort ? 3 : 5], s[isShort ? 3 : 6]);
        var a = s.Length is 5 or 9 ? Helpers.Round(HexPair(s[isShort ? 4 : 7], s[isShort ? 4 : 8]) / 255.0, 3) : 1;
        return new(r, g, b, a);
    }

    private static bool TryParseHsl(string s, out PrestoColor color)
    {
        var m = HslExec().Match(s);
        if (!m.Success) m = HslaExec().Match(s);
        if (!m.Success)
        {
            color = default;
            return false;
        }
        var h = Num(m.Groups[1]) / 360;
        var sat = Num(m.Groups[2]) / 100;
        var l = Num(m.Groups[3]) / 100;
        var a = m.Groups[4].Success ? Num(m.Groups[4]) : 1;
        if (sat == 0)
        {
            // Upstream leaves the achromatic branch unscaled (0-1 rather than 0-255); kept for parity.
            color = new(l, l, l, a);
            return true;
        }
        var q = l < .5 ? l * (1 + sat) : l + sat - l * sat;
        var p = 2 * l - q;
        color = new(
            Helpers.Round(Hue2Rgb(p, q, h + 1.0 / 3) * 255, 0),
            Helpers.Round(Hue2Rgb(p, q, h) * 255, 0),
            Helpers.Round(Hue2Rgb(p, q, h - 1.0 / 3) * 255, 0),
            a);
        return true;
    }

    private static double Hue2Rgb(double p, double q, double t)
    {
        if (t < 0) t += 1;
        if (t > 1) t -= 1;
        return t < 1.0 / 6 ? p + (q - p) * 6 * t :
               t < .5 ? q :
               t < 2.0 / 3 ? p + (q - p) * (2.0 / 3 - t) * 6 :
               p;
    }

    private static int HexPair(char hi, char lo) => int.Parse([hi, lo], NumberStyles.HexNumber, CultureInfo.InvariantCulture);

    private static double Num(Group g) => JsNumber.ToNumber(g.ValueSpan);
}
