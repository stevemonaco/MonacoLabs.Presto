using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using MonacoLabs.Presto.Controls;
using Tempo.ViewModels;
using Tempo.Views;

namespace Tempo.Tests;

public sealed class SurfaceTests : TempoTestBase
{
    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CompactOpensDetailsAndSettingsInBottomSheets(bool reduced)
    {
        var (window, vm) = await ShowAsync(reduced, new Size(360, 780), "Write");
        var detail = Find<Sheet>(window, "DetailSheet");

        vm.OpenDetailCommand.Execute(vm.Tasks[0]);
        TestFrames.Tick(80);
        Assert.True(detail.IsOpen);
        Assert.Equal(0.5 * detail.Bounds.Height, detail.Position.Value, 1);
        Assert.Same(detail, Find<TaskDetail>(window, "Detail").FindAncestorOfType<Sheet>());
        Assert.False(Find<Drawer>(window, "DetailDrawer").IsOpen);
        vm.IsDetailOpen = false;
        TestFrames.Tick(30);
        Assert.False(detail.IsOpen);

        vm.OpenSettingsCommand.Execute(null);
        TestFrames.Tick();
        var settings = Find<Sheet>(window, "SettingsSheet");
        var rows = settings.GetVisualDescendants().OfType<StackPanel>().First(p => p.Classes.Contains("settings")).Children;
        Assert.True(settings.IsOpen);
        Assert.Contains("open", Find<Button>(window, "NavSettings").Classes);
        Assert.Empty(window.GetVisualDescendants().OfType<FlyoutPresenter>());
        Assert.All(rows, row => Assert.True(row.Opacity < 1));
        TestFrames.Tick(50);
        Assert.All(rows, row => Assert.Equal(1, row.Opacity));

        vm.RequestClearCommand.Execute(null);
        TestFrames.Tick(40);
        Assert.False(settings.IsOpen);
        Assert.DoesNotContain("open", Find<Button>(window, "NavSettings").Classes);
        Assert.True(Find<Modal>(window, "ConfirmModal").IsOpen);

        window.Close();
        await vm.FlushAsync();
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CrossingTheBreakpointMovesOpenDetailsAndClosesSettings(bool reduced)
    {
        var (window, vm) = await ShowAsync(reduced, new Size(360, 780), "Write");
        vm.OpenDetailCommand.Execute(vm.Tasks[0]);
        TestFrames.Tick(30);

        window.Width = 1100;
        TestFrames.Tick(30);
        Assert.True(vm.IsDetailOpen);
        Assert.True(Find<Drawer>(window, "DetailDrawer").IsOpen);
        Assert.False(Find<Sheet>(window, "DetailSheet").IsOpen);
        Assert.Same(Find<Drawer>(window, "DetailDrawer"), Find<TaskDetail>(window, "Detail").FindAncestorOfType<Drawer>());

        window.Width = 360;
        TestFrames.Tick(30);
        Assert.True(Find<Sheet>(window, "DetailSheet").IsOpen);
        Assert.False(Find<Drawer>(window, "DetailDrawer").IsOpen);
        vm.IsDetailOpen = false;
        vm.OpenSettingsCommand.Execute(null);
        TestFrames.Tick(30);

        window.Width = 1100;
        TestFrames.Tick(30);
        Assert.False(Find<Sheet>(window, "SettingsSheet").IsOpen);
        Assert.Empty(window.GetVisualDescendants().OfType<FlyoutPresenter>());
        Assert.DoesNotContain("open", Find<Button>(window, "SettingsButton").Classes);
        Assert.DoesNotContain("open", Find<Button>(window, "NavSettings").Classes);

        window.Close();
        await vm.FlushAsync();
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task TheBottomBarsSettingsOpensTheSheetOverThePageWithoutMovingThePill(bool reduced)
    {
        var (window, vm) = await ShowAsync(reduced, new Size(360, 780), "Write");
        var nav = Find<ListBox>(window, "BottomNav");
        var button = Find<Button>(window, "NavSettings");
        Assert.True(button.IsEffectivelyVisible);
        Assert.False(Find<Button>(window, "SettingsButton").IsEffectivelyVisible);

        button.Command!.Execute(null);
        TestFrames.Tick(30);
        Assert.True(Find<Sheet>(window, "SettingsSheet").IsOpen);
        Assert.Equal(AppView.Focus, vm.CurrentView);
        Assert.Equal(AppView.Focus, nav.SelectedItem);

        window.Width = 1100;
        TestFrames.Tick(30);
        Assert.False(button.IsEffectivelyVisible);
        Assert.True(Find<Button>(window, "SettingsButton").IsEffectivelyVisible);

        window.Close();
        await vm.FlushAsync();
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AtTheNarrowestTheToastFitsTheWindowAboveTheBottomBar(bool reduced)
    {
        var (window, vm) = await ShowAsync(reduced, new Size(320, 640), "Write");
        vm.SelectedTask = vm.Tasks[0];
        vm.DeleteSelectedCommand.Execute(null);
        TestFrames.Tick(30);

        var toast = window.GetVisualDescendants().OfType<Border>().Single(b => b.Classes.Contains("toast"));
        var top = toast.TranslatePoint(default, window)!.Value;
        Assert.InRange(top.X, 16, 16.5);
        Assert.Equal(320 - 32, toast.Bounds.Width, 0.5);
        Assert.True(top.Y + toast.Bounds.Height < Find<ListBox>(window, "BottomNav").TranslatePoint(default, window)!.Value.Y);

        window.Close();
        await vm.FlushAsync();
    }
}
