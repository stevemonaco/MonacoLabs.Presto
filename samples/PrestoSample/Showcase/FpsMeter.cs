using System.Globalization;
using Avalonia.Controls;
using Avalonia.Layout;
using Monaco.Presto;
using Monaco.Presto.AvaloniaBridge;
using Timer = Monaco.Presto.Timer;

namespace PrestoSample.Showcase;

/// <summary>
/// Two frame rates side by side: the UI thread's, counted from an uncapped timer on the shared engine, and the render
/// thread's, which a <c>CompositorScene</c> measures on its own engine and pushes in through <see cref="SceneFps"/>.
/// The two are independent on purpose — a frozen UI thread stalls the first and leaves the second climbing.
/// </summary>
public sealed class FpsMeter : UserControl
{
    private const double WindowMs = 500;

    private readonly TextBlock _uiText = new() { Classes = { "code" }, Text = Format("ui", 0) };
    private readonly TextBlock _sceneText = new() { Classes = { "code" }, IsVisible = false };

    private double _windowStart;
    private int _frames;

    public FpsMeter()
    {
        Content = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12, Children = { _uiText, _sceneText } };
        PrestoAvalonia.CreateScope(this).Execute(_ => Presto.CreateTimer(new() { OnUpdate = Measure }));
    }

    /// <summary>The render thread's frame rate, as a scene posts it with <c>PostToUI</c>; the readout appears once one does.</summary>
    public double SceneFps
    {
        set
        {
            _sceneText.Text = Format("scene", value);
            _sceneText.IsVisible = true;
        }
    }

    /// <summary>
    /// Counts one frame and, once <paramref name="windowStart"/> is a measuring window behind
    /// <paramref name="timer"/>, reports the rate over that window and opens the next one.
    /// </summary>
    public static bool TrySample(Timer timer, ref int frames, ref double windowStart, out double fps)
    {
        frames++;
        fps = 0;
        var elapsed = timer.CurrentTime - windowStart;
        if (elapsed < WindowMs) return false;
        fps = frames * 1000 / elapsed;
        frames = 0;
        windowStart = timer.CurrentTime;
        return true;
    }

    /// <summary>A rate as the meter writes it, for a scene drawing the same readout itself.</summary>
    public static string Format(string label, double fps) => string.Create(CultureInfo.InvariantCulture, $"{label} {fps:0} fps");

    private void Measure(Timer timer)
    {
        if (TrySample(timer, ref _frames, ref _windowStart, out var fps)) _uiText.Text = Format("ui", fps);
    }
}
