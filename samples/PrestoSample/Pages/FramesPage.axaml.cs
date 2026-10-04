using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Monaco.Presto;
using Monaco.Presto.AvaloniaBridge;
using PrestoSample.Showcase;

namespace PrestoSample.Pages;

/// <summary>
/// Frame-by-frame playback: a <see cref="SpriteSheet"/> restarted by the fps and direction controls and paused while
/// scrubbed, a second sheet started from XAML, and an <see cref="ImageSequence"/> whose timeline also writes the caption.
/// </summary>
public partial class FramesPage : ShowcasePage
{
    private const double SequenceFps = 4;

    private static readonly (int Frame, string Text)[] Beats = [(0, "Ready"), (1, "Walk"), (3, "Duck"), (4, "Jump"), (6, "Cheer")];

    private Animation? _run;
    private Timeline? _story;

    public FramesPage()
    {
        InitializeComponent();
        Sequence.Frames = ImageSequence.FromAssets("avares://PrestoSample/Assets/Sprites/female_{0:000}.png", 8);
        Scrub.AddHandler(PointerPressedEvent, ScrubPressed, RoutingStrategies.Tunnel, handledEventsToo: true);
        Scrub.AddHandler(PointerReleasedEvent, ScrubReleased, RoutingStrategies.Tunnel, handledEventsToo: true);
    }

    protected override void OnAttached()
    {
        Run();
        _story = Presto.CreateTimeline(new() { Loop = !Presto.Defaults.ReducedMotion })
            .Add(Sequence, Recipes.PlayFrames(SequenceFps, loop: false), 0);
        foreach (var (frame, text) in Beats)
        {
            var at = frame * 1000 / SequenceFps;
            _story.Call(_ => Caption.Text = text, at)
                .Add(Caption, b => b.Opacity(from: 0).Y(from: 8).Duration(200).Ease(Eases.OutQuad), at);
        }
    }

    private void Run()
    {
        var progress = Hero.Frame / Hero.EffectiveFrameCount;
        _run?.Revert();
        _run = Scope.Execute(_ => Hero.Play(FpsSlider.Value, reverse: ReverseToggle.IsChecked == true));
        _run.IterationProgress = ReverseToggle.IsChecked == true ? 1 - progress : progress;
    }

    private void PlaybackChanged(object? sender, RoutedEventArgs e)
    {
        if (_run is not null) Run();
    }

    private void ScrubPressed(object? sender, PointerPressedEventArgs e)
    {
        var frame = Hero.Frame;
        _run?.Revert();
        _run = null;
        Hero.Frame = frame;
    }

    private void ScrubReleased(object? sender, PointerReleasedEventArgs e) => Run();

    private void PlayClicked(object? sender, RoutedEventArgs e) => _story?.Play();

    private void PauseClicked(object? sender, RoutedEventArgs e) => _story?.Pause();

    private void RestartClicked(object? sender, RoutedEventArgs e) => _story?.Restart();
}
