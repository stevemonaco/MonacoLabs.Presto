using System.Collections.ObjectModel;
using System.Collections.Specialized;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.VisualTree;
using Avalonia.Controls.Documents;

namespace MonacoLabs.Presto.Tests;

/// <summary>
/// <see cref="Presence"/>'s lifecycle (LIFECYCLE-PLAN §2): every arrival is classified Initial or Enter by one rule, each
/// plays only its own builder, arrivals in one pass stagger by arrival order, and a departure snapshots its exit before
/// reverting its entrance.
/// </summary>
public sealed class PresenceLifecycleTests : CompositorTestBase
{
    private static readonly AnimationBuilder FadeIn = Presto.Build().Opacity(from: 0, to: 1).Duration(200).Ease("linear");
    private static readonly AnimationBuilder FadeOut = Presto.Build().Opacity(to: 0).Duration(200).Ease("linear");

    [AvaloniaFact]
    public void EverythingShownWithTheWindowArrivesAsInitial()
    {
        var first = Recorded(new Border());
        var alone = Recorded(new Border());
        var window = new Window { Content = new StackPanel { Children = { first.Control, new Border { Child = alone.Control } } } };

        window.Show();
        Tick();

        Assert.Equal([PresenceMoment.Initial], first.Moments);
        Assert.Equal([PresenceMoment.Initial], alone.Moments);
    }

    [AvaloniaFact]
    public void ItemsAListLoadsWithAreInitialAndItemsAddedLaterEnter()
    {
        var moments = new Dictionary<string, PresenceMoment>();
        var items = new ObservableCollection<string> { "a", "b" };
        var list = new ItemsControl
        {
            ItemsSource = items,
            ItemTemplate = new FuncDataTemplate<string>((item, _) =>
            {
                var row = new Border { Height = 10 };
                row.AttachedToVisualTree += (_, _) => moments[item] = Presence.ClassifyArrival(row);
                return row;
            }),
        };
        Show(list);

        items.Add("c");
        Tick();

        Assert.Equal(PresenceMoment.Initial, moments["a"]);
        Assert.Equal(PresenceMoment.Initial, moments["b"]);
        Assert.Equal(PresenceMoment.Enter, moments["c"]);
    }

    [AvaloniaFact]
    public void TheOnlyRowOfAListNavigatedInIsInitialEvenThroughSingleChildWrappers()
    {
        var host = new ContentControl();
        Show(host, new Border());
        var moments = new List<PresenceMoment>();
        var list = new ItemsControl
        {
            ItemsSource = new[] { "only" },
            ItemTemplate = new FuncDataTemplate<string>((_, _) =>
            {
                var row = new Border { Height = 10 };
                row.AttachedToVisualTree += (_, _) => moments.Add(Presence.ClassifyArrival(row));
                return row;
            }),
        };

        host.Content = new Border { Child = list };
        Tick();

        Assert.Equal([PresenceMoment.Initial], moments);
    }

    [AvaloniaFact]
    public void AChildAddedToALoadedPanelEnters()
    {
        var panel = Show();
        var added = Recorded(new Border());

        panel.Children.Add(added.Control);

        Assert.Equal([PresenceMoment.Enter], added.Moments);
    }

    [AvaloniaFact]
    public void SwappedInContentEntersWhileTheContentOfItsViewIsInitial()
    {
        var host = new ContentControl();
        Show(host, new Border());
        var inside = Recorded(new Border());
        var view = Recorded(new Border { Child = new StackPanel { Children = { inside.Control, new Border() } } });

        host.Content = view.Control;

        Assert.Equal([PresenceMoment.Enter], view.Moments);
        Assert.Equal([PresenceMoment.Initial], inside.Moments);
    }

    [AvaloniaFact]
    public void ElementsAddedTogetherInOneSubtreeAreInitial()
    {
        var panel = Show();
        var first = Recorded(new Border());
        var second = Recorded(new Border());

        panel.Children.Add(new StackPanel { Children = { first.Control, second.Control } });

        Assert.Equal([PresenceMoment.Initial], first.Moments);
        Assert.Equal([PresenceMoment.Initial], second.Moments);
    }

    [AvaloniaFact]
    public void AViewShownAgainIsInitialAgain()
    {
        var host = new ContentControl();
        Show(host, new Border());
        var inside = Recorded(new Border());
        var view = new StackPanel { Children = { inside.Control, new Border() } };
        host.Content = view;
        Tick();

        host.Content = null;
        Tick();
        host.Content = view;

        Assert.Equal([PresenceMoment.Initial, PresenceMoment.Initial], inside.Moments);
    }

