using Avalonia;

namespace MonacoLabs.Presto;

/// <summary>
/// An effect that <see cref="Animate.DoOnCueProperty"/> and <see cref="Animate.DoOnChangedProperty"/> invoke on the
/// element they are set on. Derive from it to write your own.
/// </summary>
public abstract class CueAction
{
    /// <summary>
    /// Runs the effect on <paramref name="target"/> with the raised or watched <paramref name="value"/>; <paramref name="first"/>
    /// is true for a first watched value, which an action that only marks a moment skips, and always false for a cue.
    /// </summary>
    protected internal abstract void Invoke(Visual target, object? value, bool first);

    internal static T Require<T>(Visual target, string extension) where T : Visual =>
        target as T ?? throw new InvalidOperationException(
            $"{extension} acts on a {typeof(T).Name}, which a {target.GetType().Name} is not; set it on a {typeof(T).Name}.");
}
