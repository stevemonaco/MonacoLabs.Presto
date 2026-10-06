using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.VisualTree;
using PrestoSample.Showcase.Pages;

namespace PrestoSample.Tests;

public sealed class ConnectedNavPageTests
{
    private const int Picked = 5;
    private static readonly Color Tint = SceneHarness.Tint(0);

    [AvaloniaFact]
    public async Task OpeningAPlaceFliesItsPhotoFromTheCardIntoTheHero()
    {
        var page = new ConnectedNavPage();
        var window = SceneHarness.Show(page, 1100, 700);
        var photo = Photo(Cards(page)[Picked]);
        var from = SceneHarness.Slot(photo, window);

        SceneHarness.Click(window, Cards(page)[Picked]);
        var hero = Flood(Hero(page));
        TestFrames.Tick(2);

        var early = SceneHarness.Extent(page, Tint);
        Assert.True(early is { } box && box.Width < from.Width * 1.5, $"the hero was drawn at {early} two frames in, not near the card at {from}");

        await SceneHarness.Settle();
        var to = SceneHarness.Slot(hero, window);
        Assert.True(SceneHarness.Extent(page, Tint) is { } landed && SceneHarness.Near(landed, to), "the hero did not land in its slot");
        Assert.All(Coordinated(page), control => Assert.Equal(1, control.Opacity));
        SceneHarness.Close(window);
    }

    [AvaloniaFact]
    public async Task BackFliesThePhotoHomeAndBringsEveryCardBack()
    {
        var page = new ConnectedNavPage();
        var window = SceneHarness.Show(page, 1100, 700);
        var cards = Cards(page);
        SceneHarness.Click(window, cards[Picked]);
        await SceneHarness.Settle();
        var heroSlot = SceneHarness.Slot(Hero(page), window);

        SceneHarness.Click(window, page.GetLogicalDescendants().OfType<Button>().First(button => Equals(button.Content, "Back")));
        var photo = Flood(Photo(cards[Picked]));
        TestFrames.Tick(2);

        var early = SceneHarness.Extent(page, Tint);
        Assert.True(early is { } box && box.Width > photo.Bounds.Width * 1.5, $"the photo was drawn at {early} two frames in, not near the hero at {heroSlot}");

        await SceneHarness.Settle();
        Assert.True(SceneHarness.Extent(page, Tint) is { } landed && SceneHarness.Near(landed, SceneHarness.Slot(photo, window)), "the photo did not land in its card");
        Assert.All(cards, card => Assert.Equal(1, card.Opacity));
        SceneHarness.Close(window);
    }

    [AvaloniaFact]
    public async Task ACardInTheScrolledGalleryFliesFromWhereItIsSeenAndBackIntoIt()
    {
        const int far = 13;
        var page = new ConnectedNavPage();
        var window = SceneHarness.Show(page, 1100, 700);
        page.GetLogicalDescendants().OfType<ScrollViewer>().First().Offset = new Vector(0, 330);
        TestFrames.Tick();
        var photo = Photo(Cards(page)[far]);
        var from = SceneHarness.Slot(photo, window);

        SceneHarness.Click(window, Cards(page)[far]);
        Flood(Hero(page));
        TestFrames.Tick(2);

        var early = SceneHarness.Extent(page, Tint);
        Assert.True(early is { } box && Math.Abs(box.Top - from.Top) < 30, $"the hero started at {early}, not from the scrolled card at {from}");

        await SceneHarness.Settle();
        SceneHarness.Click(window, page.GetLogicalDescendants().OfType<Button>().First(button => Equals(button.Content, "Back")));
        Flood(photo);
        await SceneHarness.Settle();

        Assert.True(SceneHarness.Extent(page, Tint) is { } landed && SceneHarness.Near(landed, SceneHarness.Slot(photo, window)), "the photo did not land in its scrolled card");
        SceneHarness.Close(window);
    }

    [AvaloniaFact]
    public async Task RapidTogglesLeaveThePhotoDrawnInItsCard()
    {
        var page = new ConnectedNavPage();
        var window = SceneHarness.Show(page, 1100, 700);
        var cards = Cards(page);
        var photo = Flood(Photo(cards[Picked]));

        for (var i = 0; i < 8; i++)
        {
            window.UpdateLayout();
            SceneHarness.Click(window, cards[Picked]);
            var back = page.GetLogicalDescendants().OfType<Button>().First(button => Equals(button.Content, "Back"));
            // Back is hit where the compositor draws it, so it can be pressed once its staggered fade-in shows it.
            Assert.True(await SceneHarness.Pump(() => Hittable(window, back)), "Back never became clickable");
            SceneHarness.Click(window, back);
            Assert.IsType<ScrollViewer>(page.GetControl<Border>("Host").Child);
            TestFrames.Tick(3);
        }
        await SceneHarness.Settle();

        Assert.True(SceneHarness.Extent(page, Tint) is { } landed && SceneHarness.Near(landed, SceneHarness.Slot(photo, window)), "the photo was left away from its card");
        Assert.All(cards, card => Assert.Equal(1, card.Opacity));
        SceneHarness.Close(window);
    }

    private static bool Hittable(Window window, Button button) =>
        window.GetInputElementsAt(SceneHarness.Center(button, window)).Any(hit => hit is Visual visual && (visual == button || button.IsVisualAncestorOf(visual)));

    private static Border[] Cards(ConnectedNavPage page) =>
        [.. page.GetLogicalDescendants().OfType<Border>().Where(border => border.Classes.Contains("place"))];

    private static Border Photo(Border card) =>
        card.GetLogicalDescendants().OfType<Border>().First(border => border.Classes.Contains("photo"));

    private static Border Hero(ConnectedNavPage page) =>
        page.GetLogicalDescendants().OfType<Border>().First(border => border.Classes.Contains("hero"));

    private static IEnumerable<Control> Coordinated(ConnectedNavPage page) =>
        page.GetControl<Border>("Host").GetLogicalDescendants().OfType<Control>().Where(control => control.Parent is StackPanel { Parent: Grid });

    // The transition moves the Border's visual, so a flat tint in place of the scenery shows exactly where it is drawn.
    private static Border Flood(Border photo)
    {
        photo.Child = null;
        photo.Background = new ImmutableSolidColorBrush(Tint);
        return photo;
    }
}
