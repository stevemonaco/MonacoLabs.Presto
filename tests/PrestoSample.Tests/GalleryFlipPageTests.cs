using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Avalonia.Media;
using PrestoSample.Showcase;
using PrestoSample.Showcase.Pages;

namespace PrestoSample.Tests;

/// <summary>
/// SAMPLES-PLAN §4.4's reflow and shared element and §7.4's churn case, read back from the rendered frame. The clicks are real pointer
/// input because the DevTools MCP clicks through the automation peer and cannot move a pointer at all.
/// </summary>
public sealed class GalleryFlipPageTests
{
    private static readonly Color ProbeColor = Colors.Magenta;

    [AvaloniaFact]
    public async Task AFilterChangeEasesASurvivingCardOutOfTheSlotItLeft()
    {
        var page = new GalleryFlipPage();
        var window = SceneHarness.Show(page, 1200, 900);
        var category = Datasets.Categories[0];
        var probe = SceneHarness.Cards(page).Last(card => CardView.Record(card).Category == category);
        SceneHarness.Flood(probe, ProbeColor);
        Assert.True(await SceneHarness.Settles(page, window, probe), "the field never finished arriving");

        var left = SceneHarness.Slot(probe, window);
        SceneHarness.Click(window, Chip(page, category));

        var arrived = SceneHarness.Slot(probe, window);
        var move = Distance(left.Position, arrived.Position);
        Assert.NotEqual(0, move);
        var flight = SceneHarness.Extent(page, ProbeColor);
        Assert.NotNull(flight);
        Assert.True(
            Distance(flight.Value.Position, arrived.Position) > move / 2,
            $"layout moved the card from {left} to {arrived} and it was already drawn at {flight} rather than easing there");

        Assert.True(await SceneHarness.Settles(page, window, probe), "the card never caught up with the slot layout gave it");
        SceneHarness.Close(window);
    }

    [AvaloniaFact]
    public async Task ACardDroppedMidReflowLeavesEveryOtherOneInItsSlot()
    {
        var (window, page, shown) = await Filtered();

        SceneHarness.Click(window, page.GetControl<Button>("Drop"));
        Assert.True(
            await SceneHarness.Pump(() => SceneHarness.Cards(page).Count() == shown.Length - 1, rounds: 120),
            "the drop never took a card out of the panel");
        await SceneHarness.Settle();

        AssertEachCardIsDrawnInItsSlot(page, window);
        SceneHarness.Close(window);
    }

    [AvaloniaFact]
    public async Task AnExpandReversedMidFlightPutsEveryCardBackInItsSlot()
    {
        var (window, page, shown) = await Filtered();

        var probe = shown[^1];
        var cell = SceneHarness.Slot(probe, window);
        SceneHarness.Click(window, probe);

        var pane = SceneHarness.Slot(page.GetControl<Border>("Host"), window);
        var lifted = SceneHarness.Extent(page, SceneHarness.Colour(probe));
        Assert.True(
            lifted is { } first && first.X > (cell.X + pane.X) / 2 && first.Width < (cell.Width + pane.Width) / 2,
            $"layout put the card in {pane} and it was already drawn at {lifted} rather than easing out of {cell}");
        Assert.True(
            await SceneHarness.Pump(
                () => SceneHarness.Extent(page, SceneHarness.Colour(probe)) is { } next && next.X < lifted.Value.X - 40 && next.Width > lifted.Value.Width + 40,
                rounds: 60),
            $"the card stopped at {SceneHarness.Extent(page, SceneHarness.Colour(probe))} instead of carrying on towards {pane}");
        var flight = SceneHarness.Extent(page, SceneHarness.Colour(probe))!.Value;
        Assert.True(flight.X > pane.X && flight.Width < pane.Width, $"the card had already arrived at {flight}");

        SceneHarness.Click(window, probe);
        Assert.True(await SceneHarness.Settles(page, window, probe), "the card never came back to the cell it was lifted out of");
        Assert.True(SceneHarness.Near(cell, SceneHarness.Slot(probe, window)), $"the card came back to {SceneHarness.Slot(probe, window)} instead of {cell}");
        await SceneHarness.Settle();

        AssertEachCardIsDrawnInItsSlot(page, window);
        SceneHarness.Close(window);
    }

