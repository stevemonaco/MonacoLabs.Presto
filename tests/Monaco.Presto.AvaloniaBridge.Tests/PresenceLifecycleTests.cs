using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Headless.XUnit;
using Avalonia.Layout;
using Avalonia.Media;

namespace Monaco.Presto.AvaloniaBridge.Tests;

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
    public void ClearingEveryLifecycleBuilderRevertsTheArrival()
    {
        var panel = Show();
        var row = Arriving(enter: FadeIn);
        panel.Children.Add(row);

        Presence.SetEnter(row, null);

        Assert.Equal(1, row.Opacity);
    }

    private static (Control Control, List<PresenceMoment> Moments) Recorded(Control control)
    {
        var moments = new List<PresenceMoment>();
        control.AttachedToVisualTree += (_, _) => moments.Add(Presence.ClassifyArrival(control));
        return (control, moments);
    }

    private static Border Arriving(AnimationBuilder? initial = null, AnimationBuilder? enter = null, AnimationBuilder? exit = null)
    {
        var element = new Border { Width = 40, Height = 20, HorizontalAlignment = HorizontalAlignment.Left };
        Presence.SetInitial(element, initial);
        Presence.SetEnter(element, enter);
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
