using Avalonia.Controls;
using Avalonia.Interactivity;
using MonacoLabs.Presto;
using MonacoLabs.Presto.Controls;
using Timer = MonacoLabs.Presto.Timer;

namespace PrestoSample.Showcase.Pages;

public partial class MorphClockPage : ShowcasePage
{
    private const int CountFrom = 10;

    private static readonly AnimationBuilder ColonPulse = Presto.Build().Opacity(from: .25, to: 1).Duration(800).Ease(Eases.OutQuad);

    private readonly DateTime _start;
    private readonly (TextBlock Morph, TextBlock Roll, SplitFlap Flap)[] _stats;
    private readonly Random _random = new(7);
    private int _elapsed;
    private int _left = CountFrom;
    private Timer? _countdown;
    private SubmitState _submit;
    private bool _playing;
    private bool _menuOpen;

    public MorphClockPage() : this(DateTime.Now)
    {
    }

    /// <summary>Creates the page with its clock reading <paramref name="start"/>, so a test knows which digits the next tick changes.</summary>
    public MorphClockPage(DateTime start)
    {
        _start = start;
        InitializeComponent();
        Hours.Text = start.ToString("HH");
        Minutes.Text = start.ToString("mm");
        Seconds.Text = start.ToString("ss");
        _stats = [(MorphVisitors, RollVisitors, FlapVisitors), (MorphOrders, RollOrders, FlapOrders), (MorphReturns, RollReturns, FlapReturns)];
        HowCode.Code = string.Join("\n\n", Snippets.MorphClockTick, Snippets.MorphClockCountdown, Snippets.MorphClockButtons, Snippets.MorphClockStats);
    }

    protected override void OnAttached() =>
        Presto.CreateTimer(new TimerParams { Duration = 1000, Loop = true, OnLoop = _ => Tick(_start.AddSeconds(++_elapsed)) });

    #region snippet MorphClockTick
    private void Tick(DateTime now)
    {
        MorphIfChanged(Hours, now.ToString("HH"));
        MorphIfChanged(Minutes, now.ToString("mm"));
        MorphIfChanged(Seconds, now.ToString("ss"));
        if (!Presto.Defaults.ReducedMotion) ColonPulse.Animate(new[] { FirstColon, SecondColon });
    }

    // Two-digit numbers pair from the end, so 59 to 00 morphs both digits and 56 to 57 morphs the 6 alone.
    private static void MorphIfChanged(TextBlock block, string text)
    {
        if (block.Text != text) Recipes.MorphGlyphs(block, text);
    }
    #endregion

    #region snippet MorphClockCountdown
    private void StartStop(object? sender, RoutedEventArgs e)
    {
        if (_countdown != null)
        {
            StopCountdown();
            return;
        }
        if (_left == 0)
        {
            _left = CountFrom;
            Recipes.MorphGlyphs(Countdown, $"0:{_left:00}");
        }
        StartButton.Content = "Stop";
        _countdown = Scope.Execute(_ => Presto.CreateTimer(new TimerParams { Duration = 1000, Loop = true, OnLoop = _ => CountDown() }));
    }

    private void CountDown()
    {
        if (_left == 0)
        {
            StopCountdown();
            Recipes.MorphGlyphs(Countdown, "DONE");
            return;
        }
        _left--;
        Recipes.MorphGlyphs(Countdown, $"0:{_left:00}");
    }

    private void StopCountdown()
    {
        _countdown?.Cancel();
        _countdown = null;
        StartButton.Content = "Start";
    }
    #endregion

    #region snippet MorphClockButtons
    private void Submit(object? sender, RoutedEventArgs e)
    {
        if (_submit == SubmitState.Idle) _ = Send();
    }

    private async Task Send()
    {
        ShowSubmit(SubmitState.Busy);
        await Presto.CreateTimer(new() { Duration = 1100 });
        ShowSubmit(SubmitState.Succeeded);
        await Presto.CreateTimer(new() { Duration = 1400 });
        ShowSubmit(SubmitState.Idle);
    }

    private void ShowSubmit(SubmitState state)
    {
        _submit = state;
        Recipes.MorphGlyphs(SubmitLabel, state switch { SubmitState.Busy => "Sending", SubmitState.Succeeded => "✓", _ => "Submit" });
    }

    // Inter has ▶ and ‖ and Archivo has ≡ and ×; a glyph a font lacks has no outline and would only crossfade.
    private void PlayPause(object? sender, RoutedEventArgs e)
    {
        _playing = !_playing;
        Recipes.MorphGlyphs(PlayLabel, _playing ? "‖" : "▶");
    }

    private void Menu(object? sender, RoutedEventArgs e)
    {
        _menuOpen = !_menuOpen;
        Recipes.MorphGlyphs(MenuLabel, _menuOpen ? "×" : "≡");
    }
    #endregion

    #region snippet MorphClockStats
    private void Refresh(object? sender, RoutedEventArgs e)
    {
        foreach (var (morph, roll, flap) in _stats)
        {
            var value = _random.Next(100, 20000);
            Recipes.MorphGlyphs(morph, value.ToString());
            Recipes.RollDigits(roll, value, "0");
            flap.Text = value.ToString().PadLeft(5);
        }
    }
    #endregion
}
