using Monaco.Presto.Values;

namespace Monaco.Presto.Targets;

/// <summary>Turns a typed property value into tweenable channels and back, for types the core does not parse itself.</summary>
public interface IValueCodec
{
    bool TryDecode(object value, DecomposedValue into);

    object Encode(double number, Tween tween);
}
