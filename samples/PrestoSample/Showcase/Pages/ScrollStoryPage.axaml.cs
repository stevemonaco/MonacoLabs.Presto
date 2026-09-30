using Avalonia.Controls;
using Avalonia.Media;
using Monaco.Presto;
using Monaco.Presto.AvaloniaBridge;
using Monaco.Presto.AvaloniaBridge.Events;

namespace PrestoSample.Showcase.Pages;

public partial class ScrollStoryPage : ShowcasePage
{
    private const double ScrubDuration = 1000;
    private const double FadeDuration = 120;
    private const double RevealDuration = 640;
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

    private Animatable? _vane;

    public ScrollStoryPage() => InitializeComponent();

    /// <summary>The page's observers, each named by the Id its crossings are reported under.</summary>
    public IReadOnlyList<ScrollObserver> Observers => _observers;

    /// <summary>Every threshold crossing as <c>"id enter|leave forward|backward"</c>; the rail shows the last of them.</summary>
    public IReadOnlyList<string> Crossings => _crossings;

    protected override void OnAttached()
    {
        BuildStrip();

        Watch("hero", Hero, new ScrollObserverParams { Enter = "start start", Sync = 0.35 }).Link(Parallax());

        Watch("pin", PinTrack, new ScrollObserverParams
        {
            Enter = "start start",
            Leave = "end end",
            Sync = true,
            OnEnter = observer => Crossed(observer, "enter"),
            OnLeave = observer => Crossed(observer, "leave"),
        }).Link(Scrub());

        Watch("quote", Quote, new ScrollObserverParams { Sync = Eases.InOutQuad }).Link(Quote.Animate(new()
        {
            ["X"] = TweenValue.FromTo(-70, 70),
            ["Opacity"] = TweenValue.FromTo(0, 1),
            Duration = ScrubDuration,
            Ease = Eases.None,
            Autoplay = false,
        }));

        Control[] chapters = [Chapter1, Chapter2, Chapter3];
        TextBlock[] headings = [Heading1, Heading2, Heading3];
        for (var i = 0; i < chapters.Length; i++)
        {
            var chapter = chapters[i];
            var heading = headings[i];
            Watch($"chapter {i + 1}", chapter, new ScrollObserverParams
            {
                Enter = "bottom-=120 top",
                Leave = "top+=40 bottom",
                OnEnter = observer =>
                {
                    Crossed(observer, "enter");
                    // Presence.Reveal only plays in a virtualizing list; set on an attached block, OnAttached plays at once.
                    if (Animate.GetOnAttached(heading) is null) Animate.SetOnAttached(heading, Recipes.LineMask());
                },
                OnLeave = observer => Crossed(observer, "leave"),
            }).Link(chapter.Animate(new()
            {
                ["Y"] = TweenValue.FromTo(52, 0),
                ["Opacity"] = TweenValue.FromTo(0, 1),
                Duration = RevealDuration,
                Ease = Eases.OutQuint,
                Autoplay = false,
            }));
        }

        Watch("rail", Article, new ScrollObserverParams { Enter = "start start", Leave = "end end", OnUpdate = ShowScroll });
        _vane = Vane.CreateAnimatable(new() { ["Rotate"] = 260, ["ScaleY"] = 200, Ease = Eases.OutQuad });
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

    private Timeline Parallax() => Presto.CreateTimeline(new TimelineParams
        {
            Autoplay = false,
            Defaults = new DefaultsParams { Duration = ScrubDuration, Ease = Eases.None },
        })
        .Add(HeroSky, new() { ["Y"] = TweenValue.FromTo(0, 90), ["Scale"] = TweenValue.FromTo(1, 1.2) }, 0)
        .Add(HeroRidge, new() { ["Y"] = TweenValue.FromTo(0, 44) }, 0)
        .Add(HeroTitle, new() { ["Y"] = TweenValue.FromTo(0, -80), ["Opacity"] = TweenValue.FromTo(1, 0.15) }, 0);

    private Timeline Scrub() => Presto.CreateTimeline(new TimelineParams
        {
            Autoplay = false,
            Defaults = new DefaultsParams { Duration = ScrubDuration, Ease = Eases.None },
        })
        .Add(PinStrip, new() { ["X"] = TweenValue.FromTo(0, -StripPan) }, 0)
        .Add(PinNumber, new()
        {
            ["Text"] = new TweenParams { From = 0, To = 100, TextModifier = value => Utils.RoundPad(value, 0) },
        }, 0)
        .Add(PinStage, new() { ["Opacity"] = TweenValue.FromTo(0, 1), Duration = FadeDuration }, 0)
        .Add(PinStage, new() { ["Opacity"] = TweenValue.FromTo(1, 0), Duration = FadeDuration }, ScrubDuration - FadeDuration);

    private ScrollObserver Watch(string id, Control target, ScrollObserverParams parameters)
    {
        var observer = PrestoAvalonia.OnScroll(parameters with { Id = id, Target = target });
        _observers.Add(observer);
        return observer;
    }

    private void Crossed(ScrollObserver observer, string kind)
    {
        _crossings.Add($"{observer.Id} {kind} {(observer.Backward ? "backward" : "forward")}");
        CrossingText.Text = _crossings[^1];
    }

    private void ShowScroll(ScrollObserver observer)
    {
        RailFill.Width = observer.Progress * RailTrack.Bounds.Width;
        RailText.Text =
            $"{Utils.RoundPad(observer.Progress * 100, 0)}% · {Utils.RoundPad(observer.Velocity, 3)} px/ms · {(observer.Backward ? "backward" : "forward")}";
        _vane?.Set("Rotate", observer.Backward ? 180 : 0).Set("ScaleY", 1 + (Utils.Clamp(observer.Velocity, 0, 4) * 0.5));
    }

    private void Remeasure(object? sender, SizeChangedEventArgs e) => PrestoAvalonia.RefreshScroll(Scroller);
}
