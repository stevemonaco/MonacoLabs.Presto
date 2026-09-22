namespace Monaco.Presto;

/// <summary>Something a <see cref="Scope"/> can undo: timers, animatables and nested scopes.</summary>
public interface IRevertible
{
    void Revert();
}
