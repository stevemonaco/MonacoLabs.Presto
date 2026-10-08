using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Avalonia.Media.Immutable;
using PrestoSample.Showcase.Pages;

namespace PrestoSample.Tests;

public sealed class ConnectedPageTests
{
    private const int Picked = 5;

    [AvaloniaFact]
    public async Task OpeningACardFliesItsArtIntoTheDetailAndFadesEveryOtherCard()
    {
        var page = new ConnectedPage();
        var window = SceneHarness.Show(page, 1100, 600);
        var cards = Cards(page);
        var art = Art(cards[Picked]);

        SceneHarness.Click(window, cards[Picked]);
        await SceneHarness.Settle();

        Assert.Same(page.GetControl<Border>("Hero"), art.Parent);
        Assert.All(cards.Where((_, i) => i != Picked), card => Assert.Equal(0, card.Opacity));
        SceneHarness.Close(window);
    }

    [AvaloniaFact]
    public async Task BackPutsTheArtHomeAndBringsEveryCardBack()
    {
        var page = new ConnectedPage();
        var window = SceneHarness.Show(page, 1100, 600);
        var cards = Cards(page);
        var art = Art(cards[Picked]);
        SceneHarness.Click(window, cards[Picked]);
        await SceneHarness.Settle();

        SceneHarness.Click(window, page.GetControl<Button>("Back"));
        await SceneHarness.Settle();

        Assert.Same(cards[Picked], art.Parent!.Parent);
        Assert.All(cards, card => Assert.Equal(1, card.Opacity));
        SceneHarness.Close(window);
    }

    [AvaloniaFact]
    public async Task RapidTogglesLeaveEveryCardDrawnInItsSlot()
    {
        var page = new ConnectedPage();
        var window = SceneHarness.Show(page, 1100, 600);
        var cards = Cards(page);
        for (var i = 0; i < cards.Length; i++)
        {
            var tint = new ImmutableSolidColorBrush(SceneHarness.Tint(i));
            var shell = (Border)cards[i].Children[0];
            shell.Background = shell.BorderBrush = tint;
            // Glyphs are antialiased a channel at a time, so text over one tint can land on another card's exact color.
            foreach (var text in cards[i].GetLogicalDescendants().OfType<TextBlock>()) text.Foreground = tint;
        }

        for (var i = 0; i < 10; i++)
        {
            SceneHarness.Click(window, cards[Picked]);
            TestFrames.Tick(3);
            SceneHarness.Click(window, page.GetControl<Button>("Back"));
            TestFrames.Tick(3);
        }
        await SceneHarness.Settle();

        Assert.Same(cards[Picked], Art(cards[Picked]).Parent!.Parent);
        for (var i = 0; i < cards.Length; i++)
        {
            Assert.Equal(1, cards[i].Opacity);
            var drawn = SceneHarness.Extent(page, SceneHarness.Tint(i));
            var slot = SceneHarness.Slot(cards[i], window);
            Assert.True(drawn is { } box && SceneHarness.Near(box, slot), $"card {i} was left drawn at {drawn} instead of {slot}");
        }
        SceneHarness.Close(window);
    }

    private static Grid[] Cards(ConnectedPage page) => [.. page.GetControl<UniformGrid>("Albums").Children.Cast<Grid>()];

    private static Border Art(Grid card) =>
        card.GetLogicalDescendants().OfType<Border>().First(border => border.Classes.Contains("art"));
}
