using Avalonia;
using Avalonia.Animation;
using Avalonia.Data;

namespace MonacoLabs.Presto;

internal static class ThemeTransitions
{
    /// <summary>
    /// Overrides <paramref name="target"/>'s transitions at <see cref="BindingPriority.Animation"/> with <paramref name="source"/>
    /// minus those on <paramref name="property"/>, or returns null when none is on it.
    /// </summary>
    public static IDisposable? Lift(Avalonia.Animation.Animatable target, IEnumerable<ITransition>? source, AvaloniaProperty property)
    {
        if (source is null || !source.Any(transition => Targets(transition, property))) return null;
        var kept = new Transitions();
        kept.AddRange(source.Where(transition => !Targets(transition, property)));
        return target.SetValue(Avalonia.Animation.Animatable.TransitionsProperty, kept, BindingPriority.Animation);
    }

    private static bool Targets(ITransition transition, AvaloniaProperty property) =>
        transition is TransitionBase { Property: var transitioned } && transitioned == property;
}
