using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.VisualTree;
using Monaco.Presto.AvaloniaBridge;
using PrestoSample.Showcase;
using PrestoSample.Showcase.Pages;

namespace PrestoSample.Tests;

/// <summary>
/// SAMPLES-PLAN §4.5's three acceptance clauses and the edge auto-scroll, driven with real pointer input: the DevTools
/// MCP can click through an automation peer but has no way to move a pointer, so every drag here is a headless window drag.
/// </summary>
public sealed class BoardPageTests
{
    private const double LanePitch = 170;
    private const double RowPitch = 64;

    [AvaloniaFact]
    public async Task AFastFlingLandsTheCardInTheLaneTheThrowCarriesItTo()
    {
        var page = new BoardPage();
        var window = SceneHarness.Show(page, 900, 520);
        await SceneHarness.Settle();
        var card = Cards(page, 0)[2];

        // The pointer only crosses one lane; the throw the release winds from its velocity carries it into the next.
        Fling(window, SceneHarness.Center(card, window), new Vector(34, 0), 5);

        Assert.True(
            await SceneHarness.Pump(() => ReferenceEquals(card.Parent, Lane(page, 2)), rounds: 200),
            $"the card ended up in {(card.Parent as Control)?.Name ?? "nothing"} rather than Lane2");
        SceneHarness.Close(window);
    }

    [AvaloniaFact]
    public async Task TheGapClosingBehindALiftedCardNeverPullsItOffThePointer()
    {
        var page = new BoardPage();
        var window = SceneHarness.Show(page, 900, 520);
        var cards = Cards(page, 0);
        var card = cards[1];
        var below = cards[2];
        SceneHarness.Flood(card, SceneHarness.Tint(0));
        SceneHarness.Flood(below, SceneHarness.Tint(1));
        await SceneHarness.Settle();

        var lifted = SceneHarness.Extent(page, SceneHarness.CardColor(card))!.Value;
        var gap = SceneHarness.Slot(below, window);
        var grab = SceneHarness.Center(card, window);
        window.MouseDown(grab, MouseButton.Left);
        for (var i = 1; i <= 5; i++)
        {
            window.MouseMove(grab + new Vector(30 * i, 0));
            TestFrames.Tick();
            var box = SceneHarness.Extent(page, SceneHarness.CardColor(card));
            Assert.True(
                box is { } drawn && Math.Abs(drawn.X - (lifted.X + (30 * i))) <= 3 && Math.Abs(drawn.Y - lifted.Y) <= 3,
                $"the pointer had taken the card {30 * i}px right of {lifted} and it was drawn at {box}");
        }

        var closed = SceneHarness.Slot(below, window);
        Assert.Equal(gap.Y - RowPitch, closed.Y, 1);
        Assert.True(
            SceneHarness.Extent(page, SceneHarness.CardColor(below)) is { } easing && easing.Y > closed.Y + 8,
            "the card below the lifted one jumped into the gap instead of easing into it");

        window.MouseUp(grab + new Vector(150, 0), MouseButton.Left);
        Assert.True(await SceneHarness.Settles(page, window, below), "the card below never caught up with the gap it was closing");
        SceneHarness.Close(window);
    }

    [AvaloniaFact]
    public async Task ACardBinnedWhileTheBoardIsStillReflowingLeavesNoHole()
    {
        var page = new BoardPage();
        var window = SceneHarness.Show(page, 900, 520);
        var cards = SceneHarness.Cards(page).ToArray();
        for (var i = 0; i < cards.Length; i++) SceneHarness.Flood(cards[i], SceneHarness.Tint(i));
        await SceneHarness.Settle();

        var binned = Cards(page, 0)[0];
        var mover = Cards(page, 1)[0];
        Fling(window, SceneHarness.Center(binned, window), new Vector(80, 0), 6);
        // A second drag while the first card is still spinning out, so its removal lands inside this reflow.
        Fling(window, SceneHarness.Center(mover, window), new Vector(-40, 0), 4);

        Assert.True(await SceneHarness.Pump(() => binned.Parent == null, rounds: 200), "the bin never took the card");
        // Something was still easing when the bin took the card, which is what makes this a dismissal mid-reflow.
        Assert.Contains(
            SceneHarness.Cards(page),
            card => !SceneHarness.Near(SceneHarness.Extent(page, SceneHarness.CardColor(card)) ?? default, SceneHarness.Slot(card, window)));

        await SceneHarness.Settle();
        var shown = SceneHarness.Slot(page.GetControl<ScrollViewer>("Scroller"), window);
        foreach (var card in SceneHarness.Cards(page).Where(card => shown.Contains(SceneHarness.Slot(card, window))))
        {
            var box = SceneHarness.Extent(page, SceneHarness.CardColor(card));
            Assert.True(
                box is { } drawn && SceneHarness.Near(drawn, SceneHarness.Slot(card, window)),
                $"a card was left drawn at {box} instead of {SceneHarness.Slot(card, window)}");
        }
        for (var lane = 0; lane < Datasets.Lanes.Count; lane++)
        {
            var left = Cards(page, lane);
            Assert.Equal(left.Select((_, i) => i * RowPitch), left.Select(Canvas.GetTop));
        }
        SceneHarness.Close(window);
    }

