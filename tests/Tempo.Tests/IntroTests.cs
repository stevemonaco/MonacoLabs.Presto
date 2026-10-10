using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;
using MonacoLabs.Presto.Compositor;
using Tempo.Model;
using Tempo.ViewModels;
using Tempo.Views;

namespace Tempo.Tests;

public sealed class IntroTests : TempoTestBase
{
    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task TheIntroHandsOffAfterItsLengthWithParticlesOnlyAtFullMotion(bool reduced)
    {
        var (window, vm) = await ShowIntroAsync(Task.CompletedTask, reduced);
        Assert.IsType<IntroView>(window.Content);

        var particles = false;
        var elapsed = TickUntilShell(window, 4000, () => particles |= OverlayLayer.GetOverlayLayer(window)!.Children.OfType<CompositorCanvas>().Any());

        Assert.Equal(!reduced, particles);

        if (reduced)
            Assert.InRange(elapsed, 0, 500);
        else
            Assert.InRange(elapsed, 2000, 3000);
        window.Close();
        await vm.FlushAsync();
    }

    [AvaloniaFact]
    public async Task TheHandoffWaitsForASlowLoad()
    {
        var load = new TaskCompletionSource();
        var (window, vm) = await ShowIntroAsync(load.Task);

        TestFrames.Tick(3500 / FrameTime);
        Assert.IsType<IntroView>(window.Content);

        load.SetResult();
        Assert.InRange(TickUntilShell(window, 100), 0, 2 * FrameTime);
        window.Close();
        await vm.FlushAsync();
    }

    [AvaloniaTheory]
    [InlineData("Space")]
    [InlineData("Enter")]
    [InlineData("Escape")]
    [InlineData("Click")]
    public async Task ASkipHandsOffAtOnceWithTheWordmarkDrawn(string input)
    {
        var (window, vm) = await ShowIntroAsync(Task.CompletedTask);
        TestFrames.Tick(200 / FrameTime);
        var wordmark = window.GetVisualDescendants().OfType<TextBlock>().Single(t => t.Text == "tempo");

        switch (input)
        {
            case "Space":
                window.KeyPress(Key.Space, RawInputModifiers.None, PhysicalKey.Space, " ");
                break;
            case "Enter":
                window.KeyPress(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, null);
                break;
            case "Escape":
                window.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null);
                break;
            default:
                window.MouseDown(new Point(20, 300), MouseButton.Left);
                window.MouseUp(new Point(20, 300), MouseButton.Left);
                break;
        }

        Assert.InRange(TickUntilShell(window, 100), 0, 2 * FrameTime);
        Assert.Equal(1, wordmark.Opacity);
        Assert.False(vm.Focus.IsRunning);
        window.Close();
        await vm.FlushAsync();
    }

    [AvaloniaTheory]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public async Task WithTheIntroOffOrSkippedTheWindowStartsOnTheShell(bool playIntro, bool skipIntro)
    {
        TestFrames.Install();
        var vm = await LoadAsync();
        vm.PlayIntro = playIntro;
        var window = new MainWindow { DataContext = vm };

        window.Start(Task.CompletedTask, skipIntro);
        window.Show();
        TestFrames.Tick();

        Assert.IsType<ShellView>(window.Content);
        Assert.Empty(window.GetVisualDescendants().OfType<IntroView>());
        window.Close();
        await vm.FlushAsync();
    }
}
