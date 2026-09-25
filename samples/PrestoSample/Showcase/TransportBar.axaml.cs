using System.Globalization;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Monaco.Presto;
using Timer = Monaco.Presto.Timer;

namespace PrestoSample.Showcase;

/// <summary>Playback controls for any <see cref="Timer"/>, a timeline included: transport buttons, a scrubber, a speed slider and a duration slider.</summary>
/// <remarks>
/// The bar follows the timer through its <see cref="Timer.OnUpdate"/>, chained after any handler already there, and
/// Loop sets its <see cref="Timer.Loop"/>; both are put back when the bar is given another timer. The scrubber and the
/// readout track the current iteration, so they keep sweeping while the timer loops forever.
/// </remarks>
public partial class TransportBar : UserControl
{
    private Timer? _timer;
    private Action<Timer>? _baseOnUpdate;
    private LoopCount _baseLoop;
    private double _baseDuration;
    private bool _scrubbing;
    private bool _syncing;

    public TransportBar()
    {
        InitializeComponent();
        // Tunnelling: the thumb handles both events and captures the pointer, so neither one bubbles back up here.
        Scrubber.AddHandler(PointerPressedEvent, (_, _) => _scrubbing = true, RoutingStrategies.Tunnel);
        Scrubber.AddHandler(PointerReleasedEvent, (_, _) => _scrubbing = false, RoutingStrategies.Tunnel);
    }

    /// <summary>The timer the bar drives; the duration slider scales whatever duration it has when it is set here.</summary>
    public Timer? Timer
    {
        get => _timer;
        set
        {
            if (_timer is { } previous)
            {
                previous.OnUpdate = _baseOnUpdate;
                previous.Loop = _baseLoop;
            }
            _timer = value;
            _baseDuration = value?.Duration ?? 0;
            _baseLoop = value?.Loop ?? 0;
            _baseOnUpdate = value?.OnUpdate;
            StretchSlider.Value = 1;
            SpeedSlider.Value = value?.Speed ?? 1;
            if (value is null) return;
            value.OnUpdate = timer =>
            {
                _baseOnUpdate?.Invoke(timer);
                Sync(timer);
            };
            if (LoopToggle.IsChecked == true) value.Loop = LoopCount.Infinite;
        }
    }

    private void Sync(Timer timer)
    {
        if (!_scrubbing)
        {
            _syncing = true;
            Scrubber.Value = timer.IterationProgress;
            _syncing = false;
        }
        TimeText.Text = string.Create(CultureInfo.InvariantCulture, $"{timer.IterationCurrentTime:0} ms · {timer.IterationProgress * 100:0}%");
    }

    private void Play(object? sender, RoutedEventArgs e) => _timer?.Play();

    private void Pause(object? sender, RoutedEventArgs e) => _timer?.Pause();

    private void Restart(object? sender, RoutedEventArgs e) => _timer?.Restart();

    private void Reverse(object? sender, RoutedEventArgs e) => _timer?.Reverse();

    private void Alternate(object? sender, RoutedEventArgs e) => _timer?.Alternate();

    private void LoopChanged(object? sender, RoutedEventArgs e)
    {
        if (_timer is not { } timer) return;
        var time = timer.CurrentTime;
        timer.Loop = LoopToggle.IsChecked == true ? LoopCount.Infinite : _baseLoop;
        // Left past the new end, the timer would complete on its next tick without rendering or updating the bar.
        if (time > timer.Duration) timer.CurrentTime = timer.Duration - timer.IterationDuration + (time % timer.IterationDuration);
        if (timer.Completed) timer.Restart();
    }

    private void Scrub(object? sender, RangeBaseValueChangedEventArgs e)
    {
        // IterationProgress reads the rendered position, which a reversed timer mirrors, but seeks the unmirrored one.
        if (!_syncing && _timer is { } timer) timer.IterationProgress = timer.Reversed ? 1 - e.NewValue : e.NewValue;
    }

    private void SpeedChanged(object? sender, RangeBaseValueChangedEventArgs e)
    {
        if (_timer is { } timer) timer.Speed = e.NewValue;
    }

    private void StretchChanged(object? sender, RangeBaseValueChangedEventArgs e)
    {
        if (_timer is not { } timer || !double.IsFinite(_baseDuration)) return;
        // Stretch keeps time, not progress, so rescale CurrentTime or the playhead jumps back (SAMPLES-PLAN §8);
        // it needs the finite base loop count to scale, so the live one is swapped out around the call.
        var loop = timer.Loop;
        var time = timer.CurrentTime;
        var iterationDuration = timer.IterationDuration;
        timer.Loop = _baseLoop;
        timer.Stretch(_baseDuration * e.NewValue);
        timer.Loop = loop;
        timer.CurrentTime = time * timer.IterationDuration / iterationDuration;
    }
}
