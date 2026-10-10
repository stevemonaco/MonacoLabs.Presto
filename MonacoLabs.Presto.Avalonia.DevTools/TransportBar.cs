using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;

namespace MonacoLabs.Presto.DevTools;

/// <summary>Time controls for <see cref="Presto.Engine"/> (freeze, step, dilation) and the latest frame's statistics.</summary>
internal sealed class TransportBar : StackPanel
{
    private static readonly double[] s_dilations = [0.1, 0.25, 0.5, 1, 2];

    private readonly Sparkline _sparkline = new() { Width = 120, Height = 18 };
    private readonly TextBlock _counters = new() { VerticalAlignment = VerticalAlignment.Center, FontSize = 12 };
    private readonly TextBlock _speed = new() { VerticalAlignment = VerticalAlignment.Center, FontSize = 12, Opacity = 0.7 };
    private readonly TextBlock _hidden = new() { VerticalAlignment = VerticalAlignment.Center, FontSize = 12, Text = "hidden · paused" };
    private double _shownFrame = double.NaN;
    private double _shownSpeed = double.NaN;

    internal TransportBar()
    {
        Orientation = Orientation.Horizontal;
        Spacing = 4;
        Margin = new Thickness(6, 4);
        Freeze.Click += (_, _) =>
        {
            if (Presto.Engine.Frozen) Presto.Engine.Thaw();
            else Presto.Engine.Freeze();
            Refresh();
        };
        Step.Click += (_, _) =>
        {
            Presto.Engine.Freeze().Step();
            Refresh();
        };
        Children.Add(Freeze);
        Children.Add(Step);
        Presets = new ToggleButton[s_dilations.Length];
        for (var i = 0; i < s_dilations.Length; i++)
        {
            var dilation = s_dilations[i];
            var preset = Presets[i] = DevToolsOverlay.Compact(new ToggleButton { Content = $"{dilation}×" });
            preset.Click += (_, _) =>
            {
                Presto.Engine.TimeDilation = dilation;
                Refresh();
            };
            Children.Add(preset);
        }
        Children.Add(new Border { Child = _sparkline, Margin = new Thickness(8, 0), VerticalAlignment = VerticalAlignment.Center });
        Children.Add(_counters);
        Children.Add(_speed);
        Children.Add(_hidden);
        Refresh();
    }

    internal ToggleButton Freeze { get; } = DevToolsOverlay.Compact(new ToggleButton { Content = "Freeze" });

    internal RepeatButton Step { get; } = DevToolsOverlay.Compact(new RepeatButton { Content = "Step", Delay = 400, Interval = 1000 / 60 });

    internal ToggleButton[] Presets { get; }

    /// <summary>Syncs the controls with the engine and, when it has run a frame since the last call, samples that frame.</summary>
    internal void Refresh()
    {
        var engine = Presto.Engine;
        Freeze.IsChecked = engine.Frozen;
        _hidden.IsVisible = engine.Hidden && engine.PauseOnHidden;
        for (var i = 0; i < Presets.Length; i++) Presets[i].IsChecked = engine.TimeDilation == s_dilations[i];
        if (engine.Speed != _shownSpeed)
        {
            _shownSpeed = engine.Speed;
            _speed.Text = $"speed {_shownSpeed:0.##}×";
        }
        ref readonly var frame = ref engine.Diagnostics.LastFrame;
        if (frame.EngineTime == _shownFrame) return;
        _shownFrame = frame.EngineTime;
        var milliseconds = frame.UpdateDuration.TotalMilliseconds;
        _sparkline.Push(milliseconds);
        _counters.Text = $"{frame.RootsTicked} roots · {frame.TweensRendered} tweens · {frame.ValuesStepped} springs · {milliseconds:0.00} ms";
    }
}
