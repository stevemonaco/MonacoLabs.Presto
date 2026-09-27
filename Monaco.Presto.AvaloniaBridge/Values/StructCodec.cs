using Monaco.Presto.Targets;
using Monaco.Presto.Values;

namespace Monaco.Presto.AvaloniaBridge.Values;

/// <summary>
/// A codec for a value type built from a fixed number of doubles: <paramref name="decompose"/> lists its components and
/// <paramref name="compose"/> rebuilds it from the tween's lerped channels.
/// </summary>
internal sealed class StructCodec<T>(Func<T, double[]> decompose, Func<double[], T> compose) : IValueCodec
    where T : struct
{
    public bool TryDecode(object value, DecomposedValue into)
    {
        if (value is not T typed) return false;
        ValueParser.Decompose(decompose(typed), into);
        return true;
    }

    public object Encode(double number, Tween tween) => compose(tween._numbers);
}
