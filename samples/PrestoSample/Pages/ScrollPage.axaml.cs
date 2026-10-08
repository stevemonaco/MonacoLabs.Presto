using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Monaco.Presto;
using Monaco.Presto.AvaloniaBridge;
using Monaco.Presto.AvaloniaBridge.Events;
using PrestoSample.Showcase;

namespace PrestoSample.Pages;

public partial class ScrollPage : ShowcasePage
{
    private static readonly IBrush s_entered = new SolidColorBrush(Color.Parse("#304c8dff"));

    private IBrush? _idle;

    public ScrollPage()
    {
        InitializeComponent();
        #region snippet ScrollReveal
        Presence.SetReveal(RevealCard, Presto.Build().Y(from: 60, to: 0).Opacity(from: 0, to: 1).Duration(700).Ease(Eases.OutQuad));
        #endregion
    }

    protected override void OnAttached()
    {
        _idle = ThresholdCard.Background;


        #region snippet ScrollThreshold
        PrestoAvalonia.OnScroll(new()
        {
            Target = ThresholdCard,
            Enter = "bottom-=120 top",
            Leave = "top center",
            OnEnter = _ => ShowThresholdState(true),
            OnLeave = _ => ShowThresholdState(false),
        });
        #endregion

        #region snippet ScrollScrub
        var scrub = Presto.CreateTimeline(new TimelineParams { Autoplay = false })
            .Add(Rider, new AvaloniaAnimationParams
            {
                ["X"] = 260,
                ["Rotate"] = 360,
                Duration = 1000,
                Ease = Eases.None,
            })
            .Add(Rider, new AvaloniaAnimationParams
            {
                ["CornerRadius"] = 16,
                Duration = 1000,
                Ease = Eases.None,
            }, 0);

        PrestoAvalonia.OnScroll(new()
        {
            Target = SyncCard,
            Sync = true,
            OnUpdate = ShowState,
        }).Link(scrub);
        #endregion
    }

    #region snippet ScrollJumps
    private void BackToTop(object? sender, RoutedEventArgs e) => PrestoAvalonia.ScrollTo(Scroller, 0);

    private void JumpTo(object? sender, RoutedEventArgs e) =>
        PrestoAvalonia.ScrollIntoView((Control)((Button)sender!).Tag!, ScrollAlignment.Start, new Thickness(0, 12, 0, 0));
    #endregion

    private void ShowState(ScrollObserver observer)
    {
        ProgressText.Text = Utils.RoundPad(observer.Progress, 3);
        VelocityText.Text = Utils.RoundPad(observer.Velocity, 3);
        DirectionText.Text = observer.Backward ? "backward" : "forward";
    }

    private void ShowThresholdState(bool entered)
    {
        ThresholdCard.Background = entered ? s_entered : _idle;
        StateText.Text = entered ? "ThresholdCard in view" : "ThresholdCard out of view";
    }
}
