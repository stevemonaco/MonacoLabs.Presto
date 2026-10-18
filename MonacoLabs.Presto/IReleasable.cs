namespace MonacoLabs.Presto;

/// <summary>A library <see cref="IRevertible"/> that can tell its scope when reverting it again would restore nothing.</summary>
internal interface IReleasable
{
    bool IsReleased { get; }
}
