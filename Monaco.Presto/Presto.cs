using Monaco.Presto.Core;

namespace Monaco.Presto;

/// <summary>Entry point mirroring the anime.js module exports: the engine singleton, global defaults and factory methods.</summary>
public static class Presto
{
    public static Engine Engine { get; } = new(new StopwatchFrameSource());

    public static PrestoDefaults Defaults => Globals.Defaults;

    public static Timer CreateTimer(TimerParams? parameters = null) => new Timer(parameters ?? new(), null, 0).Init();
}
