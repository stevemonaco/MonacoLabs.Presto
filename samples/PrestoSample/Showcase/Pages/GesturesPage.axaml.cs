using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Monaco.Presto;
using Monaco.Presto.AvaloniaBridge;
using Monaco.Presto.AvaloniaBridge.Draggables;

namespace PrestoSample.Showcase.Pages;

public partial class GesturesPage : ShowcasePage
{
    private const double MaxPull = 140;
    private const double Threshold = 64;
    private const double RefreshDelay = 1200;
    private const double RowHeight = 40;
    private const double SlotHeight = 44;
    private const double DismissAt = .4;
    private const double HandleHeight = 28;

    private readonly Stack<(Panel Slot, int Index, int Side)> _dismissed = [];
    private Draggable? _curtain;
    private Draggable? _sheet;
    private bool _pulling;
    private bool _handleTapped;
    private int _fresh = 8;

    public GesturesPage()
    {
        InitializeComponent();
        HowCode.Code = $"{Snippets.GesturesCurtain}\n\n{Snippets.GesturesInboxReveal}";
        foreach (var album in Datasets.Albums.Take(_fresh)) Feed.Children.Add(FeedRow(album));
        foreach (var card in Datasets.Cards.Take(8)) Inbox.Children.Add(Slot(card));
        foreach (var album in Datasets.Albums.Skip(8).Take(4)) Home.Children.Add(FeedRow(album));
        foreach (var album in Datasets.Albums.Skip(4).Take(3)) Notifications.Children.Add(FeedRow(album));
        foreach (var album in Datasets.Albums.Skip(12).Take(5)) Queue.Children.Add(FeedRow(album));
        Handle.PointerPressed += (_, _) => _handleTapped = true;
    }

    private double Travel => Math.Max(0, CurtainStage.Bounds.Height - HandleHeight);

    private double Rest(double step) => (step / 2 - 1) * Travel;

    private double SheetTravel => Math.Max(0, Sheet.Bounds.Height - HandleHeight);

    protected override void OnAttached()
    {
        PrestoAvalonia.CreateDrawable(Ring);
        var pull = PrestoAvalonia.CreateDraggable(Feed, new DraggableParams
        {
            Trigger = PullViewport,
            X = new DraggableAxisParams { Enabled = false },
            Y = new DraggableAxisParams
            {
                // Applied only while the pointer holds the list: the release re-applies an axis modifier to its own output (SAMPLES-PLAN §8).
                Modifier = y => _pulling ? MaxPull * (1 - Math.Exp(-Math.Max(y, 0) / MaxPull)) : y,
                Snap = y => y >= Threshold ? Threshold : 0,
            },
            OnGrab = _ => _pulling = true,
            OnRelease = _ => _pulling = false,
            OnUpdate = drag =>
            {
                PullVelocity.Text = Utils.RoundPad(drag.Velocity, 2);
                // Follow has no Draw shorthand.
                PrestoAvalonia.Set(Ring, new AvaloniaAnimationParams { ["Draw"] = $"0 {Math.Clamp(drag.Y / Threshold, 0, 1) * .8}" });
            },
            OnSettle = drag =>
            {
                PullVelocity.Text = Utils.RoundPad(drag.Velocity, 2);
                if (drag.Y >= Threshold) Refresh(drag);
            },
        });
        Follow.SetRotate(Arrow, pull.YValue.Map([0, Threshold], [0, 180]));
        foreach (var slot in Inbox.Children.OfType<Panel>()) Swipeable(slot);
        _curtain = PrestoAvalonia.CreateDraggable(Shade, new DraggableParams
        {
            X = new DraggableAxisParams { Enabled = false },
            ContainerBounds = _ => new Rect(0, -Travel, 0, Travel),
            ContainerFriction = .7,
            Snap = y => Rest(Math.Round(2 + 2 * y / Travel)),
            OnDrag = _ => _handleTapped = false,
            OnUpdate = drag => CurtainVelocity.Text = Utils.RoundPad(drag.Velocity, 2),
            OnRelease = drag =>
            {
                if (!_handleTapped) return;
                _handleTapped = false;
                drag.Animate.Set("Y", Rest((Math.Round(drag.ProgressY * 2) + 1) % 3), 600, Eases.Spring(bounce: .25, duration: 600));
            },
            OnSettle = drag => CurtainVelocity.Text = Utils.RoundPad(drag.Velocity, 2),
        });
        #region snippet GesturesCurtain
        var pulled = _curtain.YValue.Map(y => Travel > 0 ? 1 + (y / Travel) : 0);
        Follow.SetY(Home, pulled.Map(p => .3 * p * Travel));
        Follow.SetScale(Home, pulled.Map([0, 1], [1, .94]));
        Follow.Bind(Scrim, OpacityProperty, pulled.Map([0, 1], [0, .6]));
        #endregion
        _sheet = PrestoAvalonia.CreateDraggable(Sheet, new DraggableParams
        {
            X = new DraggableAxisParams { Enabled = false },
            ContainerBounds = _ => new Rect(0, 0, 0, SheetTravel),
            // Stops a whole travel apart, so a flick aimed past one crosses its bound and bounces there.
            Snap = y => Utils.Snap(y, SheetTravel),
            ReleaseInertia = new Inertia(0),
            OnUpdate = drag => SheetVelocity.Text = Utils.RoundPad(drag.Velocity, 2),
            OnSettle = drag => SheetVelocity.Text = Utils.RoundPad(drag.Velocity, 2),
        });
        AfterFirstArrange(CurtainStage, () =>
        {
            _curtain!.Refresh().Y = Rest(0);
            _sheet!.Refresh().Y = SheetTravel;
        });
    }

