namespace MonacoLabs.Presto.Targets;

/// <summary>Reads and writes one property of a target.</summary>
public interface IPropertyAccessor
{
    /// <summary>The codec decoding the property's type into channels, or null when the property holds a value the core parses itself.</summary>
    IValueCodec? Codec { get; }

    /// <summary>Reads the value the tween starts from; null is taken as 0.</summary>
    object? GetValue(object target);

    /// <summary>
    /// Writes the current frame. <paramref name="number"/> is the modified, interpolated scalar of number and unit tweens and
    /// is NaN for color and complex tweens, whose channels are on <see cref="Tween"/> as <c>_numbers</c> and <c>_strings</c>.
    /// </summary>
    void SetValue(object target, double number, Tween tween);

    /// <summary>Writes back the value <see cref="GetValue"/> returned before the animation ran.</summary>
    void Revert(object target, object? originalValue);
}
