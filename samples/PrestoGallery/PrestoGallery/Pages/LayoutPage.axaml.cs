using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Interactivity;
using Avalonia.Media;
using MonacoLabs.Presto;
using MonacoLabs.Presto.Compositor;
using PrestoGallery.Showcase;

namespace PrestoGallery.Pages;

public partial class LayoutPage : ShowcasePage
{
    private static readonly (string Icon, string Title)[] Kinds =
        [("HeartIcon", "Liked"), ("StarIcon", "Starred"), ("PlayIcon", "Playing")];

    private static readonly string[] Views = ["ShortView", "ParagraphView", "IconsView"];

    private static readonly string[] TagNames =
        ["Avalonia", "Motion", "FLIP", "Springs", "Compositor", "Stagger", "Timelines", "Easing", "Presence", "Layout", "Gestures", "Lottie", "Keyframes", "Morph"];

    // Coral and AccentPale are left to the avatar card, whose test finds it by color.
    private static readonly IBrush[] Swatches = [Palette.Accent, Palette.Aqua, Palette.Amber, Palette.Violet, Palette.Lime, Palette.Rose];

    public const double WideFrame = 560;
    private const double NarrowFrame = 300;

    private int _next;
    private int _view;
    private bool _moved;
    private int _nextTag;

    public LayoutPage()
    {
        InitializeComponent();
        for (var i = 0; i < 8; i++) AddCard();
        SlotA.Child = NewSquare();
        SwapHost.Child = BuildView();
        for (var i = 0; i < 7; i++) AddTag(Tags.Children.Count);
        for (var i = 0; i < 10; i++) Tiles.Children.Add(NewTile(i));
        JustifyBox.ItemsSource = Enum.GetValues<FlexJustifyContent>();
        JustifyBox.SelectedItem = FlexJustifyContent.SpaceBetween;
        AlignmentBox.ItemsSource = Enum.GetValues<WrapPanelItemsAlignment>();
        AlignmentBox.SelectedItem = WrapPanelItemsAlignment.Justify;
    }

    private double Duration => Slow.IsChecked == true ? 4000 : 400;

    private AnimationBuilder Move => Recipes.Reflow(Duration, ease: Eases.OutQuint);

    protected override void OnAttached() => Arm();

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
        Scope.Execute(_ => PrestoAvalonia.AnimateSharedElement(square, square, Move));
        from.Child = null;
        to.Child = square;
    }

    private void Freeze(object? sender, RoutedEventArgs e) => UiThread.Freeze(TimeSpan.FromSeconds(2));

    private void Rearm(object? sender, RoutedEventArgs e) => Arm();

    private void Arm()
    {
        foreach (var card in Cards.Children)
        {
            if (card != XamlCard) Scope.Execute(_ => card.AnimateLayout(Duration, Eases.OutQuint));
        }
        var avatarMode = Uncorrected.IsChecked == true ? LayoutAnimateMode.PositionAndSize : LayoutAnimateMode.PositionAndSizeCorrected;
        Scope.Execute(_ =>
        {
            GrowBox.AnimateLayout(Duration, Eases.OutQuint, LayoutAnimateMode.PositionAndSize);
            AvatarCard.AnimateLayout(Duration, Eases.OutQuint, avatarMode);
            SwapHost.AnimateContentSize(Duration, Eases.OutQuint);
        });
        Animate.SetContentSize(Faq, Move);
        foreach (var child in Tags.Children) ArmReflow(child);
        foreach (var child in Tiles.Children) ArmReflow(child);
    }

    // Corrected, so a tag that grows or a tile that stretches keeps its label unstretched.
    private void ArmReflow(Control child) =>
        Scope.Execute(_ => child.AnimateLayout(Duration, Eases.OutQuint, LayoutAnimateMode.PositionAndSizeCorrected));

    private void JustifySelected(object? sender, SelectionChangedEventArgs e)
    {
        if (JustifyBox.SelectedItem is FlexJustifyContent justify) Tags.JustifyContent = justify;
    }

    private void AlignmentSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (AlignmentBox.SelectedItem is WrapPanelItemsAlignment alignment) Tiles.ItemsAlignment = alignment;
    }

    private void AddTagClicked(object? sender, RoutedEventArgs e) => AddTag(Random.Shared.Next(Tags.Children.Count + 1));

    private void RemoveTag(object? sender, RoutedEventArgs e)
    {
        if (Tags.Children.Count > 0) Tags.Children.RemoveAt(Random.Shared.Next(Tags.Children.Count));
        ApplyGrow();
    }

    private void ShuffleTiles(object? sender, RoutedEventArgs e)
    {
        foreach (var tile in Utils.Shuffle(Tiles.Children.ToArray())) Tiles.Children.Move(Tiles.Children.IndexOf(tile), Tiles.Children.Count - 1);
    }

    private void ResizePanels(object? sender, RoutedEventArgs e)
    {
        var width = FlexFrame.MaxWidth > NarrowFrame ? NarrowFrame : WideFrame;
        foreach (var frame in new[] { FlexFrame, WrapFrame })
        {
            Scope.Execute(_ => frame.Animate(b => b.Property(MaxWidthProperty, width).Duration(Duration).Ease(Eases.OutQuint)));
        }
    }

    private void GrowFirstTag(object? sender, RoutedEventArgs e) => ApplyGrow();

    private void ApplyGrow()
    {
        for (var i = 0; i < Tags.Children.Count; i++) Flex.SetGrow(Tags.Children[i], i == 0 && GrowTag.IsChecked == true ? 1 : 0);
    }

    private void AddTag(int index)
    {
        var tag = new Border
        {
            Classes = { "tag" },
            Background = Swatches[_nextTag % Swatches.Length],
            Child = new TextBlock { Text = TagNames[_nextTag++ % TagNames.Length] },
        };
        Presence.SetEnter(tag, Recipes.PopIn());
        Presence.SetExit(tag, Recipes.PopOut());
        ArmReflow(tag);
        Tags.Children.Insert(index, tag);
        ApplyGrow();
    }

    private static Border NewTile(int index) => new()
    {
        Classes = { "tile" },
        Background = Swatches[(index + 2) % Swatches.Length],
        Child = new TextBlock { Text = (index + 1).ToString() },
    };

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
        Scope.Execute(_ => card.AnimateLayout(Duration, Eases.OutQuint));
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
