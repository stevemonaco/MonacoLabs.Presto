using MonacoLabs.Presto.Core;
using MonacoLabs.Presto.Values;

namespace MonacoLabs.Presto;

/// <summary>Adds one property's keyframes, in order, for <see cref="PropertyBuilder{TSelf}.Property(PropertyKey, Action{TweenKeyframesBuilder})"/>.</summary>
public sealed class TweenKeyframesBuilder
{
    private const double PathTolerance = .02;
    private const int MaxFittedSegments = 16;
    private const int ProbesPerSegment = 7;

    private static readonly Ease Hold = static s => s < 1 ? 0 : 1;

    private readonly List<TweenValue> _frames = [];

    internal TweenKeyframesBuilder() { }

    /// <summary>Adds a frame ending on <paramref name="to"/>; the first frame starts from the property's current value.</summary>
    /// <remarks>
    /// Every frame is a <see cref="TweenParams"/>, never a bare value: a bare array reads as <c>[from, to]</c>, which would
    /// turn the first frame into a start value.
    /// </remarks>
    public TweenKeyframesBuilder To(TweenValue to, TweenValue? duration = null, TweenValue? delay = null, EaseValue? ease = null)
    {
        _frames.Add(new TweenParams { To = to, Duration = duration, Delay = delay, Ease = ease });
        return this;
    }

    /// <summary>
    /// Adds a color tween from <paramref name="from"/> to <paramref name="to"/> that follows the <see cref="PrestoColor.Mix"/>
    /// path in <paramref name="space"/>, as <paramref name="segments"/> frames whose eases are slices of the one ease, so the
    /// timing is exactly that ease's. Unset, segments are halved until the rendered color stays within about deltaEOK 0.02 of the
    /// path, up to 16; with per-target ends, duration or ease they are 4, or one per 30° of hue arc (4-24) in <see cref="ColorSpace.Oklch"/>.
    /// </summary>
    /// <remarks>
    /// The frames carry their own ease and duration, so the animation's are not used: unset, they are the
    /// <see cref="Engine.Current"/> engine's defaults when this is called. A spring ease sets the duration to its settling time.
    /// Ends may be per-target <see cref="FunctionValue"/>s, which are resolved once per target and then mixed.
    /// </remarks>
    public TweenKeyframesBuilder Color(TweenValue from, TweenValue to, ColorSpace space = ColorSpace.Oklab, HueInterpolation hue = HueInterpolation.Shorter,
                                      TweenValue? duration = null, TweenValue? delay = null, EaseValue? ease = null, int? segments = null)
    {
        if (segments < 1) throw new ArgumentOutOfRangeException(nameof(segments), segments, "A color tween needs at least one segment.");
        var defaults = Engine.Current.Defaults;
        var easeValue = ease ?? defaults.Ease;
        var durationValue = duration ?? defaults.Duration;
        if (durationValue.Raw is Stagger) throw new ArgumentException("A Stagger duration cannot be split into color segments; pass a FunctionValue.", nameof(duration));
        PrestoColor? fromColor = from.Raw is FunctionValue ? null : ParseColor(from, nameof(from));
        PrestoColor? toColor = to.Raw is FunctionValue ? null : ParseColor(to, nameof(to));
        void Add(int i, TweenValue? frameFrom, TweenValue frameTo, TweenValue frameDuration, EaseValue frameEase) =>
            _frames.Add(new TweenParams { From = i == 0 ? frameFrom : null, To = frameTo, Duration = frameDuration, Delay = i == 0 ? delay : null, Ease = frameEase });

        if (fromColor is { } a && toColor is { } b && easeValue.Raw is not FunctionValue && durationValue.Raw is not FunctionValue)
        {
            var e = ResolveTiming(easeValue, durationValue, this, 0, 1, out var total);
            var times = segments is { } count ? UniformTimes(count) : FitSegments(a, b, e, space, hue);
            for (var i = 0; i < times.Length - 1; i++)
            {
                var (t0, t1) = (times[i], times[i + 1]);
                Add(i, from, i == times.Length - 2 ? to : PrestoColor.Mix(a, b, e(t1), space, hue), total * t1 - total * t0, Slice(e, t0, t1));
            }
            return this;
        }

        var n = segments ?? (space != ColorSpace.Oklch ? 4
            : Math.Clamp((int)Math.Ceiling((fromColor is { } f && toColor is { } t ? PrestoColor.HueArc(f, t, hue) : 360) / 30), 4, 24));
        object? lastTarget = null;
        var lastIndex = -1;
        PrestoColor ra = default, rb = default;
        var re = Eases.None;
        var rTotal = 0d;
        // Every frame resolves the same target in turn, so one cached target keeps random per-target values consistent.
        void Resolve(object target, int index, int count)
        {
            if (ReferenceEquals(target, lastTarget) && index == lastIndex) return;
            (lastTarget, lastIndex) = (target, index);
            ra = fromColor ?? ParseColor(((FunctionValue)from.Raw!)(target, index, count), nameof(from));
            rb = toColor ?? ParseColor(((FunctionValue)to.Raw!)(target, index, count), nameof(to));
            re = ResolveTiming(easeValue, durationValue, target, index, count, out rTotal);
        }

        for (var i = 0; i < n; i++)
        {
            var segment = i;
            Add(i,
                i > 0 ? (TweenValue?)null : fromColor != null ? from : (FunctionValue)((target, index, count) => { Resolve(target, index, count); return TweenValue.Of(ra); }),
                segment == n - 1 && toColor != null ? to : (FunctionValue)((target, index, count) =>
                {
                    Resolve(target, index, count);
                    return TweenValue.Of(segment == n - 1 ? rb : PrestoColor.Mix(ra, rb, re((segment + 1.0) / n), space, hue));
                }),
                (FunctionValue)((target, index, count) => { Resolve(target, index, count); return rTotal / n; }),
                (FunctionValue)((target, index, count) => { Resolve(target, index, count); return Slice(re, (double)segment / n, (segment + 1.0) / n); }));
        }
        return this;
    }

