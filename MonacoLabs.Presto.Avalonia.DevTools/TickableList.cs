using System.Collections.ObjectModel;
using System.Linq.Expressions;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using MonacoLabs.Presto.Diagnostics;

namespace MonacoLabs.Presto.DevTools;

/// <summary>
/// The tree of registered roots, their timeline children and their tweens, flattened into a virtualizing list. Rows are
/// kept per timer or tween and updated in place, and recently ended roots follow the live ones, dimmed.
/// </summary>
internal sealed class TickableList : DockPanel
{
    private static readonly FuncValueConverter<bool, double> s_dimmed = new(ended => ended ? 0.45 : 1);
    private static readonly FuncValueConverter<bool, Transform> s_quarterTurn = new(expanded => new RotateTransform(expanded ? 90 : 0));

    private readonly Dictionary<object, TickableRow> _rows = new(ReferenceEqualityComparer.Instance);
    private readonly List<TickableRow> _visible = [];
    private readonly ObservableCollection<TickableRow> _items = [];
    private TickableRow? _selected;
    private bool _syncing;
    private int _generation;

    internal TickableList()
    {
        StateFilter.ItemsSource = new[] { "All states" }.Concat(Enum.GetNames<TickableState>().Select(n => n.ToLowerInvariant())).ToArray();
        StateFilter.SelectedIndex = 0;
        var filters = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Margin = new Thickness(6, 2),
            Children = { StateFilter, HideInternal, CaptureTraces, Search, Pick },
        };
        SetDock(filters, Dock.Top);
        Children.Add(filters);

        CaptureTraces.IsCheckedChanged += (_, _) => PrestoDiagnostics.CaptureCreationTrace = CaptureTraces.IsChecked == true;

