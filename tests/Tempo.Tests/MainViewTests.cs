using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using MonacoLabs.Presto;
using Tempo.ViewModels;
using Tempo.Views;

namespace Tempo.Tests;

/// <summary>A <see cref="MainView"/> without the <c>windowed</c> class, as a single-view lifetime shows it, in a plain window.</summary>
public sealed class MainViewTests : TempoTestBase
{
    [AvaloniaFact]
    public async Task TheShortcutsWorkWhileNothingHasFocus()
    {
        var (window, view, vm) = await HostAsync(reduced: false, intro: false, "A", "B");
        Assert.Null(window.FocusManager!.GetFocusedElement());

        Space(window);
        Assert.True(vm.Focus.IsRunning);
        Space(window);
        Assert.False(vm.Focus.IsRunning);

        window.KeyPress(Key.OemComma, RawInputModifiers.Control, PhysicalKey.Comma, ",");
        Assert.True(vm.IsSettingsOpen);
        vm.IsSettingsOpen = false;
        TestFrames.Tick(30);
        Assert.Null(window.FocusManager.GetFocusedElement());

        vm.SelectedTask = vm.Tasks[0];
        window.KeyPressQwerty(PhysicalKey.Delete, RawInputModifiers.None);
        Assert.Equal(["B"], vm.Tasks.Select(t => t.Title));

        window.KeyPressQwerty(PhysicalKey.N, RawInputModifiers.None);
        Assert.True(Find<TextBox>(window, "AddBox").IsFocused);
        window.Close();
        await vm.FlushAsync();
    }

    [AvaloniaFact]
    public async Task NAndSpaceDoNotFireWhileTypingInTheAddBox()
    {
        var (window, _, vm) = await HostAsync(reduced: false, intro: false);
        Find<TextBox>(window, "AddBox").Focus();

        window.KeyTextInput("Plan");
        Space(window);
        window.KeyTextInput(" ");
        window.KeyPressQwerty(PhysicalKey.N, RawInputModifiers.None);
        window.KeyTextInput("n");

        Assert.Equal("Plan n", vm.NewTaskTitle);
        Assert.False(vm.Focus.IsRunning);
        window.Close();
        await vm.FlushAsync();
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task TheIntroHandsOffToTheShell(bool reduced)
    {
        var (window, view, vm) = await HostAsync(reduced, intro: true);
        Assert.IsType<IntroView>(view.Content);

        TickUntilShell(view, 4000);

        Assert.IsType<ShellView>(view.Content);
        Space(window);
        Assert.True(vm.Focus.IsRunning);
        window.Close();
        await vm.FlushAsync();
    }

    [AvaloniaFact]
    public async Task OffAWindowTheRootPaintsTheOpaqueBaseAndLeavesNoCaptionClearance()
    {
        var (window, view, vm) = await HostAsync(reduced: false, intro: false);

        Assert.Equal(Color.Parse("#121318"), Assert.IsAssignableFrom<ISolidColorBrush>(view.Background).Color);
        Assert.False(Find<Border>(window, "CaptionClearance").IsVisible);
        window.Close();
        await vm.FlushAsync();
    }

    [AvaloniaFact]
    public async Task UnderMainWindowTheViewIsTransparentAndClearsTheCaptionButtons()
    {
        var (window, vm) = await ShowAsync(reduced: false);

        Assert.Null(window.View.Background);
        Assert.True(Find<Border>(window, "CaptionClearance").IsEffectivelyVisible);
        window.Close();
        await vm.FlushAsync();
    }

    [AvaloniaFact]
    public async Task DetachingTheViewRemovesItsShortcutsFromTheTopLevel()
    {
        var (window, _, vm) = await HostAsync(reduced: false, intro: false);
        Assert.Equal(3, window.KeyBindings.Count);

        window.Content = null;
        TestFrames.Tick();
        Space(window);

        Assert.Empty(window.KeyBindings);
        Assert.False(vm.Focus.IsRunning);
        window.Close();
        await vm.FlushAsync();
    }

    private async Task<(Window Window, MainView View, MainViewModel ViewModel)> HostAsync(bool reduced, bool intro, params string[] titles)
    {
        TestFrames.Install();
        var vm = await LoadWithTasksAsync(titles);
        vm.Motion = reduced ? MotionPreference.Reduced : MotionPreference.Full;
        var view = new MainView { DataContext = vm };
        view.Start(Task.CompletedTask, skipIntro: !intro);
        var window = new Window { Width = 1100, Height = 720, Content = view };
        window.Show();
        if (!intro)
            TestFrames.Tick(30);
        return (window, view, vm);
    }

    private static void Space(Window window)
    {
        window.KeyPressQwerty(PhysicalKey.Space, RawInputModifiers.None);
        window.KeyReleaseQwerty(PhysicalKey.Space, RawInputModifiers.None);
        TestFrames.Tick(2);
    }
}
