using Avalonia.Controls;
using Avalonia.Interactivity;
using MonacoLabs.Presto;
using MonacoLabs.Presto.Controls;
using Timer = MonacoLabs.Presto.Timer;

namespace PrestoGallery.Showcase.Pages;

public partial class MorphClockPage : ShowcasePage
{
    private const int CountFrom = 10;

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
        Clock.Text = start.ToString("HH:mm:ss");
        _stats = [(MorphVisitors, RollVisitors, FlapVisitors), (MorphOrders, RollOrders, FlapOrders), (MorphReturns, RollReturns, FlapReturns)];
        HowCode.Code = string.Join("\n\n", Snippets.MorphClockTick, Snippets.MorphClockCountdown, Snippets.MorphClockButtons, Snippets.MorphClockStats);
    }

    protected override void OnAttached() =>
        Presto.CreateTimer(new TimerParams { Duration = 1000, Loop = true, OnLoop = _ => Tick(_start.AddSeconds(++_elapsed)) });

    #region snippet MorphClockTick
    // Numerals pair from the end, so a tick morphs only the digits that change; the colons and the rest stay the block's own text.
    // Under reduced motion a ticking clock changes at once rather than crossfading every second.
    private void Tick(DateTime now) => Recipes.MorphGlyphs(Clock, now.ToString("HH:mm:ss"), reduced: ReducedMorph.Instant);
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
        Recipes.MorphGlyphs(Countdown, $"0:{_left:00}", reduced: ReducedMorph.Instant);
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
        await Task.Delay(1100);
        ShowSubmit(SubmitState.Succeeded);
        await Presto.Delay(1400);
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
