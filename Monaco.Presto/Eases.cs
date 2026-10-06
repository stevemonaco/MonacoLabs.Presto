using System.Runtime.CompilerServices;
using Monaco.Presto.Core;
using Monaco.Presto.Diagnostics;
using Monaco.Presto.Values;

namespace Monaco.Presto;

/// <summary>Catalog of built-in eases and the in/out/inOut/outIn composition helpers.</summary>
public static partial class Eases
{
    private const double HalfPI = Math.PI / 2;
    private const double DoublePI = Math.PI * 2;

    // Declared before the eases so a factory naming one during static initialization finds it.
    private static readonly ConditionalWeakTable<Ease, string> s_names = new();

    /// <summary>The identity ease, which <c>"linear"</c> and <c>"none"</c> resolve to.</summary>
    public static Ease None { get; } = t => t;

    /// <summary>Power ease-in, <c>t^power</c>.</summary>
    public static Ease In(double power = 1.68) => Named(t => Math.Pow(t, power), "in", power);

    /// <summary>Power ease-out, mirroring <c>t^power</c>.</summary>
    public static Ease Out(double power = 1.68) => Named(Out(In(power)), "out", power);

    /// <summary>Power ease-in-out over <c>t^power</c>.</summary>
    public static Ease InOut(double power = 1.68) => Named(InOut(In(power)), "inOut", power);

    /// <summary>Power ease-out-in over <c>t^power</c>.</summary>
    public static Ease OutIn(double power = 1.68) => Named(OutIn(In(power)), "outIn", power);

    /// <summary>Returns <paramref name="easeIn"/> unchanged.</summary>
    public static Ease In(Ease easeIn) => t => easeIn(t);

    /// <summary>Mirrors an ease-in into its ease-out.</summary>
    public static Ease Out(Ease easeIn) => t => 1 - easeIn(1 - t);

    /// <summary>Plays an ease-in over the first half and its mirror over the second.</summary>
    public static Ease InOut(Ease easeIn) => t => t < .5 ? easeIn(t * 2) / 2 : 1 - easeIn(t * -2 + 2) / 2;

    /// <summary>Plays the mirrored ease-out over the first half and the ease-in over the second.</summary>
    public static Ease OutIn(Ease easeIn) => t => t < .5 ? (1 - easeIn(1 - t * 2)) / 2 : (easeIn(t * 2 - 1) + 1) / 2;

    /// <summary>Quadratic (t²) ease that starts slow and accelerates into the end value.</summary>
    public static Ease InQuad { get; } = In(2);

    /// <summary>Quadratic (t²) ease that starts fast and settles into the end value.</summary>
    public static Ease OutQuad { get; } = Out(2);

    /// <summary>Quadratic (t²) ease that accelerates out of the start value and settles into the end value.</summary>
    public static Ease InOutQuad { get; } = InOut(2);

    /// <summary>Quadratic (t²) ease that settles into the midpoint and accelerates out of it into the end value.</summary>
    public static Ease OutInQuad { get; } = OutIn(2);

    /// <summary>Cubic (t³) ease that starts slow and accelerates into the end value.</summary>
    public static Ease InCubic { get; } = In(3);

    /// <summary>Cubic (t³) ease that starts fast and settles into the end value.</summary>
    public static Ease OutCubic { get; } = Out(3);

    /// <summary>Cubic (t³) ease that accelerates out of the start value and settles into the end value.</summary>
    public static Ease InOutCubic { get; } = InOut(3);

    /// <summary>Cubic (t³) ease that settles into the midpoint and accelerates out of it into the end value.</summary>
    public static Ease OutInCubic { get; } = OutIn(3);

    /// <summary>Quartic (t⁴) ease that starts slow and accelerates into the end value.</summary>
    public static Ease InQuart { get; } = In(4);

    /// <summary>Quartic (t⁴) ease that starts fast and settles into the end value.</summary>
    public static Ease OutQuart { get; } = Out(4);

    /// <summary>Quartic (t⁴) ease that accelerates out of the start value and settles into the end value.</summary>
    public static Ease InOutQuart { get; } = InOut(4);

    /// <summary>Quartic (t⁴) ease that settles into the midpoint and accelerates out of it into the end value.</summary>
    public static Ease OutInQuart { get; } = OutIn(4);

    /// <summary>Quintic (t⁵) ease that starts slow and accelerates into the end value.</summary>
    public static Ease InQuint { get; } = In(5);

    /// <summary>Quintic (t⁵) ease that starts fast and settles into the end value.</summary>
    public static Ease OutQuint { get; } = Out(5);

    /// <summary>Quintic (t⁵) ease that accelerates out of the start value and settles into the end value.</summary>
    public static Ease InOutQuint { get; } = InOut(5);

    /// <summary>Quintic (t⁵) ease that settles into the midpoint and accelerates out of it into the end value.</summary>
    public static Ease OutInQuint { get; } = OutIn(5);

