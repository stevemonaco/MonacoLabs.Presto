using System.Globalization;

namespace MonacoLabs.Presto.Values;

/// <summary>
/// JavaScript <c>Number</c> conversions for string-valued targets. <see cref="Format"/> is .NET's shortest round-trip form
/// with the exponent spelled as JavaScript does; .NET switches to exponent notation at 1e15 and 1e-5 where JavaScript
/// switches at 1e21 and 1e-7, and that difference is not emulated.
/// </summary>
internal static class JsNumber
{
    public static string Format(double v)
    {
        if (double.IsNaN(v)) return "NaN";
        if (double.IsInfinity(v)) return v > 0 ? "Infinity" : "-Infinity";
        if (v == 0) return "0";
        var s = v.ToString("R", CultureInfo.InvariantCulture);
        var e = s.IndexOf('E');
        if (e < 0) return s;
        // .NET zero-pads the exponent to two digits ("1E-07"); JavaScript does not ("1e-7").
        return $"{s.AsSpan(0, e)}e{s[e + 1]}{s.AsSpan(e + 2).TrimStart('0')}";
    }

    /// <summary>JavaScript unary <c>+</c> on a string: whitespace is 0, unparsable input is NaN.</summary>
    public static double ToNumber(ReadOnlySpan<char> s) =>
        s.IsWhiteSpace() ? 0 : double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var n) ? n : double.NaN;

    /// <summary>JavaScript unary <c>+</c> on a boxed value, with <paramref name="fallback"/> where JavaScript would yield <c>NaN</c> or <c>undefined</c>.</summary>
    public static double ToNumber(object? value, double fallback) => value switch
    {
        double number => number,
        string text => ToNumber(text.AsSpan()),
        int or long or float or short or byte or decimal => Convert.ToDouble(value, CultureInfo.InvariantCulture),
        _ => fallback,
    };
}
