using System.Globalization;
using Avalonia.Interactivity;
using MonacoLabs.Presto;
using PrestoSample.Showcase;

namespace PrestoSample.Pages;

public partial class TimelinePage : ShowcasePage
{
    private readonly SliderSync _scrub;
    private Timeline? _timeline;
    private int _callHits;

    public TimelinePage()
    {
        InitializeComponent();
        _scrub = new SliderSync(ScrubSlider, () => _timeline, UpdateReadouts);
    }

    protected override void OnAttached()
    {
        #region snippet TimelinePageChain
        var synced = SyncedButton.Animate(new()
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
            .Add(AbsoluteButton, new() { ["X"] = 300 }, 0)
            .Add(AppendToggle, new() { ["X"] = 300 })
            .Add(RelativeSlider, new() { ["X"] = 300 }, "-=400")
            .Add(PreviousEndTextBox, new() { ["X"] = 300 }, "<")
            .Add(PreviousStartCheck, new() { ["X"] = 300 }, "<<")
            .Label("flash", "+=200")
            .Add(LabelButton, new() { ["X"] = 300, ["Rotate"] = 360 }, "flash")
            .Add(new TimerParams
            {
                Duration = 400,
                OnUpdate = timer => TimerChildText.Text = timer.CurrentTime.ToString("0", CultureInfo.InvariantCulture),
            }, 20)
            .Call(_ => CallText.Text = $"{++_callHits} hits", "flash")
            .Sync(synced, 0);
        #endregion

        Code.Code = Snippets.TimelinePageChain;
        UpdateReadouts();
    }

    private void Play(object? sender, RoutedEventArgs e) => Run(static t => t.Play());

    private void Pause(object? sender, RoutedEventArgs e) => Run(static t => t.Pause());

    private void Restart(object? sender, RoutedEventArgs e) => Run(static t => t.Restart());

    private void Reverse(object? sender, RoutedEventArgs e) => Run(static t => t.Reverse());

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
        _scrub.Show(timeline);
    }
}
