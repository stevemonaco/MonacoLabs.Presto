using MonacoLabs.Presto.Core;
using MonacoLabs.Presto.Diagnostics;

namespace MonacoLabs.Presto;

public static partial class Eases
{
    private const int PathSamples = 512;

    /// <summary>
    /// Ease drawn by SVG path data (M, L, H, V and C commands) with x as progress and y as the eased value, y up, like
    /// GSAP's CustomEase. The path is normalized to start at (0, 0) and end at (1, 1), except a path that ends at its start height, which ends at 0; y may overshoot in between.
    /// </summary>
    /// <exception cref="FormatException">The path uses another command or is malformed.</exception>
    /// <exception cref="ArgumentException">The path's x decreases somewhere or spans no width.</exception>
    public static Ease FromPath(string pathData)
    {
        var (xs, ys) = EasePathParser.Flatten(pathData);
        if (xs.Count < 2 || xs[^1] <= xs[0]) throw new ArgumentException("Ease path must span a positive x range.", nameof(pathData));
        double x0 = xs[0], y0 = ys[0], width = xs[^1] - x0, height = ys[^1] - y0;
        if (height == 0) height = 1;

        var table = new double[PathSamples + 1];
        var segment = 1;
        for (var i = 1; i < PathSamples; i++)
        {
            var x = x0 + width * i / PathSamples;
            while (xs[segment] < x) segment++;
            var y = ys[segment - 1] + (ys[segment] - ys[segment - 1]) * (x - xs[segment - 1]) / (xs[segment] - xs[segment - 1]);
            table[i] = (y - y0) / height;
        }
        table[PathSamples] = (ys[^1] - y0) / height;

        Ease ease = t =>
        {
            if (t <= 0) return 0;
            if (t >= 1) return table[PathSamples];
            var position = t * PathSamples;
            var index = (int)position;
            return table[index] + (table[index + 1] - table[index]) * (position - index);
        };
        return PrestoDiagnostics.Active ? Name(ease, $"fromPath({pathData})") : ease;
    }
}
