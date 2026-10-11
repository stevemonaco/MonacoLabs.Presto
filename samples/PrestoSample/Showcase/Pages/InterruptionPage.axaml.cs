using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Interactivity;
using MonacoLabs.Presto;
using static MonacoLabs.Presto.PrestoKeys;

namespace PrestoSample.Showcase.Pages;

public partial class InterruptionPage : ShowcasePage
{
    private const double RetargetDuration = 900;
    private const double BurstSpacing = 160;
    private const int BurstTargets = 5;
    private const int BurstSeed = 7;
    private const int TrailLength = 24;
    private const double TrailDot = 8;
    private const double HoverDuration = 500;
    private const double HoverLift = -14;
    private const double HoverScale = 1.12;

    private static readonly Composition[] Modes = [Composition.Replace, Composition.Overlap, Composition.Additive];
    private static readonly string[] Names = ["replace", "overlap", "additive"];

    private readonly Canvas[] _panes;
    private readonly Border[] _pucks;
    private readonly TextBlock[] _counts;
    private readonly Ellipse[][] _trails;
    private readonly int[] _shown = [-1, -1, -1];

    private int _trailHead;
    private Timeline? _burst;

    public InterruptionPage()
    {
        InitializeComponent();
        HowCode.Code = $"{Snippets.InterruptionRetarget}\n\n{Snippets.InterruptionCancelNewest}\n\n{Snippets.InterruptionHover}";
        _panes = [ReplacePane, OverlapPane, AdditivePane];
        _pucks = [ReplacePuck, OverlapPuck, AdditivePuck];
        _counts = [ReplaceCount, OverlapCount, AdditiveCount];
        _trails = new Ellipse[Modes.Length][];
        for (var i = 0; i < Modes.Length; i++)
        {
            _trails[i] = new Ellipse[TrailLength];
            for (var k = 0; k < TrailLength; k++)
            {
                var dot = new Ellipse
                {
                    Width = TrailDot,
                    Height = TrailDot,
                    Fill = _pucks[i].Background,
                    Opacity = 0,
                    IsHitTestVisible = false,
                };
                _trails[i][k] = dot;
                _panes[i].Children.Insert(0, dot);
            }
        }
    }

    /// <summary>How many animations are still running on <paramref name="mode"/>'s puck.</summary>
    public int Alive(Composition mode) => Presto.GetAnimations(_pucks[Array.IndexOf(Modes, mode)]).Count;

    /// <summary>Retargets all three pucks five times, 160 ms apart, along the same seeded script every time.</summary>
    public void Burst()
    {
        var random = Utils.CreateSeededRandom(BurstSeed);
        _burst = Presto.CreateTimeline();
        for (var k = 0; k < BurstTargets; k++)
        {
            var target = new Point(random(0.1, 0.9, 2), random(0.1, 0.9, 2));
            _burst.Call(_ => Retarget(target), k * BurstSpacing);
        }
    }

    /// <summary>Cancels each pane's most recent animation that is still running.</summary>
    public void CancelNewest()
    {
        #region snippet InterruptionCancelNewest
        foreach (var puck in _pucks)
        {
            if (Presto.GetAnimations(puck) is [.., var newest]) newest.Cancel();
        }
        #endregion
    }

    protected override void OnAttached()
    {
        for (var i = 0; i < Modes.Length; i++) ClearTrail(i);
        Presto.CreateTimer(new TimerParams { OnUpdate = _ => Refresh() });
    }

    private void Refresh()
    {
        _trailHead = (_trailHead + 1) % TrailLength;
        for (var i = 0; i < Modes.Length; i++)
        {
            Place(_trails[i][_trailHead], Drawn(Modes[i]));
            for (var age = 0; age < TrailLength; age++)
            {
                _trails[i][(_trailHead - age + TrailLength) % TrailLength].Opacity = 0.7 * (1 - ((double)age / TrailLength));
            }
            var alive = Alive(Modes[i]);
            if (alive == _shown[i]) continue;
            _shown[i] = alive;
            _counts[i].Text = string.Create(CultureInfo.InvariantCulture, $"{Names[i]} · {alive} live");
        }
    }

    /// <summary>Where <paramref name="mode"/>'s puck center is drawn in its pane: its canvas slot plus the tweened translation.</summary>
    public Point Drawn(Composition mode)
    {
        var puck = _pucks[Array.IndexOf(Modes, mode)];
        return new Point(
            Canvas.GetLeft(puck) + (double)Presto.Get(puck, X)! + (puck.Width / 2),
            Canvas.GetTop(puck) + (double)Presto.Get(puck, Y)! + (puck.Height / 2));
    }

    private static void Place(Ellipse dot, Point center)
    {
        Canvas.SetLeft(dot, center.X - (TrailDot / 2));
        Canvas.SetTop(dot, center.Y - (TrailDot / 2));
    }

    private void Retarget(Point relative)
    {
        for (var i = 0; i < Modes.Length; i++)
        {
            var pane = _panes[i];
            var puck = _pucks[i];
            var mode = Modes[i];
            #region snippet InterruptionRetarget
            Scope.Execute(_ => puck.Animate(b => b
                .X((relative.X * pane.Bounds.Width) - Canvas.GetLeft(puck) - (puck.Width / 2))
                .Y((relative.Y * pane.Bounds.Height) - Canvas.GetTop(puck) - (puck.Height / 2))
                .Duration(RetargetDuration)
                .Ease(Eases.OutQuad)
                .Composition(mode)));
            #endregion
        }
    }

    private void TileEntered(object? sender, PointerEventArgs e) => Hover((Border)sender!, raised: true);

    private void TileExited(object? sender, PointerEventArgs e) => Hover((Border)sender!, raised: false);

    private void Hover(Border tile, bool raised)
    {
        var (fromY, toY, fromScale, toScale) = raised ? (0d, HoverLift, 1d, HoverScale) : (HoverLift, 0d, HoverScale, 1d);
        var restart = HoverMode.IsChecked == true;
        #region snippet InterruptionHover
        Scope.Execute(_ => Presto.Animate(tile, new AvaloniaAnimationParams
        {
            [Y] = restart ? TweenValue.FromTo(fromY, toY) : toY,
            [Scale] = restart ? TweenValue.FromTo(fromScale, toScale) : toScale,
            Duration = HoverDuration,
            Ease = Eases.OutQuint,
        }));
        #endregion
    }

    private void PanePressed(object? sender, PointerPressedEventArgs e)
    {
        var pane = (Canvas)sender!;
        var click = e.GetPosition(pane);
        Retarget(new Point(click.X / pane.Bounds.Width, click.Y / pane.Bounds.Height));
    }

    private void BurstClicked(object? sender, RoutedEventArgs e) => Burst();

    private void CancelNewestClicked(object? sender, RoutedEventArgs e) => CancelNewest();

    private void ResetPucksClicked(object? sender, RoutedEventArgs e)
    {
        _burst?.Cancel();
        for (var i = 0; i < Modes.Length; i++)
        {
            Presto.Remove(_pucks[i]);
            Presto.Set(_pucks[i], new AvaloniaAnimationParams { [X] = 0, [Y] = 0 });
            ClearTrail(i);
        }
    }

    private void ClearTrail(int pane)
    {
        foreach (var dot in _trails[pane]) Place(dot, Drawn(Modes[pane]));
    }
}
