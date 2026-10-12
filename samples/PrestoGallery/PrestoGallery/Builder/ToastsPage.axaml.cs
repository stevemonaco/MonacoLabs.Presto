using Avalonia.Controls;
using Avalonia.Interactivity;
using MonacoLabs.Presto;
using MonacoLabs.Presto.Controls;
using PrestoGallery.Showcase;

namespace PrestoGallery.Builder;

/// <summary>
/// A <see cref="ToastHost"/> whose item template attaches its entrance, countdown bar and exit from XAML; the page only
/// shows and closes toasts.
/// </summary>
public partial class ToastsPage : ShowcasePage
{
    private int _next;

    public ToastsPage() => InitializeComponent();

    protected override void OnAttached()
    {
        if (ToastList.Items.Count == 0) Notify();
    }

    #region snippet ToastsShow
    private void Notify() => ToastList.Show(Notice.Sample(_next++), duration: 5000);

    private void NotifyClicked(object? sender, RoutedEventArgs e) => Notify();

    private void Close(object? sender, RoutedEventArgs e) => ToastHost.GetToast((Control)sender!)?.Dismiss();
    #endregion
}
