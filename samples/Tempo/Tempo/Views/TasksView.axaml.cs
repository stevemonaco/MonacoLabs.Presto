using Avalonia.Controls;
using Avalonia.Input;
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
            _viewModel.AddBoxFocusRequested -= FocusAddBox;
        _viewModel = DataContext as MainViewModel;
        if (_viewModel is not null)
            _viewModel.AddBoxFocusRequested += FocusAddBox;
    }

    private void FocusAddBox(object? sender, EventArgs e) => AddBox.Focus();

    private void OpenDetail(object? sender, TappedEventArgs e)
    {
        if ((e.Source as Control)?.DataContext is TaskItemViewModel task)
            _viewModel?.OpenDetailCommand.Execute(task);
    }
}
