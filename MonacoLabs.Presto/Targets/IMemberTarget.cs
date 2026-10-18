namespace MonacoLabs.Presto.Targets;

/// <summary>A library type animated by name, which <see cref="ObjectTargetAdapter"/> resolves through its own static member table instead of reflection.</summary>
internal interface IMemberTarget
{
    IReadOnlyDictionary<string, IPropertyAccessor> Members { get; }
}
