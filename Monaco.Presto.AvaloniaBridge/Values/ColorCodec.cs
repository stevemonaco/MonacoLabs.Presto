using Avalonia.Media;
using Monaco.Presto.Targets;
using Monaco.Presto.Values;

namespace Monaco.Presto.AvaloniaBridge.Values;

/// <summary>Decomposes a <see cref="Color"/> into RGBA channels and rebuilds it from the tween's lerped channels.</summary>
internal sealed class ColorCodec : IValueCodec
{
    public bool TryDecode(object value, DecomposedValue into)
    {
        if (value is not Color color) return false;
        into.Kind = ValueKind.Color;
        into.Numbers = color.ToPresto().ToChannels();
        return true;
    }

    public object Encode(double number, Tween tween) => PrestoColor.FromChannels(tween._numbers).ToAvalonia();
}
