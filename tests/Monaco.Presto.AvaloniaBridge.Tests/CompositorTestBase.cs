using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media;
using Avalonia.Threading;
using Monaco.Presto.AvaloniaBridge.Compositor;

namespace Monaco.Presto.AvaloniaBridge.Tests;

/// <summary>
/// Drives compositor frames from a headless test: the render timer runs a whole compositor frame synchronously on the
/// UI thread, and scene time is the compositor's own stopwatch, so the pumps let real time pass between frames.
/// </summary>
public abstract class CompositorTestBase : BridgeTestBase
{
    /// <summary>Shows <paramref name="canvas"/> in a window and ticks once, which is when the batched attach message reaches the handler.</summary>
    protected static (StackPanel Panel, CompositorCanvas Canvas) Show(CompositorCanvas canvas)
    {
        var panel = new StackPanel { Children = { canvas } };
        new Window { Content = panel, Width = 100, Height = 100 }.Show();
        Tick();
        return (panel, canvas);
    }

    /// <inheritdoc cref="Show"/>
    protected static (StackPanel Panel, CompositorCanvas Canvas) ShowScene(CompositorScene scene) =>
        Show(new CompositorCanvas { Scene = scene, Width = 64, Height = 64 });

    /// <summary>Commits what the UI thread has queued for the compositor, as a real frame does, then runs one compositor frame.</summary>
    protected static void Tick()
    {
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
    }

    /// <summary>Ticks until <paramref name="until"/> holds or <paramref name="rounds"/> run out.</summary>
    protected static async Task<bool> Pump(Func<bool> until, int rounds = 120)
    {
        for (var i = 0; i < rounds && !until(); i++)
        {
            Tick();
            await Task.Delay(8);
        }
        return until();
    }

    /// <summary>
    /// Records what the render thread hands the scene, and animates a plain target the way a real scene would, so later
    /// frames are observable.
    /// </summary>
    protected sealed class ProbeScene(double duration = 400, bool throwOnAttached = false) : CompositorScene
    {
        internal const string Boom = "boom";

        public SceneTarget Target { get; } = new();

        public List<object> Messages { get; } = [];

        public Engine? InvokedOn { get; set; }

        public Engine? AmbientEngine { get; private set; }

        public int Attachments { get; private set; }

        public Size? Rendered { get; private set; }

        public void RelayToUI(Action action) => PostToUI(action);

        protected override void OnAttached()
        {
            Attachments++;
            AmbientEngine = Engine.Current;
            Presto.Animate(Target, new() { ["Opacity"] = 0, Duration = duration, Ease = "linear" });
            if (throwOnAttached) throw new InvalidOperationException(Boom);
        }

        protected override void OnMessage(object message)
        {
            if (message is Boom) throw new InvalidOperationException(Boom);
            Messages.Add(message);
        }

        protected override void OnRender(ImmediateDrawingContext context, Size size) => Rendered = size;
    }

    protected sealed class SceneTarget
    {
        public double Opacity { get; set; } = 1;
    }
}
