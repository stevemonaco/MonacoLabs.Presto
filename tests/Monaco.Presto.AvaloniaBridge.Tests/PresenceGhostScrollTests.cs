using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace Monaco.Presto.AvaloniaBridge.Tests;

/// <summary>A Presence exit ghost follows its anchor and its clipping ancestors when they scroll mid-exit (PLAN Phase 67).</summary>
public sealed class PresenceGhostScrollTests : BridgeTestBase
{
    private const double Scrolled = 60;

    private static readonly AnimationBuilder Long = Presto.Build().Opacity(to: 0).Duration(2000).Ease("linear");

    private static Rect WindowRect(Visual visual, Window window) => new(visual.TranslatePoint(default, window)!.Value, visual.Bounds.Size);

    private static void AssertRect(Rect expected, Rect actual)
    {
        // The overlay rounds layout to whole pixels and the scrolled content does not.
        Assert.Equal(expected.X, actual.X, 1d);
        Assert.Equal(expected.Y, actual.Y, 1d);
        Assert.Equal(expected.Width, actual.Width, 1d);
        Assert.Equal(expected.Height, actual.Height, 1d);
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void ScrollingMidExitMovesTheGhostWithItsAnchorAndReclipsIt(bool pieces)
    {
        Control element = pieces ? TextRevealTests.Block(TextPieces.Words) : Box();
        Presence.SetExit(element, Long);
        var anchor = new Panel { Children = { element } };
        var card = new Border { Width = 240, Height = 120, ClipToBounds = true, Child = anchor };
        var scroll = new ScrollViewer { Content = new StackPanel { Children = { new Border { Height = 40 }, card, new Border { Height = 400 } } } };
        var window = new Window { Content = scroll, Width = 260, Height = 200 };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        anchor.Children.Remove(element);
        Dispatcher.UIThread.RunJobs();
        var box = Assert.IsType<Canvas>(Assert.Single(Assert.Single(OverlayLayer.GetOverlayLayer(window)!.Children.OfType<Canvas>()).Children));
        var ghosts = box.GetVisualDescendants().OfType<Image>().ToArray();
        Assert.Equal(pieces, ghosts.Length > 1);
        var before = ghosts.Select(ghost => ghost.TranslatePoint(default, window)!.Value).ToArray();
        Frames.Advance(500, 50);

        scroll.Offset = new Vector(0, Scrolled);
        Dispatcher.UIThread.RunJobs();
        Frames.Advance(100, 50);

        Assert.True(ghosts[0].Opacity is > 0 and < 1);
        for (var i = 0; i < ghosts.Length; i++)
        {
            var at = ghosts[i].TranslatePoint(default, window)!.Value;
            Assert.Equal(before[i].X, at.X, 1d);
            Assert.Equal(before[i].Y - Scrolled, at.Y, 1d);
        }
        var viewport = WindowRect(scroll.GetVisualDescendants().OfType<ScrollContentPresenter>().Single(), window);
        AssertRect(WindowRect(card, window).Intersect(viewport), WindowRect(box, window));
    }
}
