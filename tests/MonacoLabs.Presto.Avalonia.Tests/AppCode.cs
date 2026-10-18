using System.Runtime.CompilerServices;
using MonacoLabs.Presto;

namespace PrestoTestApp;

/// <summary>Creates timers from outside the <c>MonacoLabs.Presto</c> namespaces, as an app does, for creation-trace tests.</summary>
internal static class AppCode
{
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static MonacoLabs.Presto.Timer CreateTimer() => Presto.CreateTimer(new TimerParams { Duration = 1000 });
}
