using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;
using MonacoLabs.Presto;
using PrestoGallery.Showcase;
using Timer = MonacoLabs.Presto.Timer;

namespace PrestoGallery.Pages;

/// <summary>
/// Presence's lifecycle side by side: three views, the last a virtualized list, whose rows carry Initial, Enter and Exit,
/// and a log written by each builder's OnBegin, so it records what Presence classified rather than what the page intended.
/// Below, a filtered list whose rows fly to a second list in the same <c>Animate.ItemGroup</c>, a crossfading swapper and a wizard whose host's <c>Presence.Mode</c> makes each step wait for the last to leave.
/// </summary>
public partial class LifecyclePage : UserControl
{
    private static readonly IBrush InitialTint = Brush.Parse("#93c5fd");
    private static readonly IBrush EnterTint = Brush.Parse("#86efac");
    private static readonly IBrush ExitTint = Brush.Parse("#fca5a5");
    private static readonly IBrush UnloadTint = Brush.Parse("#cbd5e1");
    private static readonly IBrush TodoTint = Brush.Parse("#7dd3fc");
    private static readonly IBrush DoneTint = Brush.Parse("#cbd5e1");
    private static readonly string[] Steps = ["AccountStep", "PlanStep", "DoneStep"];
    private static readonly (StaggerFrom From, string Name)[] ExitOrders =
        [(StaggerFrom.First, nameof(StaggerFrom.First)), (StaggerFrom.Last, nameof(StaggerFrom.Last)), (StaggerFrom.Center, nameof(StaggerFrom.Center))];

    private readonly (string Title, ObservableCollection<string> Rows, ItemsControl List, Panel ListAndEmptyLine, Control View)[] _views;
    private readonly (string Initial, string Enter, string Empty) _code;
    private AnimationBuilder _exit;
    private string _exitCode;
    private readonly FilteredView<Chore> _shown;
    private readonly Control[] _panels = [Panel("Inbox", "#7dd3fc"), Panel("Archive", "#fca5a5")];
    private int _view;
    private int _panel;
    private int _next;
    private int _step;

    public LifecyclePage()
    {
        InitializeComponent();
        var (initial, initialCode) = Snippet.Of(Recipes.FadeUp(distance: 18, duration: 460, ease: Eases.OutQuart).Delay(Presto.Stagger(70)));
        var (enter, enterCode) = Snippet.Of(Recipes.SlideIn(Edge.Left, distance: 40, duration: 380, ease: Eases.OutBack(1.4)));
        var (empty, emptyCode) = Snippet.Of(Recipes.Fade(duration: 360));
        _code = (initialCode, enterCode, emptyCode);
        (_exit, _exitCode) = Exit(ExitOrders[0]);
        initial.OnBegin(timer => Moments.Record("Initial", InitialTint, RowOf(timer)));
        enter.OnBegin(timer => Moments.Record("Enter", EnterTint, RowOf(timer)));

        _views =
        [
            View("Inbox", ["Reply to Grace", "Review the pull request", "Book the venue", "Pay the invoice"], initial, enter, empty),
            View("Projects", ["Presto 0.2", "Gallery restyle", "Docs pass"], initial, enter, empty),
            View("Archive · 300 rows, virtualized", [.. Enumerable.Range(1, 300).Select(i => $"Archived task #{i}")], initial, enter, empty, virtualized: true),
        ];
        ExitOrderPicker.ItemsSource = ExitOrders.Select(order => order.Name);
        ExitOrderPicker.SelectedIndex = 0;
        HostModePicker.SelectedIndex = (int)PresenceMode.Wait;
        Host.Content = _views[0].View;
        Swapper.Content = _panels[0];
        WizardCode.Code = $"{Snippets.LifecycleWizard}\n\n{Snippets.LifecycleStep}";
        SwapCode.Code = Snippets.LifecycleSwap;
        _shown = ShowTransferLists();
        TransferCode.Code = Snippets.LifecycleTransfer;
        ModePicker.SelectedIndex = (int)Presence.GetMode(Wizard);
        ShowStep(0);
    }

    private (string Title, ObservableCollection<string> Rows, ItemsControl List, Panel ListAndEmptyLine, Control View) Current => _views[_view];

