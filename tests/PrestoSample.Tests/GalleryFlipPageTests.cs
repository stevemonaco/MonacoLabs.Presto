using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Avalonia.Media;
using PrestoSample.Showcase;
using PrestoSample.Showcase.Pages;
using PrestoSample.ViewModels;
using PrestoSample.Views;

namespace PrestoSample.Tests;

/// <summary>
/// SAMPLES-PLAN §4.4's reflow and shared element and §7.4's churn case, read back from the rendered frame. The clicks are real pointer
/// input because the DevTools MCP clicks through the automation peer and cannot move a pointer at all.
/// </summary>
public sealed class GalleryFlipPageTests
{
    private static readonly Color ProbeColor = Colors.Magenta;

    [AvaloniaFact]
    public async Task OpeningThePageFadesEveryCardUpOnceWhereLayoutPutsIt()
    {
        TestFrames.Install();
        var gallery = new MainViewModel();
        var window = SceneHarness.Show(new MainView { DataContext = gallery }, 1400, 1000, frames: 20);
        gallery.SelectedPage = gallery.Sections.SelectMany(section => section.Pages).First(page => page.Title == "Card gallery");
        window.UpdateLayout();
        var cards = SceneHarness.Cards(gallery.CurrentPage!).ToArray();
        for (var i = 0; i < cards.Length; i++)
        {
            SceneHarness.Flood(cards[i], SceneHarness.Tint(i));
            // Glyphs are antialiased a channel at a time, so at part opacity they are off the tint's blend with the stage.
            foreach (var text in cards[i].GetLogicalDescendants().OfType<TextBlock>()) text.Opacity = 0;
        }

        var shown = new double[cards.Length];
        for (var frame = 0; frame < 90; frame++)
        {
            TestFrames.Tick();
            await Task.Delay(8);
            var (pixels, stride) = SceneHarness.Capture(window);
            for (var i = 0; i < cards.Length; i++)
            {
                var slot = SceneHarness.Slot(cards[i], window);
                var opacity = Opacity(pixels, stride, slot);
                Assert.True(opacity >= shown[i] - 0.03, $"card {i} was drawn at {shown[i]:0.00} and then {opacity:0.00} in frame {frame}");
                shown[i] = opacity;
                // By then the rise, which eases with the fade, has a pixel left, while a layout transition would still be tens away.
                if (opacity < 0.95) continue;

                var (across, top) = Drawn(pixels, stride, slot, SceneHarness.Tint(i));
                Assert.True(
                    Math.Abs(across) <= 1.5 && top >= -1.5 && top <= 3,
                    $"card {i} was drawn {across:0.0}px across and {top:0.0}px down from its slot at {opacity:0.00} in frame {frame}, not just lowered by its entry");
            }
            if (frame == 0) Assert.True(shown[^1] < 0.1, $"the last card was already drawn at {shown[^1]:0.00} before its entry");
        }

        Assert.All(shown, opacity => Assert.True(opacity > 0.98, $"a card finished its entry at {opacity:0.00}"));
        SceneHarness.Close(window);
    }

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
    public async Task TheDetailPaneBlursTheGridBehindItAndClosingLeavesNoEffect()
    {
        var (window, page, shown) = await Filtered();
        var field = page.GetControl<ItemsControl>("Field");
        Assert.Null(field.Effect);

        SceneHarness.Click(window, shown[0]);
        Assert.True(
            await SceneHarness.Pump(() => field.Effect is BlurEffect { Radius: > 5 }),
            $"opening the pane left the grid with {field.Effect}");

        SceneHarness.Click(window, shown[0]);
        Assert.True(await SceneHarness.Pump(() => field.Effect == null), $"closing the pane left {field.Effect} on the grid");
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

    // Read against the stage at full opacity, so while the page switch fades the page in this is the product of the two.
    private static double Opacity(byte[] pixels, int stride, Rect slot)
    {
        var background = Palette.Surface.Color;
        var red = pixels[((int)slot.Center.Y * stride) + ((int)slot.Center.X * 4)];
        return Math.Max(0, (red - background.R) / (255d - background.R));
    }

    // Walks in from outside the slot to the first pixel of the card's tint; not upwards, as the row below is drawn over it.
    private static (double Across, double Top) Drawn(byte[] pixels, int stride, Rect slot, Color tint)
    {
        var x = (int)slot.Center.X;
        var y = (int)slot.Center.Y;
        int left = (int)slot.X - 24, right = (int)slot.Right + 24, top = (int)slot.Y - 24;
        while (left < x && !Tinted(pixels, stride, left, y, tint)) left++;
        while (right > x && !Tinted(pixels, stride, right, y, tint)) right--;
        while (top < y && !Tinted(pixels, stride, x, top, tint)) top++;
        return (((left + right + 1) / 2d) - slot.Center.X, top - slot.Y);
    }

    private static bool Tinted(byte[] pixels, int stride, int x, int y, Color tint)
    {
        if (x < 0 || y < 0 || (x * 4) + 3 >= stride || (y * stride) >= pixels.Length) return false;
        var background = Palette.Surface.Color;
        var i = (y * stride) + (x * 4);
        var alpha = (pixels[i] - background.R) / (255d - background.R);
        return alpha >= 0.5 &&
            Math.Abs(pixels[i + 1] - (background.G * (1 - alpha))) <= 4 &&
            Math.Abs(pixels[i + 2] - (background.B + (alpha * (tint.B - background.B)))) <= 4;
    }

    private static ListBoxItem Chip(Control page, string name) =>
        page.GetControl<ListBox>("Filters").GetLogicalDescendants().OfType<ListBoxItem>().First(item => (string?)item.Content == name);

    private static Rect? Drawn(Visual page, Border card) => SceneHarness.Extent(page, SceneHarness.Colour(card));

    private static double Distance(Point from, Point to) => new Vector(to.X - from.X, to.Y - from.Y).Length;
}
