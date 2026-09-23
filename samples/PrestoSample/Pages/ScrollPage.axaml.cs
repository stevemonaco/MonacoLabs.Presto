using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Monaco.Presto;
using Monaco.Presto.AvaloniaBridge;
using Monaco.Presto.AvaloniaBridge.Events;

namespace PrestoSample.Pages;

public partial class ScrollPage : UserControl
{
    private static readonly IBrush s_entered = new SolidColorBrush(Color.Parse("#304c8dff"));

    private IBrush? _idle;

    public ScrollPage() => InitializeComponent();

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _idle = ThresholdCard.Background;

        PrestoAvalonia.CreateScope(this).Execute(_ =>
        {
            var reveal = RevealCard.Animate(new()
            {
                ["Y"] = TweenValue.FromTo(60, 0),
                ["Opacity"] = TweenValue.FromTo(0, 1),
                Duration = 700,
                Ease = Eases.OutQuad,
                Autoplay = false,
            });
            PrestoAvalonia.OnScroll(new() { Target = RevealCard }).Link(reveal);

            PrestoAvalonia.OnScroll(new()
            {
                Target = ThresholdCard,
                Enter = "bottom-=120 top",
                Leave = "top center",
                OnEnter = _ => ShowThresholdState(true),
                OnLeave = _ => ShowThresholdState(false),
            });

            var scrub = Presto.CreateTimeline(new TimelineParams { Autoplay = false })
                .Add(Rider, new AvaloniaAnimationParams { ["X"] = 260, ["Rotate"] = 360, Duration = 1000, Ease = Eases.None })
                .Add(Rider, new AvaloniaAnimationParams { ["CornerRadius"] = 16, Duration = 1000, Ease = Eases.None }, 0);

            PrestoAvalonia.OnScroll(new()
            {
                Target = SyncCard,
                Sync = true,
                OnUpdate = ShowState,
            }).Link(scrub);
        });
    }

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
