using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
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
    private const double Follow = 320;
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

    private readonly List<Animatable> _letters = [];
    private readonly Spring _relax = Eases.Spring(bounce: .45, duration: 450);
    private int _wordIndex;
    private TextSplitter? _word;
    private Timeline? _rotation;
    private Timer? _cycle;
    private TextSplitter? _wordmark;
    private Timeline? _terminal;

    public TypographyPage()
    {
        InitializeComponent();
        foreach (var _ in TerminalScript) TerminalLines.Children.Add(new TextBlock { Classes = { "code" } });
    }

    protected override void OnAttached()
    {
        _wordIndex = 0;
        Slot.Width = Measure(Word, RotatorWords[0]).Width;
        ShowWord(RotatorWords[0]);
        _cycle = Presto.CreateTimer(new TimerParams { Duration = CycleDuration, Loop = LoopCount.Infinite, OnLoop = _ => Rotate() });

        _wordmark = PrestoAvalonia.SplitText(Wordmark, new SplitTextParams { Words = null, Chars = new SplitPieceParams() })
            .AddEffect(split =>
            {
                foreach (var letter in split.Chars)
                {
                    _letters.Add(letter.CreateAnimatable(new() { ["Y"] = Follow, ["Scale"] = Follow, ["Foreground"] = Follow }));
                }
                return () =>
                {
                    foreach (var letter in _letters) letter.Revert();
                    _letters.Clear();
                };
            });

        _terminal = BuildTerminal();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        // Each rotation is a timeline built outside Scope, which prunes superseded animations but not timelines (SAMPLES-PLAN §8 row 18).
        _rotation?.Revert();
        _word?.Revert();
    }

    private void Rotate()
    {
        _rotation?.Cancel();
        var next = RotatorWords[_wordIndex = (_wordIndex + 1) % RotatorWords.Length];
        _rotation = Presto.CreateTimeline()
            .Add(_word!.Chars, new()
            {
                ["Y"] = Drop,
                ["Opacity"] = 0,
                Duration = 380,
                Ease = Eases.InBack(),
                Delay = Presto.Stagger(25),
            })
            .Call(_ => ShowWord(next))
            .Add(Slot, new() { ["Width"] = Measure(Word, next).Width, Duration = 600, Ease = Eases.OutExpo }, "<");
    }

    private void ShowWord(string word)
    {
        _word?.Revert();
        Word.Text = word;
        // The word block has a fixed width so the swap never resizes it, which would re-split it 150 ms later.
        _word = PrestoAvalonia.SplitText(Word, new SplitTextParams { Words = null, Chars = new SplitPieceParams() })
            .AddEffect(split => Presto.Animate(split.Chars, new AnimationParams
            {
                ["Y"] = TweenValue.FromTo(Drop, 0),
                ["Opacity"] = new TweenParams { From = 0, To = 1, Duration = 300, Ease = Eases.OutQuad },
                Duration = 700,
                Ease = Eases.OutBack(),
                Delay = Presto.Stagger(35, new() { From = StaggerFrom.First }),
            }));
    }

    private Timeline BuildTerminal()
    {
        var timeline = Presto.CreateTimeline(new() { Loop = LoopCount.Infinite, LoopDelay = TerminalHold });
        for (var i = 0; i < TerminalScript.Length; i++)
        {
            timeline
                .Add(TerminalLines.Children[i], new()
                {
                    ["Text"] = PrestoAvalonia.ScrambleText(new()
                    {
                        Text = TerminalScript[i],
                        Chars = "lowercase",
                        OverrideChars = "",
                        Cursor = "_",
                        RevealRate = 48,
                        SettleDuration = 120,
                    }),
                }, "+=180")
                .Add(Status, new() { ["Text"] = PrestoAvalonia.ScrambleText(new() { Text = StatusScript[i], Chars = "blocks" }) }, "<");
        }

        return timeline
            .Add(Percent, new()
            {
                ["Text"] = new TweenParams { From = 0, To = 100, TextModifier = value => Utils.RoundPad(value, 0) },
                Duration = timeline.IterationDuration,
                Ease = Eases.InOutSine,
            }, 0)
            .Add(Status, new() { ["Text"] = PrestoAvalonia.ScrambleText(new() { Text = "decoded · 0 errors", Chars = "blocks" }) });
    }

    private void LeanWordmark(object? sender, PointerEventArgs e)
    {
        var pointer = e.GetPosition(WordmarkStage);
        for (var i = 0; i < _letters.Count; i++)
        {
            var letter = _wordmark!.Chars[i];
            // Bounds is the layout slot, so the lift and scale a letter already has never feed back into its own pull.
            var centre = ((Visual)letter.Parent!).TranslatePoint(letter.Bounds.Center, WordmarkStage)!.Value;
            var pull = Eases.InOutQuad(Math.Clamp(1 - (Point.Distance(pointer, centre) / Falloff), 0, 1));
            _letters[i]
                .Set("Y", -Lift * pull, Follow, Eases.OutQuint)
                .Set("Scale", 1 + (Swell * pull), Follow, Eases.OutQuint)
                .Set("Foreground", Tint(pull), Follow, Eases.OutQuint);
        }
    }

    private void RelaxWordmark(object? sender, PointerEventArgs e)
    {
        foreach (var letter in _letters) Relax(letter);
    }

    private void Relax(Animatable letter) => letter
        .Set("Y", 0, _relax.SettlingDuration, _relax)
        .Set("Scale", 1, _relax.SettlingDuration, _relax)
        .Set("Foreground", Tint(0), Follow * 2, Eases.OutQuad);

    private static double[] Tint(double pull)
    {
        var ramp = Palette.AccentRamp;
        // No pull rests on the dim step the wordmark is drawn in, not on the near-black deepest one.
        var at = 1 + (pull * (ramp.Count - 2));
        var low = ramp[(int)at];
        var high = ramp[Math.Min((int)at + 1, ramp.Count - 1)];
        var t = at - (int)at;
        return [Utils.Lerp(low.R, high.R, t), Utils.Lerp(low.G, high.G, t), Utils.Lerp(low.B, high.B, t), 1];
    }

    private static Size Measure(TextBlock style, string text)
    {
        var probe = new TextBlock
        {
            Text = text,
            FontFamily = style.FontFamily,
            FontSize = style.FontSize,
            FontWeight = style.FontWeight,
            LetterSpacing = style.LetterSpacing,
        };
        probe.Measure(Size.Infinity);
        return probe.DesiredSize;
    }

    private void ReplayReveal(object? sender, RoutedEventArgs e)
    {
        var reveal = Animate.GetOnAttached(Paragraph);
        Animate.SetOnAttached(Paragraph, null);
        Animate.SetOnAttached(Paragraph, reveal);
    }

    private void ReplayRotator(object? sender, RoutedEventArgs e)
    {
        _wordIndex = RotatorWords.Length - 1;
        Rotate();
        _cycle?.Restart();
    }

    private void ReplayWordmark(object? sender, RoutedEventArgs e)
    {
        var wave = Presto.CreateTimeline();
        for (var i = 0; i < _letters.Count; i++)
        {
            var letter = _letters[i];
            wave.Call(_ => letter
                    .Set("Y", -Lift, Follow, Eases.OutQuint)
                    .Set("Scale", 1 + Swell, Follow, Eases.OutQuint)
                    .Set("Foreground", Tint(1), Follow, Eases.OutQuint), i * WaveStagger)
                .Call(_ => Relax(letter), (i * WaveStagger) + Follow);
        }
    }

    private void ReplayTerminal(object? sender, RoutedEventArgs e) => _terminal?.Restart();
}