    private (AnimationBuilder, string) Exit((StaggerFrom From, string Name) order)
    {
        var from = order.From;
        var (exit, code) = Snippet.Of(Presto.Build().Opacity(to: 0).X(to: 60).Duration(300).Ease(Eases.InQuad).Delay(Presto.Stagger(40, new() { From = from })));
        exit.OnBegin(timer => Moments.Record("Exit", ExitTint, RowOf(timer)));
        return (exit, code.Replace("From = from", $"From = StaggerFrom.{order.Name}"));
    }

    /// <remarks>A <see cref="ListBox"/> virtualizes its rows, so scrolling realizes and recycles them, which plays nothing.</remarks>
    private (string, ObservableCollection<string>, ItemsControl, Panel, Control) View(
        string title, string[] rows, AnimationBuilder initial, AnimationBuilder enter, AnimationBuilder empty, bool virtualized = false)
    {
        var items = new ObservableCollection<string>(rows);
        // Bound rather than set once, because a virtualized list recycles a row's controls for another item.
        var template = new FuncDataTemplate<string>((_, _) =>
        {
            var row = new Border { Classes = { "row" }, Background = InitialTint, Child = new TextBlock { [!TextBlock.TextProperty] = new CompiledBinding() } };
            Presence.SetInitial(row, initial);
            Presence.SetEnter(row, enter);
            Presence.SetExit(row, _exit);
            return row;
        });
        ItemsControl list = virtualized
            ? new ListBox { Classes = { "archive" }, Height = 260, ItemsSource = items, ItemTemplate = template }
            : new ItemsControl { ClipToBounds = false, ItemsSource = items, ItemTemplate = template };
        var emptyLine = new TextBlock { Classes = { "empty" }, Text = "No rows", IsVisible = false };
        Presence.SetArrive(emptyLine, empty);
        items.CollectionChanged += (_, _) => emptyLine.IsVisible = items.Count == 0;
        var listAndEmptyLine = new Panel { Children = { list, emptyLine } };
        var view = new Border
        {
            Classes = { "view" },
            Child = new StackPanel { Children = { new TextBlock { Classes = { "title" }, Text = title }, listAndEmptyLine } },
        };
        return (title, items, list, listAndEmptyLine, view);
    }

    private void ExitOrderChanged(object? sender, SelectionChangedEventArgs e)
    {
        (_exit, _exitCode) = Exit(ExitOrders[ExitOrderPicker.SelectedIndex]);
        foreach (var view in _views)
            foreach (var row in view.List.GetVisualDescendants().OfType<Border>().Where(row => row.Classes.Contains("row")))
                Presence.SetExit(row, _exit);
        PrintRowCode();
    }

    private void HostModeChanged(object? sender, SelectionChangedEventArgs e)
    {
        foreach (var view in _views) Presence.SetMode(view.ListAndEmptyLine, (PresenceMode)HostModePicker.SelectedIndex);
        PrintRowCode();
    }

    private void PrintRowCode() => RowCode.Code = $"""
        Presence.SetInitial(row, {_code.Initial});
        Presence.SetEnter(row, {_code.Enter});
        Presence.SetExit(row, {_exitCode});
        Presence.SetMode(listAndEmptyLine, PresenceMode.{(PresenceMode)HostModePicker.SelectedIndex});
        Presence.SetArrive(emptyLine, {_code.Empty});
        """;

    private static string RowOf(Timer timer) =>
        ((Animation)timer).Targets[0] is Control { DataContext: string text } ? $"\"{text}\"" : "a row";

    private void Navigate(object? sender, RoutedEventArgs e)
    {
        Moments.Record("Unload", UnloadTint, $"{Current.Title} left: nothing plays");
        _view = (_view + 1) % _views.Length;
        Host.Content = Current.View;
    }

    private void ShowAgain(object? sender, RoutedEventArgs e)
    {
        var view = Current.View;
        Moments.Record("Unload", UnloadTint, $"{Current.Title} left: nothing plays");
        // Detaching unloads the view at once, so putting it straight back loads it again without an empty frame between.
        Host.Content = null;
        Host.Content = view;
    }

    private void WizardBack(object? sender, RoutedEventArgs e) => ShowStep(_step - 1);

    private void WizardNext(object? sender, RoutedEventArgs e) => ShowStep(_step + 1);

