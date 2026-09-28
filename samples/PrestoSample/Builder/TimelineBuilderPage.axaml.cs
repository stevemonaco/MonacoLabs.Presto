using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Monaco.Presto;
using Monaco.Presto.AvaloniaBridge;
using Monaco.Presto.AvaloniaBridge.Compositor;
using PrestoSample.Showcase;

namespace PrestoSample.Builder;

/// <summary>A timeline assembled from one builder with a scrub slider, and a builder spun on the compositor so it survives a frozen UI thread.</summary>
public partial class TimelineBuilderPage : ShowcasePage
{
    private bool _syncingScrub;

    public TimelineBuilderPage() => InitializeComponent();

    public Timeline? Timeline { get; private set; }

    public CompositorAnimation? Spin { get; private set; }

    protected override void OnAttached()
    {
        var (fadeUp, fadeUpCode) = Snippet.Of(Presto.Build()
            .Opacity(from: 0, to: 1)
            .Y(from: 40, to: 0)
            .Duration(600)
            .Ease(Eases.OutQuad));

        (Timeline, var timelineCode) = Snippet.Of(Presto.CreateTimeline(new() { Loop = true, LoopDelay = 800, OnUpdate = _ => SyncScrub() })
            .Add(CardA, fadeUp)
            .Add(CardB, fadeUp, "-=400")
            .Add(CardC, fadeUp, "-=400")
            .Add(CardD, fadeUp, "<"));
        TimelineCode.Text = $"{Snippet.Declare("var fadeUp", fadeUpCode)}\n\n{timelineCode};";

        (Spin, SpinCode.Text) = Snippet.Of(Gear.AnimateOnCompositor(Presto.Build()
            .Rotate(360)
            .Duration(2400)
            .Ease(Eases.None)
            .Loop()));
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

    // Posted below Render priority so the button paints its pressed state before the dispatcher stops answering.
    private void Freeze(object? sender, RoutedEventArgs e) =>
        Dispatcher.UIThread.Post(() => Thread.Sleep(2000), DispatcherPriority.Background);
}
