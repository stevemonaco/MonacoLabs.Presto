using System.Collections.Specialized;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Monaco.Presto;
using Monaco.Presto.AvaloniaBridge;
using PrestoSample.Showcase;
using Timer = Monaco.Presto.Timer;

namespace PrestoSample.Builder;

/// <summary>
/// A toast stack whose item template attaches its entrance, countdown and exit from XAML; the page only removes items.
/// </summary>
public partial class ToastsPage : ShowcasePage
{
    private const int MaxToasts = 4;

    #region snippet ToastsBuilders
    public static readonly AnimationBuilder Countdown = Presto.Build()
        .ScaleX(from: 1, to: 0)
        .Duration(5000)
        .Ease(Eases.None)
        .OnComplete(Expire)
        .WhenReduced(reduced => reduced.Opacity(from: 1, to: .35));
    #endregion

    public ToastsPage()
    {
        InitializeComponent();
        ViewModel.Toasts.CollectionChanged += TrimStack;
    }

    public ToastsViewModel ViewModel => (ToastsViewModel)DataContext!;

    protected override void OnAttached()
    {
        if (ViewModel.Toasts.Count == 0) ViewModel.NotifyCommand.Execute(null);
    }

    private static void Expire(Timer timer)
    {
        if (((Animation)timer).Targets[0] is Control { DataContext: Toast toast } bar && bar.FindAncestorOfType<ToastsPage>() is { } page)
            page.ViewModel.Toasts.Remove(toast);
    }

    private void TrimStack(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action != NotifyCollectionChangedAction.Add || ViewModel.Toasts.Count <= MaxToasts) return;
        // Posted because the collection may not change inside its own CollectionChanged.
        Dispatcher.UIThread.Post(() =>
        {
            while (ViewModel.Toasts.Count > MaxToasts) ViewModel.Toasts.RemoveAt(0);
        });
    }

    private void Close(object? sender, RoutedEventArgs e)
    {
        if (sender is Control { DataContext: Toast toast }) ViewModel.Toasts.Remove(toast);
    }
}
