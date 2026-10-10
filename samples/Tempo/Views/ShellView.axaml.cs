using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using MonacoLabs.Presto;
using MonacoLabs.Presto.Compositor;
using MonacoLabs.Presto.Controls;
using Tempo.Motion;
using Tempo.ViewModels;

namespace Tempo.Views;

public partial class ShellView : UserControl
{
    private const double CompactBreakpoint = 720;

    private MainViewModel? _viewModel;
    private CompositorAnimation? _drift;
    private bool? _compact;
    private IDisposable? _detailOpen;

    public ShellView()
    {
        InitializeComponent();
        SettingsSheet.PropertyChanged += (_, e) =>
        {
            if (e.Property == Sheet.IsOpenProperty)
                NavSettings.Classes.Set("open", SettingsSheet.IsOpen);
        };
        // Code-only: the Tasks page adopts the one TasksPane, so the column and the page never hold two lists' state.
        // Not on null: the template reaches nested presenters, and a typed FuncDataTemplate matches null content.
        PageHost.DataTemplates.Add(new FuncDataTemplate<MainViewModel>(vm => vm is not null, (_, _) => TasksPage()));
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (_viewModel is not null)
        {
            _viewModel.TaskDeleted -= ShowUndo;
            _viewModel.SettingsRequested -= ShowSettings;
            _viewModel.ConfirmRequested -= ShowConfirm;
            _viewModel.PropertyChanged -= RedriftOnMotionChange;
            _viewModel.PlayClear = null;
        }
        _viewModel = DataContext as MainViewModel;
        if (_viewModel is not null)
        {
            _viewModel.TaskDeleted += ShowUndo;
            _viewModel.SettingsRequested += ShowSettings;
            _viewModel.ConfirmRequested += ShowConfirm;
            _viewModel.PropertyChanged += RedriftOnMotionChange;
            _viewModel.PlayClear = PlayClear;
        }
        _compact = null;
        InvalidateMeasure();
    }

    // In measure rather than on SizeChanged, so the first frame already has the right layout.
    protected override Size MeasureOverride(Size availableSize)
    {
        var compact = availableSize.Width < CompactBreakpoint;
        if (_viewModel is not null && !double.IsInfinity(availableSize.Width) && compact != _compact)
        {
            _compact = compact;
            _viewModel.IsCompact = compact;
            Classes.Set("compact", compact);
            if (!compact)
                Move(TasksPane, TasksColumn);
            PlaceSurfaces(compact);
        }
        return base.MeasureOverride(availableSize);
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        Drift();
    }

    protected override void OnUnloaded(RoutedEventArgs e)
    {
        base.OnUnloaded(e);
        _drift?.Revert();
        _drift = null;
    }

    // Back in its column once the page leaves, after the detach, so a hidden column holds it between visits.
    private Decorator TasksPage()
    {
        var page = new Decorator();
        Move(TasksPane, page);
        page.DetachedFromVisualTree += (_, _) => Dispatcher.UIThread.Post(() =>
        {
            if (TasksPane.Parent == page)
                Move(TasksPane, TasksColumn);
        });
        return page;
    }

    // Code-only: one detail and one settings panel move to the arrangement's surfaces, and only the live detail surface binds
    // IsDetailOpen, so an open detail moves across the breakpoint and open settings close.
    private void PlaceSurfaces(bool compact)
    {
        _detailOpen?.Dispose();
        DetailDrawer.IsOpen = false;
        DetailSheet.IsOpen = false;
        DetailDrawer.Content = null;
        DetailSlot.Child = null;
        if (compact)
            DetailSlot.Child = Detail;
        else
            DetailDrawer.Content = Detail;
        _detailOpen = (compact ? DetailSheet : DetailDrawer).Bind(Sheet.IsOpenProperty, new ReflectionBinding(nameof(MainViewModel.IsDetailOpen)));

        var flyout = (Flyout)FlyoutBase.GetAttachedFlyout(SettingsButton)!;
        flyout.Hide();
        SettingsSheet.IsOpen = false;
        flyout.Content = null;
        SettingsSlot.Child = null;
        if (compact)
            SettingsSlot.Child = Settings;
        else
            flyout.Content = Settings;
    }

