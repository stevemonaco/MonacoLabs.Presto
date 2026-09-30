using Avalonia.Controls;
using PrestoSample.ViewModels;

namespace PrestoSample.Views;

public partial class MainView : UserControl
{
    public MainView() => InitializeComponent();

    private void NavSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (DataContext is not MainViewModel viewModel) return;
        // A section root only groups pages: putting it back leaves the current page selected and on screen.
        if (Nav.SelectedItem is GalleryPage page) viewModel.SelectedPage = page;
        else Nav.SelectedItem = viewModel.SelectedPage;
    }
}
