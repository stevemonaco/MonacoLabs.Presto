using MonacoLabs.Presto.Core;
using MonacoLabs.Presto.Diagnostics;

namespace MonacoLabs.Presto;

public static partial class Eases
{
    /// <summary>
    /// Staircase ease of <paramref name="steps"/> equal jumps, each taken at the end of its interval or, with
    /// <paramref name="fromStart"/>, at its start (the CSS <c>jump-end</c> and <c>jump-start</c> terms).
    /// </summary>
    public static Ease Steps(int steps = 10, bool fromStart = false)
    {
        Func<double, double> roundMethod = fromStart ? Math.Ceiling : Math.Floor;
        var stepSize = 1.0 / steps;
        Ease ease = t => roundMethod(Helpers.Clamp(t, 0, 1) * steps) * stepSize;
        return PrestoDiagnostics.Active ? Name(ease, fromStart ? $"steps({steps}, true)" : $"steps({steps})") : ease;
    }
}
