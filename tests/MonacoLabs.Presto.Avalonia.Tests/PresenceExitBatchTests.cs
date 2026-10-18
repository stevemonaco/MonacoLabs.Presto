using System.Collections.ObjectModel;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace MonacoLabs.Presto.Tests;

/// <summary>
/// Exits under one panel in one dispatcher turn form a batch whose ghosts start together from a Send-priority job,
/// indexed by where they stand, so staggers know the total.
/// </summary>
public sealed class PresenceExitBatchTests : BridgeTestBase
{
    private static readonly string[] Five = ["a", "b", "c", "d", "e"];

    private static AnimationBuilder FadeOut(TweenValue delay) => Presto.Build().Opacity(to: 0).Duration(100).Ease("linear").Delay(delay);

    private static ItemsControl List(ObservableCollection<string> items, AnimationBuilder exit, ITemplate<Panel?>? panel = null)
    {
        var list = new ItemsControl
        {
            ItemsSource = items,
            ItemTemplate = new FuncDataTemplate<string>((_, _) =>
            {
                var row = new Border { Height = 20, Background = Brushes.Red };
                Presence.SetExit(row, exit);
                return row;
            }),
        };
        if (panel is not null) list.ItemsPanel = panel;
        return list;
    }

    private static Image[] Ghosts(Window window) =>
        OverlayLayer.GetOverlayLayer(window)!.Children.OfType<Canvas>().SelectMany(host => host.GetVisualDescendants().OfType<Image>()).ToArray();

    private static Dictionary<object, double> Delays(Window window) =>
        Ghosts(window).ToDictionary(ghost => ghost.DataContext!, ghost => Assert.Single(Presto.GetAnimations(ghost)).Delay);

    [AvaloniaFact]
    public void ClearingAListBottomUpStaggersTheGhostsTopRowFirst()
    {
        var items = new ObservableCollection<string>(Five);
        var window = Show(List(items, FadeOut(Presto.Stagger(50))));

        for (var i = items.Count - 1; i >= 0; i--) items.RemoveAt(i);
        Dispatcher.UIThread.RunJobs();

        var delays = Delays(window);
        for (var i = 0; i < Five.Length; i++) Assert.Equal(50 * i, delays[Five[i]]);
    }

    [AvaloniaFact]
    public void AStaggerFromLastDelaysTheTopGhostMost()
    {
        var items = new ObservableCollection<string>(Five);
        var window = Show(List(items, FadeOut(Presto.Stagger(50, new() { From = StaggerFrom.Last }))));

        items.Clear();
        Dispatcher.UIThread.RunJobs();

        var delays = Delays(window);
        Assert.Equal(200, delays["a"]);
        Assert.Equal(0, delays["e"]);
    }

    [AvaloniaFact]
    public void AGridStaggerOverAClearedUniformGridDelaysByCellDistance()
    {
        var items = new ObservableCollection<string>(Enumerable.Range(0, 9).Select(i => $"{i}"));
        var grid = new FuncTemplate<Panel?>(() => new UniformGrid { Columns = 3 });
        var window = Show(List(items, FadeOut(Presto.Stagger(50, new() { Grid = [3, 3] })), grid));

        items.Clear();
        Dispatcher.UIThread.RunJobs();

        var delays = Delays(window);
        for (var i = 0; i < 9; i++) Assert.Equal(50 * Math.Sqrt(((i % 3) * (i % 3)) + ((i / 3) * (i / 3))), delays[$"{i}"], tolerance: 0.5);
    }

    [AvaloniaFact]
    public void TwoListsClearedInOneJobStaggerIndependently()
    {
        var first = new ObservableCollection<string>(["a", "b", "c"]);
        var second = new ObservableCollection<string>(["x", "y", "z"]);
        var exit = FadeOut(Presto.Stagger(50));
        var window = Show(new StackPanel { Children = { List(first, exit), List(second, exit) } });

        first.Clear();
        second.Clear();
        Dispatcher.UIThread.RunJobs();

        var delays = Delays(window);
        Assert.Equal([0, 50, 100], new[] { delays["a"], delays["b"], delays["c"] });
        Assert.Equal([0, 50, 100], new[] { delays["x"], delays["y"], delays["z"] });
    }

    [AvaloniaFact]
    public void ASingleRemovalPlaysAtIndexZeroOfOne()
    {
        var items = new ObservableCollection<string>(Five);
        (int Index, int Total)? seen = null;
        var window = Show(List(items, FadeOut(new FunctionValue((_, index, total) =>
        {
            seen = (index, total);
            return 0;
        }))));

        items.Remove("c");
        Dispatcher.UIThread.RunJobs();

        Assert.Single(Ghosts(window));
        Assert.Equal((0, 1), seen);
    }

    [AvaloniaFact]
    public void AnElementRemovedAndInsertedAgainInOneJobLeavesNoGhostAndTheOthersKeepTheirIndices()
    {
        var seen = new Dictionary<object, (int Index, int Total)>();
        var exit = FadeOut(new FunctionValue((target, index, total) =>
        {
            seen[((Image)target).DataContext!] = (index, total);
            return 0;
        }));
        var panel = new StackPanel();
        foreach (var name in Five)
        {
            var row = new Border { Height = 20, Background = Brushes.Red, DataContext = name };
            Presence.SetExit(row, exit);
            panel.Children.Add(row);
        }
        var window = Show(panel);
        var d = panel.Children[3];

        panel.Children.RemoveRange(1, 3);
        Assert.Equal(3, Ghosts(window).Length);
        panel.Children.Insert(1, d);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(["b", "c"], Ghosts(window).Select(ghost => ghost.DataContext).Order());
        Assert.Equal((0, 2), seen["b"]);
        Assert.Equal((1, 2), seen["c"]);
        Assert.False(seen.ContainsKey("d"));
    }

    [AvaloniaFact]
    public void AGhostStandsInTheOverlayUntilTheJobStartsItsExitBeforeAnyLayoutPass()
    {
        var items = new ObservableCollection<string>(Five);
        var window = Show(List(items, FadeOut(0)));
        var laidOutUnstarted = false;
        window.LayoutUpdated += (_, _) => laidOutUnstarted |= Ghosts(window).Any(ghost => Presto.GetAnimations(ghost).Count == 0);

        items.Remove("c");
        var ghost = Assert.Single(Ghosts(window));
        Assert.Empty(Presto.GetAnimations(ghost));
        Dispatcher.UIThread.RunJobs();

        Assert.Single(Presto.GetAnimations(ghost));
        Assert.False(laidOutUnstarted);
    }

    [AvaloniaFact]
    public void UnderReduceAllAClearLeavesNoGhostInTheSameCall()
    {
        var items = new ObservableCollection<string>(Five);
        var window = Show(List(items, FadeOut(Presto.Stagger(50))));
        Presto.Defaults.ReduceAll = true;

        items.Clear();

        Assert.Empty(Ghosts(window));
    }
}
