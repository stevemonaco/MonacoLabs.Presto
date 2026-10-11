using Avalonia;
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
    public async Task AWheelNotchOverTheTaskListEasesUnlessReduced(bool reduced)
    {
        var (window, vm) = await ShowSeededAsync(reduced, new Size(1100, 560));
        TestFrames.Tick(30);
        var list = Find<ListBox>(window, "TaskList");
        var scroll = list.GetVisualDescendants().OfType<ScrollViewer>().First();
        Assert.True(MonacoLabs.Presto.Scroll.GetSmooth(scroll));

        window.MouseWheel(list.TranslatePoint(new Point(40, 40), window)!.Value, new Vector(0, -1));
        TestFrames.Tick();
        if (reduced)
            Assert.Equal(50, scroll.Offset.Y, 1);
        else
            Assert.InRange(scroll.Offset.Y, .01, 49);
        TestFrames.Tick(30);
        Assert.Equal(50, scroll.Offset.Y, 1);
        window.Close();
        await vm.FlushAsync();
    }

    [AvaloniaFact]
    public async Task CompactRowsOpenTheirDetailsFromTheMoreButtonWhichWideHides()
    {
        var (window, vm) = await ShowSeededAsync(reduced: true, new Size(1100, 700));
        TestFrames.Tick(30);
        Assert.All(MoreButtons(window), b => Assert.False(b.IsEffectivelyVisible));

        window.Width = 400;
        vm.CurrentView = AppView.Tasks;
        TestFrames.Tick(30);
        var more = Find<ListBox>(window, "TaskList").ContainerFromIndex(1)!.GetVisualDescendants().OfType<Button>().Single(b => b.Classes.Contains("row-more"));
        Assert.True(more.IsEffectivelyVisible);
        more.Command!.Execute(more.CommandParameter);
        TestFrames.Tick(2);

        Assert.True(vm.IsDetailOpen);
        Assert.Same(vm.VisibleTasks[1], vm.SelectedTask);
        window.Close();
        await vm.FlushAsync();
    }

    private static IEnumerable<Button> MoreButtons(Window window) =>
        window.GetVisualDescendants().OfType<Button>().Where(b => b.Classes.Contains("row-more"));

    [AvaloniaFact]
    public async Task ASwipeThatDriftsVerticallyKeepsSwipingAndNeverReorders()
    {
        var (window, vm) = await ShowSeededAsync(reduced: true);
        TestFrames.Tick(90);
        var order = vm.VisibleTasks.ToList();
        var container = Find<ListBox>(window, "TaskList").ContainerFromIndex(1)!;
        var row = container.GetVisualDescendants().OfType<Border>().First(b => b.Classes.Contains("task-row"));
        var title = container.GetVisualDescendants().OfType<TextBlock>().First(t => t.Name == "Title");
        var start = title.TranslatePoint(new Point(10, title.Bounds.Height / 2), window)!.Value;

        window.MouseDown(start, MouseButton.Left);
        window.MouseMove(start + new Point(-12, 1));
        window.MouseMove(start + new Point(-40, 25));
        window.MouseMove(start + new Point(-60, 45));

        Assert.True(Convert.ToDouble(MonacoLabs.Presto.Presto.Get(row, "X", withUnit: false)) < -30);
        window.MouseUp(start + new Point(-60, 45), MouseButton.Left);
        TestFrames.Tick(60);
        Assert.Equal(order, vm.VisibleTasks);
        window.Close();
        await vm.FlushAsync();
    }

    [AvaloniaFact]
    public async Task ATouchHoldLetGoInPlaceOpensTheRowsDetails()
    {
        var (window, vm) = await ShowSeededAsync(reduced: true);
        TestFrames.Tick(90);
        var title = Find<ListBox>(window, "TaskList").ContainerFromIndex(1)!.GetVisualDescendants().OfType<TextBlock>().First(t => t.Name == "Title");
        var at = title.TranslatePoint(new Point(10, title.Bounds.Height / 2), window)!.Value;
        var pointer = new Pointer(Pointer.GetNextFreeId(), PointerType.Touch, true);

        title.RaiseEvent(new PointerPressedEventArgs(title, pointer, window, at, 1,
            new PointerPointProperties(RawInputModifiers.LeftMouseButton, PointerUpdateKind.LeftButtonPressed), KeyModifiers.None));
        pointer.Capture(title);
        TestFrames.Tick(30);
        var target = (Interactive)pointer.Captured!;
        target.RaiseEvent(new PointerReleasedEventArgs(target, pointer, window, at, 2,
            new PointerPointProperties(RawInputModifiers.None, PointerUpdateKind.LeftButtonReleased), KeyModifiers.None, MouseButton.Left));
        TestFrames.Tick(30);

        Assert.True(vm.IsDetailOpen);
        Assert.Same(vm.VisibleTasks[1], vm.SelectedTask);
        window.Close();
        await vm.FlushAsync();
    }

    [AvaloniaFact]
    public async Task DraggingARowByItsTitleReordersWithoutTickingIt()
    {
        var (window, vm) = await ShowSeededAsync(reduced: true);
        TestFrames.Tick(30);
        var list = Find<ListBox>(window, "TaskList");
        var first = vm.VisibleTasks[0];
        var title = list.ContainerFromIndex(0)!.GetVisualDescendants().OfType<TextBlock>().First(t => t.Name == "Title");
        var start = title.TranslatePoint(new Point(10, title.Bounds.Height / 2), window)!.Value;
        var row = list.ContainerFromIndex(0)!.Bounds.Height;

        window.MouseDown(start, MouseButton.Left);
        window.MouseMove(start + new Point(0, 10));
        window.MouseMove(start + new Point(0, row * 2));
        window.MouseUp(start + new Point(0, row * 2), MouseButton.Left);
        TestFrames.Tick(30);

        Assert.True(vm.VisibleTasks.IndexOf(first) > 0);
        Assert.False(first.IsDone);
        window.Close();
        await vm.FlushAsync();
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AnAddedTaskScrollsIntoViewWhenTheListOverflows(bool reduced)
    {
        var (window, vm) = await ShowSeededAsync(reduced, new Size(1100, 560));
        var list = Find<ListBox>(window, "TaskList");
        var scroll = list.GetVisualDescendants().OfType<ScrollViewer>().First();
        TestFrames.Tick(30);
        Assert.True(scroll.Extent.Height > scroll.Viewport.Height);
        Assert.Equal(0, scroll.Offset.Y);

        vm.NewTaskTitle = "Brand new";
        vm.AddTaskCommand.Execute(null);
        TestFrames.Tick(60);

        Assert.True(scroll.Offset.Y > 0);
        var row = list.ContainerFromIndex(vm.VisibleTasks.Count - 1)!;
        var bottom = row.TranslatePoint(new Point(0, row.Bounds.Height), scroll)!.Value.Y;
        Assert.InRange(bottom, row.Bounds.Height, scroll.Viewport.Height + .5);
        window.Close();
        await vm.FlushAsync();
    }

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

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ATagChipFiltersTheRowsAndTheIndicatorGlidesToIt(bool reduced)
    {
        var (window, vm) = await ShowAsync(reduced, "A", "B", "C");
        (vm.Tasks[0].Tag, vm.Tasks[2].Tag) = (TaskTag.Code, TaskTag.Code);
        var chips = Find<ListBox>(window, "TagChips");
        Assert.Equal(0, chips.SelectedIndex);
        Assert.Equal(Offset(chips.ContainerFromIndex(0)!, chips), Offset(Pill(chips), chips));

        chips.SelectedIndex = 1 + (int)TaskTag.Code;
        TestFrames.Tick(40);

        Assert.Equal(TaskTag.Code, vm.TagFilter);
        Assert.Equal([vm.Tasks[0], vm.Tasks[2]], Rows(window).Select(r => r.DataContext));
        Assert.Equal(Offset(chips.ContainerFromIndex(chips.SelectedIndex)!, chips), Offset(Pill(chips), chips));
        Assert.Empty(Ghosts(window));
        window.Close();
        await vm.FlushAsync();
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task TheAddBoxPlusTurnsIntoACheckWhileItHasTextAndBackWhenItEmpties(bool reduced)
    {
        var (window, vm) = await ShowAsync(reduced);
        var icon = Find<Avalonia.Controls.Shapes.Path>(window, "AddIcon");
        var plus = icon.Data;

        Find<TextBox>(window, "AddBox").Focus();
        window.KeyTextInput("Go");
        TestFrames.Tick(60);
        Assert.Contains("ready", icon.Classes);
        Assert.NotSame(plus, icon.Data);

        window.KeyPressQwerty(PhysicalKey.Backspace, RawInputModifiers.None);
        window.KeyPressQwerty(PhysicalKey.Backspace, RawInputModifiers.None);
        TestFrames.Tick(60);
        Assert.Equal("", vm.NewTaskTitle);
        Assert.DoesNotContain("ready", icon.Classes);
        Assert.Same(plus, icon.Data);
        Assert.True(icon.RenderTransform is null || icon.RenderTransform.Value.IsIdentity);
        window.Close();
        await vm.FlushAsync();
    }

    private static Border Pill(ListBox chips) => chips.GetVisualDescendants().OfType<Border>().Single(b => b.Classes.Contains("segment-pill"));

    private static Point? Offset(Visual visual, Visual relativeTo) => visual.TranslatePoint(default, relativeTo);

    private static IEnumerable<Image> Ghosts(Window window) => OverlayLayer.GetOverlayLayer(window)!.GetVisualDescendants().OfType<Image>();
}
