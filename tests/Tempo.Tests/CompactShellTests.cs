using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using Tempo.ViewModels;
using Tempo.Views;

namespace Tempo.Tests;

public sealed class CompactShellTests : TempoTestBase
{
    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CompactShowsTheBottomBarAndEachTabSwapsThePage(bool reduced)
    {
        var (window, vm) = await ShowAsync(reduced, new Size(360, 780), "Write");
        var bar = Find<ListBox>(window, "BottomNav");
        Assert.True(bar.IsEffectivelyVisible);
        Assert.False(Find<ListBox>(window, "Switch").IsEffectivelyVisible);
        Assert.Equal(AppView.Focus, bar.SelectedItem);
        Assert.Single(window.GetVisualDescendants().OfType<FocusView>());
        Assert.False(Find<TasksView>(window, "TasksPane").IsEffectivelyVisible);

        bar.SelectedItem = AppView.Tasks;
        TestFrames.Tick(60);
        Assert.Equal(AppView.Tasks, vm.CurrentView);
        Assert.True(Find<TasksView>(window, "TasksPane").IsEffectivelyVisible);
        Assert.Single(Rows(window));
        Assert.Empty(window.GetVisualDescendants().OfType<FocusView>());

        bar.SelectedItem = AppView.Stats;
        TestFrames.Tick(60);
        Assert.Single(window.GetVisualDescendants().OfType<StatsView>());
        Assert.False(Find<TasksView>(window, "TasksPane").IsEffectivelyVisible);

        bar.SelectedItem = AppView.Focus;
        TestFrames.Tick(60);
        Assert.Single(window.GetVisualDescendants().OfType<FocusView>());
        Assert.Empty(window.GetVisualDescendants().OfType<StatsView>());
        Assert.Single(window.GetVisualDescendants().OfType<TasksView>());

        window.Close();
        await vm.FlushAsync();
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task WideningFromTheTasksPageReturnsTheListToItsColumn(bool reduced)
    {
        var (window, vm) = await ShowAsync(reduced, new Size(360, 780), "Write");
        vm.CurrentView = AppView.Tasks;
        TestFrames.Tick(60);

        window.Width = 1100;
        TestFrames.Tick(60);

        Assert.Equal(AppView.Focus, vm.CurrentView);
        var tasks = Find<TasksView>(window, "TasksPane");
        Assert.True(tasks.IsEffectivelyVisible);
        Assert.Same(Find<Border>(window, "TasksColumn"), tasks.Parent);
        Assert.Single(window.GetVisualDescendants().OfType<FocusView>());

        window.Close();
        await vm.FlushAsync();
    }

    [AvaloniaFact]
    public async Task PagesSwapForwardInTheBarsOrderAndBackAgainstIt()
    {
        var (window, vm) = await ShowAsync(reduced: true, new Size(360, 780));
        var host = Find<TransitioningContentControl>(window, "PageHost");

        foreach (var (view, back) in new[] { (AppView.Tasks, true), (AppView.Stats, false), (AppView.Focus, true), (AppView.Stats, false) })
        {
            vm.CurrentView = view;
            Assert.Equal(back, host.IsTransitionReversed);
            TestFrames.Tick(30);
        }

        window.Close();
        await vm.FlushAsync();
    }
}
