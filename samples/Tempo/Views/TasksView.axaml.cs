using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using MonacoLabs.Presto;
using Tempo.ViewModels;

namespace Tempo.Views;

public partial class TasksView : UserControl
{
    private MainViewModel? _viewModel;

    public TasksView()
    {
        InitializeComponent();
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (_viewModel is not null)
        {
            _viewModel.AddBoxFocusRequested -= FocusAddBox;
            _viewModel.TaskAdded -= RevealAdded;
        }
        _viewModel = DataContext as MainViewModel;
        if (_viewModel is not null)
        {
            _viewModel.AddBoxFocusRequested += FocusAddBox;
            _viewModel.TaskAdded += RevealAdded;
        }
    }

    private void FocusAddBox(object? sender, EventArgs e) => AddBox.Focus();

    // Code-only: a scroll to an added item has no XAML trigger; it moves only when the row would be out of view.
    private void RevealAdded(object? sender, TaskItemViewModel task)
    {
        var index = _viewModel!.VisibleTasks.IndexOf(task);
        if (index >= 0 && TaskList.IsAttachedToVisualTree() && TaskList.ItemsPanelRoot is not null)
            PrestoAvalonia.ScrollToIndex(TaskList, index, ScrollAlignment.Nearest);
    }

    private void OpenDetail(object? sender, TappedEventArgs e)
    {
        if ((e.Source as Control)?.DataContext is TaskItemViewModel task)
            _viewModel?.OpenDetailCommand.Execute(task);
    }
}
