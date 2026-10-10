using System.Globalization;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using MonacoLabs.Presto;
using MonacoLabs.Presto.Lottie;
using PrestoSample.Showcase;

namespace PrestoSample.Pages;

/// <summary>
/// Lottie playback through <see cref="LottieView"/>: a spinner looped from XAML, marker segments played by buttons and on
/// hover, a player scrubbed through Progress with speed, direction and inherited filters, a timeline that plays a segment
/// beside a sibling, and render-thread playback that survives a frozen UI thread.
/// </summary>
public partial class LottiePage : ShowcasePage
{
    private Animation? _run;
    private Animation? _spin;

    public LottiePage()
    {
        InitializeComponent();
        #region snippet LottieMarkers
        foreach (var segment in Toggle.Composition!.Segments)
        {
            var button = new Button { Content = segment.Name };
            button.Click += (_, _) => Scope.Execute(_ => Toggle.Play(segment.Name!));
            MarkerButtons.Children.Add(button);
        }
        MarkerList.Text = string.Join("   ", Toggle.Composition.Segments.Select(s => $"{s.Name} {s.From}–{s.To}"));
        #endregion
        MarkerCode.CSharp = Snippets.LottieMarkers;
        TimelineCode.CSharp = Snippets.LottieTimeline;
        RenderThreadCode.CSharp = Snippets.LottieRenderThread;
        Scrub.AddHandler(PointerPressedEvent, (_, _) => _run?.Pause(), RoutingStrategies.Tunnel, handledEventsToo: true);
        Scrub.AddHandler(PointerReleasedEvent, (_, _) => Run(), RoutingStrategies.Tunnel, handledEventsToo: true);
    }

    protected override void OnAttached()
    {
        Run();
        Spin();
        #region snippet LottieTimeline
        Presto.CreateTimeline(new() { Loop = !Presto.Defaults.ReducedMotion, Alternate = true, LoopDelay = 800 })
            .Add(Switch, Recipes.PlayLottie("on", loop: false), 0)
            .Add(SwitchLabel, b => b.Opacity(from: 0).X(from: -24).Duration(333).Ease(Eases.OutQuad), 0);
        #endregion
    }

    private void Run()
    {
        var reverse = ReverseToggle.IsChecked == true;
        var progress = Player.Progress;
        _run?.Revert();
        _run = Scope.Execute(_ => Player.Play(loop: true, reverse: reverse, speed: SpeedSlider.Value));
        _run.IterationProgress = reverse ? 1 - progress : progress;
        PlayerCode.CSharp = $"""
            var run = Player.Play(loop: true, reverse: {(reverse ? "true" : "false")}, speed: {SpeedSlider.Value.ToString("0.##", CultureInfo.InvariantCulture)});
            run.IterationProgress = {(reverse ? "1 - " : "")}Player.Progress;

            // Scrub pressed: the binding drives Progress until it is released and played again
            run.Pause();
            """;
    }

    private void Spin()
    {
        _spin?.Revert();
        _spin = null;
        if (RenderToggle.IsChecked == true)
        {
            #region snippet LottieRenderThread
            RenderSpinner.RenderThread = true;
            RenderSpinner.PlayOnRenderThread();
            #endregion
        }
        else
        {
            RenderSpinner.RenderThread = false;
            _spin = Scope.Execute(_ => RenderSpinner.Play(loop: true));
        }
    }

    private void PlaybackChanged(object? sender, RoutedEventArgs e)
    {
        if (_run is not null) Run();
    }

    private void RenderThreadChanged(object? sender, RoutedEventArgs e) => Spin();

    private void FreezeClicked(object? sender, RoutedEventArgs e) => UiThread.Freeze(TimeSpan.FromSeconds(2));
}
