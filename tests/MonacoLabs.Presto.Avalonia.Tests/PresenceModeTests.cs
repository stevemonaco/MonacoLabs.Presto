using System.Collections.ObjectModel;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Headless.XUnit;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;

namespace MonacoLabs.Presto.Tests;

/// <summary>
/// <see cref="Presence.ModeProperty"/> (GAPS-PLAN §7): under <see cref="PresenceMode.Wait"/> an arrival is held at opacity 0,
/// its entrance not played, until the exits in flight under its host have ended.
/// </summary>
public sealed class PresenceModeTests : CompositorTestBase
{
    private static readonly AnimationBuilder FadeOut = Presto.Build().Opacity(to: 0).Duration(200).Ease("linear");

    [AvaloniaFact]
    public void UnderSyncTheEnterPlaysWhileTheGhostExits()
    {
        var host = Show(PresenceMode.Sync, Content());
        var began = 0;
        var incoming = Content(() => began++);

        host.Content = incoming;
        Tick();
        Frames.Advance(100, 16);

        Assert.Equal(1, began);
        Assert.InRange(incoming.Opacity, .1, .9);
        Assert.Single(Ghosts(host));
    }

    [AvaloniaFact]
    public void UnderWaitTheNewContentEntersOnlyOnceTheExitEnds()
    {
        var host = Show(PresenceMode.Wait, Content());
        var began = 0;
        var incoming = Content(() => began++);

        host.Content = incoming;
        Tick();
        Frames.Advance(150, 16);
        Assert.Equal(0, began);
        Assert.Equal(0, incoming.Opacity);

        RunUntil(() => Ghosts(host).Count == 0);
        Frames.Advance(16);
        Assert.Equal(1, began);
        Frames.Advance(300, 16);

        Assert.Equal(1, incoming.Opacity);
        Assert.Equal(0d, X(incoming));
    }

    [AvaloniaFact]
    public void NewContentWithoutABuilderAppearsWhenTheExitEnds()
    {
        var host = Show(PresenceMode.Wait, Content());
        var incoming = new Border { Width = 40, Height = 20, Background = Brushes.Blue };

        host.Content = incoming;
        Tick();
        Assert.Equal(0, incoming.Opacity);

        RunUntil(() => Ghosts(host).Count == 0);

        Assert.Equal(1, incoming.Opacity);
    }

    [AvaloniaFact]
    public void AThirdContentWaitsForTheFirstExitAndTheHeldSecondLeavesNoGhost()
    {
        var host = Show(PresenceMode.Wait, Content());
        var secondBegan = 0;
        var thirdBegan = 0;
        var second = Content(() => secondBegan++);
        var third = Content(() => thirdBegan++);

        host.Content = second;
        Tick();
        Frames.Advance(50, 16);
        host.Content = third;
        Tick();

        Assert.Single(Ghosts(host));
        Assert.Equal(0, third.Opacity);

        RunUntil(() => Ghosts(host).Count == 0);
        Frames.Advance(300, 16);
        Tick();

        Assert.Equal(0, secondBegan);
        Assert.Equal(1, thirdBegan);
        Assert.False(second.IsAnimating(Visual.OpacityProperty));
        Assert.Equal(1, second.Opacity);
        Assert.Equal(1, third.Opacity);
    }

    [AvaloniaFact]
    public void AWaitingListHoldsAnAddedRowUntilTheRemovedRowsGhostEnds()
    {
        var items = new ObservableCollection<string> { "a", "b" };
        var rows = new Dictionary<string, Border>();
        var list = new ItemsControl
        {
            ItemsSource = items,
            ItemTemplate = new FuncDataTemplate<string>((item, _) => rows[item] = Content()),
        };
        Presence.SetMode(list, PresenceMode.Wait);
        ShowWhite(list);

        items.Remove("b");
        items.Add("c");
        Tick();
        Frames.Advance(150, 16);
        Assert.Equal(0, rows["c"].Opacity);

        RunUntil(() => Ghosts(list).Count == 0);
        Frames.Advance(100, 16);

        Assert.InRange(rows["c"].Opacity, .1, .9);
    }

