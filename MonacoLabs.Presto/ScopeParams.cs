namespace MonacoLabs.Presto;

/// <summary>Parameters for <see cref="Presto.CreateScope"/>.</summary>
public record ScopeParams
{
    /// <summary>What the scope is anchored to; the core never reads it, the Avalonia bridge treats it as the root control.</summary>
    public object? Root { get; init; }

    /// <summary>Mutates a copy of the global defaults; whatever it leaves alone keeps the global value.</summary>
    public Action<PrestoDefaults>? Defaults { get; init; }
}
