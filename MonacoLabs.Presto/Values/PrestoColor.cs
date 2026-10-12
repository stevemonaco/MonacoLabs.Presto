using System.Globalization;
using System.Text.RegularExpressions;
using MonacoLabs.Presto.Core;

namespace MonacoLabs.Presto.Values;

/// <summary>
/// An RGBA color with 0-255 channels and 0-1 alpha (upstream <c>ColorArray</c>), parsed from <c>#rgb</c>, <c>#rgba</c>,
/// <c>#rrggbb</c>, <c>#rrggbbaa</c>, <c>rgb()</c>, <c>rgba()</c>, <c>hsl()</c> and <c>hsla()</c> strings.
/// </summary>
public readonly partial record struct PrestoColor(double R, double G, double B, double A = 1)
{
    [GeneratedRegex(@"(^#([\da-f]{3}){1,2}$)|(^#([\da-f]{4}){1,2}$)", RegexOptions.ECMAScript | RegexOptions.IgnoreCase)]
    private static partial Regex HexTest();

    [GeneratedRegex(@"rgb\(\s*(\d*\.?\d+%?)\s*,\s*(\d*\.?\d+%?)\s*,\s*(\d*\.?\d+%?)\s*\)", RegexOptions.ECMAScript | RegexOptions.IgnoreCase)]
    private static partial Regex RgbExec();

    [GeneratedRegex(@"rgba\(\s*(\d*\.?\d+%?)\s*,\s*(\d*\.?\d+%?)\s*,\s*(\d*\.?\d+%?)\s*,\s*(-?\d*\.?\d+%?)\s*\)", RegexOptions.ECMAScript | RegexOptions.IgnoreCase)]
    private static partial Regex RgbaExec();

    [GeneratedRegex(@"hsl\(\s*(-?\d+|-?\d*.\d+)\s*,\s*(-?\d+|-?\d*.\d+)%\s*,\s*(-?\d+|-?\d*.\d+)%\s*\)", RegexOptions.ECMAScript | RegexOptions.IgnoreCase)]
    private static partial Regex HslExec();

    [GeneratedRegex(@"hsla\(\s*(-?\d+|-?\d*.\d+)\s*,\s*(-?\d+|-?\d*.\d+)%\s*,\s*(-?\d+|-?\d*.\d+)%\s*,\s*(-?\d+|-?\d*.\d+)\s*\)", RegexOptions.ECMAScript | RegexOptions.IgnoreCase)]
    private static partial Regex HslaExec();

    internal static bool IsHex(string s) => HexTest().IsMatch(s);

    /// <summary>Parses a color string.</summary>
    /// <exception cref="FormatException"><paramref name="s"/> is not one of the forms <see cref="TryParse"/> recognizes.</exception>
    public static PrestoColor Parse(string s) =>
        TryParse(s, out var color) ? color : throw new FormatException($"'{s}' is not a hex, rgb() or hsl() color.");

    /// <summary>Parses a color string, returning false for anything that is not one of the recognized hex, <c>rgb</c> or <c>hsl</c> forms.</summary>
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

    /// <summary>The color in OKLab.</summary>
    public OklabColor ToOklab()
    {
        ColorConversions.SrgbToOklab(R / 255, G / 255, B / 255, out var l, out var a, out var b);
        return new(l, a, b, A);
    }

    /// <summary>The color in OKLCH, hue in degrees [0, 360); a mix treats the hue as powerless when the chroma is below 4e-4.</summary>
    public OklchColor ToOklch()
    {
        var lab = ToOklab();
        ColorConversions.OklabToOklch(lab.A, lab.B, out var c, out var h);
        return new(lab.L, c, h, A);
    }

    /// <summary>An OKLab color mapped into sRGB with the CSS Color 4 gamut map; channels are left unrounded.</summary>
    public static PrestoColor FromOklab(double l, double a, double b, double alpha = 1)
    {
        ColorConversions.OklabToSrgbInGamut(l, a, b, out var r, out var g, out var bl);
        return new(r * 255, g * 255, bl * 255, alpha);
    }

    /// <summary>An OKLCH color (hue in degrees) mapped into sRGB with the CSS Color 4 gamut map; channels are left unrounded.</summary>
    public static PrestoColor FromOklch(double l, double c, double h, double alpha = 1)
    {
        ColorConversions.OklchToOklab(c, h, out var a, out var b);
        return FromOklab(l, a, b, alpha);
    }

    /// <summary>
    /// The CSS Color 4 mix of two colors at <paramref name="t"/> in <paramref name="space"/>, with premultiplied alpha and,
    /// in <see cref="ColorSpace.Oklch"/>, the <paramref name="hue"/> arc. A <paramref name="t"/> outside 0-1 extrapolates;
    /// the result is gamut mapped with unrounded channels, and 0 and 1 return the ends exactly.
    /// </summary>
    /// <remarks>Unlike CSS, equal chromatic hues under <see cref="HueInterpolation.Increasing"/> or <see cref="HueInterpolation.Decreasing"/> make a full turn.</remarks>
    public static PrestoColor Mix(PrestoColor from, PrestoColor to, double t, ColorSpace space = ColorSpace.Oklab, HueInterpolation hue = HueInterpolation.Shorter)
    {
        if (t == 0) return from;
        if (t == 1) return to;
        var alpha = from.A + (to.A - from.A) * t;
        var clampedAlpha = Helpers.Clamp(alpha, 0, 1);
        switch (space)
        {
            case ColorSpace.Srgb:
                return FromSrgb(
                    Lerp(from.R / 255, to.R / 255, from.A, to.A, alpha, t),
                    Lerp(from.G / 255, to.G / 255, from.A, to.A, alpha, t),
                    Lerp(from.B / 255, to.B / 255, from.A, to.A, alpha, t),
                    clampedAlpha);
            case ColorSpace.LinearSrgb:
                return FromSrgb(
                    ColorConversions.LinearToSrgb(Lerp(ColorConversions.SrgbToLinear(from.R / 255), ColorConversions.SrgbToLinear(to.R / 255), from.A, to.A, alpha, t)),
                    ColorConversions.LinearToSrgb(Lerp(ColorConversions.SrgbToLinear(from.G / 255), ColorConversions.SrgbToLinear(to.G / 255), from.A, to.A, alpha, t)),
                    ColorConversions.LinearToSrgb(Lerp(ColorConversions.SrgbToLinear(from.B / 255), ColorConversions.SrgbToLinear(to.B / 255), from.A, to.A, alpha, t)),
                    clampedAlpha);
            case ColorSpace.Oklch:
                var p = from.ToOklch();
                var q = to.ToOklch();
                ResolveHues(p, q, hue, out var h1, out var h2);
                ColorConversions.OklchToOklab(Lerp(p.C, q.C, from.A, to.A, alpha, t), h1 + (h2 - h1) * t, out var oa, out var ob);
                return FromOklab(Lerp(p.L, q.L, from.A, to.A, alpha, t), oa, ob, clampedAlpha);
            default:
                var x = from.ToOklab();
                var y = to.ToOklab();
                return FromOklab(
                    Lerp(x.L, y.L, from.A, to.A, alpha, t),
                    Lerp(x.A, y.A, from.A, to.A, alpha, t),
                    Lerp(x.B, y.B, from.A, to.A, alpha, t),
                    clampedAlpha);
        }
    }

    /// <summary>This color with the given OKLCH channels replaced (CSS relative color), gamut mapped and keeping its alpha; a gray's hue counts as 0.</summary>
    public PrestoColor WithOklch(double? l = null, double? c = null, double? h = null)
    {
        var o = ToOklch();
        return FromOklch(l ?? o.L, Math.Max(c ?? o.C, 0), h ?? PowerlessHue(o), A);
    }

    /// <summary>This color with the given amounts added to its OKLCH channels, gamut mapped and keeping its alpha; chroma stops at 0.</summary>
    public PrestoColor ShiftOklch(double dl = 0, double dc = 0, double dh = 0)
    {
        var o = ToOklch();
        return FromOklch(o.L + dl, Math.Max(o.C + dc, 0), PowerlessHue(o) + dh, A);
    }

    /// <summary>The degrees an <see cref="ColorSpace.Oklch"/> mix travels around the hue wheel, 360 for a full turn.</summary>
    internal static double HueArc(PrestoColor from, PrestoColor to, HueInterpolation hue)
    {
        ResolveHues(from.ToOklch(), to.ToOklch(), hue, out var h1, out var h2);
        return Math.Abs(h2 - h1);
    }

    private static void ResolveHues(OklchColor p, OklchColor q, HueInterpolation hue, out double h1, out double h2)
    {
        h1 = p.H;
        h2 = q.H;
        ColorConversions.ResolvePowerlessHues(p.C, ref h1, q.C, ref h2);
        var fullTurn = hue is HueInterpolation.Increasing or HueInterpolation.Decreasing && Math.Abs(h2 - h1) < 1e-9 &&
                       p.C >= ColorConversions.AchromaticChroma && q.C >= ColorConversions.AchromaticChroma;
        h2 = fullTurn ? h1 + (hue == HueInterpolation.Increasing ? 360 : -360) : ColorConversions.UnwrapHue(h1, h2, hue);
    }

    private static double PowerlessHue(OklchColor o) => o.C < ColorConversions.AchromaticChroma ? 0 : o.H;

    // Premultiplied; a fully transparent mix takes the nearer end's channels so nothing divides by zero.
    private static double Lerp(double a, double b, double fromAlpha, double toAlpha, double alpha, double t) =>
        alpha == 0 ? (t < .5 ? a : b) : (a * fromAlpha + (b * toAlpha - a * fromAlpha) * t) / alpha;

    private static PrestoColor FromSrgb(double r, double g, double b, double alpha)
    {
        if (ColorConversions.IsInGamut(r, g, b)) return new(Helpers.Clamp(r, 0, 1) * 255, Helpers.Clamp(g, 0, 1) * 255, Helpers.Clamp(b, 0, 1) * 255, alpha);
        ColorConversions.SrgbToOklab(r, g, b, out var l, out var oa, out var ob);
        return FromOklab(l, oa, ob, alpha);
    }

    private static bool TryParseRgb(string s, out PrestoColor color)
    {
        var m = RgbExec().Match(s);
        if (!m.Success) m = RgbaExec().Match(s);
        if (!m.Success)
        {
            color = default;
            return false;
        }
        color = new(Channel(m.Groups[1]), Channel(m.Groups[2]), Channel(m.Groups[3]), m.Groups[4].Success ? Alpha(m.Groups[4]) : 1);
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
            var gray = Helpers.Round(l * 255, 0);
            color = new(gray, gray, gray, a);
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

    private static double Channel(Group g) => g.ValueSpan[^1] == '%' ? JsNumber.ToNumber(g.ValueSpan[..^1]) * 255 / 100 : Num(g);

    private static double Alpha(Group g) => g.ValueSpan[^1] == '%' ? JsNumber.ToNumber(g.ValueSpan[..^1]) / 100 : Num(g);
}
