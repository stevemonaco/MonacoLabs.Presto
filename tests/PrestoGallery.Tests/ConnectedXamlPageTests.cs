using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.VisualTree;
using MonacoLabs.Presto;
using PrestoGallery.Showcase;
using PrestoGallery.Showcase.Pages;

namespace PrestoGallery.Tests;

public sealed class ConnectedXamlPageTests
{
    private const int Picked = 5;
    private static readonly Color Tint = SceneHarness.Tint(0);
    private static readonly Color TextTint = SceneHarness.Tint(1);

    [Fact]
    public void SwitchingHostsStartsTheListBackAtTheTop()
    {
        var model = new ConnectedXamlViewModel();
        model.Gallery.Offset = new Vector(0, 330);

        model.CrossFade = true;

        Assert.Equal(default, model.Gallery.Offset);
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OpeningADestinationFliesItsPhotoFromTheCardIntoTheHero(bool crossFade)
    {
        var page = new ConnectedXamlPage();
        Model(page).CrossFade = crossFade;
        var window = await Show(page);
        // The views still overlap for the whole fade, but the hero is only drawn in its exact tint once it is opaque.
        if (crossFade) page.GetVisualDescendants().OfType<TransitioningContentControl>().Single().PageTransition = Recipes.PageFade(16);
        var card = Cards(page)[Picked];
        var from = SceneHarness.Slot(Photo(card), window);

        SceneHarness.Click(window, card);
        var hero = Flood(Hero(page));
        TestFrames.Tick(2);

        var early = SceneHarness.Extent(page, Tint);
        // The click's frame and these two are all the compositor clock has moved since the flight began.
        const double flown = 3 * 16;
        var reach = from.Width + ((hero.Width - from.Width) * Eases.OutQuint(flown / 620));
        Assert.True(early is { } box && box.Width < reach + 2, $"the hero was drawn at {early} {flown} ms in, wider than {reach:F0} from the card at {from}");

        await SceneHarness.Settle();
        Assert.True(SceneHarness.Extent(page, Tint) is { } landed && SceneHarness.Near(landed, SceneHarness.Slot(hero, window)), "the hero did not land in its slot");
        SceneHarness.Close(window);
    }

    [AvaloniaFact]
    public async Task BackFliesThePhotoHomeIntoTheScrolledList()
    {
        const int far = 13;
        var page = new ConnectedXamlPage();
        var window = await Show(page);
        Model(page).Gallery.Offset = new Vector(0, 330);
        TestFrames.Tick();
        SceneHarness.Click(window, Cards(page)[far]);
        await SceneHarness.Settle();
        var heroSlot = SceneHarness.Slot(Hero(page), window);

        SceneHarness.Click(window, page.GetVisualDescendants().OfType<Button>().First(button => Equals(button.Content, "Back")));
        var photo = Flood(Photo(Cards(page)[far]));
        TestFrames.Tick(2);

        var early = SceneHarness.Extent(page, Tint);
        Assert.True(early is { } box && box.Width > photo.Bounds.Width * 1.5, $"the photo was drawn at {early} two frames in, not near the hero at {heroSlot}");

        await SceneHarness.Settle();
        Assert.True(SceneHarness.Extent(page, Tint) is { } landed && SceneHarness.Near(landed, SceneHarness.Slot(photo, window)), "the photo did not land in its scrolled card");
        SceneHarness.Close(window);
    }

    [AvaloniaFact]
    public async Task TheDetailTextEntersBehindThePhoto()
    {
        var page = new ConnectedXamlPage();
        var window = await Show(page);

        SceneHarness.Click(window, Cards(page)[Picked]);
        var text = page.GetVisualDescendants().OfType<StackPanel>().Single(panel => Connected.GetAnchor(panel) is not null);
        var brush = new ImmutableSolidColorBrush(TextTint);
        text.Background = brush;
        foreach (var block in text.Children.OfType<TextBlock>()) block.Foreground = brush;
        TestFrames.Tick();

        Assert.Null(SceneHarness.Extent(page, TextTint));
        Assert.True(await SceneHarness.Pump(() => SceneHarness.Extent(page, TextTint) is { } box && SceneHarness.Near(box, SceneHarness.Slot(text, window))),
            "the detail text did not come to rest, fully opaque, in its slot");
        SceneHarness.Close(window);
    }

    [AvaloniaFact]
    public void ThePagePrintsItsSnippets()
    {
        var page = new ConnectedXamlPage();

        Assert.Equal(Snippets.ConnectedXamlCard, page.GetControl<CodePanel>("CardCode").Code);
        Assert.Equal(Snippets.ConnectedXamlDetail, page.GetControl<CodePanel>("DetailCode").Code);
        Assert.Equal(Snippets.ConnectedXamlHosts, page.GetControl<CodePanel>("HostsCode").Code);
        Assert.Contains("public static AnimationBuilder Arc =>", page.GetControl<CodePanel>("ArcCode").Code);
    }

    // Waits out flights claimed from keys an earlier test's window prepared as it closed.
    private static async Task<Window> Show(ConnectedXamlPage page)
    {
        var window = SceneHarness.Show(page, 1100, 700);
        await SceneHarness.Settle();
        return window;
    }

    private static ConnectedXamlViewModel Model(ConnectedXamlPage page) => (ConnectedXamlViewModel)page.DataContext!;

    // The cards are the templates of Buttons, so they are visual descendants only.
    private static Border[] Cards(ConnectedXamlPage page) =>
        [.. page.GetVisualDescendants().OfType<Border>().Where(border => border.Classes.Contains("place"))];

    private static Border Photo(Border card) =>
        card.GetVisualDescendants().OfType<Border>().First(border => border.Classes.Contains("photo"));

    private static Border Hero(ConnectedXamlPage page) =>
        page.GetVisualDescendants().OfType<Border>().First(border => border.Classes.Contains("hero"));

    // The transition moves the Border's visual, so a flat tint in place of the scenery shows exactly where it is drawn.
    private static Border Flood(Border photo)
    {
        photo.Child = null;
        photo.Background = new ImmutableSolidColorBrush(Tint);
        return photo;
    }
}
