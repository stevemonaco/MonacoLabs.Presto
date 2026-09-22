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

    public static implicit operator TweenValue(double value) => new(value);
    public static implicit operator TweenValue(int value) => new((double)value);
    public static implicit operator TweenValue(float value) => new((double)value);
    public static implicit operator TweenValue(long value) => new((double)value);
    public static implicit operator TweenValue(string value) => new(value);
    public static implicit operator TweenValue(TweenParams value) => new(value);
    public static implicit operator TweenValue(FunctionValue value) => new(value);
    public static implicit operator TweenValue(Stagger value) => new(value);
    public static implicit operator TweenValue(Ease value) => new(value);
    public static implicit operator TweenValue(Spring value) => new(value);
    public static implicit operator TweenValue(TweenValue[] value) => new(value);
    public static implicit operator TweenValue(double[] value) => new(Array.ConvertAll(value, v => (TweenValue)v));
    public static implicit operator TweenValue(int[] value) => new(Array.ConvertAll(value, v => (TweenValue)v));
    public static implicit operator TweenValue(string[] value) => new(Array.ConvertAll(value, v => (TweenValue)v));
    public static implicit operator TweenValue(TweenParams[] value) => new(Array.ConvertAll(value, v => (TweenValue)v));

    /// <summary>Wraps a value that has no implicit conversion, such as a bridge-typed color or thickness.</summary>
    public static TweenValue Of(object? typed) => typed switch
    {
        TweenValue value => value,
        int or float or long or short or byte or decimal => new(Convert.ToDouble(typed, CultureInfo.InvariantCulture)),
        _ => new(typed),
    };

    public static TweenValue FromTo(TweenValue from, TweenValue to) => new(new[] { from, to });

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
