using System;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Monaco.Presto;
using Monaco.Presto.AvaloniaBridge;

namespace PrestoSample.Pages;

public partial class TimelinePage : UserControl
{
    private Timeline? _timeline;
    private Animation? _synced;
    private bool _syncingSlider;
    private int _callHits;

    public TimelinePage() => InitializeComponent();

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        _synced = SyncedSquare.Animate(new()
        {
            ["X"] = 300,
            ["Background"] = "#ff6b6b",
            Duration = 1500,
            Autoplay = false,
        });

        _timeline = Presto.CreateTimeline(new()
        {
            Defaults = new() { Duration = 600, Ease = Eases.OutQuad },
            Autoplay = false,
            OnUpdate = _ => UpdateReadouts(),
        })
            .Add(AbsoluteSquare, new() { ["X"] = 300 }, 0)
            .Add(AppendSquare, new() { ["X"] = 300 })
            .Add(RelativeSquare, new() { ["X"] = 300 }, "-=400")
            .Add(PreviousEndSquare, new() { ["X"] = 300 }, "<")
            .Add(PreviousStartSquare, new() { ["X"] = 300 }, "<<")
            .Label("flash", "+=200")
            .Add(LabelSquare, new() { ["X"] = 300, ["Rotate"] = 360 }, "flash")
            .Add(new TimerParams
            {
                Duration = 400,
                OnUpdate = timer => TimerChildText.Text = timer.CurrentTime.ToString("0", CultureInfo.InvariantCulture),
            }, 20)
            .Call(_ => CallText.Text = $"{++_callHits} hits", "flash")
            .Sync(_synced, 0);

        UpdateReadouts();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _timeline?.Revert();
        _timeline = null;
        _synced?.Revert();
        _synced = null;
    }

    private void Play(object? sender, RoutedEventArgs e) => Run(static t => t.Play());

    private void Pause(object? sender, RoutedEventArgs e) => Run(static t => t.Pause());

    private void Restart(object? sender, RoutedEventArgs e) => Run(static t => t.Restart());

    private void Reverse(object? sender, RoutedEventArgs e) => Run(static t => t.Reverse());

    private void Scrub(object? sender, RangeBaseValueChangedEventArgs e)
    {
        if (_syncingSlider) return;
        Run(t => t.Progress = e.NewValue);
    }

    private void Run(Action<Timeline> control)
    {
        if (_timeline == null) return;
        control(_timeline);
        UpdateReadouts();
    }

    private void UpdateReadouts()
    {
        if (_timeline is not { } timeline) return;
        ReadoutText.Text = string.Create(CultureInfo.InvariantCulture,
            $"{timeline.CurrentTime,6:0} / {timeline.Duration:0} ms · flash = {timeline.Labels["flash"]:0} ms");

        _syncingSlider = true;
        ScrubSlider.Value = timeline.Progress;
        _syncingSlider = false;
    }
}
