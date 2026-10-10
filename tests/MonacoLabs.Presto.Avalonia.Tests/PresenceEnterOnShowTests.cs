using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.VisualTree;

namespace MonacoLabs.Presto.Tests;

/// <summary>
/// <see cref="Presence.EnterOnShowProperty"/> (GAPS-2-PLAN §3.2, "Revealed by an ancestor"): showing a host replays the
/// entrances of the descendants it reveals as one batch.
/// </summary>
public sealed class PresenceEnterOnShowTests : CompositorTestBase
{
    private static AnimationBuilder Staggered =>
        Presto.Build().Opacity(from: 0).X(from: 40).Duration(100).Delay(Presto.Stagger(100)).Ease("linear")
            .WhenReduced(reduced => reduced.Opacity(from: 0));

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void ShowingTheHostStaggersItsRevealedChildrenInOrder(bool reduced)
    {
        Presto.Defaults.ReducedMotion = reduced;
        var (host, rows) = ShowHost(enterOnShow: true, Staggered);

        host.IsVisible = true;
        Tick();
        Frames.Advance(50, 10);

        Assert.InRange(rows[0].Opacity, .3, .7);
        Assert.InRange(X(rows[0]), reduced ? 0 : 12, reduced ? 0 : 28);
        Assert.Equal([0d, 0d], [rows[1].Opacity, rows[2].Opacity]);

        Frames.Advance(100, 10);

        Assert.Equal(1, rows[0].Opacity);
        Assert.InRange(rows[1].Opacity, .3, .7);
        Assert.Equal(0, rows[2].Opacity);
    }

    [AvaloniaFact]
    public void WithoutEnterOnShowNothingReplays()
    {
        var entered = 0;
        var (host, rows) = ShowHost(enterOnShow: false, Staggered.OnBegin(_ => entered++));

        host.IsVisible = true;
        Tick();
        Frames.Advance(50, 10);

        Assert.Equal(0, entered);
        Assert.All(rows, row => Assert.Equal(1, row.Opacity));
    }

    [AvaloniaFact]
    public void ExpandingAnExpanderReplaysItsContentsEntrances()
    {
        var rows = Rows(Staggered);
        var expander = new Expander { Header = "h", Content = new StackPanel { Children = { rows[0], rows[1], rows[2] } } };
        expander.Styles.Add(new Style(x => x.OfType<Expander>().Template().OfType<Border>().Name("ExpanderContent"))
        {
            Setters = { new Setter(Presence.EnterOnShowProperty, true) },
        });
        ShowWhite(expander);
        Frames.Advance(300, 10);

        expander.IsExpanded = true;
        Tick();
        Frames.Advance(50, 10);

        Assert.InRange(rows[0].Opacity, .3, .7);
        Assert.Equal(0, rows[2].Opacity);
    }

    // A class selector applies the setter before the template binding hides the part, which raises no change of its own.
    [AvaloniaFact]
    public void TheFirstExpandOfANeverOpenedExpanderReplaysTheRowsThatAttachInThatPass()
    {
        var rows = Rows(Staggered);
        var expander = new Expander { Header = "h", Classes = { "settings" }, Content = new StackPanel { Children = { rows[0], rows[1], rows[2] } } };
        var page = new UserControl { Content = expander };
        page.Styles.Add(new Style(x => x.OfType<Expander>().Class("settings").Template().OfType<Border>().Name("ExpanderContent"))
        {
            Setters = { new Setter(Presence.EnterOnShowProperty, true) },
        });
        ShowWhite(page);
        Frames.Advance(300, 10);
        Assert.False(rows[0].IsAttachedToVisualTree());

        expander.IsExpanded = true;
        Tick();
        Frames.Advance(50, 10);

        Assert.InRange(rows[0].Opacity, .3, .7);
        Assert.Equal(0, rows[2].Opacity);
    }

    [AvaloniaFact]
    public void HidingTheHostPlaysOnlyItsOwnExit()
    {
        var (exited, childExited, entered) = (0, 0, 0);
        var (host, rows) = ShowHost(enterOnShow: true, Staggered.OnBegin(_ => entered++));
        host.IsVisible = true;
        Tick();
        Frames.Advance(400, 10);
        entered = 0;
        Presence.SetExit(host, Presto.Build().Opacity(to: 0).Duration(100).OnBegin(_ => exited++));
        foreach (var row in rows) Presence.SetExit(row, Presto.Build().Opacity(to: 0).Duration(100).OnBegin(_ => childExited++));

        host.IsVisible = false;
        RunUntil(() => !host.IsVisible);

        Assert.Equal((1, 0, 0), (exited, childExited, entered));
    }

    [AvaloniaFact]
    public void AHiddenChildIsSkippedAndOneUnderANestedHostShownWithItEntersOnce()
    {
        var entered = new List<string>();
        Border Row(string name)
        {
            var row = new Border { Width = 40, Height = 20, Background = Brushes.Red };
            Presence.SetEnter(row, Presto.Build().Opacity(from: 0).Duration(100).OnBegin(_ => entered.Add(name)));
            return row;
        }
        var inner = new StackPanel { IsVisible = false, Children = { Row("nested") } };
        var outer = new StackPanel { IsVisible = false, Children = { Row("first"), Row("hidden"), inner, Row("last") } };
        outer.Children[1].IsVisible = false;
        Presence.SetEnterOnShow(inner, true);
        Presence.SetEnterOnShow(outer, true);
        ShowWhite(outer);

        inner.IsVisible = true;
        outer.IsVisible = true;
        Tick();
        Frames.Advance(16);

        Assert.Equal(["first", "nested", "last"], entered);
    }

    [AvaloniaFact]
    public void UnderReduceAllRevealedChildrenLandAtOnce()
    {
        Presto.Defaults.ReduceAll = true;
        var (host, rows) = ShowHost(enterOnShow: true, Staggered);

        host.IsVisible = true;
        Tick();
        Frames.Advance(16);

        Assert.All(rows, row => Assert.Equal((1d, 0d), (row.Opacity, X(row))));
    }

    private static Border[] Rows(AnimationBuilder enter)
    {
        var rows = new Border[3];
        for (var i = 0; i < rows.Length; i++)
        {
            rows[i] = new Border { Width = 40, Height = 20, Background = Brushes.Red };
            Presence.SetEnter(rows[i], enter);
        }
        return rows;
    }

    private static (Control Host, Border[] Rows) ShowHost(bool enterOnShow, AnimationBuilder enter)
    {
        var rows = Rows(enter);
        var host = new StackPanel { IsVisible = false, Children = { rows[0], rows[1], rows[2] } };
        Presence.SetEnterOnShow(host, enterOnShow);
        ShowWhite(host);
        return (host, rows);
    }
}
