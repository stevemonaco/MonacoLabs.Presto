using System.Reflection;
using System.Runtime.ExceptionServices;

namespace MonacoLabs.Presto.Tests;

/// <summary>
/// Stops the headless compositor drawing windows into their framebuffers, most of the sample suites' time, while it still
/// applies batches, runs compositor animations and updates the server-side scene that tests read.
/// </summary>
/// <remarks>
/// A window gets a render target only once the compositor's render interface context says it can create one, and the
/// compositor updates a target's scene whether or not it has one. Like <see cref="CompositorClock"/>, each lookup throws
/// naming a missing member.
/// </remarks>
internal static class HeadlessDrawing
{
    private static DrawGate? s_gate;

    /// <summary>Keeps every window of this test's session that has not rendered yet from drawing, until <see cref="Resume"/>.</summary>
    public static void Suspend()
    {
        var server = CompositorClock.Server();
        var manager = CompositorClock.Property(server.GetType(), "RenderInterface").GetValue(server)!;
        var backend = CompositorClock.Property(manager.GetType(), "Value").GetValue(manager)!;
        if (backend is DrawGate) return;
        const string context = "Avalonia.Platform.IPlatformRenderInterfaceContext";
        var contract = backend.GetType().GetInterface(context) ?? throw new InvalidOperationException(
            $"HeadlessDrawing wraps {context}, which {backend.GetType().FullName} no longer implements.");
        s_gate = (DrawGate)DispatchProxy.Create(contract, typeof(DrawGate));
        s_gate.Inner = backend;
        CompositorClock.Field(manager.GetType(), "_backend").SetValue(manager, s_gate);
    }

    /// <summary>Lets this session's windows draw from their next render.</summary>
    /// <returns>Whether drawing was suspended, in which case no frame shows the current scene yet.</returns>
    public static bool Resume()
    {
        if (s_gate is not { Draws: false } gate) return false;
        gate.Draws = true;
        return true;
    }

    internal class DrawGate : DispatchProxy
    {
        public object Inner { get; set; } = null!;

        public bool Draws { get; set; }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (!Draws && targetMethod!.Name == "IsReadyToCreateRenderTarget") return false;
            try
            {
                return targetMethod!.Invoke(Inner, args);
            }
            catch (TargetInvocationException e) when (e.InnerException is not null)
            {
                ExceptionDispatchInfo.Throw(e.InnerException);
                throw;
            }
        }
    }
}
