using System.Globalization;
using MonacoLabs.Presto.Core;

namespace MonacoLabs.Presto;

/// <summary>
/// Numeric, random and time helpers. Every numeric helper also has an overload that
/// omits the value and returns a <see cref="ChainedUtil"/>, so transforms compose into an <c>AnimationParams.Modifier</c>.
/// </summary>
/// <remarks>Upstream's <c>padStart</c>/<c>padEnd</c> are omitted; <see cref="string.PadLeft(int, char)"/> and <see cref="string.PadRight(int, char)"/> already cover them.</remarks>
public static class Utils
{
    private static int s_seed;

    /// <summary>Constrains <paramref name="v"/> to the range [<paramref name="min"/>, <paramref name="max"/>].</summary>
    public static double Clamp(double v, double min, double max) => Helpers.Clamp(v, min, max);

    /// <inheritdoc cref="Clamp(double, double, double)"/>
    public static ChainedUtil Clamp(double min, double max) => new(v => Helpers.Clamp(v, min, max));

    /// <summary>Rounds <paramref name="v"/> to <paramref name="decimals"/> decimal places.</summary>
    public static double Round(double v, int decimals) => Helpers.Round(v, decimals);

    /// <inheritdoc cref="Round(double, int)"/>
    public static ChainedUtil Round(int decimals) => new(v => Helpers.Round(v, decimals));

    /// <summary>Snaps <paramref name="v"/> to the nearest multiple of <paramref name="increment"/>; an increment of 0 leaves it alone.</summary>
    public static double Snap(double v, double increment) => increment == 0 ? v : Helpers.Round(v / increment, 0) * increment;

    /// <summary>Snaps <paramref name="v"/> to the closest of <paramref name="points"/> by absolute distance; the earliest point wins a tie.</summary>
    public static double Snap(double v, double[] points)
    {
        if (points.Length == 0) return v;
        var closest = points[0];
        for (var i = 1; i < points.Length; i++)
        {
            if (Math.Abs(points[i] - v) < Math.Abs(closest - v)) closest = points[i];
        }
        return closest;
    }

    /// <inheritdoc cref="Snap(double, double)"/>
    public static ChainedUtil Snap(double increment) => new(v => Snap(v, increment));

    /// <inheritdoc cref="Snap(double, double[])"/>
    public static ChainedUtil Snap(double[] points) => new(v => Snap(v, points));

    /// <summary>Wraps <paramref name="v"/> into [<paramref name="min"/>, <paramref name="max"/>), cycling past either end instead of clamping.</summary>
    public static double Wrap(double v, double min, double max)
    {
        var range = max - min;
        return ((v - min) % range + range) % range + min;
    }

    /// <inheritdoc cref="Wrap(double, double, double)"/>
    public static ChainedUtil Wrap(double min, double max) => new(v => Wrap(v, min, max));

    /// <summary>Re-maps <paramref name="value"/> from the input range onto the output range, extrapolating outside it.</summary>
    public static double MapRange(double value, double inLow, double inHigh, double outLow, double outHigh) =>
        outLow + (value - inLow) / (inHigh - inLow) * (outHigh - outLow);

    /// <inheritdoc cref="MapRange(double, double, double, double, double)"/>
    public static ChainedUtil MapRange(double inLow, double inHigh, double outLow, double outHigh) =>
        new(v => MapRange(v, inLow, inHigh, outLow, outHigh));

    /// <summary>Interpolates between <paramref name="start"/> and <paramref name="end"/> by <paramref name="factor"/>, which is not clamped.</summary>
    public static double Lerp(double start, double end, double factor) => Helpers.Lerp(start, end, factor);

    /// <summary>Chainable <see cref="Lerp(double, double, double)"/>: the chained value becomes the factor.</summary>
    public static ChainedUtil Lerp(double start, double end) => new(v => Helpers.Lerp(start, end, v));

    /// <summary>Frame rate independent damped lerp; <paramref name="deltaTime"/> is in milliseconds and <paramref name="factor"/> is in [0, 1].</summary>
    public static double Damp(double start, double end, double deltaTime, double factor) =>
        factor == 0 ? start : factor == 1 ? end : Helpers.Lerp(start, end, 1 - Math.Exp(-factor * deltaTime * .1));

    /// <summary>Chainable <see cref="Damp(double, double, double, double)"/>: the chained value becomes the factor.</summary>
    public static ChainedUtil Damp(double start, double end, double deltaTime) => new(v => Damp(start, end, deltaTime, v));

