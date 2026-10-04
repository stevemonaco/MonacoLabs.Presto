using System.Diagnostics;

namespace Monaco.Presto.Diagnostics;

/// <summary>Per-timer diagnostics state, allocated only for roots registered while diagnostics are enabled.</summary>
internal sealed class TimerDiagnostics
{
    internal bool Announced;
    internal string? Origin;
    internal StackTrace? CreationTrace;
    internal Scope? Scope;
}
