namespace MonacoLabs.Presto.Diagnostics;

/// <summary>What one <see cref="Engine.Update"/> did, recorded while <see cref="EngineDiagnostics.Enabled"/> is set.</summary>
/// <remarks><c>UpdateDuration</c> is real time; <c>TweensRendered</c> counts the tweens owned by the ticked roots, an upper bound on those that wrote a value.</remarks>
public readonly record struct FrameStats(
    double EngineTime,
    double Delta,
    TimeSpan UpdateDuration,
    int RootsTicked,
    int TweensRendered,
    int ValuesStepped,
    int SinksFlushed);
