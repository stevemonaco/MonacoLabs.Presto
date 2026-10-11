using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.VisualTree;
using Tempo.ViewModels;

namespace Tempo.Tests;

public sealed class CompactLayoutTests : TempoTestBase
{
    public static TheoryData<int, bool> Widths => new() { { 320, false }, { 320, true }, { 360, false }, { 360, true }, { 719, false }, { 719, true } };

    [AvaloniaTheory]
    [MemberData(nameof(Widths))]
    public async Task EveryPageFitsTheWidthWithTheFocusControlsOnOneRow(int width, bool reduced)
    {
        var (window, vm) = await ShowSeededAsync(reduced, new Size(width, 640));
        var host = Find<TransitioningContentControl>(window, "PageHost");

        foreach (var view in new[] { AppView.Tasks, AppView.Focus, AppView.Stats })
        {
            vm.CurrentView = view;
            TestFrames.Tick(90);
            var over = Overflowing(host).ToList();
            Assert.True(over.Count == 0, $"{view}: {string.Join(", ", over)}");
            if (view == AppView.Focus)
            {
                var centers = window.GetVisualDescendants().OfType<StackPanel>().Single(s => s.Classes.Contains("controls")).Children
                    .Select(c => c.TranslatePoint(new Point(0, c.Bounds.Height / 2), host)!.Value.Y);
                Assert.Single(centers.Distinct());
            }
        }
        window.Close();
        await vm.FlushAsync();
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CompactTagChipsScrollSidewaysWithThePillOnTheChosenChip(bool reduced)
    {
        var (window, vm) = await ShowSeededAsync(reduced, new Size(320, 640));
        vm.CurrentView = AppView.Tasks;
        TestFrames.Tick(60);
        var chips = Find<ListBox>(window, "TagChips");
        var scroll = chips.GetVisualDescendants().OfType<ScrollViewer>().First();
        Assert.True(scroll.Extent.Width > scroll.Viewport.Width);

        vm.TagFilterIndex = MainViewModel.TagFilters.Count - 1;
        TestFrames.Tick(60);
        var chosen = chips.ContainerFromIndex(vm.TagFilterIndex)!;
        var pill = chips.GetVisualDescendants().OfType<Border>().Single(b => b.Classes.Contains("segment-pill"));
        Assert.Equal(CenterX(chosen, chips), CenterX(pill, chips), 1);
        Assert.InRange(CenterX(chosen, chips), 0, chips.Bounds.Width);
        window.Close();
        await vm.FlushAsync();
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task WideStatsFitsAndScrollsToItsLastCard(bool reduced)
    {
        var (window, vm) = await ShowSeededAsync(reduced, new Size(960, 640));
        vm.CurrentView = AppView.Stats;
        TestFrames.Tick(90);
        var scroll = Find<ScrollViewer>(window, "Scroll");
        var last = Find<TextBlock>(window, "BestDay").FindAncestorOfType<Border>()!;
        Assert.Empty(Overflowing(Find<TransitioningContentControl>(window, "PageHost")));
        Assert.True(scroll.Extent.Height > scroll.Viewport.Height);

        scroll.ScrollToEnd();
        TestFrames.Tick(5);
        Assert.InRange(last.TranslatePoint(new Point(0, last.Bounds.Height), scroll)!.Value.Y, 0, scroll.Viewport.Height + .5);
        window.Close();
        await vm.FlushAsync();
    }

    [AvaloniaTheory]
    [InlineData(685, 604)]
    [InlineData(360, 520)]
    public async Task AShortCompactPageShrinksTheDialBetweenTheModeAndTheControls(int width, int height)
    {
        var (window, vm) = await ShowSeededAsync(reduced: true, new Size(width, height));
        vm.CurrentView = AppView.Focus;
        TestFrames.Tick(30);
        var host = Find<TransitioningContentControl>(window, "PageHost");
        var dial = window.GetVisualDescendants().OfType<Panel>().Single(p => p.Name == "Dial");
        var mode = window.GetVisualDescendants().OfType<TransitioningContentControl>().Single(t => t.Classes.Contains("mode"));
        var controls = window.GetVisualDescendants().OfType<StackPanel>().Single(s => s.Classes.Contains("controls"));
        double Top(Visual v) => v.TranslatePoint(default, host)!.Value.Y;

        Assert.InRange(dial.Bounds.Height, 100, 299);
        Assert.True(Top(dial) >= Top(mode) + mode.Bounds.Height, "The dial overlaps the mode label.");
        Assert.True(Top(dial) + dial.Bounds.Height <= Top(controls), "The dial overlaps the controls.");
        window.Close();
        await vm.FlushAsync();
    }

    private static double CenterX(Visual visual, Visual to) => visual.TranslatePoint(new Point(visual.Bounds.Width / 2, 0), to)!.Value.X;

    // Within its card, or the page outside one; a horizontally scrolling list, an edge scroller's chrome and a card-wide scroll shadow run past by design, and untrimmed text must fit its block (a digit's glyph overhangs its 36 DIP slot by a pixel).
    private static IEnumerable<string> Overflowing(Control page) =>
        from c in page.GetVisualDescendants().OfType<Control>()
        where c.IsEffectivelyVisible && !c.GetVisualAncestors().OfType<ScrollContentPresenter>().Any(p => p.CanHorizontallyScroll) && !IsEdgeScrollChrome(c) && !c.Classes.Contains("scroll-shadow")
        let within = c.GetVisualAncestors().OfType<Border>().FirstOrDefault(b => b.Classes.Contains("card")) ?? page
        let box = new Rect(c.Bounds.Size).TransformToAABB(c.TransformToVisual(within)!.Value)
        where box.Left < -.5 || box.Right > within.Bounds.Width + .5
            || c is TextBlock { TextTrimming: var trimming } text && trimming == TextTrimming.None && text.TextLayout.Width > text.Bounds.Width + 2
        select $"{c.GetType().Name}#{c.Name}.{string.Join('.', c.Classes)} {box}";

    // The scroller and its template up to the presenter reach the window edge; the content inside the presenter is still checked.
    private static bool IsEdgeScrollChrome(Control control)
    {
        foreach (var visual in control.GetSelfAndVisualAncestors())
        {
            if (visual is ScrollContentPresenter && visual != control)
                return false;
            if (visual is Control { Classes: var classes } && classes.Contains("edge-scroll"))
                return true;
        }
        return false;
    }
}