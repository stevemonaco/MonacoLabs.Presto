using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;

namespace Monaco.Presto.AvaloniaBridge.DevTools;

/// <summary>
/// The DevTools panel, docked to the bottom of the window's <see cref="OverlayLayer"/>, resizable from its top edge and
/// collapsible to the transport bar. It uses no Presto animations and refreshes from <see cref="TopLevel.RequestAnimationFrame"/>,
/// so freezing the engine leaves it responsive and the inspector never shows up in what it inspects.
/// </summary>
internal sealed class DevToolsOverlay : Panel
{
    private const double MinDockHeight = 80;

    private readonly Border _dock;
    private readonly Border _grip;
    private readonly Action<TimeSpan> _onFrame;
    private double _dockHeight = 260;
    private Control? _layer;
    private TopLevel? _top;

    internal DevToolsOverlay(Action close)
    {
        _onFrame = OnFrame;
        ZIndex = int.MaxValue;
        Resources.ThemeDictionaries[ThemeVariant.Light] = Palette("#F3F3F3", "#D0D0D0");
        Resources.ThemeDictionaries[ThemeVariant.Dark] = Palette("#202020", "#3C3C3C");

        var closeButton = Compact(new Button { Content = "Close" });
        closeButton.Click += (_, _) => close();
        var collapse = Compact(new ToggleButton { Content = "Collapse" });
        collapse.IsCheckedChanged += (_, _) => Collapse(collapse.IsChecked == true);
        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, Margin = new Thickness(6, 4), Children = { collapse, closeButton } };
        DockPanel.SetDock(actions, Dock.Right);
        var header = new DockPanel { Children = { actions, Transport } };

        _grip = new Border { Height = 5, Background = Brushes.Transparent, Cursor = new Cursor(StandardCursorType.SizeNorthSouth) };
        DockPanel.SetDock(_grip, Dock.Top);
        DockPanel.SetDock(header, Dock.Top);

        _dock = new Border
        {
            VerticalAlignment = VerticalAlignment.Bottom,
            Height = _dockHeight,
            BorderThickness = new Thickness(0, 1, 0, 0),
            Child = new DockPanel { Children = { _grip, header, Body } },
        };
        _dock.Bind(Border.BackgroundProperty, _dock.GetResourceObservable("PrestoDevToolsBackground"));
        _dock.Bind(Border.BorderBrushProperty, _dock.GetResourceObservable("PrestoDevToolsBorder"));
        _grip.PointerMoved += (_, e) =>
        {
            if (!e.GetCurrentPoint(_grip).Properties.IsLeftButtonPressed) return;
            _dockHeight = Math.Clamp(Bounds.Height - e.GetPosition(this).Y, MinDockHeight, Math.Max(MinDockHeight, Bounds.Height));
            _dock.Height = _dockHeight;
        };
        Inspector.Width = 560;
        List.ClipToBounds = true;
        DockPanel.SetDock(Inspector, Dock.Right);
        var springs = new ScrollViewer { MaxHeight = 110, Content = Springs };
        DockPanel.SetDock(springs, Dock.Bottom);
        Body.Children.Add(new DockPanel { Children = { Inspector, springs, List } });
        Children.Add(_dock);
    }

    internal TransportBar Transport { get; } = new();

    /// <summary>The area below the transport bar, hidden while collapsed.</summary>
    internal Panel Body { get; } = new();

    internal TickableList List { get; } = new();

    internal RootInspector Inspector { get; } = new();

    internal SpringsPanel Springs { get; } = new();

    internal TargetHighlighter Highlighter { get; } = new();

    internal static T Compact<T>(T button) where T : Button
    {
        button.Padding = new Thickness(8, 2);
        button.MinHeight = 0;
        button.FontSize = 12;
        button.VerticalAlignment = VerticalAlignment.Center;
        return button;
    }

    internal static TextBlock Cell(double opacity = 1) => new()
    {
        FontSize = 12,
        Opacity = opacity,
        VerticalAlignment = VerticalAlignment.Center,
        TextTrimming = TextTrimming.CharacterEllipsis,
        Margin = new Thickness(0, 0, 8, 0),
    };

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _layer = Parent as Control;
        if (_layer != null)
        {
            _layer.SizeChanged += LayerSizeChanged;
            Fit();
        }
        _top = TopLevel.GetTopLevel(this);
        RequestFrame();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        if (_layer != null) _layer.SizeChanged -= LayerSizeChanged;
        Highlighter.Hide();
        _layer = null;
        _top = null;
    }

    private void Collapse(bool collapsed)
    {
        Body.IsVisible = _grip.IsVisible = !collapsed;
        if (collapsed) Highlighter.Hide();
        _dock.Height = collapsed ? double.NaN : _dockHeight;
    }

    private void RequestFrame() => _top?.RequestAnimationFrame(_onFrame);

    private void OnFrame(TimeSpan _)
    {
        if (_top is null) return;
        Transport.Refresh();
        if (Body.IsVisible)
        {
            List.Refresh();
            Inspector.Refresh(List.SelectedTimer, List.SelectedTween);
            Springs.Refresh();
            Highlighter.Update(_top, List.HoveredRow, List.SelectedRow);
        }
        RequestFrame();
    }

    private void LayerSizeChanged(object? sender, SizeChangedEventArgs e) => Fit();

    private void Fit()
    {
        Width = _layer!.Bounds.Width;
        Height = _layer.Bounds.Height;
    }

    private static ResourceDictionary Palette(string background, string border) => new()
    {
        ["PrestoDevToolsBackground"] = new SolidColorBrush(Color.Parse(background)),
        ["PrestoDevToolsBorder"] = new SolidColorBrush(Color.Parse(border)),
    };
}
