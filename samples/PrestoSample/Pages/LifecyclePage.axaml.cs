using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Monaco.Presto;
using Monaco.Presto.AvaloniaBridge;
using Timer = Monaco.Presto.Timer;

namespace PrestoSample.Pages;

/// <summary>
/// Presence's lifecycle side by side: three views, the last a virtualized list, whose rows carry Initial, Enter and Exit,
/// and a log written by each builder's OnBegin, so it records what Presence classified rather than what the page intended.
/// </summary>
public partial class LifecyclePage : UserControl
{
    private const int MaxMoments = 12;

    private static readonly IBrush InitialTint = Brush.Parse("#93c5fd");
    private static readonly IBrush EnterTint = Brush.Parse("#86efac");
    private static readonly IBrush ExitTint = Brush.Parse("#fca5a5");
    private static readonly IBrush UnloadTint = Brush.Parse("#cbd5e1");

    private readonly (string Title, ObservableCollection<string> Rows, ItemsControl List, Control View)[] _views;
    private readonly ObservableCollection<(string Moment, IBrush Tint, string Detail)> _moments = [];
    private int _view;
    private int _next;

    public LifecyclePage()
    {
        InitializeComponent();
        Moments.ItemsSource = _moments;
        Moments.ItemTemplate = new FuncDataTemplate<(string Moment, IBrush Tint, string Detail)>((entry, _) => MomentLine(entry));

        var initial = Presto.Build().Opacity(from: 0, to: 1).Y(from: 18, to: 0).Delay(Presto.Stagger(70)).Duration(460).Ease(Eases.OutQuart)
            .OnBegin(timer => Record("Initial", InitialTint, RowOf(timer)));
        var enter = Presto.Build().Opacity(from: 0, to: 1).X(from: -40, to: 0).Duration(380).Ease(Eases.OutBack(1.4))
            .OnBegin(timer => Record("Enter", EnterTint, RowOf(timer)));
        var exit = Presto.Build().Opacity(to: 0).X(to: 60).Duration(300).Ease(Eases.InQuad)
            .OnBegin(_ => Record("Exit", ExitTint, "a ghost of the removed row"));

        _views =
        [
            View("Inbox", ["Reply to Grace", "Review the pull request", "Book the venue", "Pay the invoice"], initial, enter, exit),
            View("Projects", ["Presto 0.2", "Gallery restyle", "Docs pass"], initial, enter, exit),
            View("Archive · 300 rows, virtualized", [.. Enumerable.Range(1, 300).Select(i => $"Archived task #{i}")], initial, enter, exit, virtualized: true),
        ];
        Host.Content = _views[0].View;
    }

    private (string Title, ObservableCollection<string> Rows, ItemsControl List, Control View) Current => _views[_view];

    /// <remarks>A <see cref="ListBox"/> virtualizes its rows, so scrolling realizes and recycles them, which plays nothing.</remarks>
    private static (string, ObservableCollection<string>, ItemsControl, Control) View(
        string title, string[] rows, AnimationBuilder initial, AnimationBuilder enter, AnimationBuilder exit, bool virtualized = false)
    {
        var items = new ObservableCollection<string>(rows);
        // Bound rather than set once, because a virtualized list recycles a row's controls for another item.
        var template = new FuncDataTemplate<string>((_, _) =>
        {
            var row = new Border { Classes = { "row" }, Background = InitialTint, Child = new TextBlock { [!TextBlock.TextProperty] = new Binding() } };
            Presence.SetInitial(row, initial);
            Presence.SetEnter(row, enter);
            Presence.SetExit(row, exit);
            return row;
        });
        ItemsControl list = virtualized
            ? new ListBox { Classes = { "archive" }, Height = 260, ItemsSource = items, ItemTemplate = template }
            : new ItemsControl { ClipToBounds = false, ItemsSource = items, ItemTemplate = template };
        var view = new Border
        {
            Classes = { "view" },
            Child = new StackPanel { Children = { new TextBlock { Classes = { "title" }, Text = title }, list } },
        };
        return (title, items, list, view);
    }

    private static string RowOf(Timer timer) =>
        ((Animation)timer).Targets[0] is Control { DataContext: string text } ? $"\"{text}\"" : "a row";

    private void Record(string moment, IBrush tint, string detail)
    {
        _moments.Insert(0, (moment, tint, detail));
        while (_moments.Count > MaxMoments) _moments.RemoveAt(_moments.Count - 1);
    }

    private static Control MomentLine((string Moment, IBrush Tint, string Detail) entry) => new StackPanel
    {
        Orientation = Orientation.Horizontal,
        Spacing = 10,
        Margin = new Thickness(0, 0, 0, 4),
        Children =
        {
            new Border { Classes = { "moment" }, Background = entry.Tint, Child = new TextBlock { Text = entry.Moment } },
            new TextBlock { Text = entry.Detail, VerticalAlignment = VerticalAlignment.Center, Opacity = .8 },
        },
    };

    private void Navigate(object? sender, RoutedEventArgs e)
    {
        Record("Unload", UnloadTint, $"{Current.Title} left: nothing plays");
        _view = (_view + 1) % _views.Length;
        Host.Content = Current.View;
    }

    private void ShowAgain(object? sender, RoutedEventArgs e)
    {
        var view = Current.View;
        Record("Unload", UnloadTint, $"{Current.Title} left: nothing plays");
        // Detaching unloads the view at once, so putting it straight back loads it again without an empty frame between.
        Host.Content = null;
        Host.Content = view;
    }

    private void AddRow(object? sender, RoutedEventArgs e)
    {
        var index = ShownRange() is { } shown ? Random.Shared.Next(shown.First, shown.Last + 1) : 0;
        Current.Rows.Insert(index, $"New task #{++_next}");
    }

    private void RemoveRow(object? sender, RoutedEventArgs e)
    {
        if (ShownRange() is { } shown) Current.Rows.RemoveAt(Random.Shared.Next(shown.First, shown.Last + 1));
    }

    /// <summary>
    /// The indices of the rows wholly on screen, so a change lands where it can be seen in a scrolled list; a virtualizing
    /// panel also keeps rows realized just outside its viewport.
    /// </summary>
    private (int First, int Last)? ShownRange()
    {
        var list = Current.List;
        var shown = list.GetRealizedContainers()
            .Where(container => container.TranslatePoint(default, list) is { } top && top.Y >= 0 && top.Y + container.Bounds.Height <= list.Bounds.Height)
            .Select(list.IndexFromContainer)
            .Where(index => index >= 0)
            .ToList();
        return shown.Count == 0 ? null : (shown.Min(), shown.Max());
    }
}
