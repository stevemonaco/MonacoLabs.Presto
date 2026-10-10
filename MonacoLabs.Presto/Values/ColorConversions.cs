using MonacoLabs.Presto.Core;

namespace MonacoLabs.Presto.Values;

/// <summary>CSS Color 4 conversions between sRGB (channels 0-1), linear sRGB, OKLab and OKLCH, plus its gamut mapping.</summary>
internal static class ColorConversions
{
    public const double AchromaticChroma = 4e-4;

    private const double GamutEpsilon = 1e-9;
    private const double ChromaEpsilon = 1e-4;
    private const double JustNoticeable = 0.02;

    public static double SrgbToLinear(double c)
    {
        var abs = Math.Abs(c);
        return abs <= 0.04045 ? c / 12.92 : Math.Sign(c) * Math.Pow((abs + 0.055) / 1.055, 2.4);
    }

    public static double LinearToSrgb(double c)
    {
        var abs = Math.Abs(c);
        return abs > 0.0031308 ? Math.Sign(c) * (1.055 * Math.Pow(abs, 1 / 2.4) - 0.055) : c * 12.92;
    }

    // Matrices as culori writes them, so the fixtures agree to the last bits.
    public static void LinearSrgbToOklab(double r, double g, double b, out double l, out double oa, out double ob)
    {
        var lc = Math.Cbrt(0.412221469470763 * r + 0.5363325372617348 * g + 0.0514459932675022 * b);
        var mc = Math.Cbrt(0.2119034958178252 * r + 0.6806995506452344 * g + 0.1073969535369406 * b);
        var sc = Math.Cbrt(0.0883024591900564 * r + 0.2817188391361215 * g + 0.6299787016738222 * b);
        l = 0.210454268309314 * lc + 0.7936177747023054 * mc - 0.0040720430116193 * sc;
        oa = 1.9779985324311684 * lc - 2.4285922420485799 * mc + 0.450593709617411 * sc;
        ob = 0.0259040424655478 * lc + 0.7827717124575296 * mc - 0.8086757549230774 * sc;
    }

    public static void OklabToLinearSrgb(double l, double oa, double ob, out double r, out double g, out double b)
    {
        var lc = Cube(l + 0.3963377773761749 * oa + 0.2158037573099136 * ob);
        var mc = Cube(l - 0.1055613458156586 * oa - 0.0638541728258133 * ob);
        var sc = Cube(l - 0.0894841775298119 * oa - 1.2914855480194092 * ob);
        r = 4.0767416360759574 * lc - 3.3077115392580616 * mc + 0.2309699031821044 * sc;
        g = -1.2684379732850317 * lc + 2.6097573492876887 * mc - 0.3413193760026573 * sc;
        b = -0.0041960761386756 * lc - 0.7034186179359362 * mc + 1.7076146940746117 * sc;
    }

    public static void SrgbToOklab(double r, double g, double b, out double l, out double oa, out double ob) =>
        LinearSrgbToOklab(SrgbToLinear(r), SrgbToLinear(g), SrgbToLinear(b), out l, out oa, out ob);

    public static void OklabToSrgb(double l, double oa, double ob, out double r, out double g, out double b)
    {
        OklabToLinearSrgb(l, oa, ob, out var lr, out var lg, out var lb);
        r = LinearToSrgb(lr);
        g = LinearToSrgb(lg);
        b = LinearToSrgb(lb);
    }

    public static void OklabToOklch(double oa, double ob, out double c, out double h)
    {
        c = Math.Sqrt(oa * oa + ob * ob);
        h = NormalizeHue(Math.Atan2(ob, oa) * 180 / Math.PI);
    }

    public static void OklchToOklab(double c, double h, out double oa, out double ob)
    {
        var radians = h * Math.PI / 180;
        oa = c * Math.Cos(radians);
        ob = c * Math.Sin(radians);
    }

    public static double NormalizeHue(double h)
    {
        h %= 360;
        return h < 0 ? h + 360 : h;
    }

    /// <summary>An achromatic end takes the other end's hue; when both are achromatic both take 0.</summary>
    public static void ResolvePowerlessHues(double c1, ref double h1, double c2, ref double h2)
    {
        h1 = c1 >= AchromaticChroma ? h1 : c2 >= AchromaticChroma ? h2 : 0;
        if (c2 < AchromaticChroma) h2 = h1;
    }

    /// <summary>Shifts <paramref name="h2"/> by ±360 once so a lerp from <paramref name="h1"/> takes the arc CSS Color 4 picks.</summary>
    public static double UnwrapHue(double h1, double h2, HueInterpolation mode)
    {
        var d = h2 - h1;
        return mode switch
        {
            HueInterpolation.Shorter when d > 180 => h2 - 360,
            HueInterpolation.Shorter when d < -180 => h2 + 360,
            HueInterpolation.Longer when d is > 0 and < 180 => h2 - 360,
            HueInterpolation.Longer when d is > -180 and <= 0 => h2 + 360,
            HueInterpolation.Increasing when d < 0 => h2 + 360,
            HueInterpolation.Decreasing when d > 0 => h2 - 360,
            _ => h2,
        };
    }

    public static double DeltaEok(double l1, double a1, double b1, double l2, double a2, double b2)
    {
        double dl = l1 - l2, da = a1 - a2, db = b1 - b2;
        return Math.Sqrt(dl * dl + da * da + db * db);
    }

    public static bool IsInGamut(double r, double g, double b) =>
        r is > -GamutEpsilon and < 1 + GamutEpsilon && g is > -GamutEpsilon and < 1 + GamutEpsilon && b is > -GamutEpsilon and < 1 + GamutEpsilon;

    /// <summary>The CSS Color 4 gamut map from OKLab to sRGB channels clipped to 0-1, searching chroma at constant L and hue.</summary>
    public static void OklabToSrgbInGamut(double l, double oa, double ob, out double r, out double g, out double b)
    {
        OklabToSrgb(l, oa, ob, out r, out g, out b);
        if (!IsInGamut(r, g, b))
        {
            if (l >= 1)
            {
                r = g = b = 1;
                return;
            }
            if (l <= 0)
            {
                r = g = b = 0;
                return;
            }
            var chroma = Math.Sqrt(oa * oa + ob * ob);
            double start = 0, end = chroma;
            while (end - start > ChromaEpsilon)
            {
                var mid = (start + end) * 0.5;
                double ca = oa * mid / chroma, cb = ob * mid / chroma;
                OklabToSrgb(l, ca, cb, out r, out g, out b);
                var inGamut = IsInGamut(r, g, b);
                Clip(ref r, ref g, ref b);
                SrgbToOklab(r, g, b, out var kl, out var ka, out var kb);
                if (inGamut || DeltaEok(l, ca, cb, kl, ka, kb) <= JustNoticeable) start = mid;
                else end = mid;
            }
        }
        // The search ends on its last candidate, which CSS returns clipped; an in-gamut one is unchanged by the clip.
        Clip(ref r, ref g, ref b);
    }

    private static void Clip(ref double r, ref double g, ref double b)
    {
        r = Helpers.Clamp(r, 0, 1);
        g = Helpers.Clamp(g, 0, 1);
        b = Helpers.Clamp(b, 0, 1);
    }

    private static double Cube(double v) => v * v * v;
}
