using System.Globalization;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Monaco.Presto;
using Monaco.Presto.AvaloniaBridge;
using Timer = Monaco.Presto.Timer;

namespace PrestoSample.Showcase;

/// <summary>Playback controls for any <see cref="Timer"/>, a timeline included: transport buttons, a scrubber, a speed slider and a duration slider.</summary>
/// <remarks>
/// The bar reads the timer from a timer of its own, because <see cref="Timer.OnUpdate"/> and
/// <see cref="Timer.OnComplete"/> can only be set when a timer is created (SAMPLES-PLAN §8).
/// </remarks>
public partial class TransportBar : UserControl
{
    private Timer? _timer;
    private double _baseDuration;
    private bool _scrubbing;
    private bool _syncing;

    public TransportBar()
    {
        InitializeComponent();
        // Tunnelling: the thumb handles both events and captures the pointer, so neither one bubbles back up here.
        Scrubber.AddHandler(PointerPressedEvent, (_, _) => _scrubbing = true, RoutingStrategies.Tunnel);
        Scrubber.AddHandler(PointerReleasedEvent, (_, _) => _scrubbing = false, RoutingStrategies.Tunnel);
        PrestoAvalonia.CreateScope(this).Execute(_ => Presto.CreateTimer(new() { OnUpdate = _ => Sync() }));
    }

    /// <summary>The timer the bar drives; the duration slider scales whatever duration it has when it is set here.</summary>
    public Timer? Timer
    {
        get => _timer;
        set
        {
            _timer = value;
            _baseDuration = value?.Duration ?? 0;
            StretchSlider.Value = 1;
            SpeedSlider.Value = value?.Speed ?? 1;
        }
    }

    private void Sync()
    {
        if (_timer is not { } timer) return;
        if (LoopToggle.IsChecked == true && timer.Completed) timer.Restart();
        if (!_scrubbing)
        {
            _syncing = true;
            Scrubber.Value = timer.Progress;
            _syncing = false;
        }
        TimeText.Text = string.Create(CultureInfo.InvariantCulture, $"{timer.CurrentTime:0} ms · {timer.Progress * 100:0}%");
    }

    private void Play(object? sender, RoutedEventArgs e) => _timer?.Play();

    private void Pause(object? sender, RoutedEventArgs e) => _timer?.Pause();

    private void Restart(object? sender, RoutedEventArgs e) => _timer?.Restart();

    private void Reverse(object? sender, RoutedEventArgs e) => _timer?.Reverse();

    private void Alternate(object? sender, RoutedEventArgs e) => _timer?.Alternate();

    private void Scrub(object? sender, RangeBaseValueChangedEventArgs e)
    {
        if (!_syncing && _timer is { } timer) timer.Progress = e.NewValue;
    }

    private void SpeedChanged(object? sender, RangeBaseValueChangedEventArgs e)
    {
        if (_timer is { } timer) timer.Speed = e.NewValue;
    }

    private void StretchChanged(object? sender, RangeBaseValueChangedEventArgs e)
    {
        if (_timer is not { } timer || !double.IsFinite(_baseDuration)) return;
        // Stretch keeps the current time rather than the progress, so the playhead would land on an earlier beat of the
        // retimed sequence and the picture would jump backwards (SAMPLES-PLAN §8).
        var progress = timer.Progress;
        timer.Stretch(_baseDuration * e.NewValue);
        timer.Progress = progress;
    }
}
