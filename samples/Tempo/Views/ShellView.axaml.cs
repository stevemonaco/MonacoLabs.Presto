using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using MonacoLabs.Presto.Controls;
using Tempo.ViewModels;

namespace Tempo.Views;

public partial class ShellView : UserControl
{
    private MainViewModel? _viewModel;

    public ShellView()
    {
        InitializeComponent();
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (_viewModel is not null)
        {
            _viewModel.TaskDeleted -= ShowUndo;
            _viewModel.SettingsRequested -= ShowSettings;
        }
        _viewModel = DataContext as MainViewModel;
        if (_viewModel is not null)
        {
            _viewModel.TaskDeleted += ShowUndo;
            _viewModel.SettingsRequested += ShowSettings;
        }
    }

    // Code-only: a flyout has no IsOpen to bind (BACKLOG, BRIDGE-POPOVER), so Ctrl+, and the button share OpenSettingsCommand.
    private void ShowSettings(object? sender, EventArgs e) => FlyoutBase.ShowAttachedFlyout(SettingsButton);

    // Code-only: a toast has no XAML trigger (BACKLOG, BRIDGE-TOAST).
    private void ShowUndo(object? sender, TaskItemViewModel task) => Toasts.Show(task, 5000);

    private void Undo(object? sender, RoutedEventArgs e)
    {
        ToastHost.GetToast((Control)sender!)?.Dismiss();
        _viewModel?.UndoDeleteCommand.Execute(null);
    }
}
