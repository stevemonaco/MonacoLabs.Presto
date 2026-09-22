namespace Monaco.Presto.Core;

internal static class Helpers
{
    public static double Clamp(double v, double min, double max) => v < min ? min : v > max ? max : v;

    public static double ClampInfinity(double v) =>
        double.IsPositiveInfinity(v) ? Constants.MaxValue : double.IsNegativeInfinity(v) ? -Constants.MaxValue : v;

    public static double Round(double v, int decimals)
    {
        if (decimals < 0) return v;
        if (decimals == 0) return RoundHalfUp(v);
        var p = Math.Pow(10, decimals);
        return RoundHalfUp(v * p) / p;
    }

    public static double NormalizeTime(double v) =>
        v <= Constants.MinValue ? Constants.MinValue : ClampInfinity(Round(v, 11));

    // JavaScript Math.round: ties go toward +Infinity. .NET's MidpointRounding.ToPositiveInfinity is a ceiling, not this.
    private static double RoundHalfUp(double v)
    {
        var floor = Math.Floor(v);
        return v - floor >= 0.5 ? floor + 1 : floor;
    }

    public static void ForEachChildren(Clock parent, Action<Timer> callback, bool reverse = false)
    {
        var next = reverse ? parent._tail : parent._head;
        while (next != null)
        {
            var currentNext = reverse ? next._prev : next._next;
            callback(next);
            next = currentNext;
        }
    }

    public static void RemoveChild(Clock parent, Timer child)
    {
        var prev = child._prev;
        var next = child._next;
        if (prev != null) prev._next = next; else parent._head = next;
        if (next != null) next._prev = prev; else parent._tail = prev;
        child._prev = null;
        child._next = null;
    }

    public static void AddChild(Clock parent, Timer child, Func<Timer, Timer, bool>? sortMethod = null)
    {
        var prev = parent._tail;
        while (prev != null && sortMethod != null && sortMethod(prev, child)) prev = prev._prev;
        var next = prev != null ? prev._next : parent._head;
        if (prev != null) prev._next = child; else parent._head = child;
        if (next != null) next._prev = child; else parent._tail = child;
        child._prev = prev;
        child._next = next;
    }
}
