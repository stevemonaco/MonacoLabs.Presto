using System.Diagnostics;
using System.Globalization;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using MonacoLabs.Presto;
using PrestoGallery.Showcase;

namespace PrestoGallery.Builder;

/// <summary>
/// A looping scene of hops, landings, an equalizer, a turning sun and a swooping comet, built as one paused timeline that a
/// slider seeks and its callbacks read back; a spring trail chases the comet outside the timeline, so it keeps settling while
/// the timeline is paused or scrubbed. A settings change rebuilds the timeline at the same time and play state.
/// </summary>
public partial class StoryboardPage : ShowcasePage
{
    private const int TrailLength = 8;

    private readonly Stopwatch _updateWindow = new();
    private readonly MotionValue _cometX;
    private readonly MotionValue _cometY;
    private int _updates;
    private bool _syncingScrub;

    public StoryboardPage()
    {
        InitializeComponent();
        for (var i = 0; i < TrailLength; i++)
        {
            var size = 14 - i * 1.25;
            Sky.Children.Insert(0, new Ellipse { Width = size, Height = size, Fill = Palette.Violet, Opacity = .7 - i * .08 });
        }

        #region snippet StoryboardTrail
        _cometX = Presto.CreateMotionValue();
        _cometY = Presto.CreateMotionValue();
        var xs = _cometX.Trail(TrailLength, Springs.Snappy);
        var ys = _cometY.Trail(TrailLength, Springs.Snappy);
        for (var i = 0; i < TrailLength; i++)
        {
            var dot = Sky.Children[TrailLength - 1 - i];
            Follow.SetX(dot, xs[i]);
            Follow.SetY(dot, ys[i]);
        }
        #endregion
        CodeText.Code = $"{Snippets.StoryboardTrail}\n\n{Snippets.StoryboardScene}";
    }

    public Timeline? Timeline { get; private set; }

    public int Loops { get; private set; }

    protected override void OnAttached() => Build();

    private void Build()
    {
        var time = Timeline?.CurrentTime ?? 0;
        var playing = Timeline is { Paused: false };
        Timeline?.Revert();

        #region snippet StoryboardScene
        const double beat = 1200;
        var playbackEase = (string)((ComboBoxItem)EaseBox.SelectedItem!).Content!;
        var fps = double.Parse((string)((ComboBoxItem)FpsBox.SelectedItem!).Content!, CultureInfo.InvariantCulture);
        var rate = Math.Round(RateSlider.Value, 2);
        var flash = Presto.Build()
            .ScaleX(k => k.To(1.6, 100).To(1, 500, ease: Eases.OutQuad))
            .Opacity(k => k.To(1, 100).To(.4, 500));
        var pulse = Presto.Build()
            .ScaleY(k => k.To(1, 140, ease: Eases.OutQuad).To(.25, 700, ease: Eases.InOutQuad));

        Timeline = Presto.CreateTimeline(new()
            {
                PlaybackEase = playbackEase,
                FrameRate = fps,
                PlaybackRate = rate,
                Loop = true,
                Autoplay = false,
                OnRender = _ =>
                {
                    _cometX.Set(Get(Comet, "X"));
                    _cometY.Set(Get(Comet, "Y"));
                },
            })
            .Set(Bars.Children, b => b.ScaleY(.25), 0)
            .Add(Hopper, b => b
                .X(k => k.To(250, beat).To(440, beat).To(440, beat / 2).To(0, 1.5 * beat))
                .Y(k => k
                    .To(-90, beat / 2, ease: Eases.OutQuad).To(0, beat / 2, ease: Eases.InQuad)
                    .To(-50, beat / 2, ease: Eases.OutQuad).To(0, beat / 2, ease: Eases.InQuad)
                    .To(0, beat / 2)
                    .To(-140, .75 * beat, ease: Eases.OutQuad).To(0, .75 * beat, ease: Eases.InQuad))
                .Rotate(k => k.To(180, beat).To(360, beat).To(360, beat / 2).To(0, 1.5 * beat))
                .Ease(Eases.None), 0)
            .Add(PadA, flash, 0)
            .Add(PadB, flash, beat)
            .Add(Bars.Children, pulse, Presto.Stagger(60, new() { Start = beat, From = StaggerFrom.Center }))
            .Add(PadC, flash, 2 * beat)
            .Add(Hopper, b => b.ScaleY(k => k.To(.6, 120).To(1, 480, ease: Eases.OutElastic())), 2 * beat)
            .Add(Bars.Children, pulse, Presto.Stagger(60, new() { Start = 2 * beat, From = StaggerFrom.Center }))
            .Add(Sun, b => b.Scale(k => k.To(1.4, 150).To(1, 600, ease: Eases.OutBack())), 2 * beat)
            .Add(Sun, b => b.Rotate(from: 0, to: 360).Duration(4 * beat).Ease(Eases.None), 0)
            .Add(Comet, b => b
                .X(k => k.To(460, 3 * beat, ease: Eases.InOutSine).To(0, beat, ease: Eases.InOutCubic))
                .Y(k => k.To(-30, .75 * beat).To(30, .75 * beat).To(-30, .75 * beat).To(30, .75 * beat).To(0, beat))
                .Ease(Eases.InOutSine), 0);
        #endregion
        Timeline.OnUpdate = _ => ShowProgress();
        Timeline.OnLoop = _ => LoopsText.Text = (++Loops).ToString(CultureInfo.InvariantCulture);

        Timeline.Seek(time);
        if (playing) Timeline.Play();
        PlayButton.Content = playing ? "Pause" : "Play";
    }

    private static double Get(Control control, string property) =>
        Convert.ToDouble(Presto.Get(control, property, withUnit: false), CultureInfo.InvariantCulture);

    private void ShowProgress()
    {
        var timeline = Timeline!;
        TimeText.Text = timeline.CurrentTime.ToString("0", CultureInfo.InvariantCulture);
        ProgressText.Text = timeline.IterationProgress.ToString("0.00", CultureInfo.InvariantCulture);
        _syncingScrub = true;
        ScrubSlider.Value = timeline.IterationProgress;
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
        if (Timeline is not { } timeline) return;
        if (timeline.Paused) timeline.Play();
        else timeline.Pause();
        PlayButton.Content = timeline.Paused ? "Play" : "Pause";
    }

    private void Restart(object? sender, RoutedEventArgs e)
    {
        Timeline?.Restart();
        PlayButton.Content = "Pause";
    }

    private void Scrub(object? sender, RangeBaseValueChangedEventArgs e)
    {
        if (_syncingScrub || Timeline is not { } timeline) return;
        timeline.Pause();
        timeline.IterationProgress = e.NewValue;
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
