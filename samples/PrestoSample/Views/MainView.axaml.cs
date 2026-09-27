using Avalonia.Controls;
using Monaco.Presto;
using Monaco.Presto.AvaloniaBridge;
using PrestoSample.ViewModels;

namespace PrestoSample.Views;

public partial class MainView : UserControl
{
    private Animation? _pageTransition;

    public MainView()
    {
        InitializeComponent();
        PageHost.PropertyChanged += (_, e) =>
        {
            if (e.Property == ContentControl.ContentProperty) AnimatePageIn();
        };
    }

    private void NavSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (DataContext is not MainViewModel viewModel) return;
        // A section root only groups pages: putting it back leaves the current page selected and on screen.
        if (Nav.SelectedItem is GalleryPage page) viewModel.SelectedPage = page;
        else Nav.SelectedItem = viewModel.SelectedPage;
    }

    private void AnimatePageIn()
    {
        _pageTransition?.Revert();
        _pageTransition = PageHost.Animate(new()
        {
            ["Opacity"] = new TweenParams { From = 0 },
            ["Y"] = new TweenParams { From = 12 },
            Duration = 240,
            Ease = Eases.OutQuad,
        });
    }
}
