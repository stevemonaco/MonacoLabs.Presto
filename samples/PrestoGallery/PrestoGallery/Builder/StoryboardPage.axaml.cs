using System.Diagnostics;
using System.Globalization;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using MonacoLabs.Presto;
using PrestoGallery.Showcase;

namespace PrestoGallery.Builder;

/// <summary>
/// A keyframed hop built paused with <c>Autoplay(false)</c>, seeked by a slider and read back through its callbacks; a
/// settings change rebuilds it at the same time and play state.
/// </summary>
public partial class StoryboardPage : ShowcasePage
{
    private readonly Stopwatch _updateWindow = new();
    private int _updates;
    private bool _syncingScrub;

    public StoryboardPage() => InitializeComponent();

    public Animation? Animation { get; private set; }

    public int Loops { get; private set; }

    protected override void OnAttached() => Build();

    private void Build()
    {
        var time = Animation?.CurrentTime ?? 0;
        var playing = Animation is { Paused: false };
        Animation?.Revert();

        #region snippet StoryboardHop
        var playbackEase = (string)((ComboBoxItem)EaseBox.SelectedItem!).Content!;
        var fps = double.Parse((string)((ComboBoxItem)FpsBox.SelectedItem!).Content!, CultureInfo.InvariantCulture);
        var rate = Math.Round(RateSlider.Value, 2);
        Animation = Hopper.Animate(Presto.Build()
            .Keyframes(k => k
                .At(0, f => f.X(0).Y(0).Rotate(0))
                .At(30, f => f.X(150).Y(-90).Rotate(120))
                .At(50, f => f.X(250).Y(0).Rotate(180))
                .At(70, f => f.X(330).Y(-40).Rotate(270))
                .At(100, f => f.X(440).Y(0).Rotate(360)))
            .Duration(2400)
            .Ease(Eases.None)
            .PlaybackEase(playbackEase)
            .FrameRate(fps)
            .PlaybackRate(rate)
            .Loop()
            .Autoplay(false));
        #endregion
        Animation.OnUpdate = _ => ShowProgress();
        Animation.OnLoop = _ => LoopsText.Text = (++Loops).ToString(CultureInfo.InvariantCulture);

        Animation.Seek(time);
        if (playing) Animation.Play();
        PlayButton.Content = playing ? "Pause" : "Play";
    }

    private void ShowProgress()
    {
        var animation = Animation!;
        TimeText.Text = animation.CurrentTime.ToString("0", CultureInfo.InvariantCulture);
        ProgressText.Text = animation.IterationProgress.ToString("0.00", CultureInfo.InvariantCulture);
        _syncingScrub = true;
        ScrubSlider.Value = animation.IterationProgress;
        _syncingScrub = false;

        _updates++;
        if (!_updateWindow.IsRunning) _updateWindow.Start();
        if (_updateWindow.ElapsedMilliseconds < 1000) return;
        UpdatesText.Text = _updates.ToString(CultureInfo.InvariantCulture);
        _updates = 0;
        _updateWindow.Restart();
    }

    private void TogglePlay(object? sender, RoutedEventArgs e)
    {
        if (Animation is not { } animation) return;
        if (animation.Paused) animation.Play();
        else animation.Pause();
        PlayButton.Content = animation.Paused ? "Play" : "Pause";
    }

    private void Restart(object? sender, RoutedEventArgs e)
    {
        Animation?.Restart();
        PlayButton.Content = "Pause";
    }

    private void Scrub(object? sender, RangeBaseValueChangedEventArgs e)
    {
        if (_syncingScrub || Animation is not { } animation) return;
        animation.Pause();
        animation.IterationProgress = e.NewValue;
        PlayButton.Content = "Play";
    }

    private void SettingChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (this.IsAttachedToVisualTree()) Build();
    }

    private void RateChanged(object? sender, RangeBaseValueChangedEventArgs e)
    {
        if (this.IsAttachedToVisualTree()) Build();
    }
}