    private void Refresh(Draggable drag)
    {
        drag.Disable();
        Scope.Execute(_ =>
        {
            Arrow.Animate(new() { ["Opacity"] = 0, Duration = 150 });
            var spinner = Ring.Animate(new() { ["Rotate"] = 360, Duration = 800, Ease = Eases.None, Loop = true });
            Presto.CreateTimer(new TimerParams { Duration = RefreshDelay, OnComplete = _ => Scope.Execute(_ => Refreshed(drag, spinner)) });
        });
    }

    private void Refreshed(Draggable drag, Animation spinner)
    {
        spinner.Revert();
        var rows = Enumerable.Range(0, 3)
            .Select(i => FeedRow(Datasets.Albums[(_fresh + i) % Datasets.Albums.Count]))
            .ToArray();
        _fresh += rows.Length;
        Feed.Children.InsertRange(0, rows);
        PrestoAvalonia.Set(rows, new AvaloniaAnimationParams { ["Height"] = 0, ["Opacity"] = 0 });
        Presto.Animate(rows, new AvaloniaAnimationParams
        {
            ["Height"] = RowHeight,
            ["Opacity"] = 1,
            Duration = 420,
            Ease = Eases.OutQuart,
            Delay = Presto.Stagger(70),
        });
        Arrow.Animate(new() { ["Opacity"] = 1, Duration = 200 });
        Presto.Animate(drag, new AnimationParams
        {
            ["Y"] = 0,
            Ease = Eases.Spring(bounce: .2, duration: 600),
            OnComplete = _ => drag.Enable(),
        });
    }

    private Draggable Swipeable(Panel slot)
    {
        var strip = (Border)slot.Children[0];
        var icons = ((Panel)strip.Child!).Children;
        var drag = PrestoAvalonia.CreateDraggable(slot.Children[1], new DraggableParams
        {
            Y = new DraggableAxisParams { Enabled = false },
            Snap = x => Math.Abs(x) > DismissAt * slot.Bounds.Width ? Math.Sign(x) * slot.Bounds.Width : 0,
            ReleaseStiffness = 180,
            ReleaseDamping = 22,
            OnUpdate = drag => InboxVelocity.Text = Utils.RoundPad(drag.Velocity, 2),
            OnSettle = drag =>
            {
                InboxVelocity.Text = Utils.RoundPad(drag.Velocity, 2);
                if (Math.Abs(drag.X) >= slot.Bounds.Width - 1) Dismiss(slot, drag);
            },
        });
        #region snippet GesturesInboxReveal
        var x = drag.XValue;
        Follow.Bind(strip, Border.BackgroundProperty, x.Map([-1, 0, 1], [Palette.Coral, Brushes.Transparent, Palette.Aqua]));
        Follow.Bind(icons[0], OpacityProperty, x.Map([-1, 0], [0, 1]));
        Follow.Bind(icons[1], OpacityProperty, x.Map([-1, 0], [1, 0]));
        var pop = x.Map(v => Math.Abs(v) > DismissAt * slot.Bounds.Width ? 1 : .5).Spring(Springs.Bouncy);
        foreach (var icon in icons) Follow.SetScale(icon, pop);
        #endregion
        return drag;
    }

    private void Dismiss(Panel slot, Draggable drag)
    {
        var side = Math.Sign(drag.X);
        Scope.Execute(_ => slot.Animate(new()
        {
            ["Height"] = 0,
            Duration = 360,
            Ease = Eases.OutQuart,
            OnComplete = _ =>
            {
                _dismissed.Push((slot, Inbox.Children.IndexOf(slot), side));
                Inbox.Children.Remove(slot);
                drag.Revert();
                UndoButton.IsEnabled = true;
            },
        }));
    }

    private void UndoClicked(object? sender, RoutedEventArgs e)
    {
        if (!_dismissed.TryPop(out var dismissed)) return;
        var (slot, index, side) = dismissed;
        UndoButton.IsEnabled = _dismissed.Count > 0;
        Inbox.Children.Insert(Math.Min(index, Inbox.Children.Count), slot);
        Scope.Execute(_ =>
        {
            var drag = Swipeable(slot);
            drag.X = side * Inbox.Bounds.Width;
            slot.Animate(new() { ["Height"] = SlotHeight, Duration = 360, Ease = Eases.OutQuart });
            Presto.Animate(drag, new AnimationParams { ["X"] = 0, Duration = 480, Delay = 120, Ease = Eases.OutQuart });
        });
    }

    private static Border FeedRow(Datasets.Album album) => new()
    {
        Classes = { "row" },
        Child = new StackPanel
        {
            VerticalAlignment = VerticalAlignment.Center,
            Children =
            {
                new TextBlock { Text = album.Title },
                new TextBlock { Classes = { "caption" }, Text = $"{album.Artist} · {album.Year}" },
            },
        },
    };

    private Panel Slot(Datasets.Card card) => new()
    {
        Height = SlotHeight,
        ClipToBounds = true,
        Children =
        {
            new Border
            {
                Classes = { "row" },
                Padding = default,
                Child = new Panel
                {
                    Children =
                    {
                        new PathIcon { Data = (Geometry)this.FindResource("ArchiveIcon")!, HorizontalAlignment = HorizontalAlignment.Left },
                        new PathIcon { Data = (Geometry)this.FindResource("DeleteIcon")!, HorizontalAlignment = HorizontalAlignment.Right },
                    },
                },
            },
            new Border
            {
                Classes = { "row" },
                Child = new StackPanel
                {
                    VerticalAlignment = VerticalAlignment.Center,
                    Children =
                    {
                        new TextBlock { Text = card.Owner, FontWeight = FontWeight.SemiBold },
                        new TextBlock { Classes = { "caption" }, Text = card.Title },
                    },
                },
            },
        },
    };
}
