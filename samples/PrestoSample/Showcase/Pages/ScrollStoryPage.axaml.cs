using Avalonia.Controls;
using Avalonia.Media;
using Monaco.Presto;
using Monaco.Presto.AvaloniaBridge;
using Monaco.Presto.AvaloniaBridge.Events;

namespace PrestoSample.Showcase.Pages;

public partial class ScrollStoryPage : ShowcasePage
{
    private const double ScrubDuration = 1000;
    private const double PinDistance = 960;
    // Four 232px panels 14px apart: 370 wider than the 600px of stage they pan inside.
    private const double StripPan = 370;

    private static readonly (string Step, string Text)[] Steps =
    [
        ("01", "The observer measures its target against the scroller, and again on every Refresh."),
        ("02", "A scroll wakes a ticker that reports the offset to every observer on that scroller."),
        ("03", "A synced observer seeks what it is linked to: duration times progress, each frame."),
        ("04", "Half a second after the last scroll the wake window closes and the engine sleeps."),
    ];

    private readonly List<ScrollObserver> _observers = [];
    private readonly List<string> _crossings = [];

    public ScrollStoryPage()
    {
        InitializeComponent();
        HowCode.Code = $"{Snippets.ScrollStoryPin}\n\n{Snippets.ScrollStoryWatch}\n\n{Snippets.ScrollStoryRail}";
    }

    /// <summary>The page's observers, each named by the Id its crossings are reported under.</summary>
    public IReadOnlyList<ScrollObserver> Observers => _observers;

    /// <summary>Every threshold crossing as <c>"id enter|leave forward|backward"</c>; the rail shows the last of them.</summary>
    public IReadOnlyList<string> Crossings => _crossings;

    protected override void OnAttached()
    {
        BuildStrip();

        Watch("hero", Hero, new ScrollObserverParams { Enter = "start start", Sync = 0.35 }).Link(HeroTitle.Animate(new()
        {
            ["Opacity"] = TweenValue.FromTo(1, 0.15),
            Duration = ScrubDuration,
            Ease = Eases.None,
            Autoplay = false,
        }));

        var scrub = Scrub();
        #region snippet ScrollStoryPin
        Watch("pin", PinStage, new ScrollObserverParams
        {
            Enter = "center center",
            Leave = $"center center+={PinDistance}",
            Sync = true,
            Pin = true,
            Snap = ScrollSnap.Labels,
            OnEnter = observer => Crossed(observer, "enter"),
            OnLeave = observer => Crossed(observer, "leave"),
            OnUpdate = observer => PinLabel.Text =
                scrub.Labels.MinBy(label => Math.Abs(label.Value - (observer.Progress * scrub.Duration))).Key,
        }).Link(scrub);
        #endregion

        Watch("quote", Quote, new ScrollObserverParams { Sync = Eases.InOutQuad }).Link(Quote.Animate(new()
        {
            ["X"] = TweenValue.FromTo(-70, 70),
            ["Opacity"] = TweenValue.FromTo(0, 1),
            Duration = ScrubDuration,
            Ease = Eases.None,
            Autoplay = false,
        }));

        Control[] chapters = [Chapter1, Chapter2, Chapter3];
        for (var i = 0; i < chapters.Length; i++)
        {
            Watch($"chapter {i + 1}", chapters[i], new ScrollObserverParams
            {
                Enter = "bottom-=120 top",
                Leave = "top+=40 bottom",
                OnEnter = observer => Crossed(observer, "enter"),
                OnLeave = observer => Crossed(observer, "leave"),
            });
        }

        #region snippet ScrollStoryRail
        var progress = PrestoAvalonia.ScrollValue(Scroller, progress: true);
        var velocity = PrestoAvalonia.ScrollValue(Scroller).VelocityOf();
        var backward = false;
        var direction = velocity.Map(v => (backward = v < 0 || (backward && v == 0)) ? 180 : 0);
        Follow.SetScaleX(RailFill, progress);
        Follow.SetRotate(Vane, direction.Spring(Springs.Gentle));
        Follow.SetScaleY(Vane, velocity.Map(v => 1 + (Math.Min(Math.Abs(v), 4) * .5)).Spring(Springs.Stiff));
        Action<MotionValue> show = _ => RailText.Text =
            $"{Utils.RoundPad(progress.Value * 100, 0)}% · {Utils.RoundPad(Math.Abs(velocity.Value), 3)} px/ms · {(direction.Value > 0 ? "backward" : "forward")}";
        progress.Changed += show;
        velocity.Changed += show;
        #endregion
    }

    private void BuildStrip()
    {
        foreach (var (step, text) in Steps)
        {
            PinStrip.Children.Add(new Border
            {
                Classes = { "card", "showcase" },
                Child = new StackPanel
                {
                    Spacing = 8,
                    Children =
                    {
                        new TextBlock { Classes = { "readout" }, Text = step },
                        new TextBlock { Classes = { "caption" }, Text = text, TextWrapping = TextWrapping.Wrap },
                    },
                },
            });
        }
    }

    private Timeline Scrub()
    {
        var timeline = Presto.CreateTimeline(new TimelineParams
            {
                Autoplay = false,
                Defaults = new DefaultsParams { Duration = ScrubDuration, Ease = Eases.None },
            })
            .Add(PinStrip, new() { ["X"] = TweenValue.FromTo(0, -StripPan) }, 0)
            .Add(PinNumber, new()
            {
                ["Text"] = new TweenParams { From = 0, To = 100, TextModifier = value => Utils.RoundPad(value, 0) },
            }, 0);
        for (var i = 0; i < Steps.Length; i++) timeline.Label($"step{i + 1}", i * ScrubDuration / (Steps.Length - 1));
        return timeline;
    }

    #region snippet ScrollStoryWatch
    private ScrollObserver Watch(string id, Control target, ScrollObserverParams parameters)
    {
        var observer = PrestoAvalonia.OnScroll(parameters with { Id = id, Target = target });
        _observers.Add(observer);
        return observer;
    }
    #endregion

    private void Crossed(ScrollObserver observer, string kind)
    {
        _crossings.Add($"{observer.Id} {kind} {(observer.Backward ? "backward" : "forward")}");
        CrossingText.Text = _crossings[^1];
    }

    private void Remeasure(object? sender, SizeChangedEventArgs e) => PrestoAvalonia.RefreshScroll(Scroller);
}
