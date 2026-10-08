using Monaco.Presto.Diagnostics;
using Monaco.Presto.Values;

namespace Monaco.Presto;

public static partial class Eases
{
    /// <summary>
    /// Piecewise linear ease through <paramref name="stops"/>, spaced evenly over [0, 1] unless a stop carries its own
    /// <see cref="LinearStop.Position"/>. Without stops it is <see cref="None"/>.
    /// </summary>
    public static Ease Linear(params LinearStop[] stops)
    {
        if (stops.Length == 0) return None;
        var totalPoints = stops.Length - 1;
        var xPoints = new double[stops.Length];
        var yPoints = new double[xPoints.Length];
        yPoints[0] = stops[0].Value;
        for (var i = 1; i < totalPoints; i++)
        {
            xPoints[i] = stops[i].Position ?? (double)i / totalPoints;
            yPoints[i] = stops[i].Value;
        }
        xPoints[^1] = 1;
        yPoints[^1] = stops[totalPoints].Value;
        Ease ease = t =>
        {
            for (var i = 1; i < xPoints.Length; i++)
            {
                var currentX = xPoints[i];
                if (t <= currentX)
                {
                    var prevX = xPoints[i - 1];
                    var prevY = yPoints[i - 1];
                    return prevY + (yPoints[i] - prevY) * (t - prevX) / (currentX - prevX);
                }
            }
            return yPoints[^1];
        };
        return PrestoDiagnostics.Active ? Name(ease, $"linear({string.Join(", ", stops.Select(FormatStop))})") : ease;
    }

    private static string FormatStop(LinearStop stop) =>
        stop.Position is { } position ? $"{JsNumber.Format(stop.Value)} {JsNumber.Format(position * 100)}%" : JsNumber.Format(stop.Value);
}
