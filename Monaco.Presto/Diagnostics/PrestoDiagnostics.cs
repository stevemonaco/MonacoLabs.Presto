namespace Monaco.Presto.Diagnostics;

/// <summary>Process-wide diagnostics state shared by every engine.</summary>
public static class PrestoDiagnostics
{
    private static int s_enabledEngines;

    [ThreadStatic]
    private static string? s_origin;

    /// <summary>True while any engine has <see cref="EngineDiagnostics.Enabled"/> set.</summary>
    public static bool Active => Volatile.Read(ref s_enabledEngines) > 0;

    /// <summary>Whether root timers record a <see cref="Timer.CreationTrace"/>; honoured only while their engine's diagnostics are enabled.</summary>
    public static bool CaptureCreationTrace { get; set; }

    internal static string? CurrentOrigin => s_origin;

    /// <summary>Tags the root timers created on this thread with <paramref name="origin"/> until the returned handle is disposed; a no-op while not <see cref="Active"/>.</summary>
    public static OriginScope Origin(string origin)
    {
        if (!Active) return default;
        var previous = s_origin;
        s_origin = origin;
        return new OriginScope(previous);
    }

    internal static void RestoreOrigin(string? previous) => s_origin = previous;

    internal static void EngineToggled(bool enabled)
    {
        if (!enabled) Interlocked.Decrement(ref s_enabledEngines);
        else if (Interlocked.Increment(ref s_enabledEngines) == 1) EaseParser.NameCached();
    }
}
