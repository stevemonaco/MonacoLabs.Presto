using Avalonia.Controls;
using MonacoLabs.Presto.Targets;

namespace MonacoLabs.Presto.Values;

/// <summary>
/// Tweens a pixel <see cref="GridLength"/> as its number of pixels, so a number or a pixel length animates a
/// <see cref="ColumnDefinition.Width"/> or <see cref="RowDefinition.Height"/>. A star or auto length has no pixel value to
/// interpolate from or to, so either end being one throws, as a discrete value given what it cannot be does.
/// </summary>
internal sealed class GridLengthCodec : IValueCodec
{
    /// <exception cref="ArgumentException"><paramref name="value"/> is a star or auto length.</exception>
    public bool TryDecode(object value, DecomposedValue into)
    {
        if (value is not GridLength length) return false;
        if (!length.IsAbsolute) throw new ArgumentException($"Only a pixel GridLength tweens, so '{length}' cannot; give it a pixel length first.");
        into.Number = length.Value;
        return true;
    }

    // A spring's overshoot below 0 would throw in GridLength's constructor.
    public object Encode(double number, Tween tween) => new GridLength(Math.Max(0, number));
}
