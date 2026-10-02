using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using Monaco.Presto;
using Monaco.Presto.AvaloniaBridge;
using Monaco.Presto.AvaloniaBridge.Compositor;

namespace PrestoSample.Pages;

public partial class LayoutPage : UserControl
{
    private static readonly (string Icon, string Title)[] Kinds =
        [("HeartIcon", "Liked"), ("StarIcon", "Starred"), ("PlayIcon", "Playing")];

    private static readonly string[] Views = ["ShortView", "ParagraphView", "IconsView"];

    private readonly Scope _scope = new();
    private int _next;
    private int _view;
    private bool _moved;

    public LayoutPage()
    {
        InitializeComponent();
        for (var i = 0; i < 8; i++) AddCard();
        SlotA.Child = NewSquare();
        SwapHost.Child = BuildView();
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
        // Moving rather than re-adding keeps the XAML card attached; Animate.Layout records afresh on every attach.
        foreach (var card in Utils.Shuffle(Cards.Children.ToArray())) Cards.Children.Move(Cards.Children.IndexOf(card), Cards.Children.Count - 1);
    }

    private void Add(object? sender, RoutedEventArgs e) => AddCard();

    private void RemoveCard(object? sender, RoutedEventArgs e)
    {
        if (Cards.Children.Count > 0) Cards.Children.RemoveAt(Cards.Children.Count - 1);
    }

    private void Grow(object? sender, RoutedEventArgs e)
    {
        GrowBox.Width = GrowBox.Width > 160 ? 120 : 220;
        (AvatarCard.Width, AvatarCard.Height) = AvatarCard.Width > 200 ? (150, 56) : (240, 112);
    }

    private void NextView(object? sender, RoutedEventArgs e) => SwapHost.Child = BuildView();

    private Control BuildView() => ((IDataTemplate)Resources[Views[_view++ % Views.Length]]!).Build(null)!;

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

    private void Rearm(object? sender, RoutedEventArgs e) => Arm();

    private void Arm()
    {
        foreach (var card in Cards.Children)
        {
            if (card != XamlCard) _scope.Execute(_ => card.AnimateLayout(Duration, Eases.OutQuint));
        }
        var avatarMode = Uncorrected.IsChecked == true ? LayoutAnimateMode.PositionAndSize : LayoutAnimateMode.PositionAndSizeCorrected;
        _scope.Execute(_ =>
        {
            GrowBox.AnimateLayout(Duration, Eases.OutQuint, LayoutAnimateMode.PositionAndSize);
            AvatarCard.AnimateLayout(Duration, Eases.OutQuint, avatarMode);
            SwapHost.AnimateContentSize(Duration, Eases.OutQuint);
        });
        Animate.SetContentSize(Faq, Recipes.Reflow(Duration, ease: Eases.OutQuint));
    }

    private void AddCard()
    {
        var (icon, title) = Kinds[_next % Kinds.Length];
        var card = new Border
        {
            Classes = { "card" },
            Child = new Grid
            {
                RowDefinitions = new RowDefinitions("*,Auto"),
                Children =
                {
                    new PathIcon { Data = (Geometry)Application.Current!.FindResource(icon)! },
                    new TextBlock { Classes = { "number" }, Text = (++_next).ToString() },
                    new TextBlock { Text = title, [Grid.RowProperty] = 1 },
                },
            },
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
