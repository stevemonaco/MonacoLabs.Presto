using Monaco.Presto.Values;

namespace Monaco.Presto.Targets;

/// <summary>Turns a typed property value into tweenable channels and back, for types the core does not parse itself.</summary>
public interface IValueCodec
{
    /// <summary>Fills <paramref name="into"/> from a value of the type this codec handles, returning false to let the parser reject it.</summary>
    bool TryDecode(object value, DecomposedValue into);

    /// <summary>
    /// Builds the typed value for the current frame. <paramref name="number"/> is the interpolated scalar of number and
    /// unit tweens; color and complex tweens carry their channels on <paramref name="tween"/> instead.
    /// </summary>
    object Encode(double number, Tween tween);
}