    [AvaloniaFact]
    public async Task DraggingIntoTheEdgeBandScrollsTheBoardUnderTheCard()
    {
        var page = new BoardPage();
        var window = SceneHarness.Show(page, 900, 520);
        await SceneHarness.Settle();
        var scroller = page.GetControl<ScrollViewer>("Scroller");
        Assert.True(scroller.Extent.Height > scroller.Viewport.Height, "the board fits, so the edge band has nothing to scroll");

        var grab = SceneHarness.Center(Cards(page, 0)[0], window);
        var band = new Point(grab.X, scroller.TranslatePoint(new Point(0, scroller.Viewport.Height - 6), window)!.Value.Y);
        window.MouseDown(grab, MouseButton.Left);
        for (var i = 0; i < 6; i++)
        {
            window.MouseMove(band);
            TestFrames.Tick();
        }

        Assert.True(scroller.Offset.Y > 0, "the pointer sat in the edge band and the board never scrolled");
        window.MouseUp(band, MouseButton.Left);
        SceneHarness.Close(window);
    }

    [AvaloniaFact]
    public async Task TheLaneACardIsAimedAtOpensAGapThatTheDropFillsWithoutMovingAnythingElse()
    {
        const int slot = 1;
        var page = new BoardPage();
        var window = SceneHarness.Show(page, 900, 520);
        await SceneHarness.Settle();
        var card = Cards(page, 0)[1];
        var lane = Cards(page, 1);
        var grab = SceneHarness.Center(card, window);
        var top = card.TranslatePoint(default, window)!.Value;
        Point Over(int target, int row) =>
            grab + (Lane(page, target).TranslatePoint(new Point(0, row * RowPitch), window)!.Value - top).WithX(target * LanePitch);

        window.MouseDown(grab, MouseButton.Left);
        var pointer = MoveTo(window, grab, Over(1, slot));
        Assert.Equal(lane.Select((_, i) => (i >= slot ? i + 1 : i) * RowPitch), lane.Select(Canvas.GetTop));

        pointer = MoveTo(window, pointer, Over(2, 0));
        Assert.Equal(lane.Select((_, i) => i * RowPitch), lane.Select(Canvas.GetTop));

        pointer = MoveTo(window, pointer, Over(1, slot));
        // The draggable samples velocity every other frame and keeps the fastest of three, so this lets it fall to rest.
        for (var i = 0; i < 8; i++)
        {
            window.MouseMove(pointer);
            TestFrames.Tick();
        }
        window.MouseUp(pointer, MouseButton.Left);
        var frames = 0;
        for (; frames < 60 && !ReferenceEquals(card.Parent, Lane(page, 1)); frames++) TestFrames.Tick();

        Assert.True(frames <= 20, $"a gentle drop took {frames} frames to settle");
        Assert.Same(card, Cards(page, 1)[slot]);
        for (var i = 0; i < Datasets.Lanes.Count; i++)
        {
            var left = Cards(page, i);
            Assert.Equal(left.Select((_, row) => row * RowPitch), left.Select(Canvas.GetTop));
        }
        SceneHarness.Close(window);
    }

