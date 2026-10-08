namespace Monaco.Presto.Diagnostics;

/// <summary>The handle <see cref="PrestoDiagnostics.Origin"/> returns; disposing it restores the previous origin.</summary>
public readonly struct OriginScope : IDisposable
{
    private readonly string? _previous;
    private readonly bool _taken;

    internal OriginScope(string? previous)
    {
        _previous = previous;
        _taken = true;
    }

    /// <summary>Restores the origin that was current when this scope was taken.</summary>
    public void Dispose()
    {
        if (_taken) PrestoDiagnostics.RestoreOrigin(_previous);
    }
}
