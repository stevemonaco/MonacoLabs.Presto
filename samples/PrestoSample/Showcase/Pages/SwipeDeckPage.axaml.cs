using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Monaco.Presto;
using Monaco.Presto.AvaloniaBridge;
using Monaco.Presto.AvaloniaBridge.Draggables;

namespace PrestoSample.Showcase.Pages;

public partial class SwipeDeckPage : ShowcasePage
{
    private const int Shown = 3;
    private const double SlotY = 14;
    private const double SlotScale = .06;
    private const double Tilt = .06;
    private const double ExtraTilt = 18;
    private const double Lean = .06;
    private const double MaxLean = 8;
    private const double Depth = 800;
    private const double DismissAt = 110;
    private const double FlingVelocity = .5;
    private const double StampAt = 120;
    private const double ExitDistance = 640;
    private const double ExitDuration = 380;

    // Index 0 is the top card; a card on its way off-stage is already out of this list.
    private readonly List<Border> _stack = [];
    private readonly Stack<(double X, double Y, double Rotate)> _exits = [];
    private Draggable? _drag;
    private bool _held;
    private int _first;

    public SwipeDeckPage() => InitializeComponent();

    protected override void OnAttached()
    {
        for (var slot = 0; slot < Shown; slot++)
        {
            var card = Card(_first + slot, slot);
            _stack.Add(card);
            Deck.Children.Insert(0, card);
        }
        _drag = Draggable(_stack[0]);
    }

    private static int Wrap(int index) => ((index % Datasets.Places.Count) + Datasets.Places.Count) % Datasets.Places.Count;

    private static Border Stamp(Border card, bool like) => (Border)((Panel)card.Child!).Children[like ? 2 : 3];

    private static Border Card(int index, int slot)
    {
        var place = Datasets.Places[Wrap(index)];
        var card = new Border
        {
            Classes = { "place" },
            Tag = place,
            Child = new Grid
            {
                RowDefinitions = new RowDefinitions("*,Auto"),
                Children =
                {
                    new Border
                    {
                        Background = Palette.HueGradient(place.Hue),
                        Child = new TextBlock { Classes = { "monogram" }, Text = place.City[..1] },
                    },
                    new StackPanel
                    {
                        [Grid.RowProperty] = 1,
                        Margin = new Thickness(16, 12, 16, 16),
                        Spacing = 2,
                        Children =
                        {
                            new TextBlock { Text = place.City, FontSize = 20, FontWeight = FontWeight.SemiBold },
                            new TextBlock { Classes = { "caption" }, Text = place.Country },
                            new TextBlock { Classes = { "caption" }, Text = place.Blurb, Margin = new Thickness(0, 6, 0, 0), TextWrapping = TextWrapping.Wrap },
                        },
                    },
                    BuildStamp("LIKE", Palette.Aqua, HorizontalAlignment.Left, -14),
                    BuildStamp("NOPE", Palette.Coral, HorizontalAlignment.Right, 14),
                },
            },
        };
        PrestoAvalonia.Set(card, new AvaloniaAnimationParams { ["Scale"] = 1 - (slot * SlotScale), ["Y"] = slot * SlotY });
        return card;
    }

    private static Border BuildStamp(string text, IBrush brush, HorizontalAlignment side, double angle) => new()
    {
        Classes = { "stamp", text.ToLowerInvariant() },
        HorizontalAlignment = side,
        BorderBrush = brush,
        RenderTransform = new RotateTransform(angle),
        Child = new TextBlock { Text = text, Foreground = brush },
    };

    private Draggable Draggable(Border card) => Scope.Execute(_ => PrestoAvalonia.CreateDraggable(card, new DraggableParams
    {
        Snap = _ => 0,
        ReleaseStiffness = 180,
        ReleaseDamping = 18,
        OnGrab = _ => _held = true,
        OnUpdate = drag =>
        {
            if (_held) Follow(card, drag.X);
        },
        OnRelease = drag =>
        {
            var fling = drag.Velocity > FlingVelocity;
            if (!fling && Math.Abs(drag.X) <= DismissAt) return;
            var angle = fling ? drag.Angle : Math.Atan2(drag.Y, drag.X);
            Dismiss(Math.Cos(angle), Math.Sin(angle));
        },
        OnSettle = _ => _held = false,
    }));

