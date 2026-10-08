using System.Globalization;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Monaco.Presto;
using Monaco.Presto.AvaloniaBridge;
using PrestoSample.Showcase;

namespace PrestoSample.Pages;

public partial class PlaybackSettingsPage : ShowcasePage
{
    private Animation? _animation;
    private int _clicks;

    public PlaybackSettingsPage() => InitializeComponent();

    protected override void OnAttached() => Rebuild();

    private void Rebuild()
    {
        _animation?.Revert();
        _animation = ClickButton.Animate(new()
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

    private void SettingChanged(object? sender, RangeBaseValueChangedEventArgs e) => Scope.Execute(_ => Rebuild());

    private void ToggleChanged(object? sender, RoutedEventArgs e) => Scope.Execute(_ => Rebuild());

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

    private void Clicked(object? sender, RoutedEventArgs e) =>
        ClickText.Text = string.Create(CultureInfo.InvariantCulture, $"Click me · {++_clicks}");
}
