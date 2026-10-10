namespace MonacoLabs.Presto;

/// <summary>The handle <see cref="Engine.MakeCurrent"/> returns; disposing it makes the previously current engine current again.</summary>
/// <remarks>A struct so that <c>using (engine.MakeCurrent())</c> allocates nothing on a per-frame path.</remarks>
public readonly struct EngineScope : IDisposable
{
    private readonly Engine? _previous;

    internal EngineScope(Engine? previous) => _previous = previous;

    /// <summary>Restores the engine that was <see cref="Engine.Current"/> when this scope was taken.</summary>
    public void Dispose() => Engine.RestoreCurrent(_previous);
}
