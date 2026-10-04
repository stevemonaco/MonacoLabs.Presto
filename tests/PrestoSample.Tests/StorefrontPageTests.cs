using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Monaco.Presto;
using Monaco.Presto.AvaloniaBridge;
using PrestoSample.Showcase.Pages;

namespace PrestoSample.Tests;

public sealed class StorefrontPageTests
{
    [AvaloniaFact]
    public void ThePageLoadsPastTheSkeletonAndFadesTheCardsUp()
    {
        var page = new StorefrontPage();
        var window = SceneHarness.Show(page, 1280, 900);
        Assert.NotEmpty(Presto.GetAnimations(page.GetLogicalDescendants().OfType<Border>().First(border => border.Classes.Contains("skeleton"))));

        TestFrames.Tick(60);
        Assert.DoesNotContain(page.GetControl<ItemsControl>("Skeleton"), page.GetControl<Panel>("Body").Children);
        var cards = Cards(page).ToArray();
        Assert.Equal(page.ViewModel.Shown.Count, cards.Length);
        Assert.True((double)PrestoAvalonia.Get(cards[^1], "Y")! > 0 && cards[^1].Opacity < 1, "the last card was not still waiting below its slot for its turn");

        TestFrames.Tick(90);
        Assert.All(cards, card => Assert.Equal(1, card.Opacity, 3));
        SceneHarness.Close(window);
    }

    [AvaloniaFact]
    public void HoveringACardLiftsItAndLeavingReturnsIt()
    {
        var (window, page) = Loaded();
        var card = Cards(page).First();

        window.MouseMove(Top(card, window));
        TestFrames.Tick(30);
        Assert.True((double)PrestoAvalonia.Get(card, "Y")! < 0, "the hovered card did not lift");

        window.MouseMove(new Point(1, 1));
        TestFrames.Tick(30);
        Assert.Equal(0, (double)PrestoAvalonia.Get(card, "Y")!, 6);
        SceneHarness.Close(window);
    }

    [AvaloniaFact]
    public void PressingACardScalesItDown()
    {
        var (window, page) = Loaded();
        var card = Cards(page).First();

        window.MouseDown(Top(card, window), MouseButton.Left);
        TestFrames.Tick(30);
        Assert.True((double)PrestoAvalonia.Get(card, "Scale")! < 1, "the pressed card did not shrink");
        window.MouseUp(Top(card, window), MouseButton.Left);
        SceneHarness.Close(window);
    }

    [AvaloniaFact]
    public void KeyboardFocusGlowsACard()
    {
        var (window, page) = Loaded();
        var card = Cards(page).First();

        card.Focus(NavigationMethod.Tab);
        TestFrames.Tick(30);
        Assert.True(card.Effect is DropShadowEffect { BlurRadius: > 0, Opacity: > 0 }, $"the focused card showed {card.Effect}");
        SceneHarness.Close(window);
    }

    [AvaloniaFact]
    public void AddingToTheCartCountsItBumpsTheBadgeAndFliesAThumbnail()
    {
        var (window, page) = Loaded();
        var badge = page.GetControl<Border>("Badge");

        SceneHarness.Click(window, Button(Cards(page).First(), "Add"));

        Assert.Equal(1, page.ViewModel.CartCount);
        Assert.Equal("1", badge.GetLogicalDescendants().OfType<TextBlock>().Single().Text);
        Assert.NotEmpty(Presto.GetAnimations(badge));
        Assert.Single(page.GetControl<StackPanel>("CartShelf").Children);
        Assert.Single(SceneHarness.ParticleOverlays(page));
        SceneHarness.Close(window);
    }

    [AvaloniaFact]
    public void WithReducedMotionOnAddingToTheCartFadesTheBadgeWithoutParticles()
    {
        var (window, page) = Loaded();
        var badge = page.GetControl<Border>("Badge");
        using var reduced = ReducedMotion.On();

        SceneHarness.Click(window, Button(Cards(page).First(), "Add"));
        TestFrames.Tick(2);

        Assert.Equal(1, page.ViewModel.CartCount);
        Assert.InRange(badge.Opacity, 0, 0.99);
        Assert.Equal(1, (double)(PrestoAvalonia.Get(badge, "Scale") ?? 1d), 6);
        Assert.Empty(SceneHarness.ParticleOverlays(page));
        SceneHarness.Close(window);
    }

    [AvaloniaFact]
    public void QuickViewOpensOverABlurredGridAndLeavesThroughItsExit()
    {
        var (window, page) = Loaded();
        var product = page.ViewModel.Shown[0];

        SceneHarness.Click(window, Button(Cards(page).First(), "Quick view"));
        Assert.Equal(product, page.ViewModel.QuickView);
        TestFrames.Tick(60);
        Assert.True(page.GetControl<ScrollViewer>("Catalogue").Effect is BlurEffect { Radius: > 5 }, "the grid behind the quick view was not blurred");

        SceneHarness.Click(window, Button(page.GetControl<ContentControl>("QuickHost"), "Close"));
        Assert.Null(page.ViewModel.QuickView);
        Assert.Single(OverlayLayer.GetOverlayLayer(page)!.Children.OfType<Canvas>());

        TestFrames.Tick(60);
        Assert.Null(page.GetControl<ScrollViewer>("Catalogue").Effect);
        SceneHarness.Close(window);
    }

    [AvaloniaFact]
    public void WithReducedMotionOnAHoveredCardDoesNotMove()
    {
        var (window, page) = Loaded();
        var card = Cards(page).First();

        using var reduced = ReducedMotion.On();

        window.MouseMove(Top(card, window));
        TestFrames.Tick(30);
        Assert.Equal(0, (double)(PrestoAvalonia.Get(card, "Y") ?? 0d), 6);
        SceneHarness.Close(window);
    }

    [AvaloniaFact]
    public void ASoldOutPictureIsGrayAndColorizesWhileHovered()
    {
        var (window, page) = Loaded();
        var card = Cards(page).First(card => ((Product)card.DataContext!).SoldOut);
        var picture = card.GetLogicalDescendants().OfType<FilteredImage>().Single();
        Assert.Equal(1, picture.Grayscale);
        Assert.False(Button(card, "Add").IsEnabled);

        window.MouseMove(Top(card, window));
        TestFrames.Tick(30);
        Assert.Equal(0, picture.Grayscale, 6);

        window.MouseMove(new Point(1, 1));
        TestFrames.Tick(30);
        Assert.Equal(1, picture.Grayscale, 6);
        SceneHarness.Close(window);
    }

    private static (Window Window, StorefrontPage Page) Loaded()
    {
        var page = new StorefrontPage();
        var window = SceneHarness.Show(page, 1280, 900);
        TestFrames.Tick(150);
        return (window, page);
    }

    private static IEnumerable<Border> Cards(Visual page) =>
        page.GetLogicalDescendants().OfType<Border>().Where(border => border.Classes.Contains("product"));

    private static Button Button(ILogical parent, string content) =>
        parent.GetLogicalDescendants().OfType<Button>().First(button => Equals(button.Content, content));

    // The card's picture, clear of its buttons.
    private static Point Top(Border card, Visual window) => card.TranslatePoint(new Point(card.Bounds.Width / 2, 40), window)!.Value;
}