    [AvaloniaFact]
    public async Task TheLayoutSwitchEasesACardsWidthAndTheSortEasesItsPosition()
    {
        var (window, page, shown) = await Filtered();
        // The panel draws its last card over every other one, so nothing can overlap the pixels this reads back.
        var probe = shown[^1];
        var cell = SceneHarness.Slot(probe, window);

        SceneHarness.Click(window, page.GetControl<ToggleSwitch>("AsRows"));

        var row = SceneHarness.Slot(probe, window);
        Assert.True(row.Width > cell.Width + 40, $"the list switch gave the card {row.Width}px against the grid's {cell.Width}px");
        Assert.True(
            await SceneHarness.Pump(() => Drawn(page, probe) is { } easing && easing.Width > cell.Width + 6 && easing.Width < row.Width - 6, rounds: 40),
            $"the card was never drawn between {cell.Width}px and {row.Width}px wide, so the switch snapped its size instead of easing it");
        Assert.True(await SceneHarness.Settles(page, window, probe), "the card never reached the row slot layout gave it");
        await SceneHarness.Settle();

        // The quietest card sorts last by value, so the sort both moves it and puts it on top of the field.
        var quietest = Datasets.Cards.Where(record => record.Category == Datasets.Categories[0]).MinBy(record => record.Value);
        var sorted = shown.First(card => CardView.Record(card) == quietest);
        var left = SceneHarness.Slot(sorted, window);

        SceneHarness.Click(window, page.GetControl<ToggleSwitch>("ByValue"));

        var arrived = SceneHarness.Slot(sorted, window);
        var move = Distance(left.Position, arrived.Position);
        Assert.True(move > 40, $"the sort moved the card {move}px, which is too little to tell a reflow from a snap");
        Assert.True(
            await SceneHarness.Pump(
                () => Drawn(page, sorted) is { } easing &&
                    Distance(easing.Position, left.Position) > 8 && Distance(easing.Position, arrived.Position) > 8,
                rounds: 40),
            $"the card was never drawn between {left.Position} and {arrived.Position}, so the sort snapped it into its new slot");
        Assert.True(await SceneHarness.Settles(page, window, sorted), "the card never caught up with the slot the sort gave it");

        SceneHarness.Close(window);
    }

    private static async Task<(Window Window, GalleryFlipPage Page, Border[] Shown)> Filtered()
    {
        var page = new GalleryFlipPage();
        var window = SceneHarness.Show(page, 1200, 900);
        var category = Datasets.Categories[0];
        var count = Datasets.Cards.Count(record => record.Category == category);

        SceneHarness.Click(window, Chip(page, category));
        Assert.True(
            await SceneHarness.Pump(() => SceneHarness.Cards(page).Count() == count, rounds: 120),
            "the filtered-out cards never left the panel");

        var shown = SceneHarness.Cards(page).ToArray();
        for (var i = 0; i < shown.Length; i++) SceneHarness.Flood(shown[i], SceneHarness.Tint(i));
        await SceneHarness.Settle();

        return (window, page, shown);
    }

    private static void AssertEachCardIsDrawnInItsSlot(Visual page, Window window)
    {
        foreach (var card in SceneHarness.Cards(page))
        {
            var box = SceneHarness.Extent(page, SceneHarness.Colour(card));
            Assert.True(box != null && SceneHarness.Near(box.Value, SceneHarness.Slot(card, window)), $"{CardView.Record(card).Title} was left drawn at {box} instead of {SceneHarness.Slot(card, window)}");
        }
    }

    private static ListBoxItem Chip(Control page, string name) =>
        page.GetControl<ListBox>("Filters").GetLogicalDescendants().OfType<ListBoxItem>().First(item => (string?)item.Content == name);

    private static Rect? Drawn(Visual page, Border card) => SceneHarness.Extent(page, SceneHarness.Colour(card));

    private static double Distance(Point from, Point to) => new Vector(to.X - from.X, to.Y - from.Y).Length;
}