    private void Follow(Border card, double x)
    {
        var lift = Math.Clamp(Math.Abs(x) / DismissAt, 0, 1);
        PrestoAvalonia.Set(card, new AvaloniaAnimationParams { ["Rotate"] = x * Tilt, ["RotateY"] = Math.Clamp(x * Lean, -MaxLean, MaxLean), ["Perspective"] = Depth });
        PrestoAvalonia.Set(Stamp(card, true), new AvaloniaAnimationParams { ["Opacity"] = Math.Clamp(x / StampAt, 0, 1) });
        PrestoAvalonia.Set(Stamp(card, false), new AvaloniaAnimationParams { ["Opacity"] = Math.Clamp(-x / StampAt, 0, 1) });
        for (var slot = 1; slot < _stack.Count; slot++)
        {
            PrestoAvalonia.Set(_stack[slot], new AvaloniaAnimationParams
            {
                ["Scale"] = 1 - ((slot - lift) * SlotScale),
                ["Y"] = (slot - lift) * SlotY,
            });
        }
    }

    private void Dismiss(double dx, double dy)
    {
        var drag = _drag!;
        var card = _stack[0];
        _stack.RemoveAt(0);
        drag.Disable();
        var like = dx >= 0;
        var exit = (X: drag.X + (dx * ExitDistance), Y: drag.Y + (dy * ExitDistance), Rotate: (drag.X * Tilt) + (like ? ExtraTilt : -ExtraTilt));
        _exits.Push(exit);
        UndoButton.IsEnabled = true;
        // Through the draggable's own animatable, so the flight retargets its release tween instead of racing it.
        drag.Animate.Set("X", exit.X, ExitDuration, Eases.OutQuad).Set("Y", exit.Y, ExitDuration, Eases.OutQuad);
        _first = Wrap(_first + 1);
        var entering = Card(_first + Shown - 1, Shown - 1);
        PrestoAvalonia.Set(entering, new AvaloniaAnimationParams { ["Opacity"] = 0 });
        _stack.Add(entering);
        Deck.Children.Insert(0, entering);
        Scope.Execute(_ =>
        {
            Stamp(card, like).Animate(new() { ["Opacity"] = 1, Duration = 120 });
            card.Animate(new()
            {
                ["Rotate"] = exit.Rotate,
                ["RotateY"] = 0,
                Duration = ExitDuration,
                Ease = Eases.OutQuad,
                OnComplete = _ =>
                {
                    Deck.Children.Remove(card);
                    drag.Revert();
                },
            });
            entering.Animate(new() { ["Opacity"] = 1, Duration = 240, Ease = Eases.OutQuad });
        });
        Seat();
    }

    private void Seat()
    {
        _held = false;
        var top = _stack[0];
        var drag = _drag = Draggable(top);
        Scope.Execute(_ =>
        {
            Presto.Animate(drag, new AnimationParams { ["X"] = 0, ["Y"] = 0, Ease = Eases.Spring(bounce: .25, duration: 600) });
            top.Animate(new() { ["Scale"] = 1, ["Rotate"] = 0, ["RotateY"] = 0, Ease = Eases.Spring(bounce: .25, duration: 600) });
            for (var slot = 1; slot < _stack.Count; slot++)
            {
                _stack[slot].Animate(new()
                {
                    ["Scale"] = 1 - (slot * SlotScale),
                    ["Y"] = slot * SlotY,
                    Ease = Eases.Spring(bounce: .2, duration: 600),
                });
            }
        });
    }

    private void NopeClicked(object? sender, RoutedEventArgs e) => Dismiss(-1, 0);

    private void LikeClicked(object? sender, RoutedEventArgs e) => Dismiss(1, 0);

    private void UndoClicked(object? sender, RoutedEventArgs e)
    {
        if (!_exits.TryPop(out var exit)) return;
        var drag = _drag!;
        UndoButton.IsEnabled = _exits.Count > 0;
        var top = _stack[0];
        var (x, y) = (drag.X, drag.Y);
        drag.Revert();
        PrestoAvalonia.Set(top, new AvaloniaAnimationParams { ["X"] = x, ["Y"] = y });

        var bottom = _stack[^1];
        _stack.RemoveAt(_stack.Count - 1);
        Scope.Execute(_ => bottom.Animate(new()
        {
            ["Opacity"] = 0,
            Duration = 200,
            OnComplete = _ => Deck.Children.Remove(bottom),
        }));

        _first = Wrap(_first - 1);
        var card = Card(_first, 0);
        PrestoAvalonia.Set(card, new AvaloniaAnimationParams { ["X"] = exit.X, ["Y"] = exit.Y, ["Rotate"] = exit.Rotate });
        _stack.Insert(0, card);
        Deck.Children.Add(card);
        Seat();
    }
}
