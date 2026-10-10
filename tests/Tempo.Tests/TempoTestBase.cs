using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using Microsoft.Extensions.Time.Testing;
using MonacoLabs.Presto;
using MonacoLabs.Presto.Controls;
using Tempo.Model;
using Tempo.ViewModels;
using Tempo.Views;

namespace Tempo.Tests;

/// <summary>
/// A temporary data root, a fake clock at a fixed noon and manual ticks per test; disposing deletes the root and
/// restores <see cref="PrestoDefaults.ReducedMotion"/>.
/// </summary>
public abstract class TempoTestBase : IDisposable
{
    protected const int FrameTime = 16;

    private readonly bool _reducedWas = Presto.Defaults.ReducedMotion;

    protected string Root { get; } = Path.Combine(Path.GetTempPath(), "Tempo.Tests", Guid.NewGuid().ToString("N"));

    protected FakeTimeProvider Time { get; } = new(new DateTimeOffset(2026, 10, 9, 12, 0, 0, TimeSpan.Zero));

    internal ManualTicks Ticks { get; } = new();

    public void Dispose()
    {
        Presto.Defaults.ReducedMotion = _reducedWas;
        if (Directory.Exists(Root))
            Directory.Delete(Root, recursive: true);
        GC.SuppressFinalize(this);
    }

    /// <summary>Loads from <see cref="Root"/>, which starts as a saved empty file unless <paramref name="firstRun"/> leaves it missing.</summary>
    protected async Task<MainViewModel> LoadAsync(bool firstRun = false)
    {
        var store = new TempoStore(Root);
        if (!firstRun && !store.Exists)
            await store.SaveAsync(new TempoData());
        return await MainViewModel.LoadAsync(store, Time, Ticks);
    }

    protected async Task<MainViewModel> LoadWithTasksAsync(params string[] titles)
    {
        var vm = await LoadAsync();
        foreach (var title in titles)
        {
            vm.NewTaskTitle = title;
            vm.AddTaskCommand.Execute(null);
        }
        return vm;
    }

    /// <summary>Shows a window straight on the shell, with <paramref name="titles"/> added, and lets it settle.</summary>
    protected Task<(MainWindow Window, MainViewModel ViewModel)> ShowAsync(bool reduced, params string[] titles) =>
        ShowAsync(reduced, () => LoadWithTasksAsync(titles));

    /// <summary>As <see cref="ShowAsync(bool, string[])"/>, with the window at <paramref name="size"/>.</summary>
    protected Task<(MainWindow Window, MainViewModel ViewModel)> ShowAsync(bool reduced, Size size, params string[] titles) =>
        ShowAsync(reduced, () => LoadWithTasksAsync(titles), size);

    /// <summary>Shows a window straight on the shell over the first-run sample data, and lets it settle.</summary>
    protected Task<(MainWindow Window, MainViewModel ViewModel)> ShowSeededAsync(bool reduced, Size? size = null) =>
        ShowAsync(reduced, () => LoadAsync(firstRun: true), size);

    private static async Task<(MainWindow Window, MainViewModel ViewModel)> ShowAsync(bool reduced, Func<Task<MainViewModel>> load, Size? size = null)
    {
        TestFrames.Install();
        if (reduced)
            Presto.Defaults.ReducedMotion = true;
        var vm = await load();
        var window = new MainWindow { DataContext = vm };
        if (size is { } launch)
            window.UseLaunchSize(launch);
        window.Show();
        TestFrames.Tick(30);
        return (window, vm);
    }

    /// <summary>Shows a window on the intro, which hands off once <paramref name="load"/> completes; ticks nothing.</summary>
    protected async Task<(MainWindow Window, MainViewModel ViewModel)> ShowIntroAsync(Task load, bool reduced = false, bool firstRun = false, Size? size = null)
    {
        TestFrames.Install();
        if (reduced)
            Presto.Defaults.ReducedMotion = true;
        var vm = await LoadAsync(firstRun);
        var window = new MainWindow { DataContext = vm };
        if (size is { } launch)
            window.UseLaunchSize(launch);
        window.Start(load, skipIntro: false);
        window.Show();
        return (window, vm);
    }

    /// <returns>The milliseconds ticked until the window shows the shell.</returns>
    protected static int TickUntilShell(MainWindow window, int limit, Action? eachFrame = null)
    {
        var elapsed = 0;
        while (window.Content is not ShellView)
        {
            Assert.True(elapsed < limit, $"No handoff within {limit} ms.");
            TestFrames.Tick();
            elapsed += FrameTime;
            eachFrame?.Invoke();
        }
        return elapsed;
    }

    protected static void OpenConfirm(MainViewModel vm, System.Windows.Input.ICommand request)
    {
        vm.OpenSettingsCommand.Execute(null);
        TestFrames.Tick(50);
        request.Execute(null);
        TestFrames.Tick(40);
    }

    protected static void HoldToConfirm(Window window)
    {
        var ring = Find<ProgressRing>(window, "Hold");
        ring.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Space });
        TestFrames.Tick(45);
        ring.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyUpEvent, Key = Key.Space });
    }

    protected static IEnumerable<Border> Rows(Window window) =>
        Find<ListBox>(window, "TaskList").GetVisualDescendants().OfType<Border>().Where(b => b.Classes.Contains("task-row"));

    protected static IReadOnlyList<TextBlock> ClockBlocks(Window window) => Find<StackPanel>(window, "Digits").Children.Cast<TextBlock>().ToList();

    protected static string ClockText(Window window) => string.Concat(ClockBlocks(window).Select(b => b.Text));

    protected static T Find<T>(Window window, string name) where T : Control =>
        window.GetVisualDescendants().OfType<T>().Single(c => c.Name == name);
}
