using Avalonia.Controls;
using Avalonia.Interactivity;

namespace PrestoGallery.Showcase.Pages;

public partial class MarqueePage : ShowcasePage
{
    public MarqueePage() => InitializeComponent();

    private void LogoClicked(object? sender, RoutedEventArgs e)
    {
        if (e.Source is Button { DataContext: Datasets.Album album }) Picked.Text = $"Picked {album.Artist}";
    }

    private void LongTitleChanged(object? sender, RoutedEventArgs e) =>
        NowPlaying.Content = LongTitle.IsChecked == true ? "Harbor Light (Extended Night Mix) · Lumen Parade, live at the Pier" : "Harbor Light";
}
