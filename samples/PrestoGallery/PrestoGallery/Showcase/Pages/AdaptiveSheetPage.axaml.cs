using Avalonia.Controls;
using Avalonia.Interactivity;
using MonacoLabs.Presto.Controls;

namespace PrestoGallery.Showcase.Pages;

/// <summary>
/// A notes app on a stage sized like a phone, a tablet or the whole card, whose two <see cref="Sheet"/>s change form
/// by style alone at a 720 DIP container query: details as a right drawer or a bottom sheet, settings as a flyout at
/// the gear or a bottom sheet.
/// </summary>
public partial class AdaptiveSheetPage : ShowcasePage
{
    public AdaptiveSheetPage()
    {
        InitializeComponent();
        HowCode.Code = string.Join("\n\n", Snippets.AdaptiveSheetDetails, Snippets.AdaptiveSheetSettings, Snippets.AdaptiveSheetStyles);
        Widths.SelectionChanged += WidthChanged;
    }

    private void WidthChanged(object? sender, SelectionChangedEventArgs e) =>
        Stage.Width = Widths.SelectedIndex switch { 0 => 390, 1 => 820, _ => double.NaN };

    private void NoteClicked(object? sender, RoutedEventArgs e)
    {
        Details.DataContext = ((Control)e.Source!).DataContext;
        DetailsSheet.IsOpen = true;
    }

    private void GearClicked(object? sender, RoutedEventArgs e) => SettingsSheet.IsOpen = true;
}
