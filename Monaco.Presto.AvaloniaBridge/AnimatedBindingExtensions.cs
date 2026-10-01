using Avalonia;
using Avalonia.Data;
using Monaco.Presto.AvaloniaBridge.Bindings;

namespace Monaco.Presto.AvaloniaBridge;

/// <summary>Binds a property so it animates to each new value of its source instead of jumping to it.</summary>
public static class AnimatedBindingExtensions
{
    /// <summary>
    /// Binds <paramref name="property"/> at <see cref="BindingPriority.LocalValue"/> so it eases to each value of
    /// <paramref name="source"/>, which resolves against <paramref name="target"/> as it would on the property itself.
    /// </summary>
    /// <returns>The binding; disposing it stops the animation and removes both the binding and its source.</returns>
    /// <exception cref="ArgumentException"><paramref name="source"/> is <c>TwoWay</c> or <c>OneWayToSource</c>, or the timing conflicts.</exception>
    public static IDisposable BindAnimated(this AvaloniaObject target, AvaloniaProperty property, BindingBase source, AnimatedBindingParams? options = null) =>
        target.Bind(property, new AnimatedBinding(target, property, source, options ?? new(), followsItems: true), BindingPriority.LocalValue);

    /// <summary>Binds <paramref name="property"/> at <see cref="BindingPriority.LocalValue"/> so it eases to each value of <paramref name="source"/>.</summary>
    /// <returns>The binding; disposing it stops the animation and unsubscribes from <paramref name="source"/>.</returns>
    public static IDisposable BindAnimated<T>(this AvaloniaObject target, AvaloniaProperty<T> property, IObservable<T> source, AnimatedBindingParams? options = null) =>
        target.Bind(property, new AnimatedBinding(target, property, source.ToBinding(), options ?? new(), followsItems: false), BindingPriority.LocalValue);
}
