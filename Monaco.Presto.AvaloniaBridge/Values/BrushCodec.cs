using System.Runtime.CompilerServices;
using Avalonia.Media;
using Monaco.Presto.Targets;
using Monaco.Presto.Values;

namespace Monaco.Presto.AvaloniaBridge.Values;

/// <summary>Decomposes the color of a solid brush; gradient and other brushes are not tweenable.</summary>
internal sealed class BrushCodec : IValueCodec
{
    private readonly ConditionalWeakTable<Tween, SolidColorBrush> _brushes = new();

    public bool TryDecode(object value, DecomposedValue into)
    {
        if (value is not ISolidColorBrush brush) return false;
        into.Kind = ValueKind.Color;
        into.Numbers = brush.Color.ToPresto().ToChannels();
        return true;
    }

    /// <summary>Recolors one brush per tween; the brush being animated from is often immutable or shared, so it is never touched.</summary>
    public object Encode(double number, Tween tween)
    {
        var color = PrestoColor.FromChannels(tween._numbers).ToAvalonia();
        if (_brushes.TryGetValue(tween, out var brush))
        {
            brush.Color = color;
            return brush;
        }
        brush = new SolidColorBrush(color);
        _brushes.Add(tween, brush);
        return brush;
    }
}
