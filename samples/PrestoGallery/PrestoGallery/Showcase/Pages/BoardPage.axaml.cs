using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using MonacoLabs.Presto;
using static MonacoLabs.Presto.PrestoKeys;
using MonacoLabs.Presto.Draggables;

namespace PrestoGallery.Showcase.Pages;

public partial class BoardPage : ShowcasePage
{
    private const double ReflowDuration = 420;
    private const double SpinDuration = 520;
    // Both pitches come from the page's styles: the lane width plus the column spacing, the card height plus the row gap.
    private const double LanePitch = 170;
    private const double RowPitch = 64;

    private readonly List<Border>[] _lanes = [.. Datasets.Lanes.Select(_ => new List<Border>())];
    private readonly Dictionary<Border, int> _lifted = [];
    private readonly Dictionary<Border, (int Lane, int Slot)> _aims = [];

    private Canvas[] _canvases = [];
    private TextBlock[] _headers = [];
    private Border[] _zones = [];

    public BoardPage()
    {
        InitializeComponent();
        HowCode.Code = $"{Snippets.BoardDraggable}\n\n{Snippets.BoardLand}";
        Priorities.ItemsSource = new ObservableCollection<string>(Datasets.Board[1].Take(5).Select(card => card.Title));
    }

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
        Drop.SetGroup(border, "lanes");
        Board.Children.Add(border);
        return border;
    }

    private Border BuildCard(Datasets.Card record)
    {
        var card = CardView.Build(record, eyebrow: false);
        Scope.Execute(_ => card.AnimateLayout(ReflowDuration, Eases.OutQuint));
        #region snippet BoardDraggable
        PrestoAvalonia.CreateDraggable(card, new DraggableParams
        {
            Container = Board,
            ContainerPadding = new Thickness(4),
            ContainerFriction = .9,
            Snap = Utils.Snap(LanePitch),
            Y = new DraggableAxisParams { Snap = Utils.Snap(RowPitch) },
            ReleaseMass = 1,
            ReleaseStiffness = 700,
            ReleaseDamping = 50,
            MaxVelocity = 1.5,
            ScrollSpeed = 2,
            ScrollThreshold = 28,
            DropGroup = "lanes",
            DropHit = DropHit.Overlap,
            DropMiss = DropMiss.ReturnHome,
            OnDrag = _ => Lift(card),
            // OnUpdate rather than OnSnap: it keeps firing through the release throw, so the gap follows a flung card.
            OnUpdate = drag => Aim(card, drag),
            OnRelease = drag => VelocityText.Text = Utils.RoundPad(drag.Velocity, 2),
            OnDrop = drag => Land(card, drag),
            OnDropMiss = _ => Return(card),
        });
        #endregion
        return card;
    }

    private void Layout(int lane, int? gapAt = null)
    {
        var cards = _lanes[lane];
        for (var i = 0; i < cards.Count; i++) Canvas.SetTop(cards[i], (i >= gapAt ? i + 1 : i) * RowPitch);
        _canvases[lane].Height = (cards.Count + (gapAt is null ? 0 : 1)) * RowPitch;
        _headers[lane].Text = $"{Datasets.Lanes[lane].ToUpperInvariant()} · {cards.Count}";
    }

    private void Lift(Border card)
    {
        if (_lifted.ContainsKey(card)) return;
        var lane = Array.FindIndex(_lanes, cards => cards.Contains(card));
        _lifted[card] = lane;
        _lanes[lane].Remove(card);
        card.ZIndex = 1;
        _zones[lane].ZIndex = 1;
    }

    private void Aim(Border card, Draggable drag)
    {
        var target = Array.IndexOf<Control?>(_zones, drag.Zone);
        if (target < 0 || !_lifted.ContainsKey(card)) return;
        var slot = target == BinZone ? 0 : Slot(card, target);
        var aimed = _aims.TryGetValue(card, out var aim);
        if (aimed && aim == (target, slot)) return;
        _aims[card] = (target, slot);
        if (aimed && aim.Lane != target && aim.Lane != BinZone) Layout(aim.Lane);
        if (target != BinZone) Layout(target, slot);
        DropText.Text = target == BinZone ? "bin" : $"{Datasets.Lanes[target]} · slot {slot + 1}";
    }

    private void Land(Border card, Draggable drag)
    {
        Aim(card, drag);
        _aims.Remove(card, out var aim);
        var source = PutDown(card);
        var (target, slot) = aim;

        if (target == BinZone)
        {
            Spin(card, drag, source);
            return;
        }

        #region snippet BoardLand
        drag.Reparent(_canvases[target]);
        _lanes[target].Insert(slot, card);
        #endregion
        Layout(source);
        Layout(target);
    }

    // Escape, or a throw that came to rest over no lane: the draggable springs the card home, so it takes back the slot it left.
    private void Return(Border card)
    {
        var source = PutDown(card);
        if (_aims.Remove(card, out var aim) && aim.Lane != source && aim.Lane != BinZone) Layout(aim.Lane);
        var cards = _lanes[source];
        cards.Insert(Math.Min((int)Math.Round(Canvas.GetTop(card) / RowPitch), cards.Count), card);
        Layout(source);
    }

    private int PutDown(Border card)
    {
        _lifted.Remove(card, out var source);
        card.ZIndex = 0;
        _zones[source].ZIndex = _lifted.ContainsValue(source) ? 1 : 0;
        return source;
    }

    private void Spin(Border card, Draggable drag, int source)
    {
        var canvas = _canvases[source];
        drag.Disable();
        Scope.Execute(_ => card.Animate(new AvaloniaAnimationParams
        {
            [Rotate] = 150,
            [Scale] = .2,
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

    private int Slot(Border card, int lane)
    {
        var top = card.TranslatePoint(default, _canvases[lane])?.Y ?? 0;
        return Math.Clamp((int)Math.Round(top / RowPitch), 0, _lanes[lane].Count);
    }
}
