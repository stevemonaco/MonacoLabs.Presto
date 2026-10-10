namespace MonacoLabs.Presto.Targets;

/// <summary>
/// Resolves animatable properties on a family of targets. Adapters registered in <see cref="Presto.Adapters"/> are asked in
/// order; the first one that handles the target and returns an accessor wins, otherwise <see cref="ObjectTargetAdapter"/> does.
/// </summary>
public interface ITargetAdapter
{
    /// <summary>Returns the accessor for <paramref name="property"/>, or null to let the next adapter try.</summary>
    IPropertyAccessor? Resolve(object target, string property);

    /// <summary>
    /// Returns the one name every spelling of <paramref name="property"/> shares on this target, or null when the adapter
    /// does not handle it. Tweens are keyed by this name, so two spellings of one property compose against each other.
    /// </summary>
    /// <remarks>Must not throw for a name it does not recognize: unlike <see cref="Resolve"/> this is asked speculatively.</remarks>
    string? Canonicalize(object target, string property) => null;

    /// <summary>
    /// Called by <see cref="Presto.Remove(object?, Timer?, PropertyKey?)"/> once the tweens are gone, so that an adapter driving
    /// <paramref name="targets"/> outside the engine — the Avalonia bridge's compositor animations — stops too: every
    /// property of them, or only <paramref name="property"/> when one is named.
    /// </summary>
    void Removed(IReadOnlyList<object> targets, string? property) { }
}
