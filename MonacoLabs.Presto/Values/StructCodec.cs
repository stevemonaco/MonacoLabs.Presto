using MonacoLabs.Presto.Targets;

namespace MonacoLabs.Presto.Values;

/// <summary>
/// A codec for a value type built from a fixed number of doubles: <paramref name="decompose"/> lists its components and
/// <paramref name="compose"/> rebuilds it from the tween's lerped channels.
/// </summary>
/// <param name="decompose">The value's components, one per channel, in order.</param>
/// <param name="compose">Receives the tween's channel array, which is reused every frame, so it must not keep a reference to it.</param>
public sealed class StructCodec<T>(Func<T, double[]> decompose, Func<double[], T> compose) : IValueCodec
    where T : struct
{
    /// <summary>Decomposes a <typeparamref name="T"/> into its channels; any other value is not claimed.</summary>
    public bool TryDecode(object value, DecomposedValue into)
    {
        if (value is not T typed) return false;
        ValueParser.Decompose(decompose(typed), into);
        return true;
    }

    /// <summary>The value <c>compose</c> builds from the tween's lerped channels.</summary>
    public object Encode(double number, Tween tween) => compose(tween._numbers);
}
