namespace MonacoLabs.Presto;

/// <summary>Something a <see cref="Scope"/> can undo: timers, animatables and nested scopes.</summary>
public interface IRevertible
{
    /// <summary>Restores what this object changed and releases it, leaving the targets as they were before it ran.</summary>
    void Revert();
}
