using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Monaco.Presto;
using Monaco.Presto.AvaloniaBridge;

namespace PrestoSample.Pages;

public partial class PlaybackSettingsPage : UserControl
{
    private Animation? _animation;

    public PlaybackSettingsPage() => InitializeComponent();

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Rebuild();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _animation?.Revert();
        _animation = null;
    }

    private void Rebuild()
    {
        _animation?.Revert();
        _animation = Square.Animate(new()
        {
            ["X"] = 320,
            ["Rotate"] = 180,
            ["Background"] = "#f5c542",
            Duration = DurationSlider.Value,
            Delay = DelaySlider.Value,
            Loop = InfiniteCheck.IsChecked == true ? LoopCount.Infinite : (int)LoopSlider.Value,
            LoopDelay = LoopDelaySlider.Value,
            Alternate = AlternateCheck.IsChecked == true,
            Reversed = ReversedCheck.IsChecked == true,
            Autoplay = AutoplayCheck.IsChecked == true,
            FrameRate = FrameRateSlider.Value,
            PlaybackRate = PlaybackRateSlider.Value,
            Ease = Eases.InOutQuad,
            OnUpdate = animation => StatusText.Text = string.Create(
                CultureInfo.InvariantCulture, $"{animation.IterationProgress * 100:0}% · iteration {animation.CurrentIteration}"),
        });
    }

    private void SettingChanged(object? sender, RangeBaseValueChangedEventArgs e) => Rebuild();

    private void ToggleChanged(object? sender, RoutedEventArgs e) => Rebuild();

    private void FrameRateChanged(object? sender, RangeBaseValueChangedEventArgs e)
    {
        if (_animation != null) _animation.Fps = FrameRateSlider.Value;
    }

    private void PlaybackRateChanged(object? sender, RangeBaseValueChangedEventArgs e)
    {
        if (_animation != null) _animation.Speed = PlaybackRateSlider.Value;
    }

    private void Play(object? sender, RoutedEventArgs e) => _animation?.Play();

    private void Pause(object? sender, RoutedEventArgs e) => _animation?.Pause();

    private void Restart(object? sender, RoutedEventArgs e) => _animation?.Restart();
}
