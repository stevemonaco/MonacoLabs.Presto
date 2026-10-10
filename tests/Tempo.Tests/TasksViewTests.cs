using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using MonacoLabs.Presto.Controls;
using Tempo.Model;
using Tempo.ViewModels;
using Tempo.Views;

namespace Tempo.Tests;

public sealed class TasksViewTests : TempoTestBase
{
    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task TypingATitleAndEnterAddsARowWithoutTriggeringTheWindowShortcuts(bool reduced)
    {
        var (window, vm) = await ShowAsync(reduced);
        var box = Find<TextBox>(window, "AddBox");

        box.Focus();
        window.KeyTextInput("Plan");
        window.KeyPressQwerty(PhysicalKey.Space, RawInputModifiers.None);
        window.KeyTextInput(" ");
        window.KeyPressQwerty(PhysicalKey.N, RawInputModifiers.None);
        window.KeyTextInput("n");
        window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        TestFrames.Tick(30);

        Assert.Equal(["Plan n"], vm.Tasks.Select(t => t.Title));
        Assert.False(vm.Focus.IsRunning);
        Assert.Contains(Rows(window), row => row.DataContext == vm.Tasks[0]);
        window.Close();
        await vm.FlushAsync();
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DeletingFromTheSwipeActionsRemovesTheRowBehindItsExitAndUndoRestoresIt(bool reduced)
    {
        var (window, vm) = await ShowAsync(reduced, "A", "B", "C");
        var b = vm.Tasks[1];
        var row = Assert.Single(Rows(window), r => r.DataContext == b);

        row.RaiseEvent(new ContextRequestedEventArgs());
        TestFrames.Tick(30);
        var delete = row.GetVisualDescendants().OfType<Button>().Single(a => a.Classes.Contains("delete"));
        ((IInvokeProvider)ControlAutomationPeer.CreatePeerForElement(delete)).Invoke();
        TestFrames.Tick(2);

        Assert.DoesNotContain(Rows(window), row => row.DataContext == b);
        Assert.NotEmpty(Ghosts(window));
        TestFrames.Tick(30);
        Assert.Empty(Ghosts(window));

        var toasts = Find<ToastHost>(window, "Toasts");
        var undo = Assert.Single(toasts.GetVisualDescendants().OfType<Button>());
        undo.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        TestFrames.Tick(30);

        Assert.Equal(["A", "B", "C"], vm.Tasks.Select(t => t.Title));
        Assert.Contains(Rows(window), row => row.DataContext == b);
        Assert.Empty(toasts.Items);
        window.Close();
        await vm.FlushAsync();
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CompletingStaysToDrawTheStrikeThenMovesTheRowToDone(bool reduced)
    {
        var (window, vm) = await ShowAsync(reduced, "A", "B");
        var a = vm.Tasks[0];
        var row = Assert.Single(Rows(window), r => r.DataContext == a);
        var done = window.GetVisualDescendants().OfType<Expander>().Single();
        done.IsExpanded = true;

        row.GetVisualDescendants().OfType<CheckBox>().Single().IsChecked = true;
        TestFrames.Tick(10);

        Assert.Contains(a, vm.Tasks);
        Time.Advance(MainViewModel.DoneMoveDelay);
        TestFrames.Tick(30);

        Assert.Equal([a], vm.DoneTasks);
        Assert.Contains(done.GetVisualDescendants().OfType<Border>(), r => r.Classes.Contains("task-row") && r.DataContext == a);
        Assert.DoesNotContain(Rows(window), r => r.DataContext == a);
        window.Close();
        await vm.FlushAsync();
    }

    private static IEnumerable<Border> Rows(Window window) =>
        Find<ListBox>(window, "TaskList").GetVisualDescendants().OfType<Border>().Where(b => b.Classes.Contains("task-row"));

    private static IEnumerable<Image> Ghosts(Window window) => OverlayLayer.GetOverlayLayer(window)!.GetVisualDescendants().OfType<Image>();

    private static T Find<T>(Window window, string name) where T : Control =>
        window.GetVisualDescendants().OfType<T>().Single(c => c.Name == name);
}