    /// <summary>Sinusoidal ease that starts slow and accelerates into the end value.</summary>
    public static Ease InSine { get; } = In(Sine);

    /// <summary>Sinusoidal ease that starts fast and settles into the end value.</summary>
    public static Ease OutSine { get; } = Out(Sine);

    /// <summary>Sinusoidal ease that accelerates out of the start value and settles into the end value.</summary>
    public static Ease InOutSine { get; } = InOut(Sine);

    /// <summary>Sinusoidal ease that settles into the midpoint and accelerates out of it into the end value.</summary>
    public static Ease OutInSine { get; } = OutIn(Sine);

    /// <summary>Circular ease that hardly moves at first, then sweeps into the end value.</summary>
    public static Ease InCirc { get; } = In(Circ);

    /// <summary>Circular ease that sweeps away from the start value, then barely moves into the end value.</summary>
    public static Ease OutCirc { get; } = Out(Circ);

    /// <summary>Circular ease that sweeps through the middle and barely moves at either end.</summary>
    public static Ease InOutCirc { get; } = InOut(Circ);

    /// <summary>Circular ease that barely moves around the midpoint and sweeps at either end.</summary>
    public static Ease OutInCirc { get; } = OutIn(Circ);

    /// <summary>Exponential ease that starts slow and accelerates into the end value.</summary>
    public static Ease InExpo { get; } = In(Expo);

    /// <summary>Exponential ease that starts fast and settles into the end value.</summary>
    public static Ease OutExpo { get; } = Out(Expo);

    /// <summary>Exponential ease that accelerates out of the start value and settles into the end value.</summary>
    public static Ease InOutExpo { get; } = InOut(Expo);

    /// <summary>Exponential ease that settles into the midpoint and accelerates out of it into the end value.</summary>
    public static Ease OutInExpo { get; } = OutIn(Expo);

    /// <summary>Bouncing ease that bounces against the start value before pulling away to the end value.</summary>
    public static Ease InBounce { get; } = In(Bounce);

    /// <summary>Bouncing ease that bounces on the end value before coming to rest on it.</summary>
    public static Ease OutBounce { get; } = Out(Bounce);

    /// <summary>Bouncing ease that bounces against the start value, then on the end value before coming to rest.</summary>
    public static Ease InOutBounce { get; } = InOut(Bounce);

    /// <summary>Bouncing ease that comes to rest on the midpoint, then bounces against it on the way to the end value.</summary>
    public static Ease OutInBounce { get; } = OutIn(Bounce);

    /// <summary>The eases that take no arguments, by their anime.js names; <see cref="EaseParser"/> resolves them and <see cref="TryGetName"/> reports them.</summary>
    // "none" precedes "linear" so the static constructor leaves None named "linear".
    internal static readonly Dictionary<string, Ease> Fixed = new()
    {
        ["none"] = None, ["linear"] = None,
        ["inQuad"] = InQuad, ["outQuad"] = OutQuad, ["inOutQuad"] = InOutQuad, ["outInQuad"] = OutInQuad,
        ["inCubic"] = InCubic, ["outCubic"] = OutCubic, ["inOutCubic"] = InOutCubic, ["outInCubic"] = OutInCubic,
        ["inQuart"] = InQuart, ["outQuart"] = OutQuart, ["inOutQuart"] = InOutQuart, ["outInQuart"] = OutInQuart,
        ["inQuint"] = InQuint, ["outQuint"] = OutQuint, ["inOutQuint"] = InOutQuint, ["outInQuint"] = OutInQuint,
        ["inSine"] = InSine, ["outSine"] = OutSine, ["inOutSine"] = InOutSine, ["outInSine"] = OutInSine,
        ["inCirc"] = InCirc, ["outCirc"] = OutCirc, ["inOutCirc"] = InOutCirc, ["outInCirc"] = OutInCirc,
        ["inExpo"] = InExpo, ["outExpo"] = OutExpo, ["inOutExpo"] = InOutExpo, ["outInExpo"] = OutInExpo,
        ["inBounce"] = InBounce, ["outBounce"] = OutBounce, ["inOutBounce"] = InOutBounce, ["outInBounce"] = OutInBounce,
    };

    /// <summary>The <see cref="PrestoDefaults.Ease"/> every engine starts with, <c>out(2)</c>; named even while diagnostics are inactive.</summary>
    internal static Ease Default { get; } = Out(2);

    static Eases()
    {
        foreach (var (name, ease) in Fixed) s_names.AddOrUpdate(ease, name);
        s_names.AddOrUpdate(Default, "out(2)");
    }

    /// <summary>
    /// The anime.js name of <paramref name="ease"/>, such as <c>"outQuad"</c> or <c>"out(3)"</c>. Fixed eases always have one;
    /// eases from the parameterized factories and <see cref="EaseParser"/> only when created while <see cref="PrestoDiagnostics.Active"/> is set.
    /// </summary>
    public static bool TryGetName(Ease ease, out string name) => s_names.TryGetValue(ease, out name!);

