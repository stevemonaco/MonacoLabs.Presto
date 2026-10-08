using Monaco.Presto.Targets;

namespace Monaco.Presto.Values;

/// <summary>
/// The codec of a property type nothing interpolates, such as a <see cref="bool"/> or an enum: its tween runs one channel
/// from 0 to 1 and writes its from or its to value whole, switching between them by a <see cref="DiscreteRule"/>.
/// </summary>
/// <param name="type">The property's type; a number given for a <see cref="bool"/> or an enum is converted to it.</param>
/// <param name="rule">The rule a tween follows when its <see cref="TweenParams.Discrete"/> is not set.</param>
public sealed class DiscreteCodec(Type type, DiscreteRule rule = DiscreteRule.Midpoint) : IValueCodec
{
    /// <summary>The rule a tween follows when its <see cref="TweenParams.Discrete"/> is not set.</summary>
    public DiscreteRule Rule => rule;

    /// <summary>Stores <paramref name="value"/> in <see cref="DecomposedValue.Discrete"/>.</summary>
    /// <exception cref="ArgumentException"><paramref name="value"/> is relative (<c>"+=1"</c>) or not of the property's type.</exception>
    public bool TryDecode(object value, DecomposedValue into)
    {
        into.Discrete = Coerce(value);
        return true;
    }

    /// <summary>The tween's from or to value, whichever its rule picks at <paramref name="number"/>.</summary>
    public object Encode(double number, Tween tween) => Pick(number, tween)!;

    internal object Coerce(object value) => value switch
    {
        _ when type.IsInstanceOfType(value) => value,
        double number when type == typeof(bool) => number != 0,
        double number when type.IsEnum => Enum.ToObject(type, (long)number),
        string text when ValueParser.IndexOfRelativeOperator(text) == 0 => throw new ArgumentException($"'{text}' is relative, which a discrete {type} value cannot be."),
        _ => throw new ArgumentException($"A discrete {type} value cannot be set to {value} ({value.GetType()})."),
    };

    internal static object? Pick(double number, Tween tween)
    {
        var (from, to) = (tween._fromObject, tween._toObject);
        var switched = tween._discrete switch
        {
            DiscreteRule.AtStart => number > 0,
            DiscreteRule.AtEnd => number >= 1,
            DiscreteRule.TrueThroughout when to is true != from is true => to is true ? number > 0 : number >= 1,
            DiscreteRule.FalseThroughout when to is false != from is false => to is false ? number > 0 : number >= 1,
            _ => number >= .5,
        };
        return switched ? to : from;
    }
}