    [AvaloniaFact]
    public void ClearingTheModeReleasesTheHold()
    {
        var host = Show(PresenceMode.Wait, Content());
        var incoming = Content();
        host.Content = incoming;
        Tick();

        host.ClearValue(Presence.ModeProperty);
        Frames.Advance(100, 16);

        Assert.InRange(incoming.Opacity, .1, .9);
        Assert.Single(Ghosts(host));
    }

    [AvaloniaFact]
    public void UnderReduceAllTheWaitEndsWithTheInstantExit()
    {
        Presto.Defaults.ReduceAll = true;
        var host = Show(PresenceMode.Wait, Content());
        var incoming = Content();

        host.Content = incoming;
        Tick();
        Frames.Advance(16);

        Assert.Empty(Ghosts(host));
        Assert.Equal(1, incoming.Opacity);
    }

    [AvaloniaFact]
    public void UnderReduceAllAShowUnderAWaitHostLandsInTheSameCall()
    {
        Presto.Defaults.ReduceAll = true;
        var shown = Content();
        shown.IsVisible = false;
        WaitPanel(shown);

        shown.IsVisible = true;

        Assert.Equal(1, shown.Opacity);
        Assert.Equal(0d, X(shown));
    }

    [AvaloniaFact]
    public void AnExitFromAStyleSetterGhostsTheSwappedContentAndStartsTheWait()
    {
        var step = new FuncDataTemplate<object?>((_, _) => new Border { Width = 40, Height = 20, Background = Brushes.Red, Classes = { "step" } });
        var host = new ContentControl { Content = step.Build(null) };
        Presence.SetMode(host, PresenceMode.Wait);
        var page = new Border
        {
            Child = host,
            Styles = { new Style(x => x.OfType<Border>().Class("step")) { Setters = { new Setter(Presence.ExitProperty, FadeOut) } } },
        };
        ShowWhite(page);
        var incoming = step.Build(null)!;

        host.Content = incoming;
        Tick();

        Assert.Single(Ghosts(host));
        Assert.Equal(0, incoming.Opacity);
    }

