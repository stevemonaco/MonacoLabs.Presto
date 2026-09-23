using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using Monaco.Presto;
using Monaco.Presto.AvaloniaBridge;
using Monaco.Presto.AvaloniaBridge.Compositor;

namespace PrestoSample.Pages;

public partial class LayoutPage : UserControl
{
    private readonly Scope _scope = new();
    private int _next;
    private bool _moved;

    public LayoutPage()
    {
        InitializeComponent();
        for (var i = 0; i < 8; i++) AddCard();
        SlotA.Child = NewSquare();
        Arm();
    }

    private double Duration => Slow.IsChecked == true ? 4000 : 400;

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _scope.Revert();
    }

    private void Shuffle(object? sender, RoutedEventArgs e)
    {
        var cards = Cards.Children.ToArray();
        Cards.Children.Clear();
        foreach (var card in Utils.Shuffle(cards)) Cards.Children.Add(card);
    }

    private void Add(object? sender, RoutedEventArgs e) => AddCard();

    private void RemoveCard(object? sender, RoutedEventArgs e)
    {
        if (Cards.Children.Count > 0) Cards.Children.RemoveAt(Cards.Children.Count - 1);
    }

    private void Grow(object? sender, RoutedEventArgs e) => GrowBox.Width = GrowBox.Width > 160 ? 120 : 220;

    private void MoveShared(object? sender, RoutedEventArgs e)
    {
        var (from, to) = _moved ? (SlotB, SlotA) : (SlotA, SlotB);
        if (from.Child is not Border square) return;

        _moved = !_moved;
        _scope.Execute(_ => PrestoAvalonia.AnimateSharedElement(square, square, Duration, Eases.OutQuint));
        from.Child = null;
        to.Child = square;
    }

    // Posted below Render priority so the button paints its pressed state before the dispatcher stops answering.
    private void Freeze(object? sender, RoutedEventArgs e) =>
        Dispatcher.UIThread.Post(() => Thread.Sleep(2000), DispatcherPriority.Background);

    /// <summary>A transition carries the duration it was armed with, so changing the speed re-arms every control.</summary>
    private void SpeedChanged(object? sender, RoutedEventArgs e) => Arm();

    private void Arm()
    {
        foreach (var card in Cards.Children) _scope.Execute(_ => ((Control)card).AnimateLayout(Duration, Eases.OutQuint));
        _scope.Execute(_ => GrowBox.AnimateLayout(Duration, Eases.OutQuint, LayoutAnimateMode.PositionAndSize));
    }

    private void AddCard()
    {
        var card = new Border
        {
            Classes = { "card" },
            Child = new TextBlock { Text = (++_next).ToString() },
        };
        _scope.Execute(_ => card.AnimateLayout(Duration, Eases.OutQuint));
        Cards.Children.Add(card);
    }

    private static Border NewSquare() => new()
    {
        Width = 56,
        Height = 56,
        CornerRadius = new CornerRadius(6),
        Background = Brushes.OrangeRed,
    };
}
