using Monaco.Presto.Diagnostics;
using Monaco.Presto.Values;

namespace Monaco.Presto;

public static partial class Eases
{
    /// <summary>
    /// Oscillation of <paramref name="wiggles"/> full swings around the start, like GSAP's CustomWiggle: it is 0 at both ends,
    /// so the tween's to-value acts as the swing amplitude. <paramref name="random"/> seeds <see cref="WiggleType.Random"/>.
    /// </summary>
    public static Ease Wiggle(int wiggles = 10, WiggleType type = WiggleType.EaseOut, Random? random = null)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(wiggles, 1);
        var frequency = 2 * Math.PI * wiggles;
        Ease amplitude = type switch
        {
            WiggleType.EaseOut => t => 1 - t,
            WiggleType.EaseInOut => t => Math.Sin(Math.PI * t),
            WiggleType.Anticipate => t => -(1 - t) * (1 - t) * (1 - t),
            WiggleType.Uniform => _ => 1,
            WiggleType.Random => RandomSwings(2 * wiggles, random ?? Random.Shared),
            _ => throw new ArgumentOutOfRangeException(nameof(type)),
        };
        Ease ease = t => t <= 0 || t >= 1 ? 0 : amplitude(t) * Math.Sin(frequency * t);
        return PrestoDiagnostics.Active ? Name(ease, $"wiggle({wiggles}, {type})") : ease;
    }

    private static Ease RandomSwings(int swings, Random random)
    {
        var amplitudes = new double[swings];
        for (var i = 0; i < swings; i++) amplitudes[i] = random.NextDouble();
        return t => amplitudes[(int)(t * swings)] * (1 - t);
    }

    /// <summary>
    /// Jagged piecewise linear ease through <paramref name="points"/> points jittered around <paramref name="template"/>
    /// (default <see cref="None"/>), like GSAP's RoughEase; exactly 0 at the start and 1 at the end.
    /// </summary>
    public static Ease Rough(int points = 20, double strength = 1, bool clamp = false, RoughTaper taper = RoughTaper.None,
        bool randomize = true, Ease? template = null, Random? random = null)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(points, 1);
        random ??= Random.Shared;
        template ??= None;
        var maxBump = strength * .4;
        var jagged = new List<LinearStop>(points + 2) { new(0, 0) };
        for (var i = points - 1; i >= 0; i--)
        {
            var x = randomize ? random.NextDouble() : (double)i / points;
            if (x <= 0) continue;
            var edge = Math.Min(x, 1 - x) * 2;
            var bump = taper switch
            {
                RoughTaper.Out => (1 - x) * (1 - x) * maxBump,
                RoughTaper.In => x * x * maxBump,
                RoughTaper.Both => edge * edge * .5 * maxBump,
                _ => maxBump,
            };
            var y = template(x) + (randomize ? random.NextDouble() * bump - bump * .5 : i % 2 == 1 ? bump * .5 : -bump * .5);
            jagged.Add(new(clamp ? Math.Clamp(y, 0, 1) : y, x));
        }
        jagged.Sort((a, b) => a.Position!.Value.CompareTo(b.Position!.Value));
        jagged.Add(new(1, 1));
        return Named(Linear([.. jagged]), "rough", points, strength);
    }

    /// <summary>
    /// GSAP's SlowMo: eases in to a linear middle covering <paramref name="linearRatio"/> of the time, whose slope
    /// <paramref name="power"/> flattens, then eases out; <paramref name="yoyoMode"/> rises to 1 and returns to 0 instead.
    /// </summary>
    public static Ease SlowMo(double linearRatio = .7, double power = .7, bool yoyoMode = false)
    {
        linearRatio = Math.Min(1, linearRatio);
        var pow = linearRatio < 1 ? power : 0;
        var p1 = (1 - linearRatio) / 2;
        var p3 = p1 + linearRatio;
        Ease ease = p =>
        {
            var r = p + (.5 - p) * pow;
            if (p < p1)
            {
                var q = 1 - p / p1;
                return yoyoMode ? 1 - q * q : r - q * q * q * q * r;
            }
            if (p > p3)
            {
                var q = (p - p3) / p1;
                return yoyoMode ? (p >= 1 ? 0 : 1 - q * q) : r + (p - r) * q * q * q * q;
            }
            return yoyoMode ? 1 : r;
        };
        return PrestoDiagnostics.Active
            ? Name(ease, $"slowMo({JsNumber.Format(linearRatio)}, {JsNumber.Format(power)}{(yoyoMode ? ", true" : "")})")
            : ease;
    }

    /// <summary>
    /// GSAP's ExpoScaleEase: makes a scale tween from <paramref name="startScale"/> to <paramref name="endScale"/> look
    /// perceptually linear (or follow <paramref name="ease"/>) by interpolating the scale geometrically.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">A scale is not positive or the scales are equal.</exception>
    public static Ease ExpoScale(double startScale, double endScale, Ease? ease = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(startScale);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(endScale);
        if (startScale == endScale) throw new ArgumentOutOfRangeException(nameof(endScale), "Start and end scales must differ.");
        ease ??= None;
        var logRatio = Math.Log(endScale / startScale);
        var range = endScale - startScale;
        Ease scaled = p => (startScale * Math.Exp(logRatio * ease(p)) - startScale) / range;
        if (!PrestoDiagnostics.Active) return scaled;
        var inner = TryGetName(ease, out var innerName) ? $", {innerName}" : "";
        return Name(scaled, $"expoScale({JsNumber.Format(startScale)}, {JsNumber.Format(endScale)}{inner})");
    }
}
