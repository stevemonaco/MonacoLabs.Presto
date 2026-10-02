using System.Globalization;
using Monaco.Presto.Values;

namespace Monaco.Presto;

/// <summary>
/// A raw property value in the anime.js syntax: a number, a string (<c>"+=50"</c>, <c>"#ff0000"</c>, <c>"1turn"</c>),
/// a <see cref="TweenParams"/>, an array (<c>[from, to]</c> or keyframes), a <see cref="FunctionValue"/>, a
/// <see cref="Stagger"/>, an ease, or any typed value wrapped with <see cref="Of"/>. Adapters decode it against the
/// property's current value.
/// </summary>
public readonly struct TweenValue
{
    internal readonly object? Raw;

    private TweenValue(object? raw) => Raw = raw;

    /// <summary>A plain number: a property value in whatever unit the property already carries, or milliseconds where the parameter is a duration or delay.</summary>
    public static implicit operator TweenValue(double value) => new(value);

    /// <inheritdoc cref="op_Implicit(double)"/>
    public static implicit operator TweenValue(int value) => new((double)value);

    /// <inheritdoc cref="op_Implicit(double)"/>
    public static implicit operator TweenValue(float value) => new((double)value);

    /// <inheritdoc cref="op_Implicit(double)"/>
    public static implicit operator TweenValue(long value) => new((double)value);

    /// <summary>
    /// A value in the anime.js string syntax: relative to the previous value (<c>"+=50"</c>, <c>"-=50"</c>, <c>"*=2"</c>),
    /// a number with a unit (<c>"50%"</c>, <c>"1turn"</c>, <c>"0.5rad"</c>, <c>"10px"</c>), a color (<c>"#ff0000"</c>,
    /// <c>"rgba(255,0,0,.5)"</c>, <c>"hsl(0,100%,50%)"</c>), or a complex string whose every embedded number is interpolated
    /// (<c>"0 0 5px black"</c>). A string that is entirely numeric is taken as the number.
    /// </summary>
    public static implicit operator TweenValue(string value) => new(value);

    /// <summary>Per-property settings in place of a bare value, so this one property can carry its own from, duration, delay, ease or composition.</summary>
    public static implicit operator TweenValue(TweenParams value) => new(value);

    /// <summary>A delegate evaluated once per target, for values that differ between the targets.</summary>
    public static implicit operator TweenValue(FunctionValue value) => new(value);

    /// <summary>A delegate that also receives the tween this one chains off, for values relative to the previous keyframe or timeline sibling.</summary>
    public static implicit operator TweenValue(ChainedFunctionValue value) => new(value);

    /// <summary>A stagger, which is a <see cref="FunctionValue"/> spreading a range of values across the targets.</summary>
    public static implicit operator TweenValue(Stagger value) => new(value);

    /// <summary>Lets a <see cref="FunctionValue"/> used as an <see cref="EaseValue"/> return a different ease per target.</summary>
    public static implicit operator TweenValue(Ease value) => new(value);

    /// <summary>Lets a <see cref="FunctionValue"/> used as an <see cref="EaseValue"/> return a different spring per target.</summary>
    public static implicit operator TweenValue(Spring value) => new(value);

    /// <summary>A to-value that coasts from a velocity; not allowed in keyframes or a from/to pair.</summary>
    public static implicit operator TweenValue(Inertia value) => new(value);

    /// <summary>A to-value that flies from a launch velocity under acceleration; not allowed in keyframes or a from/to pair.</summary>
    public static implicit operator TweenValue(Projectile value) => new(value);

    /// <summary>
    /// Two items are a from/to pair; three or more are keyframes whose first frame is the leading pair. An array of
    /// <see cref="TweenParams"/> is always keyframes, however many there are.
    /// </summary>
    public static implicit operator TweenValue(TweenValue[] value) => new(value);

    /// <inheritdoc cref="op_Implicit(TweenValue[])"/>
    public static implicit operator TweenValue(double[] value) => new(Array.ConvertAll(value, v => (TweenValue)v));

    /// <inheritdoc cref="op_Implicit(TweenValue[])"/>
    public static implicit operator TweenValue(int[] value) => new(Array.ConvertAll(value, v => (TweenValue)v));

    /// <inheritdoc cref="op_Implicit(TweenValue[])"/>
    public static implicit operator TweenValue(string[] value) => new(Array.ConvertAll(value, v => (TweenValue)v));

    /// <summary>Keyframes, each with its own values and settings.</summary>
    public static implicit operator TweenValue(TweenParams[] value) => new(Array.ConvertAll(value, v => (TweenValue)v));

    /// <summary>Wraps a value that has no implicit conversion, such as a bridge-typed color or thickness.</summary>
    public static TweenValue Of(object? typed) => typed switch
    {
        TweenValue value => value,
        int or float or long or short or byte or decimal => new(Convert.ToDouble(typed, CultureInfo.InvariantCulture)),
        _ => new(typed),
    };

    /// <summary>Builds the <c>[from, to]</c> pair explicitly, for values whose array form has no implicit conversion.</summary>
    public static TweenValue FromTo(TweenValue from, TweenValue to) => new(new[] { from, to });

    /// <summary>Builds a keyframe array explicitly, for frames of mixed types that no implicit array conversion covers.</summary>
    public static TweenValue Keyframes(params TweenValue[] frames) => new(frames);

    internal bool IsArray => Raw is TweenValue[];

    internal bool IsParams => Raw is TweenParams;

    internal double? AsDouble => Raw as double?;

    /// <summary>Coerces a <see cref="FunctionValue"/> result as upstream <c>getFunctionValue</c>: numeric strings become numbers, null and NaN become 0.</summary>
    internal TweenValue Coerce() => Raw switch
    {
        null => 0,
        double d when double.IsNaN(d) => 0,
        string s when JsNumber.ToNumber(s) is var n && !double.IsNaN(n) => n,
        _ => this,
    };
}