    [AvaloniaTheory]
    [InlineData(true)]
    [InlineData(false)]
    public void AnEmptyLineShownByCountFadesInOnlyOnceTheLastRowsGhostEnds(bool countFirst)
    {
        var items = new ObservableCollection<string> { "a", "b" };
        var began = 0;
        var empty = new TextBlock { Text = "Nothing", IsVisible = false };
        Presence.SetEnter(empty, Presto.Build().Opacity(from: 0, to: 1).Duration(200).Ease("linear").OnBegin(_ => began++));
        if (countFirst)
        {
            ((INotifyPropertyChanged)items).PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(items.Count)) empty.IsVisible = items.Count == 0;
            };
        }
        var host = WaitPanel(new ItemsControl { ItemsSource = items, ItemTemplate = new FuncDataTemplate<string>((_, _) => Content()) }, empty);

        items.Clear();
        if (!countFirst) empty.IsVisible = true;
        Tick();
        Frames.Advance(150, 16);
        Assert.Equal(2, Ghosts(host).Count);
        Assert.Equal(0, began);
        Assert.Equal(0, empty.Opacity);

        RunUntil(() => Ghosts(host).Count == 0);
        Frames.Advance(100, 16);

        Assert.Equal(1, began);
        Assert.InRange(empty.Opacity, .1, .9);
    }

    [AvaloniaFact]
    public void AShowUnderAWaitHostWithNothingExitingPlaysInTheSameTurnBeforeAFrame()
    {
        var shown = Content();
        shown.IsVisible = false;
        WaitPanel(shown);

        shown.IsVisible = true;
        Assert.Empty(Presto.GetAnimations(shown));
        Dispatcher.UIThread.RunJobs();

        Assert.Single(Presto.GetAnimations(shown));
        Assert.Equal(0, shown.Opacity);
        Assert.Equal(30d, X(shown));
    }

    [AvaloniaFact]
    public void AShowWhileAWaitRunsIsHeldAndReleasedWithTheArrivals()
    {
        var items = new ObservableCollection<string> { "a", "b" };
        var rows = new Dictionary<string, Border>();
        var shownBegan = 0;
        var shown = Content(() => shownBegan++);
        shown.IsVisible = false;
        var host = WaitPanel(new ItemsControl { ItemsSource = items, ItemTemplate = new FuncDataTemplate<string>((item, _) => rows[item] = Content()) }, shown);

        items.Remove("b");
        Tick();
        Frames.Advance(50, 16);
        items.Add("c");
        shown.IsVisible = true;
        Tick();
        Frames.Advance(50, 16);
        Assert.Equal(0, shownBegan);
        Assert.Equal(0, shown.Opacity);
        Assert.Equal(0, rows["c"].Opacity);

        RunUntil(() => Ghosts(host).Count == 0);
        Frames.Advance(100, 16);

        Assert.Equal(1, shownBegan);
        Assert.InRange(shown.Opacity, .1, .9);
        Assert.InRange(rows["c"].Opacity, .1, .9);
    }

    [AvaloniaFact]
    public void AHeldHideUnderAWaitHostStartsNoWait()
    {
        var hidden = Content();
        var host = WaitPanel(hidden);

        hidden.IsVisible = false;
        Tick();
        var incoming = Content();
        host.Children.Add(incoming);
        Tick();
        Frames.Advance(100, 16);

        Assert.Empty(Ghosts(host));
        Assert.True(hidden.IsVisible);
        Assert.InRange(incoming.Opacity, .1, .9);
    }

    private static StackPanel WaitPanel(params Control[] children)
    {
        var host = new StackPanel();
        host.Children.AddRange(children);
        Presence.SetMode(host, PresenceMode.Wait);
        ShowWhite(host);
        return host;
    }

    [AvaloniaFact]
    public void WithoutAModeHostTheEnterPlaysWhileTheGhostExits()
    {
        var host = new ContentControl { Content = Content() };
        ShowWhite(host);
        var incoming = Content();

        host.Content = incoming;
        Tick();
        Frames.Advance(100, 16);

        Assert.Equal(PresenceMode.Sync, Presence.GetMode(host));
        Assert.InRange(incoming.Opacity, .1, .9);
        Assert.Single(Ghosts(host));
    }

    [AvaloniaFact]
    public void AHeldHideAndAnImperativeExitStartNoWait()
    {
        var hidden = Content();
        var removed = new Border { Width = 40, Height = 20, Background = Brushes.Blue };
        var panel = new StackPanel { Children = { hidden, removed } };
        Presence.SetMode(panel, PresenceMode.Wait);
        ShowWhite(panel);

        hidden.IsVisible = false;
        Assert.NotNull(PrestoAvalonia.AnimateExit(removed, FadeOut));
        panel.Children.Remove(removed);
        var incoming = Content();
        panel.Children.Add(incoming);
        Tick();
        Frames.Advance(100, 16);

        Assert.True(hidden.IsVisible);
        Assert.Single(Ghosts(panel));
        Assert.InRange(incoming.Opacity, .1, .9);
    }

    private static Border Content(Action? began = null)
    {
        var content = new Border { Width = 40, Height = 20, Background = Brushes.Red, HorizontalAlignment = HorizontalAlignment.Left };
        Presence.SetEnter(content, Presto.Build().Opacity(from: 0, to: 1).X(from: 30).Duration(200).Ease("linear").OnBegin(_ => began?.Invoke()));
        Presence.SetExit(content, FadeOut);
        return content;
    }

    private static ContentControl Show(PresenceMode mode, Control content)
    {
        var host = new ContentControl { Content = content };
        Presence.SetMode(host, mode);
        ShowWhite(host);
        return host;
    }
}