    internal TweenValue[] ToArray() => _frames.ToArray();

    private static PrestoColor ParseColor(TweenValue value, string name) => value.Raw switch
    {
        PrestoColor color => color,
        string text when PrestoColor.TryParse(text, out var color) => color,
        _ => throw new ArgumentException($"'{value.Raw}' is not a color.", name),
    };

    private static Ease ResolveTiming(EaseValue ease, TweenValue duration, object target, int index, int count, out double total)
    {
        var resolved = ease.Resolve(target, index, count, out var spring);
        total = spring?.SettlingDuration ?? JsNumber.ToNumber((duration.Raw is FunctionValue function ? function(target, index, count) : duration).Raw, 0);
        return spring?.PureEase ?? resolved;
    }

    private static Ease Slice(Ease ease, double t0, double t1)
    {
        var start = ease(t0);
        var span = ease(t1) - start;
        return span == 0 ? Hold : s => (ease(t0 + (t1 - t0) * s) - start) / span;
    }

    private static double[] UniformTimes(int n)
    {
        var times = new double[n + 1];
        for (var i = 0; i <= n; i++) times[i] = (double)i / n;
        return times;
    }

    /// <summary>Segment boundaries in normalized time, halving the worst segment until each is within <see cref="PathTolerance"/>.</summary>
    private static double[] FitSegments(PrestoColor a, PrestoColor b, Ease e, ColorSpace space, HueInterpolation hue)
    {
        List<double> times = [0, 1];
        List<double> errors = [PathError(a, b, e, space, hue, 0, 1)];
        while (times.Count - 1 < MaxFittedSegments)
        {
            var worst = 0;
            for (var i = 1; i < errors.Count; i++)
            {
                if (errors[i] > errors[worst]) worst = i;
            }
            if (errors[worst] <= PathTolerance) break;
            var (t0, t1) = (times[worst], times[worst + 1]);
            var mid = (t0 + t1) / 2;
            times.Insert(worst + 1, mid);
            errors[worst] = PathError(a, b, e, space, hue, t0, mid);
            errors.Insert(worst + 1, PathError(a, b, e, space, hue, mid, t1));
        }
        return times.ToArray();
    }

    /// <summary>The largest deltaEOK, at evenly spaced probes, between the renderer's blend of a segment's end colors and the path.</summary>
    private static double PathError(PrestoColor a, PrestoColor b, Ease e, ColorSpace space, HueInterpolation hue, double t0, double t1)
    {
        var c0 = t0 == 0 ? a : PrestoColor.Mix(a, b, e(t0), space, hue);
        var c1 = t1 == 1 ? b : PrestoColor.Mix(a, b, e(t1), space, hue);
        var (e0, span) = (e(t0), e(t1) - e(t0));
        var worst = 0d;
        for (var q = 1; q <= ProbesPerSegment; q++)
        {
            var t = t0 + (t1 - t0) * q / (ProbesPerSegment + 1);
            var rendered = RenderedBlend(c0, c1, span == 0 ? 0 : (e(t) - e0) / span);
            worst = Math.Max(worst, DeltaEok(rendered, PrestoColor.Mix(a, b, e(t), space, hue)));
        }
        return worst;
    }

    private static PrestoColor RenderedBlend(PrestoColor x, PrestoColor y, double p)
    {
        var premultiplied = x.A != y.A;
        return new(Channel(x.R, y.R), Channel(x.G, y.G), Channel(x.B, y.B), Math.Clamp(x.A + (y.A - x.A) * p, 0, 1));

        double Channel(double from, double to) => Math.Clamp(Renderer.ColorLerp(from, to, x.A, y.A, premultiplied, p), 0, 255);
    }

    private static double DeltaEok(PrestoColor x, PrestoColor y)
    {
        var (p, q) = (x.ToOklab(), y.ToOklab());
        return ColorConversions.DeltaEok(p.L, p.A, p.B, q.L, q.A, q.B);
    }
}
