using System.Diagnostics;
using System.Globalization;
using Avalonia;
using Avalonia.Media;
using MonacoLabs.Presto;
using MonacoLabs.Presto.Compositor;
using Timer = MonacoLabs.Presto.Timer;

namespace PrestoSample.Showcase.Pages;

/// <summary>
/// The SAMPLES-PLAN §4.9 frame-time graph: the UI thread's last <see cref="Capacity"/> frame times as bars, with their
/// mean and 95th percentile, and the render thread's own fps. It is drawn on the render thread, so while the UI thread is
/// frozen the newest bar keeps growing with the frame that has not arrived.
/// </summary>
public sealed class FrameGraphScene : CompositorScene
{
    public const int Capacity = 240;

    private const double CeilingMs = 50;
    private const double SlowMs = 1000 / 30d;
    private const double SmoothMs = 1000 / 60d;
    private const double Inset = 8;

    private readonly double[] _samples = new double[Capacity];
    private readonly double[] _sorted = new double[Capacity];
    private readonly SceneText _text = new(12);

    private int _count;
    private int _next;
    private long _lastSample = Stopwatch.GetTimestamp();
    private int _frames;
    private double _windowStart;
    private string _fps = FpsMeter.Format("render", 0);
    private string _stats = "";

    /// <summary>The mean of the graphed frame times, in milliseconds.</summary>
    public double Mean { get; private set; }

    /// <summary>The nearest-rank 95th percentile of the graphed frame times, in milliseconds.</summary>
    public double P95 { get; private set; }

    /// <inheritdoc/>
    protected override void OnAttached() => Presto.CreateTimer(new TimerParams { OnUpdate = Measure });

    /// <inheritdoc/>
    protected override void OnMessage(object message)
    {
        if (message is not FrameTime(var ms)) return;
        _lastSample = Stopwatch.GetTimestamp();
        _samples[_next] = ms;
        _next = (_next + 1) % Capacity;
        _count = Math.Min(_count + 1, Capacity);

        Array.Copy(_samples, _sorted, _count);
        Array.Sort(_sorted, 0, _count);
        var sum = 0d;
        for (var i = 0; i < _count; i++) sum += _sorted[i];
        Mean = sum / _count;
        P95 = _sorted[(int)Math.Ceiling(_count * .95) - 1];
    }

    /// <inheritdoc/>
    protected override void OnRender(ImmediateDrawingContext context, Size size)
    {
        context.FillRectangle(Brush(Palette.Background.Color), new Rect(size), 0);
        var plot = new Rect(Inset, _text.Height + Inset, size.Width - (2 * Inset), size.Height - _text.Height - (2 * Inset));
        var bar = plot.Width / (Capacity + 1);
        double Top(double ms) => plot.Bottom - (Math.Min(ms, CeilingMs) / CeilingMs * plot.Height);

        void Gridline(double ms, string label)
        {
            context.FillRectangle(Brush(Palette.Border.Color), new Rect(plot.X, Top(ms), plot.Width, 1), 0);
            _text.Draw(context, label, new Point(plot.X, Top(ms) - _text.Height), Brush(Palette.InkDim.Color));
        }

        for (var i = 0; i < _count; i++)
        {
            var ms = _samples[(_next - _count + i + Capacity) % Capacity];
            var x = plot.Right - ((_count - i + 1) * bar);
            context.FillRectangle(Brush(Tint(ms)), new Rect(x, Top(ms), Math.Max(1, bar - .5), plot.Bottom - Top(ms)), 0);
        }

        Gridline(SmoothMs, "60 fps");
        Gridline(SlowMs, "30 fps");

        var pending = Stopwatch.GetElapsedTime(_lastSample).TotalMilliseconds;
        if (pending > SlowMs) context.FillRectangle(Brush(Palette.Coral.Color), new Rect(plot.Right - bar, Top(pending), bar, plot.Bottom - Top(pending)), 0);

        _text.Draw(context, _fps, new Point(Inset, Inset / 2), Brush(Palette.InkDim.Color));
        _text.Draw(context, _stats, new Point(size.Width - Inset - _text.Measure(_stats), Inset / 2), Brush(Palette.Ink.Color));
    }

    private void Measure(Timer timer)
    {
        if (!FpsMeter.TrySample(timer, ref _frames, ref _windowStart, out var fps)) return;
        _fps = FpsMeter.Format("render", fps);
        _stats = string.Create(CultureInfo.InvariantCulture, $"ui frame  mean {Mean:0.0} ms  p95 {P95:0.0} ms");
    }

    private static Color Tint(double ms) => ms <= SmoothMs + 1 ? Palette.Aqua.Color : ms <= SlowMs + 1 ? Palette.AccentBright.Color : Palette.Coral.Color;

    /// <summary>One UI-thread frame time, in milliseconds.</summary>
    public sealed record FrameTime(double Ms);
}
