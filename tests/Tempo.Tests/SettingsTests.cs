using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;
using Avalonia.Headless.XUnit;
using MonacoLabs.Presto;
using Tempo.Model;
using Tempo.ViewModels;
using Tempo.Views;

namespace Tempo.Tests;

public sealed class SettingsTests : TempoTestBase
{
    [AvaloniaFact]
    public async Task TheMotionSettingSetsAndClearsTheReducedFlag()
    {
        var vm = await LoadAsync();
        App.ApplyMotionSetting(vm);

        vm.Motion = MotionPreference.Reduced;
        Assert.True(Presto.Defaults.ReducedMotion);
        vm.Motion = MotionPreference.Full;
        Assert.False(Presto.Defaults.ReducedMotion);
        await vm.FlushAsync();

        Assert.Equal(MotionPreference.Full, (await new TempoStore(Root).LoadAsync()).Settings.Motion);
    }

    [AvaloniaFact]
    public async Task OpenSettingsShowsTheFlyoutFromItsPopoverStart()
    {
        var (window, vm) = await ShowAsync(reduced: false);

        vm.OpenSettingsCommand.Execute(null);
        TestFrames.Tick();
        var presenter = Assert.Single(window.GetVisualDescendants().OfType<FlyoutPresenter>());
        Assert.True(presenter.Opacity < 1);
        TestFrames.Tick(30);
        Assert.Equal(1, presenter.Opacity);
        Assert.Single(presenter.GetVisualDescendants().OfType<SettingsFlyout>());
        window.Close();
        await vm.FlushAsync();
    }

    [Fact]
    public async Task ADurationChangePersistsAndAppliesFromTheNextSession()
    {
        var vm = await LoadAsync();
        vm.Focus.StartPauseCommand.Execute(null);

        vm.WorkMinutes = 10;
        Time.Advance(TimeSpan.FromMinutes(1));
        Ticks.Raise();
        Assert.Equal("24:00", vm.Focus.RemainingText);

        vm.Focus.AbandonCommand.Execute(null);
        Assert.Equal("10:00", vm.Focus.RemainingText);
        await vm.FlushAsync();
        Assert.Equal(10, (await new TempoStore(Root).LoadAsync()).Settings.WorkMinutes);
    }

    [AvaloniaFact]
    public async Task TheWindowPlacementRoundTripsThroughSettings()
    {
        var vm = await LoadAsync();
        var window = new MainWindow { DataContext = vm };
        window.Show();
        window.Width = 900;
        window.Height = 600;
        window.Position = new PixelPoint(120, 80);
        window.Close();
        await vm.FlushAsync();

        var restored = new MainWindow { DataContext = await LoadAsync() };

        Assert.Equal(900, restored.Width);
        Assert.Equal(600, restored.Height);
        Assert.Equal(new PixelPoint(120, 80), restored.Position);
    }

    [AvaloniaFact]
    public async Task APlacementOffEveryScreenIsNotRestored()
    {
        var vm = await LoadAsync();
        vm.SaveWindowPlacement(900, 600, -50_000, -50_000, isMaximized: false);
        await vm.FlushAsync();

        var restored = new MainWindow { DataContext = await LoadAsync() };

        Assert.NotEqual(new PixelPoint(-50_000, -50_000), restored.Position);
        Assert.NotEqual(900, restored.Width);
    }
}
