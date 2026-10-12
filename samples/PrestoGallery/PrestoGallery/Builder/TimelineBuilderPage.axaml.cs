using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using MonacoLabs.Presto;
using MonacoLabs.Presto.Compositor;
using PrestoGallery.Showcase;

namespace PrestoGallery.Builder;

/// <summary>A timeline assembled from one shared builder and one lambda, with a scrub slider, and a builder spun on the compositor so it survives a frozen UI thread.</summary>
public partial class TimelineBuilderPage : ShowcasePage
{
    private bool _syncingScrub;

    public TimelineBuilderPage() => InitializeComponent();

    public Timeline? Timeline { get; private set; }

    public CompositorAnimation? Spin { get; private set; }

    protected override void OnAttached()
    {
        #region snippet TimelineFadeUp
        var fadeUp = Presto.Build()
            .Opacity(from: 0, to: 1)
            .Y(from: 40, to: 0)
            .Duration(600)
            .Ease(Eases.OutQuad);

        Timeline = Presto.CreateTimeline(new() { Loop = !Presto.Defaults.ReducedMotion, LoopDelay = 800 })
            .Add(CardA, fadeUp)
            .Add(CardB, fadeUp, "-=400")
            .Add(CardC, fadeUp, "-=400")
            .Add(CardD, b => b.Opacity(from: 0, to: 1).Y(from: 40, to: 0).Scale(from: 0.6, to: 1).Duration(600), "<");
        #endregion
        Timeline.OnUpdate = _ => SyncScrub();
        TimelineCode.Code = Snippets.TimelineFadeUp;

        (Spin, SpinCode.Code) = Snippet.Of(Gear.AnimateOnCompositor(Presto.Build()
            .Rotate(360)
            .Duration(2400)
            .Ease(Eases.None)
            .Loop()
            .WhenReduced(r => r.Loop(false))));
    }

    private void SyncScrub()
    {
        _syncingScrub = true;
        ScrubSlider.Value = Timeline!.IterationProgress;
        _syncingScrub = false;
    }

    private void Scrub(object? sender, RangeBaseValueChangedEventArgs e)
    {
        if (_syncingScrub || Timeline is not { } timeline) return;
        timeline.Pause();
        timeline.IterationProgress = e.NewValue;
    }

    private void Play(object? sender, RoutedEventArgs e) => Timeline?.Play();

    private void Freeze(object? sender, RoutedEventArgs e) => UiThread.Freeze(TimeSpan.FromSeconds(2));
}
