namespace Monaco.Presto.Core;

/// <summary>
/// Resolves an inertia to-value once its from-number is known (INERTIA-PLAN §1): an exponential decay towards the
/// modified target, handing over to a bounce spring folded into the same ease where it crosses a bound.
/// </summary>
internal static class InertiaSolver
{
    private const double MaxDuration = 60000;
    private const double MaxSpringVelocity = Constants.K * 10;
    private const double PeakStep = 1e-3;

    /// <summary>A resolved tween, followed by <see cref="Then"/> when the motion needs a second keyframe.</summary>
    internal sealed record Plan(double To, double Duration, Ease Ease, Plan? Then = null);

    public static Plan Resolve(double from, double velocity, Inertia settings)
    {
        var (power, tau, restDelta, min, max) = (settings.Power, settings.TimeConstant, settings.RestDelta, settings.Min, settings.Max);
        var (bounceStiffness, bounceDamping) = (settings.BounceStiffness, settings.BounceDamping);
        var solverScale = Constants.K * Globals.TimeScale;
        if (from < min || from > max)
        {
            var nearest = from < min ? min.Value : max!.Value;
            var back = new Spring(stiffness: bounceStiffness, damping: bounceDamping, velocity: velocity * solverScale / (nearest - from));
            return new(nearest, back.SettlingDuration, back.PureEase);
        }

        var ideal = from + power * velocity * tau;
        var target = settings.ModifyTarget?.Invoke(ideal) ?? ideal;
        var travel = target - from;
        if (Math.Abs(travel) <= restDelta) return new(from, Constants.MinValue, Eases.None);

        double? crossed = travel > 0 && target > max ? max : travel < 0 && target < min ? min : null;
        if (crossed is not { } bound)
        {
            var duration = Math.Clamp(tau * Math.Log(Math.Abs(travel) / restDelta), 0, MaxDuration);
            var norm = 1 - Math.Exp(-duration / tau);
            return new(target, duration, u => (1 - Math.Exp(-u * duration / tau)) / norm);
        }

        var distance = bound - from;
        var crossingVelocity = (travel - distance) / tau;
        var solverVelocity = crossingVelocity * solverScale / distance;
        var rest = new Spring(stiffness: bounceStiffness, damping: bounceDamping);
        if (Math.Abs(solverVelocity) > MaxSpringVelocity) return Rebound(bound, crossingVelocity * solverScale, rest, solverScale);

        var crossing = -tau * Math.Log(1 - distance / travel);
        var thrown = new Spring(stiffness: bounceStiffness, damping: bounceDamping, velocity: solverVelocity);
        var total = crossing + thrown.SettlingDuration;
        return new(bound, total, u =>
        {
            if (u >= 1) return 1;
            var t = u * total;
            if (t < crossing) return travel * (1 - Math.Exp(-t / tau)) / distance;
            var s = (t - crossing) / solverScale;
            return 1 + thrown.Solve(s) - rest.Solve(s);
        });
    }

    // speed is in units per solver second; bound == from has no distance to normalize by
    private static Plan Rebound(double bound, double speed, Spring rest, double solverScale)
    {
        var unit = new Spring(stiffness: rest.Stiffness, damping: rest.Damping, velocity: 1);
        double Impulse(double s) => unit.Solve(s) - rest.Solve(s);
        var peak = 0.0;
        while (Impulse(peak + PeakStep) > Impulse(peak)) peak += PeakStep;
        var reach = Impulse(peak);
        var back = new Plan(bound, rest.SettlingDuration, rest.PureEase);
        return new(bound + speed * reach, peak * solverScale, u => u >= 1 ? 1 : Impulse(u * peak) / reach, back);
    }
}
