using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Headless.XUnit;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;

namespace Monaco.Presto.AvaloniaBridge.Tests;

/// <summary>
/// <see cref="Presence.ModeProperty"/> (GAPS-PLAN §7): under <see cref="PresenceMode.Wait"/> an arrival is held at opacity 0,
/// its entrance not played, until the exits in flight under its host have ended.
/// </summary>
public sealed class PresenceModeTests : CompositorTestBase
{
    private static readonly AnimationBuilder FadeOut = Presto.Build().Opacity(to: 0).Duration(200).Ease("linear");

    [AvaloniaTheory]
    [InlineData(PresenceMode.Sync)]
    [InlineData(PresenceMode.PopLayout)]
    public void WithoutWaitTheEnterPlaysWhileTheGhostExits(PresenceMode mode)
    {
        var host = Show(mode, Content());
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
