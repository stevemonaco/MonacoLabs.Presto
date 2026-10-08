using System.Runtime.CompilerServices;
using Avalonia.Media;

namespace Monaco.Presto.AvaloniaBridge;

/// <summary>
/// A 5×4 color matrix in the row-major layout <c>SKColorFilter.CreateColorMatrix</c> takes: four rows (R, G, B, A) of
/// four channel weights and an offset. Offsets are in normalized 0..1 units, which is what Skia applies (verified by
/// drawing; SkiaSharp's doc remark about 0..255 is stale). The CSS factories follow Filter Effects Level 1 §13.
/// </summary>
[InlineArray(20)]
internal struct ColorMatrix
{
    private const float LumaR = 0.2126f, LumaG = 0.7152f, LumaB = 0.0722f;

    private float _element0;

    public static ColorMatrix Identity => Rows([1, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 1, 0]);

    public readonly bool IsIdentity
    {
        get
        {
            var identity = Identity;
            for (var i = 0; i < 20; i++)
                if (Math.Abs(this[i] - identity[i]) > 1e-6f) return false;
            return true;
        }
    }

    /// <summary>The matrix that applies this one first and then <paramref name="next"/>.</summary>
    public readonly ColorMatrix Then(in ColorMatrix next)
    {
        var result = default(ColorMatrix);
        for (var row = 0; row < 4; row++)
        {
            for (var column = 0; column < 5; column++)
            {
                var sum = column == 4 ? next[row * 5 + 4] : 0f;
                for (var k = 0; k < 4; k++) sum += next[row * 5 + k] * this[k * 5 + column];
                result[row * 5 + column] = sum;
            }
        }
        return result;
    }

    public static ColorMatrix Brightness(double amount)
    {
        var a = (float)amount;
        return Rows([a, 0, 0, 0, 0, 0, a, 0, 0, 0, 0, 0, a, 0, 0, 0, 0, 0, 1, 0]);
    }

    public static ColorMatrix Contrast(double amount)
    {
        float a = (float)amount, o = 0.5f - 0.5f * a;
        return Rows([a, 0, 0, 0, o, 0, a, 0, 0, o, 0, 0, a, 0, o, 0, 0, 0, 1, 0]);
    }

    public static ColorMatrix Saturate(double amount)
    {
        var s = (float)amount;
        return Rows([
            LumaR + (1 - LumaR) * s, LumaG - LumaG * s, LumaB - LumaB * s, 0, 0,
            LumaR - LumaR * s, LumaG + (1 - LumaG) * s, LumaB - LumaB * s, 0, 0,
            LumaR - LumaR * s, LumaG - LumaG * s, LumaB + (1 - LumaB) * s, 0, 0,
            0, 0, 0, 1, 0]);
    }

    public static ColorMatrix Grayscale(double amount) => Saturate(1 - Math.Clamp(amount, 0, 1));

    public static ColorMatrix Sepia(double amount) => Lerp(Rows([
        0.393f, 0.769f, 0.189f, 0, 0,
        0.349f, 0.686f, 0.168f, 0, 0,
        0.272f, 0.534f, 0.131f, 0, 0,
        0, 0, 0, 1, 0]), amount);

    public static ColorMatrix HueRotate(double degrees)
    {
        var radians = degrees * Math.PI / 180;
        float c = (float)Math.Cos(radians), s = (float)Math.Sin(radians);
        return Rows([
            0.213f + c * 0.787f - s * 0.213f, 0.715f - c * 0.715f - s * 0.715f, 0.072f - c * 0.072f + s * 0.928f, 0, 0,
            0.213f - c * 0.213f + s * 0.143f, 0.715f + c * 0.285f + s * 0.140f, 0.072f - c * 0.072f - s * 0.283f, 0, 0,
            0.213f - c * 0.213f - s * 0.787f, 0.715f - c * 0.715f + s * 0.715f, 0.072f + c * 0.928f + s * 0.072f, 0, 0,
            0, 0, 0, 1, 0]);
    }

    public static ColorMatrix Invert(double amount)
    {
        var a = (float)Math.Clamp(amount, 0, 1);
        var d = 1 - 2 * a;
        return Rows([d, 0, 0, 0, a, 0, d, 0, 0, a, 0, 0, d, 0, a, 0, 0, 0, 1, 0]);
    }

    /// <summary>Maps luminance onto <paramref name="color"/> (black stays black, white becomes the color), blended with the identity by <paramref name="amount"/>.</summary>
    public static ColorMatrix Tint(double amount, Color color) => Duotone(amount, Colors.Black, color);

    /// <summary>Maps luminance onto the segment from <paramref name="shadow"/> to <paramref name="highlight"/>, blended with the identity by <paramref name="amount"/>.</summary>
    public static ColorMatrix Duotone(double amount, Color shadow, Color highlight)
    {
        float sr = shadow.R / 255f, sg = shadow.G / 255f, sb = shadow.B / 255f;
        float dr = highlight.R / 255f - sr, dg = highlight.G / 255f - sg, db = highlight.B / 255f - sb;
        return Lerp(Rows([
            dr * LumaR, dr * LumaG, dr * LumaB, 0, sr,
            dg * LumaR, dg * LumaG, dg * LumaB, 0, sg,
            db * LumaR, db * LumaG, db * LumaB, 0, sb,
            0, 0, 0, 1, 0]), amount);
    }

    /// <summary>Composes every filter in the fixed order Brightness → Contrast → Saturate → Grayscale → Sepia → HueRotate → Tint → Duotone → Invert.</summary>
    public static ColorMatrix Compose(double brightness, double contrast, double saturate, double grayscale, double sepia, double hueRotate,
        double tint, Color tintColor, double duotone, Color duotoneShadow, Color duotoneHighlight, double invert) =>
        Brightness(brightness)
            .Then(Contrast(contrast))
            .Then(Saturate(saturate))
            .Then(Grayscale(grayscale))
            .Then(Sepia(sepia))
            .Then(HueRotate(hueRotate))
            .Then(Tint(tint, tintColor))
            .Then(Duotone(duotone, duotoneShadow, duotoneHighlight))
            .Then(Invert(invert));

    private static ColorMatrix Rows(ReadOnlySpan<float> values)
    {
        var matrix = default(ColorMatrix);
        values.CopyTo(matrix);
        return matrix;
    }

    private static ColorMatrix Lerp(in ColorMatrix full, double amount)
    {
        var t = (float)Math.Clamp(amount, 0, 1);
        var result = Identity;
        for (var i = 0; i < 20; i++) result[i] += (full[i] - result[i]) * t;
        return result;
    }
}
