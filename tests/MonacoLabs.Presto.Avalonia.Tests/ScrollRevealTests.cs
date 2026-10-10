using System.Collections.ObjectModel;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using MonacoLabs.Presto.Events;

namespace MonacoLabs.Presto.Tests;

/// <summary>
/// <see cref="Presence.RevealProperty"/> outside virtualizing lists (Phase 65a), on <see cref="ScrollStage"/>'s target, which
/// reaches the viewport's bottom at an offset of 100.
/// </summary>
public sealed class ScrollRevealTests : BridgeTestBase
{
    private static AnimationBuilder FadeIn => Presto.Build().Opacity(from: 0, to: 1).Duration(200).Ease("linear");

    [AvaloniaFact]
    public void AnElementInAPlainScrollViewerRevealsWhenScrolledIntoView()
    {
        var (_, scroller, target, _) = Stage();
        Frames.Advance(100, 20);
        Assert.Equal(0, target.Opacity);

        ScrollTo(scroller, 150);
        Frames.Advance(50);
        Assert.InRange(target.Opacity, .01, .99);

        Frames.Advance(200, 20);
        Assert.Equal(1, target.Opacity);
    }

    [AvaloniaFact]
    public void ItWaitsForItsThreshold()
    {
        var (_, scroller, target, _) = Stage(threshold: "bottom-=50 top");

        ScrollTo(scroller, 140);
        Frames.Advance(200, 20);
        Assert.Equal(0, target.Opacity);

        ScrollTo(scroller, 160);
        Frames.Advance(300, 20);
        Assert.Equal(1, target.Opacity);
    }

    [AvaloniaFact]
    public void ItRevealsOnceAndStopsWatching()
    {
        var (_, scroller, target, _) = Stage();
        ScrollTo(scroller, 150);
        Frames.Advance(200, 20);

        ScrollTo(scroller, 0);
        ScrollTo(scroller, 150);

        Assert.Equal(1, target.Opacity);
        Assert.False(ScrollContainer.Containers.ContainsKey(scroller));
    }

    [AvaloniaFact]
    public void WithoutRevealOnceItHidesOnLeavingAndRevealsAgain()
    {
        var (_, scroller, target, _) = Stage(once: false);
        ScrollTo(scroller, 150);
        Frames.Advance(300, 20);

        ScrollTo(scroller, 0);
        Assert.Equal(0, target.Opacity);

        ScrollTo(scroller, 150);
        Frames.Advance(50);
        Assert.InRange(target.Opacity, .01, .99);
    }

    [AvaloniaFact]
    public void ElementsInViewOnAttachRevealAsOneBatchInReadingOrder()
    {
        var rows = Enumerable.Range(0, 6).Select(_ => new Border { Height = 60, Background = Brushes.Red }).ToArray();
        var panel = new StackPanel();
        // Attached bottom up, so reading order is not attach order.
        foreach (var row in rows.Reverse())
        {
            Presence.SetReveal(row, FadeIn.Clone().Delay(Presto.Stagger(50)));
            panel.Children.Insert(0, row);
        }
        Show(new ScrollViewer { Content = panel, Height = 200 });

        Frames.Advance(60, 20);
        Frames.Advance(80);

        Assert.True(rows[0].Opacity > rows[1].Opacity && rows[1].Opacity > rows[2].Opacity && rows[2].Opacity < 1);
        Assert.All(rows[4..], row => Assert.Equal(0, row.Opacity));
        Frames.Advance(400, 20);
        Assert.All(rows[..4], row => Assert.Equal(1, row.Opacity));
        Assert.All(rows[4..], row => Assert.Equal(0, row.Opacity));
    }

    [AvaloniaFact]
    public void AVirtualizingListStillRevealsThroughItsRows()
    {
        var list = new ListBox
        {
            Height = 100,
            ItemsSource = new ObservableCollection<int>(Enumerable.Range(0, 50)),
            ItemTemplate = new FuncDataTemplate<int>((_, _) =>
            {
                var row = new Border { Height = 20, Background = Brushes.Red };
                Presence.SetReveal(row, FadeIn);
                return row;
            }),
        };
        Show(list);
        Frames.Advance(200, 20);

        Assert.All(list.GetVisualDescendants().OfType<Border>().Where(row => Presence.GetReveal(row) is not null), row => Assert.Equal(1, row.Opacity));
        Assert.False(ScrollContainer.Containers.ContainsKey((ScrollViewer)list.Scroll!));
    }

    [AvaloniaFact]
    public void UnderReducedMotionItRevealsWithTheReducedForm()
    {
        var reveal = Presto.Build().Y(from: 40, to: 0).Opacity(from: 0, to: 1).Duration(100).Ease("linear")
            .WhenReduced(reduced => reduced.Opacity(from: 0, to: 1));
        var (_, scroller, target, _) = Stage(reveal);
        Presto.Defaults.ReducedMotion = true;

        ScrollTo(scroller, 150);
        Frames.Advance(50);

        Assert.InRange(target.Opacity, .01, .99);
        Assert.Equal(0, Convert.ToDouble(PrestoAvalonia.Get(target, "Y")));
        Frames.Advance(200, 20);
        Assert.Equal(1, target.Opacity);
    }

    private ScrollStage Stage(AnimationBuilder? reveal = null, string? threshold = null, bool once = true)
    {
        var target = new Border { Background = Brushes.Red };
        Presence.SetReveal(target, reveal ?? FadeIn);
        Presence.SetRevealThreshold(target, threshold);
        Presence.SetRevealOnce(target, once);
        var stage = ScrollStage.Show(target: target);
        Frames.Advance(40, 20);
        return stage;
    }

    // Two frames: the observer crosses on the first and the batch reveals on the next.
    private void ScrollTo(ScrollViewer scroller, double offset)
    {
        scroller.Offset = new Avalonia.Vector(0, offset);
        Dispatcher.UIThread.RunJobs();
        Frames.Advance(40, 20);
    }
}
