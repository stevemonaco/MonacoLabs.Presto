namespace Monaco.Presto.Core;

/// <summary>
/// Resolves a projectile to-value once its from-number is known: <c>x' = v</c>, <c>v' = a − k·v</c> in closed form. A
/// flight that turns back is split at its apex, so each tween is monotonic and one landing on its start still has a
/// distance to normalize by.
/// </summary>
internal static class ProjectileSolver
{
    public static InertiaSolver.Plan Resolve(double from, Projectile settings, double tweenDuration)
    {
        var duration = settings.Duration ?? tweenDuration;
        var (v, a) = (settings.Velocity, settings.Acceleration);
        var k = -double.LogP1(-settings.Friction) / Globals.TimeScale;
        double Displacement(double t) => k == 0 ? v * t + a * t * t / 2 : a / k * t + (v - a / k) * (1 - Math.Exp(-k * t)) / k;

        var end = Displacement(duration);
        var apex = v * a >= 0 ? 0 : k == 0 ? -v / a : double.LogP1(-k * v / a) / k;
        if (apex > 0 && apex < duration)
        {
            var landing = new InertiaSolver.Plan(from + end, duration - apex, Segment(Displacement, apex, duration));
            return new(from + Displacement(apex), apex, Segment(Displacement, 0, apex), landing);
        }
        return end == 0 ? new(from, duration, Eases.None) : new(from + end, duration, Segment(Displacement, 0, duration));
    }

    private static Ease Segment(Func<double, double> displacement, double start, double end)
    {
        var (origin, span) = (displacement(start), displacement(end) - displacement(start));
        return u => u >= 1 ? 1 : (displacement(start + u * (end - start)) - origin) / span;
    }
}
