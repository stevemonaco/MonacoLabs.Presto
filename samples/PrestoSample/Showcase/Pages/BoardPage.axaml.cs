using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Monaco.Presto;
using Monaco.Presto.AvaloniaBridge;
using Monaco.Presto.AvaloniaBridge.Compositor;
using Monaco.Presto.AvaloniaBridge.Draggables;

namespace PrestoSample.Showcase.Pages;

public partial class BoardPage : ShowcasePage
{
    private const double ReflowDuration = 420;
    private const double SpinDuration = 520;
    // Both pitches come from the page's styles: the lane width plus the column spacing, the card height plus the row gap.
    private const double LanePitch = 170;
    private const double RowPitch = 64;
    // Every lane keeps room for this many rows so that lifting a card does not resize the board: a container that
    // resizes refreshes the draggable 150ms later, which cancels a release still in flight (SAMPLES-PLAN §8).
    private const int LaneRows = 12;

    private readonly List<Border>[] _lanes = [.. Datasets.Lanes.Select(_ => new List<Border>())];
    private readonly Dictionary<Border, LayoutTransition> _reflow = [];
    private readonly Dictionary<Border, int> _lifted = [];

    private Canvas[] _canvases = [];
    private TextBlock[] _headers = [];
    private Border[] _zones = [];

    public BoardPage() => InitializeComponent();

    private int BinZone => Datasets.Lanes.Count;

    protected override void OnAttached()
    {
        _canvases = [.. Datasets.Lanes.Select((_, lane) => new Canvas { Name = $"Lane{lane}", VerticalAlignment = VerticalAlignment.Top })];
        _headers = [.. Datasets.Lanes.Select(_ => new TextBlock { Classes = { "eyebrow", "showcase" } })];
        _zones = [.. _canvases.Select((_, lane) => BuildLane(lane)), Bin];

        for (var lane = 0; lane < _lanes.Length; lane++)
        {
            foreach (var record in Datasets.Board[lane])
            {
                var card = BuildCard(record);
                _lanes[lane].Add(card);
                _canvases[lane].Children.Add(card);
            }
            Layout(lane);
        }
    }

    private Border BuildLane(int lane)
    {
        var border = new Border
        {
            Classes = { "lane" },
            Child = new Grid
            {
                RowDefinitions = new RowDefinitions("Auto,*"),
                Children = { _headers[lane], _canvases[lane] },
            },
        };
        Grid.SetRow(_canvases[lane], 1);
        Grid.SetColumn(border, lane);
        Board.Children.Add(border);
        return border;
    }

    private Border BuildCard(Datasets.Card record)
    {
        var card = CardView.Build(record, eyebrow: false);
        Arm(card);
        PrestoAvalonia.CreateDraggable(card, new DraggableParams
        {
            Container = Board,
            ContainerPadding = new Thickness(4),
            ContainerFriction = .9,
            Snap = Utils.Snap(LanePitch),
            Y = new DraggableAxisParams { Snap = Utils.Snap(RowPitch) },
            ReleaseMass = 1.2,
            ReleaseStiffness = 120,
            ReleaseDamping = 16,
            MinVelocity = .1,
            MaxVelocity = 1.5,
            ScrollSpeed = 2,
            ScrollThreshold = 28,
            OnGrab = _ => Grab(card),
            OnDrag = _ => Lift(card),
            OnSnap = _ => Aim(card),
            OnRelease = drag => VelocityText.Text = Utils.RoundPad(drag.Velocity, 2),
            OnSettle = drag => Land(card, drag),
        });
        return card;
    }

    private void Arm(Border card) =>
        _reflow[card] = Scope.Execute(_ => card.AnimateLayout(ReflowDuration, Eases.OutQuint));

    private void Layout(int lane)
    {
        var cards = _lanes[lane];
        for (var i = 0; i < cards.Count; i++) Canvas.SetTop(cards[i], i * RowPitch);
        _canvases[lane].Height = Math.Max(cards.Count, LaneRows) * RowPitch;
        _headers[lane].Text = $"{Datasets.Lanes[lane].ToUpperInvariant()} · {cards.Count}";
    }

    private void Grab(Border card)
    {
        // The drag writes the render transform and Measure reads it back, so a live transition would invert every
        // layout pass the drag outlives straight back against the pointer (SAMPLES-PLAN §8).
        if (_reflow.Remove(card, out var reflow)) reflow.Revert();
    }

    private void Lift(Border card)
    {
        if (_lifted.ContainsKey(card)) return;
        var lane = Array.FindIndex(_lanes, cards => cards.Contains(card));
        _lifted[card] = lane;
        _lanes[lane].Remove(card);
        card.ZIndex = 1;
        _zones[lane].ZIndex = 1;
        Layout(lane);
    }

    private void Aim(Border card)
    {
        if (!_lifted.ContainsKey(card)) return;
        var target = Zone(card);
        for (var i = 0; i < _zones.Length; i++) _zones[i].Classes.Set("target", i == target);
        DropText.Text = target == BinZone ? "bin" : $"{Datasets.Lanes[target]} · slot {Slot(card, target) + 1}";
    }

    private void Land(Border card, Draggable drag)
    {
        if (!_lifted.Remove(card, out var source))
        {
            Arm(card);
            return;
        }

        card.ZIndex = 0;
        _zones[source].ZIndex = _lifted.ContainsValue(source) ? 1 : 0;
        foreach (var zone in _zones) zone.Classes.Remove("target");

        var target = Zone(card);
        if (target == BinZone)
        {
            Spin(card, drag, source);
            return;
        }

        var slot = Slot(card, target);
        // Armed where the card is drawn and a line before it changes parents, so the transition plays the reparent.
        Arm(card);
        _canvases[source].Children.Remove(card);
        _canvases[target].Children.Add(card);
        _lanes[target].Insert(slot, card);
        // Written through the axes rather than Draggable.Reset(), which stops a card already spinning out (SAMPLES-PLAN §8).
        drag.X = 0;
        drag.Y = 0;
        Layout(source);
        Layout(target);
    }

    private void Spin(Border card, Draggable drag, int source)
    {
        var canvas = _canvases[source];
        drag.Disable();
        Scope.Execute(_ => card.Animate(new AvaloniaAnimationParams
        {
            ["Rotate"] = 150,
            ["Scale"] = .2,
            ["Opacity"] = 0,
            Duration = SpinDuration,
            Ease = Eases.InBack(2),
            OnComplete = _ =>
            {
                canvas.Children.Remove(card);
                drag.Revert();
            },
        }));
    }

    private int Zone(Border card)
    {
        var centre = card.TranslatePoint(new Point(card.Bounds.Width / 2, 0), Board)?.X ?? 0;
        var nearest = 0;
        var gap = double.MaxValue;
        for (var i = 0; i < _zones.Length; i++)
        {
            var distance = Math.Abs(_zones[i].Bounds.Center.X - centre);
            if (distance >= gap) continue;
            gap = distance;
            nearest = i;
        }
        return nearest;
    }

    private int Slot(Border card, int lane)
    {
        var top = card.TranslatePoint(default, _canvases[lane])?.Y ?? 0;
        return Math.Clamp((int)Math.Round(top / RowPitch), 0, _lanes[lane].Count);
    }
}
