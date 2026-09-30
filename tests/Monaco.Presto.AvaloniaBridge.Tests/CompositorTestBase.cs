using System.Runtime.InteropServices;
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

    /// <summary>Shows a red 40x20 bar at (30, 40) of a 100x100 window, ticked once so that it has a composition visual.</summary>
    protected static Border ShowBar()
    {
        var bar = new Border { Background = Brushes.Red, Width = 40, Height = 20 };
        Canvas.SetLeft(bar, 30);
        Canvas.SetTop(bar, 40);
        new Window { Content = new Canvas { Children = { bar } }, Background = Brushes.White, Width = 100, Height = 100 }.Show();
        Tick();
        return bar;
    }

    /// <summary>
    /// Long enough that neither the rendered move nor the mirror gets anywhere while the test looks at them, even in the
    /// first test of a run, whose first compositor frame also counts the JIT time since the frame before it.
    /// </summary>
    protected const double Held = 600000;

    protected static AnimationBuilder HeldMove => Presto.Build().Duration(Held).Ease(Eases.None);

    protected static AnimationBuilder SlideIn =>Presto.Build().X(from: 100).Duration(100).Ease("linear");

    /// <summary>Swaps <paramref name="host"/>'s content and lays its window out, which is when the host starts its page transition.</summary>
    protected static void Swap(TransitioningContentControl host, Control next)
    {
        host.Content = next;
        ((Window)TopLevel.GetTopLevel(host)!).UpdateLayout();
    }

    protected static double X(Visual visual) => Convert.ToDouble(PrestoAvalonia.Get(visual, "X"));

    /// <summary>Advances frames, running the dispatcher's jobs after each, until <paramref name="done"/> holds.</summary>
    protected void Settle(Func<bool> done) => RunUntil(() =>
    {
        Dispatcher.UIThread.RunJobs();
        return done();
    });

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

    /// <summary>The pixels of the last frame the window holding <paramref name="visual"/> rendered.</summary>
    protected static byte[] Capture(Visual visual) => Read(visual).Pixels;

    /// <summary>
    /// The mean difference per byte between two frames: zero while nothing moves, and rising as a visual fades into or slides
    /// over what is behind it, which is all a compositor animation leaves observable.
    /// </summary>
    protected static double Difference(byte[] first, byte[] second)
    {
        var total = 0L;
        for (var i = 0; i < first.Length; i++) total += Math.Abs(first[i] - second[i]);
        return total / (double)first.Length;
    }

    /// <summary>The box the pixels matching <paramref name="color"/> fill in the last rendered frame, or null when it has none.</summary>
    protected static Rect? Extent(Visual visual, Color color)
    {
        var (pixels, stride) = Read(visual);
        double left = int.MaxValue, top = int.MaxValue, right = -1, bottom = -1;
        for (var y = 0; y < pixels.Length / stride; y++)
        {
            for (var x = 0; x * 4 + 3 < stride; x++)
            {
                var i = (y * stride) + (x * 4);
                if (pixels[i] != color.R || pixels[i + 1] != color.G || pixels[i + 2] != color.B) continue;
                left = Math.Min(left, x);
                top = Math.Min(top, y);
                right = Math.Max(right, x + 1);
                bottom = Math.Max(bottom, y + 1);
            }
        }
        return right < 0 ? null : new Rect(left, top, right - left, bottom - top);
    }

    /// <summary>The box the red pixels of <paramref name="visual"/> fill in the last rendered frame.</summary>
    protected static Rect Box(Visual visual) =>
        Extent(visual, Colors.Red) ?? throw new InvalidOperationException("The red element is not in the rendered frame.");

    protected static double Top(Visual visual) => Box(visual).Top;

    /// <summary>The layout slot of <paramref name="control"/> in window coordinates.</summary>
    protected static Rect SlotOf(Control control) =>
        new(control.TranslatePoint(default, TopLevel.GetTopLevel(control)!)!.Value, control.Bounds.Size);

    /// <summary>The pixels of the last rendered frame inside <paramref name="box"/> that hold something other than <paramref name="background"/>.</summary>
    protected static int CountPixels(Visual visual, Rect box, Color background)
    {
        var (pixels, stride) = Read(visual);
        var clipped = box.Intersect(new Rect(visual.Bounds.Size));
        var count = 0;
        for (var y = (int)clipped.Y; y < (int)Math.Ceiling(clipped.Bottom); y++)
        {
            for (var x = (int)clipped.X; x < (int)Math.Ceiling(clipped.Right); x++)
            {
                var i = (y * stride) + (x * 4);
                if (pixels[i] != background.R || pixels[i + 1] != background.G || pixels[i + 2] != background.B) count++;
            }
        }
        return count;
    }

    private static (byte[] Pixels, int Stride) Read(Visual visual)
    {
        var window = TopLevel.GetTopLevel(visual)!;
        using var frame = window.GetLastRenderedFrame() ?? throw new InvalidOperationException("The window has not rendered yet.");
        using var locked = frame.Lock();
        var pixels = new byte[locked.RowBytes * locked.Size.Height];
        Marshal.Copy(locked.Address, pixels, 0, pixels.Length);
        return (pixels, locked.RowBytes);
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