    private static void Move(Control child, Decorator to)
    {
        if (child.Parent == to)
            return;
        ((Decorator)child.Parent!).Child = null;
        to.Child = child;
    }

    // Code-only: XAML entry points stay on the UI thread (BRIDGE-THREAD), and the drift must not cost UI-thread work.
    private void Drift()
    {
        _drift?.Revert();
        _drift = PrestoAvalonia.AnimateOnCompositor(Blobs.Children, TempoMotion.BlobDrift);
    }

    // The reduced form is resolved when the drift starts, and nothing signals a change of the flag.
    private void RedriftOnMotionChange(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.Motion) && IsLoaded)
            Drift();
    }

    // Code-only: a flyout has no IsOpen to bind (BACKLOG, BRIDGE-POPOVER), so Ctrl+, and the button share OpenSettingsCommand.
    private void ShowSettings(object? sender, EventArgs e)
    {
        if (_compact == true)
            SettingsSheet.IsOpen = true;
        else
            FlyoutBase.ShowAttachedFlyout(SettingsButton);
    }

    // Code-only: no IsOpen to bind (BACKLOG, BRIDGE-POPOVER), so the button's open class, which holds its quarter turn, follows the flyout's events.
    private void SettingsToggled(object? sender, EventArgs e) => SettingsButton.Classes.Set("open", ((FlyoutBase)sender!).IsOpen);

    // Code-only: a modal opens from an element only through Open (BACKLOG, BRIDGE-MODAL). The settings close first, since the popup
    // draws above the modal, and the button's center crosses through screen coordinates: Open(from) needs one visual root.
    private void ShowConfirm(object? sender, EventArgs e)
    {
        Button from = _viewModel!.ConfirmAction == DataAction.Clear ? Settings.ClearButton : Settings.RestoreButton;
        Point? origin = from.IsAttachedToVisualTree()
            ? ConfirmModal.PointToClient(from.PointToScreen(new Point(from.Bounds.Width / 2, from.Bounds.Height / 2)))
            : null;
        FlyoutBase.GetAttachedFlyout(SettingsButton)?.Hide();
        SettingsSheet.IsOpen = false;
        Dialog.Reset();
        if (origin is { } at)
            ConfirmModal.Open(at);
        else
            ConfirmModal.Open();
    }

    // Code-only: a sequence across two views, with the clear between the exits and the empty line (BACKLOG, Tempo findings).
    private async Task PlayClear()
    {
        var rows = Classed(TasksPane.TaskList, "task-row");
        var scatter = rows.Count > 0 ? TempoMotion.ClearScatter.Animate(rows) : null;
        if (scatter is not null)
            await scatter;
        var bars = this.GetVisualDescendants().OfType<StatsView>().FirstOrDefault() is { } stats ? Classed(stats.Bars, "bar") : [];
        if (bars.Count > 0)
            await TempoMotion.BarCollapse.Animate(bars);
        _viewModel!.ClearAll();
        // Only now: the removed rows' exits have snapshotted them scattered, and their recycled containers must come back whole.
        scatter?.Revert();
    }

    private static List<Border> Classed(Visual root, string name) =>
        [.. root.GetVisualDescendants().OfType<Border>().Where(b => b.Classes.Contains(name) && b.IsEffectivelyVisible)];

    // Code-only: a toast has no XAML trigger (BACKLOG, BRIDGE-TOAST).
    private void ShowUndo(object? sender, TaskItemViewModel task) => Toasts.Show(task, 5000);

    private void Undo(object? sender, RoutedEventArgs e)
    {
        ToastHost.GetToast((Control)sender!)?.Dismiss();
        _viewModel?.UndoDeleteCommand.Execute(null);
    }
}
