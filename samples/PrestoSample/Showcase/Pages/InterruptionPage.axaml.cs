using System.Globalization;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Monaco.Presto;
using Monaco.Presto.AvaloniaBridge;

namespace PrestoSample.Showcase.Pages;

public partial class InterruptionPage : ShowcasePage
{
    private const double RetargetDuration = 900;
    private const double TravelEnd = 460;
    private const double TravelDuration = 4200;
    private const double WobbleReach = 18;

    private static readonly Composition[] Modes = [Composition.Replace, Composition.None, Composition.Blend];
    private static readonly string[] Names = ["replace", "none", "blend"];

    private readonly List<Animation>[] _made = [[], [], []];
    private readonly Border[] _pucks;
    private readonly TextBlock[] _counts;

    private int _clicks;

    public InterruptionPage()
    {
        InitializeComponent();
        _pucks = [ReplacePuck, NonePuck, BlendPuck];
        _counts = [ReplaceCount, NoneCount, BlendCount];
    }

    /// <summary>The card's looping journey, which a blended wobble adds to and a replaced one cannot reach.</summary>
    public Animation? Travel { get; private set; }

    /// <summary>
    /// How many of the animations this page made for <paramref name="mode"/>'s puck are still running, dropping the
    /// ones that are not so the list cannot grow without bound.
    /// </summary>
    public int Alive(Composition mode)
    {
        var made = _made[Array.IndexOf(Modes, mode)];
        made.RemoveAll(animation => animation.Cancelled || animation.Completed);
        return made.Count;
    }

    protected override void OnAttached()
    {
        Travel = Presto.Animate(Card, new AvaloniaAnimationParams
        {
            ["X"] = TravelEnd,
            Duration = TravelDuration,
            Ease = Eases.InOutSine,
            Loop = true,
            Alternate = true,
            // Blended so a blended wobble can add to it; a replaced travel would be killed by a replaced wobble and
            // hidden by a blended one, so neither half of the toggle would be worth watching (SAMPLES-PLAN §8).
            Composition = Composition.Blend,
        });
        Presto.CreateTimer(new TimerParams { OnUpdate = _ => Refresh() });
    }

    private void Refresh()
    {
        for (var i = 0; i < Modes.Length; i++)
        {
            _counts[i].Text = string.Create(CultureInfo.InvariantCulture, $"{Names[i]} · {Alive(Modes[i])} live");
        }
        ClickCount.Text = string.Create(CultureInfo.InvariantCulture, $"{_clicks} clicks");
        TravelText.Text = string.Create(CultureInfo.InvariantCulture, $"card x {Presto.Get(Card, "X"):0}");
    }

    private void ClickStagePressed(object? sender, PointerPressedEventArgs e)
    {
        var click = e.GetPosition(ClickStage);
        _clicks++;
        for (var i = 0; i < Modes.Length; i++)
        {
            var puck = _pucks[i];
            // Outside Scope on purpose: a scope holds every animation made inside it until it reverts, and a spammed
            // stage makes them faster than the page could ever revert them (SAMPLES-PLAN §8).
            _made[i].Add(Presto.Animate(puck, new AvaloniaAnimationParams
            {
                ["X"] = click.X - Canvas.GetLeft(puck) - (puck.Bounds.Width / 2),
                ["Y"] = click.Y - Canvas.GetTop(puck) - (puck.Bounds.Height / 2),
                Duration = RetargetDuration,
                Ease = Eases.OutQuad,
                Composition = Modes[i],
            }));
        }
    }

    private void ResetPucksClicked(object? sender, RoutedEventArgs e)
    {
        foreach (var puck in _pucks)
        {
            Presto.Remove(puck);
            Presto.Set(puck, new AvaloniaAnimationParams { ["X"] = 0, ["Y"] = 0 });
        }
        _clicks = 0;
    }

    private void CardEntered(object? sender, PointerEventArgs e) => Presto.Animate(Card, new AvaloniaAnimationParams
    {
        // Aimed at the travel's destination rather than at the card: a blended tween's To becomes the additive group's
        // base, so a wobble aimed around 0 would take the travel back to the start with it.
        ["X"] = new TweenParams[]
        {
            new() { To = TravelEnd + WobbleReach },
            new() { To = TravelEnd - WobbleReach },
            new() { To = TravelEnd },
        },
        Duration = 140,
        Ease = Eases.InOutSine,
        Loop = 2,
        Composition = BlendWobble.IsChecked == true ? Composition.Blend : Composition.Replace,
    });
}
