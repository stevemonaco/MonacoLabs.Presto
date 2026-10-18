using System.Collections.ObjectModel;
using System.Collections.Specialized;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.VisualTree;
using MonacoLabs.Presto.Compositor;

namespace MonacoLabs.Presto.Tests;

/// <summary>
/// <see cref="Presence.ScrollAnchorProperty"/> (FIXES-PLAN §7): a change above the first visible row of a scrolled
/// virtualized list leaves that row where it is drawn, and nothing else changes what the list would do without it.
/// </summary>
public sealed class ScrollAnchorTests : CompositorTestBase
{
    private static readonly AnimationBuilder FadeIn = Presto.Build().Opacity(from: 0, to: 1).Duration(200).Ease("linear");

    /// <summary>
    /// Avalonia's own anchoring holds at some offsets and not at others: at 3100 the row moves, and at 2990 it is recycled
    /// out of view as the panel re-estimates where its rows start.
    /// </summary>
    [AvaloniaTheory]
    [InlineData(2990)]
    [InlineData(3100)]
    public void WithoutTheAnchorAnInsertAboveMovesTheRowBeingRead(double offset)
    {
        var items = Rows(200);
        var list = Show(Feed(items, anchor: false));
        Jump(list, offset);
        var (item, y) = Reading(list);

        items.Insert(1, "added");
        Tick();

        Assert.NotEqual(y, YOf(list, item));
    }

    [AvaloniaTheory]
    [InlineData("insert", 2990)]
    [InlineData("insert", 3100)]
    [InlineData("remove", 2990)]
    [InlineData("remove", 3100)]
    [InlineData("batch", 2990)]
    [InlineData("batch", 3100)]
    [InlineData("replace", 2990)]
    [InlineData("replace", 3100)]
    [InlineData("move", 2990)]
    [InlineData("move", 3100)]
    [InlineData("just above", 2990)]
    [InlineData("just above", 3100)]
    public void AChangeAboveKeepsTheRowBeingReadInPlace(string change, double offset)
    {
        var items = Rows(200);
        var list = Show(Feed(items));
        Jump(list, offset);
        var (item, y) = Reading(list);
        var index = items.IndexOf((string)item);

        Apply(items, change, index);
        Tick();

        Assert.Equal(y, YOf(list, item));
    }

    [AvaloniaTheory]
    [InlineData("inside", 3100)]
    [InlineData("reading row removed", 3100)]
    [InlineData("below", 3100)]
    [InlineData("reset", 3100)]
    [InlineData("just above", 0)]
    public void ChangesItDoesNotAnchorPlayOutAsWithoutIt(string change, double offset)
    {
        var plainItems = Rows(200);
        var plain = Show(Feed(plainItems, anchor: false));
        var anchoredItems = Rows(200);
        var anchored = Show(Feed(anchoredItems));
        Jump(plain, offset);
        Jump(anchored, offset);
        var index = anchoredItems.IndexOf((string)Reading(anchored).Item);

        Apply(plainItems, change, index);
        Apply(anchoredItems, change, index);
        Tick();

        Assert.Equal(plain.Scroll!.Offset, anchored.Scroll!.Offset);
        Assert.Equal(Reading(plain), Reading(anchored));
        if (offset == 0) Assert.Equal(("added", 0d), Reading(anchored));
    }

    [AvaloniaFact]
    public void WithItemLayoutNoContainerEasesOnTheCompensatingPass()
    {
        var items = Rows(200);
        var list = Feed(items);
        Animate.SetItemLayout(list, HeldMove);
        Show(list);
        Jump(list, 3100);
        var (item, y) = Reading(list);

        items.Insert(1, "added");
        Tick();

        Assert.Equal(y, YOf(list, item));
        Assert.All(list.GetRealizedContainers(), container => Assert.True(LayoutTransition.Live(container) is null or { Completed: true }, "a row eased"));
    }