    [AvaloniaFact]
    public async Task ADroppedCardIsDrawnWhereItWasLetGoUntilItSettles()
    {
        var page = new BoardPage();
        var window = SceneHarness.Show(page, 900, 520);
        var card = Cards(page, 0)[1];
        SceneHarness.Flood(card, SceneHarness.Tint(0));
        await SceneHarness.Settle();
        var lifted = SceneHarness.Extent(page, SceneHarness.CardColor(card))!.Value;
        var grab = SceneHarness.Center(card, window);

        // Let go mid-move, as by hand, so the release throw is still running and the card lands after the button is up.
        window.MouseDown(grab, MouseButton.Left);
        var pointer = MoveTo(window, grab, grab + (new Vector(LanePitch, RowPitch) * .8), steps: 12);
        var released = SceneHarness.Extent(page, SceneHarness.CardColor(card))!.Value;
        window.MouseUp(pointer, MouseButton.Left);
        Assert.NotSame(Lane(page, 1), card.Parent);

        var frames = new List<Rect?>();
        for (var after = -1; after < 30 && frames.Count < 200; frames.Add(SceneHarness.Extent(page, SceneHarness.CardColor(card))))
        {
            TestFrames.Tick();
            if (after >= 0 || ReferenceEquals(card.Parent, Lane(page, 1))) after++;
        }

        Assert.Same(Lane(page, 1), card.Parent);
        var landed = SceneHarness.Slot(card, window);
        var path = new Rect(released.Position, landed.Position).Normalize().Inflate(8);
        for (var frame = 0; frame < frames.Count; frame++)
        {
            Assert.True(
                frames[frame] is { } drawn && !SceneHarness.Near(drawn, lifted) && path.Contains(drawn.Position),
                $"frame {frame} after the drop drew the card at {frames[frame]}, off the way from {released} to {landed} (lifted from {lifted})");
        }
        SceneHarness.Close(window);
    }

    [AvaloniaFact]
    public async Task DraggingAPriorityByItsGripReordersTheListAndSettlesItInItsNewSlot()
    {
        var page = new BoardPage();
        var window = SceneHarness.Show(page, 900, 520);
        await SceneHarness.Settle();
        var list = page.GetControl<ListBox>("Priorities");
        var items = (ObservableCollection<string>)list.ItemsSource!;
        var moved = items[0];
        var expected = items.Skip(1).Take(2).Append(moved).Concat(items.Skip(3)).ToList();
        var container = list.ContainerFromIndex(0)!;
        var grip = container.GetVisualDescendants().OfType<Border>().First(Reorder.GetHandle);

        var grab = SceneHarness.Center(grip, window);
        window.MouseDown(grab, MouseButton.Left);
        var pointer = MoveTo(window, grab, grab + new Vector(container.Bounds.Width * 2.2, 0), steps: 10);
        window.MouseUp(pointer, MouseButton.Left);

        Assert.Equal(expected, items);
        var settled = list.ContainerFromItem(moved)!;
        Assert.True(
            await SceneHarness.Pump(() => settled.RenderTransform is null or { Value.IsIdentity: true }, rounds: 120),
            "the released priority never sprang into its slot");
        Assert.Equal(2, list.IndexFromContainer(settled));
        SceneHarness.Close(window);
    }

    [AvaloniaTheory]
    [InlineData(12)]
    [InlineData(40)]
    public async Task AVerticalFlingWithinALaneLeavesEveryCardDrawnInItsOwnSlot(double step)
    {
        var page = new BoardPage();
        var window = SceneHarness.Show(page, 900, 520);
        await SceneHarness.Settle();
        var card = Cards(page, 0)[1];

        Fling(window, SceneHarness.Center(card, window), new Vector(0, step), 4);
        for (var i = 0; i < 200; i++) TestFrames.Tick();
        await SceneHarness.Settle();

        var cards = Cards(page, 0);
        Assert.All(cards, laid => Assert.Equal((0d, 0d), ((double)PrestoAvalonia.Get(laid, "X")!, (double)PrestoAvalonia.Get(laid, "Y")!)));
        Assert.Equal(cards.Length, cards.Select(Canvas.GetTop).Distinct().Count());
        SceneHarness.Close(window);
    }

    private static Point MoveTo(Window window, Point from, Point to, int steps = 6)
    {
        for (var i = 1; i <= steps; i++)
        {
            window.MouseMove(from + ((to - from) * i / steps));
            TestFrames.Tick();
        }
        return to;
    }

    private static void Fling(Window window, Point grab, Vector step, int steps)
    {
        window.MouseDown(grab, MouseButton.Left);
        for (var i = 1; i <= steps; i++)
        {
            window.MouseMove(grab + (step * i));
            TestFrames.Tick();
        }
        window.MouseUp(grab + (step * steps), MouseButton.Left);
        TestFrames.Tick();
    }

    private static Canvas Lane(Visual page, int lane) =>
        page.GetLogicalDescendants().OfType<Canvas>().First(canvas => canvas.Name == $"Lane{lane}");

    // In slot order, which is not the order the canvas holds them in once one has been dropped back.
    private static Border[] Cards(Visual page, int lane) =>
        [.. Lane(page, lane).Children.OfType<Border>().OrderBy(Canvas.GetTop)];
}