    /// <summary>Converts <paramref name="degrees"/> to radians.</summary>
    public static double DegToRad(double degrees) => degrees * Math.PI / 180;

    /// <inheritdoc cref="DegToRad(double)"/>
    public static ChainedUtil DegToRad() => new(DegToRad);

    /// <summary>Converts <paramref name="radians"/> to degrees.</summary>
    public static double RadToDeg(double radians) => radians * 180 / Math.PI;

    /// <inheritdoc cref="RadToDeg(double)"/>
    public static ChainedUtil RadToDeg() => new(RadToDeg);

    /// <summary>Formats <paramref name="v"/> with exactly <paramref name="decimals"/> decimal places, like JavaScript's <c>toFixed</c>.</summary>
    public static string RoundPad(double v, int decimals) => v.ToString("F" + decimals.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);

    /// <summary>Returns a random number between <paramref name="min"/> and <paramref name="max"/> inclusive, rounded to <paramref name="decimals"/> places.</summary>
    public static double Random(double min = 0, double max = 1, int decimals = 0)
    {
        var m = Math.Pow(10, decimals);
        return Math.Floor((System.Random.Shared.NextDouble() * (max - min + 1 / m) + min) * m) / m;
    }

    /// <summary>
    /// Creates a deterministic generator reproducing anime.js's seeded PRNG bit for bit. Omitting <paramref name="seed"/>
    /// takes the next value of a process-wide counter.
    /// </summary>
    /// <remarks>Upstream's <c>seededMin</c>/<c>seededMax</c>/<c>seededDecimalLength</c> are omitted: C# optional arguments must be constants, so they could never be reached.</remarks>
    public static RandomGenerator CreateSeededRandom(int? seed = null)
    {
        var t = seed ?? (Interlocked.Increment(ref s_seed) - 1);
        return (min, max, decimals) =>
        {
            unchecked
            {
                t += 0x6D2B79F5;
                t = (t ^ (int)((uint)t >> 15)) * (t | 1);
                t ^= t + (t ^ (int)((uint)t >> 7)) * (t | 61);
                var m = Math.Pow(10, decimals);
                var unit = (uint)(t ^ (int)((uint)t >> 14)) / 4294967296d;
                return Math.Floor((unit * (max - min + 1 / m) + min) * m) / m;
            }
        };
    }

    /// <summary>Returns one element of <paramref name="items"/> at random, every element equally likely.</summary>
    public static T RandomPick<T>(IReadOnlyList<T> items) => items[(int)Random(0, items.Count - 1)];

    /// <summary>Shuffles <paramref name="items"/> in place with Fisher-Yates and returns it.</summary>
    public static IList<T> Shuffle<T>(IList<T> items, RandomGenerator? rnd = null)
    {
        rnd ??= Random;
        var m = items.Count;
        while (m > 0)
        {
            var i = (int)rnd(0, --m);
            (items[m], items[i]) = (items[i], items[m]);
        }
        return items;
    }

    /// <summary>Runs <paramref name="callback"/> on the next engine tick.</summary>
    public static Timer Sync(Action<Timer>? callback = null) =>
        new Timer(new TimerParams { Duration = 1 * Globals.TimeScale, OnComplete = callback }, null, 0).Resume();

    /// <summary>
    /// Wraps <paramref name="constructor"/> so each call reverts the tickable the previous call returned and hands its
    /// iteration and progress to the replacement, keeping playback position across re-creations.
    /// </summary>
    /// <remarks>Upstream's branch for constructors returning a cleanup function belongs to scopes, which Phase 6 adds.</remarks>
    public static Func<TArg, TTimer> KeepTime<TArg, TTimer>(Func<TArg, TTimer> constructor) where TTimer : Timer
    {
        TTimer? tracked = null;
        return arg =>
        {
            var previous = tracked;
            var currentIteration = 0;
            double currentIterationProgress = 0, startTime = 0;
            bool reversed = false, alternate = false;
            if (previous != null)
            {
                currentIteration = previous.CurrentIteration;
                currentIterationProgress = previous.IterationProgress;
                reversed = previous.Reversed;
                alternate = previous._alternate;
                startTime = previous._startTime;
                previous.Revert();
            }
            tracked = constructor(arg);
            if (previous != null)
            {
                var mirrored = alternate ? currentIteration % 2 == 0 ? reversed : !reversed : reversed;
                tracked.CurrentIteration = currentIteration;
                tracked.IterationProgress = mirrored ? 1 - currentIterationProgress : currentIterationProgress;
                tracked._startTime = startTime;
            }
            return tracked;
        };
    }
}
