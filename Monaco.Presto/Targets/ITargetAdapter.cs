namespace Monaco.Presto.Targets;

/// <summary>
/// Resolves animatable properties on a family of targets. Adapters registered in <see cref="Presto.Adapters"/> are asked in
/// order; the first one that handles the target and returns an accessor wins, otherwise <see cref="ObjectTargetAdapter"/> does.
/// </summary>
public interface ITargetAdapter
{
    /// <summary>Returns the accessor for <paramref name="property"/>, or null to let the next adapter try.</summary>
    IPropertyAccessor? Resolve(object target, string property);
}
