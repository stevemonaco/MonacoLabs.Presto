using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Monaco.Presto;
using Monaco.Presto.AvaloniaBridge;

namespace PrestoSample.Pages;

public partial class PresencePage : UserControl
{
    public static readonly AnimationBuilder Enter =
        Presto.Build().Opacity(from: 0, to: 1).X(from: -24, to: 0).Duration(320).Ease(Eases.OutQuart);

    public static readonly AnimationBuilder Leave =
        Presto.Build().Opacity(to: 0).X(to: 80).Scale(to: .9).Duration(320).Ease(Eases.InQuad);

    private static readonly string[] Chores =
        ["Water the plants", "Reply to Grace", "Book the dentist", "Fix the bike light", "Order coffee beans", "Back up photos", "Call the plumber"];

    private readonly Scope _scope = new();
    private readonly Control[] _views = [View("Inbox", "#7dd3fc"), View("Archive", "#fca5a5")];
    private int _next;
    private int _view;

    public PresencePage()
    {
        InitializeComponent();
        for (var i = 0; i < 4; i++) Items.Add(NextChore());
        Tasks.ItemsSource = Items;
        Tasks.ContainerPrepared += (_, e) => _scope.Execute(_ => e.Container.AnimateLayout(320, Eases.OutQuint));
        Swapper.Content = _views[0];
    }

    public ObservableCollection<string> Items { get; } = [];

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _scope.Revert();
    }

    private string NextChore() => $"{Chores[_next % Chores.Length]} #{++_next}";

    private void AddTask(object? sender, RoutedEventArgs e) => Items.Insert(Random.Shared.Next(Items.Count + 1), NextChore());

    private void RemoveRandom(object? sender, RoutedEventArgs e)
    {
        if (Items.Count > 0) Items.RemoveAt(Random.Shared.Next(Items.Count));
    }

    private void ClearTasks(object? sender, RoutedEventArgs e) => Items.Clear();

    private void RemoveTask(object? sender, RoutedEventArgs e)
    {
        if (sender is Control { DataContext: string task }) Items.Remove(task);
    }

    private void Swap(object? sender, RoutedEventArgs e) => Swapper.Content = _views[_view = 1 - _view];

    private static Control View(string title, string color)
    {
        var view = new Border
        {
            Classes = { "panel" },
            Background = SolidColorBrush.Parse(color),
            Child = new TextBlock { Text = title, FontSize = 22, FontWeight = FontWeight.SemiBold, VerticalAlignment = VerticalAlignment.Bottom },
        };
        Animate.SetOnAttached(view, Presto.Build().Opacity(from: 0, to: 1).Y(from: 16, to: 0).Duration(360).Ease(Eases.OutQuart));
        Presence.SetExit(view, Presto.Build().Opacity(to: 0).Y(to: -16).Duration(360).Ease(Eases.InQuad));
        return view;
    }
}