        Rows.ItemsSource = _items;
        Rows.ItemTemplate = new FuncDataTemplate<TickableRow>((_, _) => RowView());
        Rows.Styles.Add(new Style(x => x.OfType<ListBoxItem>())
        {
            Setters = { new Setter(TemplatedControl.PaddingProperty, new Thickness(4, 1)), new Setter(MinHeightProperty, 0d) },
        });
        Rows.Styles.Add(new Style(x => x.OfType<ToggleButton>().Class(":checked").Template().OfType<ContentPresenter>())
        {
            Setters = { new Setter(ContentPresenter.BackgroundProperty, Brushes.Transparent) },
        });
        Rows.SelectionChanged += (_, _) =>
        {
            if (!_syncing) _selected = Rows.SelectedItem as TickableRow;
        };
        Rows.PointerMoved += (_, e) => HoveredRow = (e.Source as StyledElement)?.DataContext as TickableRow;
        Rows.PointerExited += (_, _) => HoveredRow = null;
        Children.Add(Rows);
    }

    internal ComboBox StateFilter { get; } = new() { FontSize = 12, MinHeight = 0, Padding = new Thickness(6, 1) };

    internal CheckBox HideInternal { get; } = new() { Content = "Hide bridge-internal", IsChecked = true, FontSize = 12, MinHeight = 0 };

    /// <summary>Sets <see cref="PrestoDiagnostics.CaptureCreationTrace"/>, so roots created from then on record where they were created.</summary>
    internal CheckBox CaptureTraces { get; } = new() { Content = "Capture traces", IsChecked = PrestoDiagnostics.CaptureCreationTrace, FontSize = 12, MinHeight = 0 };

    internal TextBox Search { get; } = new() { PlaceholderText = "Search id, target, property", Width = 220, FontSize = 12, MinHeight = 0, Padding = new Thickness(6, 2) };

    internal PickMode Pick { get; } = new();

    internal ListBox Rows { get; } = new();

    internal TickableRow? HoveredRow { get; private set; }

    internal TickableRow? SelectedRow => _selected;

    internal IReadOnlyList<TickableRow> Items => _items;

    /// <summary>The selected timer, or the timer that owns the selected tween.</summary>
    internal Timer? SelectedTimer => _selected?.Timer ?? _selected?.Tween?.Parent;

    internal Tween? SelectedTween => _selected?.Tween;

    /// <summary>Polls the registry and every shown row; the overlay calls it once per frame.</summary>
    internal void Refresh()
    {
        var diagnostics = Presto.Engine.Diagnostics;
        _generation++;
        _visible.Clear();
        foreach (var root in diagnostics.Roots)
        {
            if (!HasEnded(root)) AddRoot(root, ended: false);
        }
        var ended = diagnostics.RecentlyEnded;
        for (var i = ended.Count - 1; i >= 0; i--)
        {
            if (HasEnded(ended[i])) AddRoot(ended[i], ended: true);
        }
        foreach (var (key, row) in _rows)
        {
            if (row.Generation != _generation) _rows.Remove(key);
        }
        Sync();
    }

    private static bool HasEnded(Timer root) => root.Completed || root.Canceled;

    private void AddRoot(Timer root, bool ended)
    {
        var row = Row(root, 0);
        row.Update(null, ended);
        if (HideInternal.IsChecked == true && root.Origin != null) return;
        if (StateFilter.SelectedIndex > 0 && row.State != (TickableState)(StateFilter.SelectedIndex - 1)) return;
        if (Search.Text is { Length: > 0 } search && !row.Matches(search)) return;
        if (Pick.Picked != null && !Pick.Matches(root)) return;
        _visible.Add(row);
        if (row.IsExpanded) AddChildren(row, root, 1, ended);
    }

    private void AddChildren(TickableRow parent, Timer timer, int depth, bool ended)
    {
        foreach (var child in timer.Children)
        {
            var row = Row(child, depth);
            row.Update(parent.State, ended);
            _visible.Add(row);
            if (row.IsExpanded) AddChildren(row, child, depth + 1, ended);
        }
        foreach (var tween in timer.Tweens)
        {
            var row = Row(tween, depth);
            row.Update(parent.State, ended);
            _visible.Add(row);
        }
    }

    private TickableRow Row(object source, int depth)
    {
        ref var row = ref CollectionsMarshal.GetValueRefOrAddDefault(_rows, source, out _);
        row ??= source is Timer timer ? new TickableRow(timer, depth) : new TickableRow((Tween)source, depth);
        row.Generation = _generation;
        return row;
    }

    private void Sync()
    {
        _syncing = true;
        for (var i = 0; i < _visible.Count; i++)
        {
            var row = _visible[i];
            if (i < _items.Count && _items[i] == row) continue;
            var at = _items.IndexOf(row);
            if (at >= 0) _items.Move(at, i);
            else _items.Insert(i, row);
        }
        while (_items.Count > _visible.Count) _items.RemoveAt(_items.Count - 1);
        if (_selected != null && Rows.SelectedItem != _selected && _items.Contains(_selected)) Rows.SelectedItem = _selected;
        _syncing = false;
    }

    private static Control RowView()
    {
        var expand = new ToggleButton
        {
            Width = 16, Height = 16, Padding = new Thickness(0), MinHeight = 0, FontSize = 10,
            Background = Brushes.Transparent, BorderThickness = new Thickness(0),
            [!ToggleButton.IsCheckedProperty] = CompiledBinding.Create((TickableRow r) => r.IsExpanded, mode: BindingMode.TwoWay),
            [!IsVisibleProperty] = CompiledBinding.Create((TickableRow r) => r.IsExpandable),
        };
        expand.Content = new TextBlock { Text = "▸", [!TextBlock.RenderTransformProperty] = CompiledBinding.Create((TickableRow r) => r.IsExpanded, converter: s_quarterTurn) };
        var head = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 4,
            [!MarginProperty] = CompiledBinding.Create((TickableRow r) => r.Indent),
            Children =
            {
                new Panel { Width = 16, Children = { expand } },
                Text(r => r.Glyph, 0.7),
                Text(r => r.Label, 1),
                Chip(r => r.Origin, Colors.SteelBlue),
                Chip(r => r.ScopeName, Colors.SeaGreen),
            },
        };
        var progress = new ProgressBar { Minimum = 0, Maximum = 1, MinWidth = 0, Height = 4, MinHeight = 0, VerticalAlignment = VerticalAlignment.Center, [!RangeBase.ValueProperty] = CompiledBinding.Create((TickableRow r) => r.Progress) };
        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("220,160,*,80,100,50"),
            [!OpacityProperty] = CompiledBinding.Create((TickableRow r) => r.IsEnded, converter: s_dimmed),
            Children = { head, Text(r => r.Target, 1), Text(r => r.Detail, 0.8), Text(r => r.StateText, 0.8), progress, Text(r => r.Iteration, 0.7) },
        };
        for (var i = 0; i < grid.Children.Count; i++) Grid.SetColumn(grid.Children[i], i);
        progress.Margin = new Thickness(0, 0, 8, 0);
        return grid;
    }

    private static Border Chip(Expression<Func<TickableRow, string?>> path, Color color) => new()
    {
        CornerRadius = new CornerRadius(3), Padding = new Thickness(4, 0), Background = new SolidColorBrush(color, 0.35),
        [!IsVisibleProperty] = CompiledBinding.Create(path, converter: ObjectConverters.IsNotNull),
        Child = Text(path, 1),
    };

    private static TextBlock Text(Expression<Func<TickableRow, string?>> path, double opacity)
    {
        var text = DevToolsOverlay.Cell(opacity);
        text.Bind(TextBlock.TextProperty, CompiledBinding.Create(path));
        return text;
    }
}