    [AvaloniaFact]
    public void RowsAlreadyRevealedDoNotRevealAgain()
    {
        var rows = new Dictionary<string, Border>();
        var items = Rows(200);
        var list = Show(Feed(items, rows: rows, reveal: FadeIn));
        for (var y = 100; y <= 3100; y += 100) Jump(list, y);
        Frames.Advance(300, 16);

        items.Insert(1, "added");
        Tick();

        Assert.NotEmpty(Realized(rows));
        Assert.All(Realized(rows), row => Assert.Equal(1, row.Opacity));
    }

    [AvaloniaFact]
    public void ARowTheCorrectionBringsIntoViewRevealsAsAScrollWould()
    {
        var rows = new Dictionary<string, Border>();
        var items = Rows(200);
        var list = Show(Feed(items, rows: rows, reveal: FadeIn));
        Jump(list, 3010);
        var index = items.IndexOf((string)Reading(list).Item);
        var above = items[index - 2];
        Assert.False(rows.TryGetValue(above, out var unseen) && unseen.IsAttachedToVisualTree(), "the row two above the reading row is already realized");

        items.RemoveAt(index - 1);
        Tick();

        Assert.True(rows[above].IsAttachedToVisualTree());
        Assert.Equal(0, rows[above].Opacity);
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void AnInsertThatLandsOutOfViewEntersNothing(bool aboveThePartlyShownRow)
    {
        var rows = new Dictionary<string, Border>();
        var items = Rows(200);
        var list = Show(Feed(items, rows: rows, enter: FadeIn));
        Jump(list, 3100);
        Frames.Advance(300, 16);
        var index = aboveThePartlyShownRow ? items.IndexOf((string)Reading(list).Item) - 1 : 1;

        items.Insert(index, "added");
        Tick();

        Assert.False(rows.TryGetValue("added", out var added) && added.IsAttachedToVisualTree() && added.IsEffectivelyVisible, "the added row is in view");
        Assert.All(Realized(rows), row => Assert.Equal(1, row.Opacity));
    }

    [AvaloniaTheory]
    [InlineData(100)]
    [InlineData(3100)]
    public void AWheelNotchBeforeTheAnchorClearsIsNotUndone(double offset)
    {
        var items = Rows(200);
        var list = Show(Feed(items));
        Jump(list, offset);
        items.Insert(1, "added");
        list.UpdateLayout();
        var anchored = list.Scroll!.Offset.Y;

        UnqueuedWheel((Window)TopLevel.GetTopLevel(list)!, new Point(20, 100), new Vector(0, 1));
        Assert.Equal(anchored - 50, list.Scroll.Offset.Y);
        list.UpdateLayout();
        Tick();

        Assert.Equal(anchored - 50, list.Scroll.Offset.Y);
    }

    [AvaloniaFact]
    public void AScrollToTheTopBeforeTheAnchorClearsStaysThereWithTheAddedRowShown()
    {
        var items = Rows(200);
        var list = Show(Feed(items));
        Jump(list, 100);
        items.Insert(0, "added");
        list.UpdateLayout();

        UnqueuedWheel((Window)TopLevel.GetTopLevel(list)!, new Point(20, 100), new Vector(0, 10));
        list.UpdateLayout();
        Tick();

        Assert.Equal(default, list.Scroll!.Offset);
        Assert.Equal(("added", 0d), Reading(list));
    }

    [AvaloniaFact]
    public void TheAnchorEndsOnceThePassThatRealizesTheChangeHasFinished()
    {
        var items = Rows(200);
        var list = Show(Feed(items));
        Jump(list, 3100);

        items.Insert(1, "added");
        Assert.True(ItemChanges.For(list).Anchoring);
        Tick();

        Assert.False(ItemChanges.For(list).Anchoring);
    }

    // Headless input extensions drain Background jobs first; a desktop lets input outrank them.
    private static void UnqueuedWheel(Window window, Point at, Vector delta) =>
        window.PlatformImpl!.GetType().GetInterface("IHeadlessWindow")!.GetMethod("MouseWheel")!.Invoke(window.PlatformImpl, [at, delta, RawInputModifiers.None]);

    private static void Apply(BatchCollection items, string change, int reading)
    {
        switch (change)
        {
            case "insert": items.Insert(1, "added"); break;
            case "remove": items.RemoveAt(1); break;
            case "batch": items.InsertRange(1, ["added", "added2", "added3", "added4", "added5"]); break;
            case "replace": items[1] = "added"; break;
            case "move": items.Move(1, reading + 20); break;
            case "just above": items.Insert(reading, "added"); break;
            case "inside": items.Insert(reading + 1, "added"); break;
            case "reading row removed": items.RemoveAt(reading); break;
            case "below": items.Insert(reading + 50, "added"); break;
            case "reset": items.ResetWithInsert(1, "added"); break;
        }
    }

    private static void Jump(ListBox list, double offset)
    {
        list.Scroll!.Offset = new Vector(0, offset);
        Tick();
    }

    private static (object Item, double Y) Reading(ListBox list) => list.GetRealizedContainers()
        .Where(container => container.IsVisible)
        .Select(container => (Item: list.ItemFromContainer(container)!, Index: list.IndexFromContainer(container), Y: YIn(list, container)))
        .Where(row => row.Y >= 0)
        .MinBy(row => row.Index) is var (item, _, y) ? (item, y) : throw new InvalidOperationException("No row is in view.");

    private static double? YOf(ListBox list, object item) => list.ContainerFromItem(item) is { IsVisible: true } container ? YIn(list, container) : null;

    private static double YIn(ListBox list, Control container) => container.TranslatePoint(default, (Visual)list.Scroll!)!.Value.Y;

    private static List<Border> Realized(Dictionary<string, Border> rows) =>
        rows.Values.Where(row => row.IsAttachedToVisualTree() && row.IsEffectivelyVisible && row.DataContext is string).ToList();

    private static BatchCollection Rows(int count) => new(Enumerable.Range(0, count).Select(i => $"row{i}"));

    private static ListBox Show(ListBox list)
    {
        new Window { Content = list, Background = Brushes.White, Width = 100, Height = 300 }.Show();
        Tick();
        return list;
    }

    private static ListBox Feed(
        BatchCollection items, bool anchor = true, Dictionary<string, Border>? rows = null, AnimationBuilder? reveal = null, AnimationBuilder? enter = null)
    {
        var list = new ListBox
        {
            Height = 200,
            VerticalAlignment = VerticalAlignment.Top,
            ItemsSource = items,
            ItemTemplate = new FuncDataTemplate<string>((item, _) =>
            {
                var row = new Border
                {
                    Width = 40,
                    Height = item is null ? 30 : item.StartsWith("added") ? 40 : int.Parse(item[3..]) % 3 == 0 ? 90 : 30,
                    HorizontalAlignment = HorizontalAlignment.Left,
                    Background = Brushes.Blue,
                };
                Presence.SetReveal(row, reveal);
                Presence.SetEnter(row, enter);
                if (item is not null && rows is not null) rows[item] = row;
                return row;
            }, supportsRecycling: false),
            Styles =
            {
                new Style(x => x.OfType<ListBoxItem>())
                {
                    Setters = { new Setter(TemplatedControl.PaddingProperty, new Thickness(0)), new Setter(Layoutable.MinHeightProperty, 0d) },
                },
            },
        };
        Presence.SetScrollAnchor(list, anchor);
        return list;
    }

    private sealed class BatchCollection(IEnumerable<string> items) : ObservableCollection<string>(items)
    {
        public void InsertRange(int index, string[] added)
        {
            for (var i = 0; i < added.Length; i++) Items.Insert(index + i, added[i]);
            OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, added, index));
        }

        public void ResetWithInsert(int index, string added)
        {
            Items.Insert(index, added);
            OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        }
    }
}
