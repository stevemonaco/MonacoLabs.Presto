using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Monaco.Presto;
using Monaco.Presto.AvaloniaBridge;
using Monaco.Presto.AvaloniaBridge.Controls;

namespace PrestoSample.Showcase.Pages;

/// <summary>
/// A shop front that uses Phases 22–25 together: recipes from XAML for the grid's entrance, hover, press, focus and exits,
/// a <c>:checked</c> chip state, an <c>Animate.ItemLayout</c> reflow, a shared-element fly to the cart, a quick view opened by clicking a card and revealed by a
/// <see cref="Modal"/> from the card over a blurred grid, a <c>Shimmer</c> skeleton, sold-out pictures that colorize on hover and a live reduced-motion switch.
/// </summary>
public partial class StorefrontPage : ShowcasePage
{
    private const double FetchDuration = 900;
    private const int ShelfSize = 3;

    /// <summary>The chip colours a <c>:checked</c> <see cref="MotionState"/> plays.</summary>
    public static readonly AnimationBuilder Picked = Presto.Build()
        .Background(Palette.Accent)
        .Foreground(Palette.Background)
        .Duration(200)
        .Ease(Eases.OutQuad);

    /// <summary>The hover that colours a sold-out product's gray picture.</summary>
    public static readonly AnimationBuilder Colorize = Recipes.Colorize();

    public StorefrontPage()
    {
        InitializeComponent();
        Skeleton.ItemsSource = Enumerable.Range(0, 8);
    }

    public StorefrontViewModel ViewModel => (StorefrontViewModel)DataContext!;

    protected override void OnAttached()
    {
        // The grid is realized when it is first shown, so its cards arrive as part of the view's initial load.
        Presto.CreateTimer(new TimerParams
        {
            Duration = FetchDuration,
            OnComplete = _ =>
            {
                Body.Children.Remove(Skeleton);
                Catalogue.IsVisible = true;
            },
        });
    }

    private void ChipChanged(object? sender, RoutedEventArgs e)
    {
        var chip = (ToggleButton)sender!;
        ViewModel.Pick((string)chip.Content!, chip.IsChecked == true);
    }

    private void AddClicked(object? sender, RoutedEventArgs e)
    {
        var card = Card(sender);
        var product = (Product)card.DataContext!;
        var thumb = new Border { Classes = { "thumb" }, Child = new Scenery { Hue = product.Hue, Index = product.Index } };
        CartShelf.Children.Insert(0, thumb);
        if (CartShelf.Children.Count > ShelfSize) CartShelf.Children.RemoveAt(ShelfSize);
        Scope.Execute(_ => PrestoAvalonia.AnimateSharedElement(card.GetVisualDescendants().OfType<FilteredImage>().First(), thumb, 700, Eases.InOutCubic));
        Recipes.Burst(Badge, count: 12, color: Palette.Accent.Color, reduced: Recipes.Fade());
        ViewModel.CartCount++;
    }

    private void CardTapped(object? sender, TappedEventArgs e)
    {
        if ((e.Source as Visual)?.FindAncestorOfType<Button>(includeSelf: true) is null) OpenQuickView((Border)sender!);
    }

    private void CardKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key is not (Key.Enter or Key.Space)) return;
        e.Handled = true;
        OpenQuickView((Border)sender!);
    }

    private void OpenQuickView(Border card)
    {
        ViewModel.QuickView = (Product)card.DataContext!;
        QuickHost.Open(card);
    }

    private void CloseQuickView(object? sender, RoutedEventArgs e) => QuickHost.Close();

    private static Border Card(object? sender) => ((Visual)sender!).FindAncestorOfType<Border>()!;
}