    private void ShowStep(int step)
    {
        _step = step;
        Wizard.Content = ((IDataTemplate)Resources[Steps[step]]!).Build(null);
        BackButton.IsEnabled = step > 0;
        NextButton.IsEnabled = step < Steps.Length - 1;
    }

    private void ModeChanged(object? sender, SelectionChangedEventArgs e) => Presence.SetMode(Wizard, (PresenceMode)ModePicker.SelectedIndex);

    #region snippet LifecycleSwap
    private void Swap(object? sender, RoutedEventArgs e) => Swapper.Content = _panels[_panel = 1 - _panel];

    private static Control Panel(string title, string color)
    {
        var panel = new Border
        {
            Classes = { "panel" },
            Background = SolidColorBrush.Parse(color),
            Child = new TextBlock { Text = title, FontSize = 22, FontWeight = FontWeight.SemiBold, VerticalAlignment = VerticalAlignment.Bottom },
        };
        Presence.SetArrive(panel, Recipes.FadeUp(distance: 16, duration: 360, ease: Eases.OutQuart));
        Presence.SetExit(panel, Recipes.FadeOutUp(distance: 16, duration: 360, ease: Eases.InQuad));
        return panel;
    }
    #endregion

    #region snippet LifecycleTransfer
    private sealed record Chore(string Title, string Tag);

    private readonly ObservableCollection<Chore> _open =
    [
        new("Water the plants", "Home"), new("Send the report", "Work"), new("Pick up the parcel", "Errand"),
        new("Fix the shelf", "Home"), new("Plan the sprint", "Work"), new("Buy coffee beans", "Errand"),
    ];
    private readonly ObservableCollection<Chore> _done = [];

    private FilteredView<Chore> ShowTransferLists()
    {
        var shown = new FilteredView<Chore>(_open);
        OpenList.ItemsSource = shown;
        DoneList.ItemsSource = _done;
        foreach (var (list, tint) in new[] { (OpenList, TodoTint), (DoneList, DoneTint) })
        {
            Animate.SetItemLayout(list, Recipes.Reflow());
            Animate.SetItemGroup(list, "lifecycle");
            list.ItemTemplate = new FuncDataTemplate<Chore>((chore, _) => ChoreRow(chore, tint));
        }
        return shown;
    }

    private Border ChoreRow(Chore chore, IBrush tint)
    {
        var row = new Border
        {
            Classes = { "row" },
            Background = tint,
            Child = new DockPanel { Children = { new TextBlock { [DockPanel.DockProperty] = Dock.Right, Text = chore.Tag, Opacity = 0.6 }, new TextBlock { Text = chore.Title } } },
        };
        Presence.SetEnter(row, Recipes.FadeUp(distance: 12, duration: 320, ease: Eases.OutQuart));
        Presence.SetExit(row, Recipes.FadeOutUp(distance: 12, duration: 260, ease: Eases.InQuad));
        row.Tapped += (_, _) => Transfer(chore);
        return row;
    }

    private void Transfer(Chore chore)
    {
        var (from, to) = _done.Contains(chore) ? (_done, _open) : (_open, _done);
        from.Remove(chore);
        to.Add(chore);
    }

    private void TagChanged(object? sender, RoutedEventArgs e)
    {
        if (sender is RadioButton { IsChecked: true, Content: string tag })
            _shown.Filter = tag == "All" ? null : chore => chore.Tag == tag;
    }
    #endregion

    private void AddRow(object? sender, RoutedEventArgs e)
    {
        // A virtualizing panel keeps its first realized row anchored, so an item inserted at its index lands above the viewport.
        var index = ShownRange() is { } shown ? Random.Shared.Next(shown.First, shown.Last) + 1 : 0;
        Current.Rows.Insert(index, $"New task #{++_next}");
    }

    private void ClearRows(object? sender, RoutedEventArgs e) => Current.Rows.Clear();

    private void RemoveRow(object? sender, RoutedEventArgs e)
    {
        if (ShownRange() is { } shown) Current.Rows.RemoveAt(Random.Shared.Next(shown.First, shown.Last + 1));
    }

    private void RemoveThree(object? sender, RoutedEventArgs e)
    {
        if (ShownRange() is not { } shown) return;
        // Last first, so the earlier indices still name the same rows.
        foreach (var index in new[] { 4, 2, 0 }.Select(step => shown.First + step).Where(index => index <= shown.Last))
            Current.Rows.RemoveAt(index);
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
