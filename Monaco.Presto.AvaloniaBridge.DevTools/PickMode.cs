using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.VisualTree;
using Monaco.Presto.AvaloniaBridge.Targets;

namespace Monaco.Presto.AvaloniaBridge.DevTools;

/// <summary>
/// The Pick toggle: while it is on, the next press on an app control is consumed and picks that control, and the list
/// shows only the roots with a tween on it or on a descendant. The clear button drops the pick.
/// </summary>
internal sealed class PickMode : StackPanel
{
    private TopLevel? _top;

    internal PickMode()
    {
        Orientation = Orientation.Horizontal;
        Spacing = 4;
        Toggle.IsCheckedChanged += (_, _) => Arm(Toggle.IsChecked == true);
        Clear.Click += (_, _) => Picked = null;
        Children.Add(Toggle);
        Children.Add(Clear);
    }

    internal ToggleButton Toggle { get; } = DevToolsOverlay.Compact(new ToggleButton { Content = "Pick" });

    internal Button Clear { get; } = DevToolsOverlay.Compact(new Button { IsVisible = false });

    internal Visual? Picked
    {
        get;
        private set
        {
            field = value;
            Clear.Content = value is null ? null : $"✕ {TickableRow.Summarize(value)}";
            Clear.IsVisible = value != null;
        }
    }

    /// <summary>Whether <paramref name="root"/> or one of its children has a tween whose target resolves to <see cref="Picked"/> or a descendant.</summary>
    internal bool Matches(Timer root)
    {
        foreach (var tween in TickableRow.AllTweens(root))
        {
            if (TargetOwner.Find(tween.Target) is { } visual && (visual == Picked || Picked!.IsVisualAncestorOf(visual))) return true;
        }
        return false;
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        Toggle.IsChecked = false;
    }

    private void Arm(bool armed)
    {
        _top?.RemoveHandler(PointerPressedEvent, OnPressed);
        _top = armed ? TopLevel.GetTopLevel(this) : null;
        _top?.AddHandler(PointerPressedEvent, OnPressed, RoutingStrategies.Tunnel, handledEventsToo: true);
    }

    private void OnPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.Source is not Visual hit || hit.FindAncestorOfType<DevToolsOverlay>(includeSelf: true) != null) return;
        e.Handled = true;
        Picked = hit;
        Toggle.IsChecked = false;
    }
}
