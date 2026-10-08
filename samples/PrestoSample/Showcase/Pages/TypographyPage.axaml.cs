using Avalonia.Controls;
using Avalonia.Interactivity;
using Monaco.Presto;
using Monaco.Presto.AvaloniaBridge;
using Monaco.Presto.AvaloniaBridge.Text;
using Timer = Monaco.Presto.Timer;

namespace PrestoSample.Showcase.Pages;

public partial class TypographyPage : ShowcasePage
{
    private const double CycleDuration = 2600;
    private const double Drop = 28;
    private const double Falloff = 160;
    private const double Lift = 30;
    private const double Swell = 0.35;
    private const double WaveHold = 320;
    private const double WaveStagger = 60;
    private const double TerminalHold = 2400;

    private static readonly string[] RotatorWords = ["timelines", "text", "shapes", "layouts", "anything"];

    private static readonly string[] TerminalScript =
    [
        "$ presto decode --stream intercept.bin",
        "  reading 4096 frames ............ ok",
        "  matching eases to the library ... ok",
        "  rebuilding the timeline ......... done",
    ];

    private static readonly string[] StatusScript = ["connecting", "reading frames", "matching eases", "rebuilding"];

    private readonly List<MotionValue> _waves = [];
    private readonly Spring _relax = Eases.Spring(bounce: .45, duration: 450);
    private int _wordIndex;
    private Timer? _cycle;
    private Timeline? _terminal;

    public TypographyPage()
    {
        InitializeComponent();
        foreach (var _ in TerminalScript) TerminalLines.Children.Add(new TextBlock { Classes = { "mono" } });
    }

    protected override void OnAttached()
    {
        ShowFirstWord();
        _cycle = Presto.Defaults.ReducedMotion
            ? null
            : Presto.CreateTimer(new TimerParams { Duration = CycleDuration, Loop = LoopCount.Infinite, OnLoop = _ => Rotate() });

        PrestoAvalonia.SplitText(Wordmark, new SplitTextParams { Words = null, Chars = new SplitPieceParams() })
            .AddEffect(Lean);

        _terminal = BuildTerminal();
    }

    private void ShowFirstWord() => Slot.Child = Headline(RotatorWords[_wordIndex = 0]);

    #region snippet TypographyRotatorAndWordmark
    private static readonly AnimationBuilder RiseIn = Presto.Build()
        .Y(from: Drop).Opacity(from: 0, duration: 300, ease: Eases.OutQuad)
        .Duration(700).Ease(Eases.OutBack()).Delay(Presto.Stagger(35));

    private static readonly AnimationBuilder DropOut = Presto.Build()
        .Y(Drop).Opacity(0)
        .Duration(380).Ease(Eases.InBack()).Delay(Presto.Stagger(25));

    private void Rotate() => Slot.Child = Headline(RotatorWords[_wordIndex = (_wordIndex + 1) % RotatorWords.Length]);

    private static TextBlock Headline(string word)
    {
        var headline = new TextBlock
        {
            Text = word,
            Classes = { "headline" },
            Foreground = Palette.Accent,
            // Keeps the rising letters inside the clip the resizing slot draws; the slot's negative margin takes it back.
            Padding = new(0, 0, 0, Drop),
        };
        TextReveal.SetPieces(headline, TextPieces.Chars);
        Presence.SetArrive(headline, RiseIn);
        Presence.SetExit(headline, DropOut);
        return headline;
    }

    private Action Lean(TextSplitter split)
    {
        var scope = new Scope();
        scope.Execute(_ =>
        {
            foreach (var letter in split.Chars)
            {
                var wave = Presto.CreateMotionValue();
                _waves.Add(wave);
                var proximity = PrestoAvalonia.PointerProximityValue(letter, Falloff, over: WordmarkStage, fromCenter: true);
                var pull = MotionValue.Combine(proximity.Map([0, 1], [0, 1], Eases.InOutQuad), wave, Math.Max).Spring(_relax);
                Follow.SetY(letter, pull.Map(p => -Lift * p));
                Follow.SetScale(letter, pull.Map(p => 1 + (Swell * p)));
                // No pull rests on the dim step the wordmark is drawn in, not on the near-black deepest one.
                Follow.Bind(letter, TextBlock.ForegroundProperty, pull.Map([0, 1 / 3d, 2 / 3d, 1], [.. Palette.AccentBrushRamp.Skip(1)]));
            }
        });
        return () =>
        {
            scope.Revert();
            _waves.Clear();
        };
    }
    #endregion

    private Timeline BuildTerminal()
    {
        var timeline = Presto.CreateTimeline(new() { Loop = !Presto.Defaults.ReducedMotion, LoopDelay = TerminalHold });
        for (var i = 0; i < TerminalScript.Length; i++)
        {
            timeline
                .Add(TerminalLines.Children[i], new()
                {
                    ["Text"] = Presto.ScrambleText(new()
                    {
                        Text = TerminalScript[i],
                        Chars = "lowercase",
                        OverrideChars = "",
                        Cursor = "_",
                        RevealRate = 48,
                        SettleDuration = 120,
                    }),
                }, "+=180")
                .Add(Status, new() { ["Text"] = Presto.ScrambleText(new() { Text = StatusScript[i], Chars = "blocks" }) }, "<");
        }

        return timeline
            .Sync(Recipes.CountUp(Percent, 100, "0", timeline.IterationDuration, ease: Eases.InOutSine), 0)
            .Add(Status, new() { ["Text"] = Presto.ScrambleText(new() { Text = "decoded · 0 errors", Chars = "blocks" }) });
    }

    private void ReplayReveal(object? sender, RoutedEventArgs e)
    {
        var reveal = Animate.GetOnAttached(Paragraph);
        Animate.SetOnAttached(Paragraph, null);
        Animate.SetOnAttached(Paragraph, reveal);
    }

    private void ReplayRotator(object? sender, RoutedEventArgs e)
    {
        ShowFirstWord();
        _cycle?.Restart();
    }

    private void ReplayWordmark(object? sender, RoutedEventArgs e)
    {
        var timeline = Presto.CreateTimeline();
        for (var i = 0; i < _waves.Count; i++)
        {
            var wave = _waves[i];
            timeline.Call(_ => wave.Set(1), i * WaveStagger).Call(_ => wave.Set(0), (i * WaveStagger) + WaveHold);
        }
    }

    private void ReplayTerminal(object? sender, RoutedEventArgs e) => _terminal?.Restart();
}