    [AvaloniaFact]
    public void AViewPutStraightBackIsInitialAgainWithoutGhosts()
    {
        var host = new ContentControl();
        var panel = Show(host);
        var moments = new List<PresenceMoment>();
        var list = new ItemsControl
        {
            ItemsSource = new[] { "a", "b" },
            ItemTemplate = new FuncDataTemplate<string>((_, _) =>
            {
                var row = Arriving(initial: FadeIn, exit: FadeOut);
                row.AttachedToVisualTree += (_, _) => moments.Add(Presence.ClassifyArrival(row));
                return row;
            }),
        };
        var view = new StackPanel { Children = { list, new Border() } };
        host.Content = view;
        Tick();
        moments.Clear();

        host.Content = null;
        host.Content = view;
        Tick();

        Assert.NotEmpty(moments);
        Assert.All(moments, moment => Assert.Equal(PresenceMoment.Initial, moment));
        Assert.DoesNotContain(OverlayLayer.GetOverlayLayer(panel)!.Children, child => child is Canvas);
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void ScrollingAVirtualizedListPlaysNothingAndLeavesNoGhosts(bool viaArrive)
    {
        var rows = new Dictionary<string, Border>();
        var list = VirtualizedList(new ObservableCollection<string>(Enumerable.Range(0, 100).Select(i => $"row{i}")), rows, viaArrive);
        var panel = Show(list);
        Frames.Advance(300, 16);
        Tick();

        list.Scroll!.Offset = new Vector(0, 600);
        Tick();

        Assert.DoesNotContain(OverlayLayer.GetOverlayLayer(panel)!.Children, child => child is Canvas);
        var realized = rows.Where(row => row.Key != "row0" && Avalonia.VisualTree.VisualExtensions.IsAttachedToVisualTree(row.Value) && row.Value.DataContext is string).ToList();
        Assert.NotEmpty(realized);
        Assert.All(realized, row => Assert.Equal(1, row.Value.Opacity));
    }

    [AvaloniaFact]
    public void AVirtualizedListStillEntersAddedItemsAndExitsRemovedOnes()
    {
        var items = new ObservableCollection<string>(Enumerable.Range(0, 50).Select(i => $"row{i}"));
        var rows = new Dictionary<string, Border>();
        var list = VirtualizedList(items, rows);
        var panel = Show(list);
        Frames.Advance(300, 16);
        Tick();

        items.RemoveAt(1);
        Tick();
        Assert.Single(OverlayLayer.GetOverlayLayer(panel)!.Children.OfType<Canvas>());

        items.Insert(1, "added");
        Tick();
        Assert.Equal(0, rows["added"].Opacity);
    }

    [AvaloniaFact]
    public void ARowRemovedFromAScrolledVirtualizedListIsGhostedWhereItWasDrawn()
    {
        var items = new ObservableCollection<string>(Enumerable.Range(0, 50).Select(i => $"row{i}"));
        var list = new ListBox
        {
            Height = 120,
            ItemsSource = items,
            ItemTemplate = new FuncDataTemplate<string>((_, _) =>
            {
                var row = new Border { Height = 20, Width = 40, HorizontalAlignment = HorizontalAlignment.Left };
                row.Bind(Border.BackgroundProperty, new Avalonia.Data.Binding { Converter = new Avalonia.Data.Converters.FuncValueConverter<string, IBrush>(text => text == "row5" ? Brushes.Red : Brushes.Blue) });
                Presence.SetExit(row, FadeOut);
                return row;
            }),
        };
        var panel = Show(list);
        list.Scroll!.Offset = new Vector(0, 150);
        Tick();
        Frames.Advance(300, 16);
        Tick();
        var drawn = Box(panel);

        items.Remove("row5");
        Tick();

        Assert.Equal(drawn, Box(panel));
    }

    [AvaloniaFact]
    public void AGhostOfARecycledRowKeepsWhatItInheritedFromItsContainer()
    {
        var items = new ObservableCollection<string>(Enumerable.Range(0, 50).Select(i => $"row{i}"));
        var list = new ListBox
        {
            Height = 200,
            ItemsSource = items,
            ItemTemplate = new FuncDataTemplate<string>((_, _) =>
            {
                var row = new TextBlock { Text = "█", Foreground = Brushes.Red, HorizontalAlignment = HorizontalAlignment.Left };
                row.Bind(Visual.IsVisibleProperty, new Avalonia.Data.Binding { Converter = new Avalonia.Data.Converters.FuncValueConverter<string, bool>(text => text == "row1") });
                Presence.SetExit(row, FadeOut);
                return row;
            }),
        };
        list.Styles.Add(new Style(x => x.OfType<ListBoxItem>()) { Setters = { new Setter(TextElement.FontSizeProperty, 40d) } });
        var panel = Show(list);
        Frames.Advance(300, 16);
        Tick();
        var drawn = Box(panel);

        items.Remove("row1");
        Tick();

        // A glyph lands on another sub-pixel offset in the snapshot, so the box can differ by its anti-aliased edge.
        var ghost = Box(panel);
        Assert.InRange(ghost.Width, drawn.Width - 1, drawn.Width + 1);
        Assert.InRange(ghost.Height, drawn.Height - 1, drawn.Height + 1);
    }

    [AvaloniaFact]
    public void ClearingAListExitsEveryRow()
    {
        var items = new ObservableCollection<string> { "a", "b", "c" };
        var list = new ItemsControl
        {
            ItemsSource = items,
            ItemTemplate = new FuncDataTemplate<string>((_, _) => Arriving(exit: FadeOut)),
        };
        var panel = Show(list);
        Tick();

        items.Clear();
        Tick();

        Assert.Equal(3, OverlayLayer.GetOverlayLayer(panel)!.Children.OfType<Canvas>().Count());
    }

    [AvaloniaFact]
    public void RevealAlonePlaysNothingOnFirstLoadOrOnAnAddedItem()
    {
        var items = new ObservableCollection<string>(Enumerable.Range(0, 50).Select(i => $"row{i}"));
        var rows = new Dictionary<string, Border>();
        Show(VirtualizedList(items, rows, reveal: FadeIn));

        items.Insert(1, "added");
        Tick();

        Assert.All(Realized(rows), row => Assert.Equal(1, row.Opacity));
        Assert.Equal(1, rows["added"].Opacity);
    }

    [AvaloniaFact]
    public void RowsRevealedInOnePassStaggerDownFromTheRowsAlreadyShown()
    {
        var staggered = Presto.Build().Opacity(from: 0, to: 1).Duration(100).Delay(Presto.Stagger(80)).Ease("linear");
        var rows = new Dictionary<string, Border>();
        var list = VirtualizedList(new ObservableCollection<string>(Enumerable.Range(0, 100).Select(i => $"row{i}")), rows, reveal: staggered);
        Show(list);
        var loaded = Realized(rows);

        list.Scroll!.Offset = new Vector(0, 90);
        Tick();
        Frames.Advance(40, 16);

        var revealed = Realized(rows).Except(loaded).OrderBy(row => int.Parse(((string)row.DataContext!)[3..])).ToList();
        Assert.True(revealed.Count >= 2);
        Assert.InRange(revealed[0].Opacity, .1, .9);
        Assert.Equal(0, revealed[^1].Opacity);
    }

    [AvaloniaFact]
    public void RecyclingOrDetachingARowRevertsItsRunningReveal()
    {
        var rows = new Dictionary<string, Border>();
        var list = VirtualizedList(new ObservableCollection<string>(Enumerable.Range(0, 100).Select(i => $"row{i}")), rows, reveal: FadeIn);
        var panel = Show(list);
        list.Scroll!.Offset = new Vector(0, 80);
        Tick();
        Frames.Advance(50, 16);
        var recycled = Realized(rows);
        list.Scroll!.Offset = new Vector(0, 160);
        Tick();
        Frames.Advance(50, 16);
        var detached = Realized(rows).Except(recycled).ToList();

        panel.Children.Remove(list);

        Assert.All(recycled, row => Assert.False(row.IsAttachedToVisualTree()));
        Assert.All(recycled, row => Assert.Equal(1, row.Opacity));
        Assert.NotEmpty(detached);
        Assert.All(detached, row => Assert.Equal(1, row.Opacity));
    }

    [AvaloniaFact]
    public void ClearingRevealMidRevealReverts()
    {
        var rows = new Dictionary<string, Border>();
        var list = VirtualizedList(new ObservableCollection<string>(Enumerable.Range(0, 100).Select(i => $"row{i}")), rows, reveal: FadeIn);
        Show(list);
        list.Scroll!.Offset = new Vector(0, 80);
        Tick();
        Frames.Advance(50, 16);
        var row = Realized(rows)[^1];
        Assert.InRange(row.Opacity, .1, .9);

        Presence.SetReveal(row, null);

        Assert.Equal(1, row.Opacity);
    }

    [AvaloniaTheory]
    [InlineData(true)]
    [InlineData(false)]
    public void ScrollingBackOverSeenRowsRevealsThemOnlyWithoutRevealOnce(bool revealOnce)
    {
        var rows = new Dictionary<string, Border>();
        var list = VirtualizedList(new ObservableCollection<string>(Enumerable.Range(0, 100).Select(i => $"row{i}")), rows, reveal: FadeIn, revealOnce: revealOnce);
        Show(list);
        ScrollTo(list, rows, 600);
        ScrollTo(list, rows, 1200);
        Frames.Advance(300, 16);

        var returned = ScrollTo(list, rows, 600);

        Assert.NotEmpty(returned);
        Assert.All(returned, row => Assert.Equal(revealOnce ? 1 : 0, row.Opacity));
    }

    [AvaloniaTheory]
    [InlineData(true)]
    [InlineData(false)]
    public void AJumpOfMoreThanAViewportSnapsTheRowsItRealizes(bool revealOnce)
    {
        var rows = new Dictionary<string, Border>();
        var list = VirtualizedList(new ObservableCollection<string>(Enumerable.Range(0, 100).Select(i => $"row{i}")), rows, reveal: FadeIn, revealOnce: revealOnce);
        Show(list);

        list.Scroll!.Offset = new Vector(0, 600);
        Tick();
        var landed = Realized(rows).Select(row => row.DataContext).ToList();
        Assert.All(Realized(rows), row => Assert.Equal(1, row.Opacity));
        list.Scroll.Offset = default;
        Tick();
        var returned = ScrollTo(list, rows, 600);

        Assert.NotEmpty(returned);
        Assert.All(returned, row => Assert.Contains(row.DataContext, landed));
        Assert.All(returned, row => Assert.Equal(revealOnce ? 1 : 0, row.Opacity));
    }

    [AvaloniaFact]
    public void TheRowsOfTheFirstLoadAndAnAddedItemCountAsSeen()
    {
        var items = new ObservableCollection<string>(Enumerable.Range(0, 50).Select(i => $"row{i}"));
        var rows = new Dictionary<string, Border>();
        var list = VirtualizedList(items, rows, reveal: FadeIn);
        Show(list);
        items.Insert(1, "added");
        Tick();
        ScrollTo(list, rows, 600);
        Frames.Advance(300, 16);

        ScrollTo(list, rows, 0);

        Assert.Contains(Realized(rows), row => (string)row.DataContext! == "added");
        Assert.All(Realized(rows), row => Assert.Equal(1, row.Opacity));
    }

    [AvaloniaFact]
    public void AnItemRemovedAndAddedBackIsRevealedAgain()
    {
        var items = new ObservableCollection<string>(Enumerable.Range(0, 50).Select(i => $"row{i}"));
        var rows = new Dictionary<string, Border>();
        var list = VirtualizedList(items, rows, reveal: FadeIn);
        Show(list);
        ScrollTo(list, rows, 600);
        Frames.Advance(300, 16);

        items.Remove("row1");
        items.Insert(1, "row1");
        Tick();
        ScrollTo(list, rows, 0);

        Assert.All(Realized(rows), row => Assert.Equal((string)row.DataContext! == "row1" ? 0 : 1, row.Opacity));
    }

    [AvaloniaFact]
    public void AResetForgetsTheSeenRows()
    {
        var items = new ObservableCollection<string>(Enumerable.Range(0, 50).Select(i => $"row{i}"));
        var rows = new Dictionary<string, Border>();
        var list = VirtualizedList(items, rows, reveal: FadeIn);
        Show(list);
        ScrollTo(list, rows, 600);
        ScrollTo(list, rows, 0);
        Frames.Advance(300, 16);

        items.Clear();
        foreach (var i in Enumerable.Range(0, 50)) items.Add($"row{i}");
        Tick();
        var revealed = ScrollTo(list, rows, 600);

        Assert.NotEmpty(revealed);
        Assert.All(revealed, row => Assert.Equal(0, row.Opacity));
    }

    [AvaloniaFact]
    public void TheSeenMemoryHoldsNoItemAfterItLeavesTheList()
    {
        var items = new ObservableCollection<string>(Enumerable.Range(0, 50).Select(i => $"row{i}"));
        var rows = new Dictionary<string, Border>();
        var list = VirtualizedList(items, rows, reveal: FadeIn);
        Show(list);
        ScrollTo(list, rows, 300);
        Assert.NotEmpty(Presence.Seen(list));

        items.RemoveAt(0);
        items[15] = "replacement";
        items.Move(1, 40);
        Tick();
        Assert.All(Presence.Seen(list), item => Assert.Contains((string)item, items));

        while (items.Count > 0) items.RemoveAt(items.Count - 1);
        Tick();
        Assert.Empty(Presence.Seen(list));
    }

    [AvaloniaTheory]
    [InlineData(300)]
    [InlineData(1000)]
    public void RowsInsertedAboveAScrolledListRevealInTheWheelNotchThatBringsThemIntoView(double offset)
    {
        var items = new ObservableCollection<string>(Enumerable.Range(0, 200).Select(i => $"row{i}"));
        var rows = new Dictionary<string, Border>();
        var list = VirtualizedList(items, rows, reveal: FadeIn, height: item => item.StartsWith("added") ? 40 : int.Parse(item[3..]) % 3 == 0 ? 90 : 30);
        Presence.SetScrollAnchor(list, true);
        var window = (Window)TopLevel.GetTopLevel(Show(list))!;
        list.Scroll!.Offset = new Vector(0, offset);
        Tick();
        Frames.Advance(300, 16);
        var added = new List<string>();
        void Insert()
        {
            added.Add($"added{added.Count}");
            items.Insert(0, added[^1]);
        }
        for (var i = 0; i < 3; i++)
        {
            Insert();
            Tick();
        }

        var revealed = new Dictionary<string, bool>();
        for (var notch = 1; list.Scroll.Offset.Y > 0 && notch < 200; notch++)
        {
            window.MouseWheel(new Point(20, 50), new Vector(0, 1));
            // A live insert landing in the same frame as the notch, as the Activity feed's timer does.
            if (notch % 4 == 0 && added.Count < 8) Insert();
            Tick();
            foreach (var item in added.Where(item => !revealed.ContainsKey(item) && rows.TryGetValue(item, out var row) && row.IsAttachedToVisualTree() && row.Opacity < 1))
            {
                revealed[item] = InView(list, rows[item]);
            }
            Frames.Advance(300, 16);
        }

        Assert.All(added, item => Assert.True(revealed.GetValueOrDefault(item), item));
    }

    [AvaloniaFact]
    public void ATemplateBuiltForNoItemIsNeitherHeldNorRevealed()
    {
        var items = new ObservableCollection<string>(Enumerable.Range(0, 50).Select(i => $"row{i}"));
        ListBox? list = null;
        var blankAttaches = 0;
        var violations = 0;
        list = new ListBox
        {
            Height = 100,
            ItemsSource = items,
            ItemTemplate = new FuncDataTemplate<string>((_, _) =>
            {
                var row = Arriving(reveal: FadeIn);
                row.Background = Brushes.Red;
                row.AttachedToVisualTree += (_, _) =>
                {
                    if (row.DataContext is not null) return;
                    blankAttaches++;
                    if (ItemChanges.For(list!).Held.Contains(row) || Presto.GetAnimations(row).Count > 0) violations++;
                };
                return row;
            }),
        };
        Show(list);

        ScrollTo(list, [], 600);
        ScrollTo(list, [], 0);

        Assert.True(blankAttaches > 0, "no template was built for no item");
        Assert.Equal(0, violations);
    }

    [AvaloniaFact]
    public void ARowRealizedOutOfViewRevealsOnceWhenScrolledIntoView()
    {
        var rows = new Dictionary<string, Border>();
        var list = ListHoldingRows(rows);
        var held = ItemChanges.For(list).Held.ToList();
        Assert.All(held, row => Assert.False(InView(list, row)));
        Assert.All(held, row => Assert.Equal(1, row.Opacity));

        var revealed = new HashSet<Control>();
        for (var notch = 0; notch < 20; notch++)
        {
            Wheel(list, -1);
            foreach (var row in held.Where(row => row.IsAttachedToVisualTree() && InView(list, row)))
            {
                Assert.Equal(revealed.Add(row) ? 0 : 1, row.Opacity);
            }
            Frames.Advance(300, 16);
        }

        Assert.NotEmpty(revealed);
        Assert.DoesNotContain(ItemChanges.For(list).Held, revealed.Contains);
    }

    [AvaloniaFact]
    public void AJumpOntoHeldRowsSnapsThem()
    {
        var rows = new Dictionary<string, Border>();
        var list = ListHoldingRows(rows);
        var row = ItemChanges.For(list).Held[^1];
        var distance = TopIn(list, row) - list.Scroll!.Viewport.Height / 2;
        Assert.True(distance > list.Scroll.Viewport.Height);

        list.Scroll.Offset += new Vector(0, distance);
        Tick();

        Assert.True(row.IsAttachedToVisualTree() && InView(list, row));
        Assert.Equal(1, row.Opacity);
        Assert.Contains(row.DataContext!, Presence.Seen(list));
        Assert.Empty(ItemChanges.For(list).Held);
    }

    [AvaloniaFact]
    public void AHeldRowThatLeavesIsForgotten()
    {
        var rows = new Dictionary<string, Border>();
        var items = new ObservableCollection<string>(Enumerable.Range(0, 100).Select(i => $"row{i}"));
        var list = ListHoldingRows(rows, items);
        var held = ItemChanges.For(list).Held.Select(row => (string)row.DataContext!).ToList();

        foreach (var item in held) items.Remove(item);
        Tick();
        Assert.DoesNotContain(ItemChanges.For(list).Held, row => held.Contains((string)row.DataContext!));
        Assert.DoesNotContain(Presence.Seen(list), item => held.Contains((string)item));

        list.Scroll!.Offset = default;
        Tick();
        Assert.Empty(ItemChanges.For(list).Held);
        Assert.All(Presence.Seen(list), item => Assert.Contains((string)item, items));
    }

    private ListBox ListHoldingRows(Dictionary<string, Border> rows, ObservableCollection<string>? items = null)
    {
        var list = VirtualizedList(items ?? new ObservableCollection<string>(Enumerable.Range(0, 100).Select(i => $"row{i}")), rows, reveal: FadeIn);
        list.ItemsPanel = new FuncTemplate<Panel?>(() => new VirtualizingStackPanel { CacheLength = 2 });
        Show(list);
        list.Scroll!.Offset = new Vector(0, 1000);
        Tick();
        Frames.Advance(300, 16);
        for (var notch = 0; notch < 20 && ItemChanges.For(list).Held.Count == 0; notch++) Wheel(list, -1);
        Assert.NotEmpty(ItemChanges.For(list).Held);
        return list;
    }

    private static void Wheel(ListBox list, double notches)
    {
        ((Window)TopLevel.GetTopLevel(list)!).MouseWheel(new Point(20, 50), new Vector(0, notches));
        Tick();
    }

    private static bool InView(ListBox list, Control row) =>
        TopIn(list, row) is var top && top + Presence.ItemContainerOf(row.GetVisualParent())!.Bounds.Height > 0 && top < list.Scroll!.Viewport.Height;

    private static double TopIn(ListBox list, Control row) =>
        Presence.ItemContainerOf(row.GetVisualParent())!.TranslatePoint(default, (Visual)list.Scroll!)!.Value.Y;
    private static List<Border> ScrollTo(ListBox list, Dictionary<string, Border> rows, double offset)
    {
        var shown = Realized(rows);
        for (var y = list.Scroll!.Offset.Y; y != offset; )
        {
            y = y < offset ? Math.Min(y + 80, offset) : Math.Max(y - 80, offset);
            shown = Realized(rows);
            list.Scroll.Offset = new Vector(0, y);
            Tick();
        }
        return Realized(rows).Except(shown).ToList();
    }

    private static List<Border> Realized(Dictionary<string, Border> rows) =>
        rows.Values.Where(row => row.IsAttachedToVisualTree() && row.DataContext is string).ToList();

    private static ListBox VirtualizedList(ObservableCollection<string> items, Dictionary<string, Border> rows, bool viaArrive = false, AnimationBuilder? reveal = null, bool revealOnce = true, Func<string, double>? height = null) => new()
    {
        Height = 100,
        ItemsSource = items,
        ItemTemplate = new FuncDataTemplate<string>((item, _) =>
        {
            var row = reveal is not null ? Arriving(reveal: reveal) : viaArrive ? Arriving(arrive: FadeIn, exit: FadeOut) : Arriving(initial: FadeIn, enter: FadeIn, exit: FadeOut);
            Presence.SetRevealOnce(row, revealOnce);
            row.Background = Brushes.Red;
            if (item is not null && height is not null) row.Height = height(item);
            if (item is not null) rows[item] = row;
            return row;
        }),
    };

    [AvaloniaFact]
    public void AListInAViewShownAgainLeavesNoGhostsOfTheContainersItReCreates()
    {
        var host = new ContentControl();
        var panel = Show(host);
        var list = new ItemsControl
        {
            ItemsSource = new[] { "a", "b", "c" },
            ItemTemplate = new FuncDataTemplate<string>((_, _) => Arriving(initial: FadeIn, exit: FadeOut)),
        };
        var view = new StackPanel { Children = { list, new Border() } };
        host.Content = view;
        Tick();

        host.Content = null;
        Tick();
        host.Content = view;
        Tick();

        Assert.DoesNotContain(OverlayLayer.GetOverlayLayer(panel)!.Children, child => child is Canvas);
    }

    [AvaloniaFact]
    public void InitialPlaysOnlyWithTheViewAndEnterOnlyOnItsOwn()
    {
        var loaded = Arriving(initial: FadeIn);
        var ignoredOnLoad = Arriving(enter: FadeIn);
        var panel = Show(loaded, ignoredOnLoad);
        var entering = Arriving(enter: FadeIn);
        var ignoredLater = Arriving(initial: FadeIn);

        panel.Children.Add(entering);
        panel.Children.Add(ignoredLater);

        Assert.Equal(0, loaded.Opacity);
        Assert.Equal(1, ignoredOnLoad.Opacity);
        Assert.Equal(0, entering.Opacity);
        Assert.Equal(1, ignoredLater.Opacity);
    }

    [AvaloniaFact]
    public void ItemsAddedInOnePassStaggerByArrivalOrder()
    {
        var staggered = Presto.Build().Opacity(from: 0, to: 1).Duration(100).Delay(Presto.Stagger(100)).Ease("linear");
        var items = new ObservableCollection<string>();
        var rows = new List<Border>();
        var list = new ItemsControl
        {
            ItemsSource = items,
            ItemTemplate = new FuncDataTemplate<string>((_, _) =>
            {
                var row = Arriving(enter: staggered);
                rows.Add(row);
                return row;
            }),
        };
        Show(list);

        items.Add("a");
        items.Add("b");
        items.Add("c");
        Tick();
        Frames.Advance(150, 16);

        Assert.Equal(1, rows[0].Opacity);
        Assert.InRange(rows[1].Opacity, .1, .9);
        Assert.Equal(0, rows[2].Opacity);
    }

    [AvaloniaFact]
    public void AListStaggersInWithTheViewItLoadsWith()
    {
        var staggered = Presto.Build().Opacity(from: 0, to: 1).Duration(100).Delay(Presto.Stagger(100)).Ease("linear");
        var rows = new List<Border>();
        var list = new ItemsControl
        {
            ItemsSource = new[] { "a", "b", "c" },
            ItemTemplate = new FuncDataTemplate<string>((_, _) =>
            {
                var row = Arriving(initial: staggered);
                rows.Add(row);
                return row;
            }),
        };
        Show(list);
        Frames.Advance(150, 16);

        Assert.Equal(1, rows[0].Opacity);
        Assert.InRange(rows[1].Opacity, .1, .9);
        Assert.Equal(0, rows[2].Opacity);
    }

    [AvaloniaFact]
    public void ALeavingElementIsGhostedAsDrawnMidEntranceAndThenReverted()
    {
        var panel = Show(new Border { Height = 30 });
        var row = Arriving(enter: FadeIn, exit: FadeOut);
        row.Background = Brushes.Red;
        panel.Children.Add(row);
        Frames.Advance(100, 16);
        Tick();
        var slot = SlotOf(row);

        panel.Children.Remove(row);
        Tick();

        Assert.Equal(1, row.Opacity);
        Assert.Null(Extent(panel, Colors.Red));
        Assert.True(CountPixels(panel, slot, Colors.White) > 0, "the ghost is not drawn where the row was");
    }

    [AvaloniaFact]
    public void AnElementAddedBackWhileItsGhostExitsDismissesTheGhostAndEnters()
    {
        var panel = Show();
        var row = Arriving(enter: FadeIn, exit: FadeOut);
        row.Background = Brushes.Red;
        panel.Children.Add(row);
        Frames.Advance(250, 16);

        panel.Children.Remove(row);
        Frames.Advance(50, 16);
        panel.Children.Add(row);

        Assert.DoesNotContain(OverlayLayer.GetOverlayLayer(panel)!.Children, child => child is Canvas);
        Assert.Equal(0, row.Opacity);
    }

    [AvaloniaFact]
    public void ABuilderSetOnAnAttachedElementPlaysNothingUntilItsNextArrival()
    {
        var row = new Border { Width = 40, Height = 20 };
        var panel = Show(row);

        Presence.SetEnter(row, FadeIn);

        Assert.Equal(1, row.Opacity);
        Assert.Empty(Presto.GetAnimations(row));

        panel.Children.Remove(row);
        panel.Children.Add(row);

        Assert.Equal(0, row.Opacity);
    }

    [AvaloniaFact]
    public void NothingIsTrackedInDesignMode()
    {
        var isDesignMode = typeof(Design).GetProperty(nameof(Design.IsDesignMode))!; // The setter is internal.
        var panel = Show();
        var row = new Border { Width = 40, Height = 20 };
        isDesignMode.SetValue(null, true);
        try
        {
            Presence.SetEnter(row, FadeIn);
        }
        finally
        {
            isDesignMode.SetValue(null, false);
        }

        panel.Children.Add(row);

        Assert.Equal(1, row.Opacity);
        Assert.Empty(Presto.GetAnimations(row));
    }

    [AvaloniaFact]
    public void ARowRealizedAfterAResetIsInitial()
    {
        var moments = new Dictionary<string, PresenceMoment>();
        var rows = new Dictionary<string, Border>();
        var items = new ResettableItems { "a", "b" };
        var list = new ItemsControl
        {
            ItemsSource = items,
            ItemTemplate = new FuncDataTemplate<string>((item, _) =>
            {
                var row = rows[item] = Arriving(enter: FadeIn);
                row.AttachedToVisualTree += (_, _) => moments[item] = Presence.ClassifyArrival(row);
                return row;
            }),
        };
        Show(list);
        Tick();

        items.Replace("c", "d");
        Tick();

        Assert.Equal(PresenceMoment.Initial, moments["c"]);
        Assert.Equal(PresenceMoment.Initial, moments["d"]);
        Assert.Equal(1, rows["c"].Opacity);
    }

    [AvaloniaFact]
    public void ArrivalsInALaterLayoutPassStartANewBatch()
    {
        var staggered = Presto.Build().Opacity(from: 0, to: 1).Duration(100).Delay(Presto.Stagger(100)).Ease("linear");
        var panel = Show();
        var (first, second) = (Arriving(enter: staggered), Arriving(enter: staggered));

        panel.Children.Add(first);
        Tick();
        panel.Children.Add(second);
        Tick();
        Frames.Advance(50, 10);

        Assert.InRange(second.Opacity, .2, .8);
    }

    [AvaloniaFact]
    public void EveryPresenceAnimationCarriesThePresenceOrigin()
    {
        List<Timer> created = [];
        Presto.Engine.Diagnostics.Enabled = true;
        Presto.Engine.Diagnostics.TimerCreated += created.Add;
        try
        {
            var panel = Show();
            var row = Arriving(enter: FadeIn, exit: FadeOut);
            var gone = Arriving(exit: FadeOut);
            panel.Children.Add(row);
            panel.Children.Add(gone);
            Tick();
            Frames.Advance(300, 16);

            row.IsVisible = false;
            panel.Children.Remove(gone);
            Tick();

            Assert.Equal(3, created.Count);
            Assert.All(created, timer => Assert.Equal("Presence", timer.Origin));
        }
        finally
        {
            Presto.Engine.Diagnostics.TimerCreated -= created.Add;
        }
    }

    private sealed class ResettableItems : ObservableCollection<string>
    {
        public void Replace(params string[] items)
        {
            Items.Clear();
            foreach (var item in items) Items.Add(item);
            OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        }
    }

    [AvaloniaFact]
    public void ClearingEveryLifecycleBuilderRevertsTheArrival()
    {
        var panel = Show();
        var row = Arriving(enter: FadeIn);
        panel.Children.Add(row);

        Presence.SetEnter(row, null);

        Assert.Equal(1, row.Opacity);
    }

    [AvaloniaFact]
    public void ChangingOrClearingOneBuilderWhileOthersStaySetRestartsTheArrivalWithTheNewSet()
    {
        var panel = Show();
        var row = Arriving(enter: FadeIn, exit: FadeOut);
        panel.Children.Add(row);

        Presence.SetEnter(row, Presto.Build().Opacity(from: .5, to: 1).Duration(200).Ease("linear"));

        Assert.Equal(.5, row.Opacity);

        Presence.SetEnter(row, null);

        Assert.Equal(1, row.Opacity);
    }

    [AvaloniaFact]
    public void ASetInitialOrEnterWinsOverArriveForItsOwnMoment()
    {
        var fadeHalf = Presto.Build().Opacity(from: .5, to: 1).Duration(200).Ease("linear");
        var initialOnLoad = Arriving(initial: fadeHalf, arrive: FadeIn);
        var enterOnLoad = Arriving(enter: fadeHalf, arrive: FadeIn);
        var panel = Show(initialOnLoad, enterOnLoad);
        var initialEntering = Arriving(initial: fadeHalf, arrive: FadeIn);
        var enterEntering = Arriving(enter: fadeHalf, arrive: FadeIn);

        panel.Children.Add(initialEntering);
        panel.Children.Add(enterEntering);

        Assert.Equal(.5, initialOnLoad.Opacity);
        Assert.Equal(0, enterOnLoad.Opacity);
        Assert.Equal(0, initialEntering.Opacity);
        Assert.Equal(.5, enterEntering.Opacity);
    }

    [AvaloniaFact]
    public void APageInATreeViewSectionBelongsToTheSection()
    {
        var (tree, sections) = SectionTree();
        var section = (TreeViewItem)tree.ContainerFromItem(sections[0])!;
        var page = (Control)section.ContainerFromIndex(0)!;

        Assert.Same(section, Presence.OwnerOfItem(page.GetVisualParent()));
    }

    [AvaloniaFact]
    public void APageRemovedFromATreeViewSectionExits()
    {
        var (tree, sections) = SectionTree();
        Frames.Advance(300, 16);
        Tick();

        sections[0].Pages.RemoveAt(0);
        Tick();

        Assert.Single(OverlayLayer.GetOverlayLayer(tree)!.Children.OfType<Canvas>());
    }

    private sealed record Section(string Name, ObservableCollection<string> Pages);

    private static (TreeView Tree, Section[] Sections) SectionTree()
    {
        Section[] sections = [new("one", ["a", "b"]), new("two", ["c"])];
        var tree = new TreeView
        {
            ItemsSource = sections,
            ItemTemplate = new FuncTreeDataTemplate<Section>((section, _) => new TextBlock { Text = section.Name }, section => section.Pages),
            Styles =
            {
                new Style(x => x.OfType<TreeViewItem>())
                {
                    Setters = { new Setter(TreeViewItem.IsExpandedProperty, true), new Setter(Presence.ExitProperty, FadeOut) },
                },
            },
        };
        new Window { Content = tree, Background = Brushes.White, Width = 200, Height = 200 }.Show();
        Tick();
        return (tree, sections);
    }

    private static (Control Control, List<PresenceMoment> Moments) Recorded(Control control)
    {
        var moments = new List<PresenceMoment>();
        control.AttachedToVisualTree += (_, _) => moments.Add(Presence.ClassifyArrival(control));
        return (control, moments);
    }

    private static Border Arriving(AnimationBuilder? initial = null, AnimationBuilder? enter = null, AnimationBuilder? exit = null, AnimationBuilder? arrive = null, AnimationBuilder? reveal = null)
    {
        var element = new Border { Width = 40, Height = 20, HorizontalAlignment = HorizontalAlignment.Left };
        Presence.SetInitial(element, initial);
        Presence.SetEnter(element, enter);
        Presence.SetArrive(element, arrive);
        Presence.SetReveal(element, reveal);
        Presence.SetExit(element, exit);
        return element;
    }

    private static StackPanel Show(params Control[] children)
    {
        var panel = new StackPanel();
        panel.Children.AddRange(children);
        panel.Children.Add(new Border { Height = 10 });
        new Window { Content = panel, Background = Brushes.White, Width = 100, Height = 100 }.Show();
        Tick();
        return panel;
    }
}