    /// <summary>Names <paramref name="ease"/> unless a factory already did.</summary>
    internal static void NameIfUnnamed(Ease ease, string name)
    {
        if (ease != None) s_names.GetValue(ease, _ => name);
    }

    private static Ease Name(Ease ease, string name)
    {
        if (ease != None) s_names.AddOrUpdate(ease, name);
        return ease;
    }

    private static Ease Named(Ease ease, string name, params ReadOnlySpan<double> args)
    {
        if (!PrestoDiagnostics.Active) return ease;
        var formatted = new string[args.Length];
        for (var i = 0; i < args.Length; i++) formatted[i] = JsNumber.Format(args[i]);
        return Name(ease, $"{name}({string.Join(", ", formatted)})");
    }
    /// <summary>Back ease-in; <paramref name="overshoot"/> controls how far it pulls back before moving forward.</summary>
    public static Ease InBack(double overshoot = 1.7) => Named(In(Back(overshoot)), "inBack", overshoot);

    /// <summary>Back ease-out; <paramref name="overshoot"/> controls how far it overshoots before settling.</summary>
    public static Ease OutBack(double overshoot = 1.7) => Named(Out(Back(overshoot)), "outBack", overshoot);

    /// <summary>Back ease-in-out; <paramref name="overshoot"/> controls how far it pulls back and how far it overshoots.</summary>
    public static Ease InOutBack(double overshoot = 1.7) => Named(InOut(Back(overshoot)), "inOutBack", overshoot);

    /// <summary>Back ease-out-in; <paramref name="overshoot"/> controls how far it overshoots the midpoint and pulls back from it.</summary>
    public static Ease OutInBack(double overshoot = 1.7) => Named(OutIn(Back(overshoot)), "outInBack", overshoot);

    /// <summary>Elastic ease-in; <paramref name="amplitude"/> is clamped to [1, 10] and <paramref name="period"/> to (0, 2].</summary>
    public static Ease InElastic(double amplitude = 1, double period = .3) => Named(In(Elastic(amplitude, period)), "inElastic", amplitude, period);

    /// <summary>Elastic ease-out; <paramref name="amplitude"/> is clamped to [1, 10] and <paramref name="period"/> to (0, 2].</summary>
    public static Ease OutElastic(double amplitude = 1, double period = .3) => Named(Out(Elastic(amplitude, period)), "outElastic", amplitude, period);

    /// <summary>Elastic ease-in-out; <paramref name="amplitude"/> is clamped to [1, 10] and <paramref name="period"/> to (0, 2].</summary>
    public static Ease InOutElastic(double amplitude = 1, double period = .3) => Named(InOut(Elastic(amplitude, period)), "inOutElastic", amplitude, period);

    /// <summary>Elastic ease-out-in; <paramref name="amplitude"/> is clamped to [1, 10] and <paramref name="period"/> to (0, 2].</summary>
    public static Ease OutInElastic(double amplitude = 1, double period = .3) => Named(OutIn(Elastic(amplitude, period)), "outInElastic", amplitude, period);

    /// <summary>
    /// Physics-based spring ease. Supplying <paramref name="bounce"/> or <paramref name="duration"/> derives
    /// <paramref name="stiffness"/> and <paramref name="damping"/> from them instead.
    /// </summary>
    public static Spring Spring(
        double? mass = null,
        double? stiffness = null,
        double? damping = null,
        double? velocity = null,
        double? bounce = null,
        double? duration = null,
        Action<Timer?>? onComplete = null)
    {
        Spring spring = new(mass, stiffness, damping, velocity, bounce, duration, onComplete);
        Named(spring.Ease, "spring", spring.Mass, spring.Stiffness, spring.Damping, spring.Velocity);
        return spring;
    }

    private static double Sine(double t) => 1 - Math.Cos(t * HalfPI);

    private static double Circ(double t) => 1 - Math.Sqrt(1 - t * t);

    private static double Expo(double t) => t == 0 ? 0 : Math.Pow(2, 10 * t - 10);

    private static double Bounce(double t)
    {
        double pow2;
        var b = 4;
        while (t < ((pow2 = Math.Pow(2, --b)) - 1) / 11) { }
        return 1 / Math.Pow(4, 3 - b) - 7.5625 * Math.Pow((pow2 * 3 - 2) / 22 - t, 2);
    }

    private static Ease Back(double overshoot) => t => (overshoot + 1) * t * t * t - overshoot * t * t;

    private static Ease Elastic(double amplitude, double period)
    {
        var a = Helpers.Clamp(amplitude, 1, 10);
        var p = Helpers.Clamp(period, Constants.MinValue, 2);
        var s = p / DoublePI * Math.Asin(1 / a);
        var e = DoublePI / p;
        return t => t == 0 || t == 1 ? t : -a * Math.Pow(2, -10 * (1 - t)) * Math.Sin((1 - t - s) * e);
    }
}
