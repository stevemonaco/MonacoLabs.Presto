using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;

namespace MonacoLabs.Presto.Draggables;

/// <summary>Attached properties that make a control a drop zone for draggables whose <c>DropGroup</c> names its group.</summary>
public static class Drop
{
    /// <summary>The group the control is a drop zone of, while it is attached to a <see cref="TopLevel"/>; null makes it no zone.</summary>
    public static readonly AttachedProperty<string?> GroupProperty =
        AvaloniaProperty.RegisterAttached<Control, string?>("Group", typeof(Drop));

    /// <summary>Whether draggables consider the zone; true by default.</summary>
    public static readonly AttachedProperty<bool> IsEnabledProperty =
        AvaloniaProperty.RegisterAttached<Control, bool>("IsEnabled", typeof(Drop), defaultValue: true);

    static Drop()
    {
        GroupProperty.Changed.AddClassHandler<Control>(HandleGroupChanged);
        IsEnabledProperty.Changed.AddClassHandler<Control>((zone, e) =>
        {
            if (e.NewValue is false) DropZones.Leave(zone);
        });
    }

    /// <summary>Gets the drop group of <paramref name="control"/>.</summary>
    public static string? GetGroup(Control control) => control.GetValue(GroupProperty);

    /// <summary>Sets the drop group of <paramref name="control"/>.</summary>
    public static void SetGroup(Control control, string? value) => control.SetValue(GroupProperty, value);

    /// <summary>Gets whether draggables consider <paramref name="control"/> as a zone.</summary>
    public static bool GetIsEnabled(Control control) => control.GetValue(IsEnabledProperty);

    /// <summary>Sets whether draggables consider <paramref name="control"/> as a zone.</summary>
    public static void SetIsEnabled(Control control, bool value) => control.SetValue(IsEnabledProperty, value);

    private static void HandleGroupChanged(Control zone, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.OldValue is null)
        {
            zone.AttachedToVisualTree += HandleAttached;
            zone.DetachedFromVisualTree += HandleDetached;
        }
        else if (e.NewValue is null)
        {
            zone.AttachedToVisualTree -= HandleAttached;
            zone.DetachedFromVisualTree -= HandleDetached;
        }
        DropZones.Unregister(zone);
        if (e.NewValue is not null && zone.IsAttachedToVisualTree()) DropZones.Register(zone);
    }

    private static void HandleAttached(object? sender, VisualTreeAttachmentEventArgs e) => DropZones.Register((Control)sender!);

    private static void HandleDetached(object? sender, VisualTreeAttachmentEventArgs e) => DropZones.Unregister((Control)sender!);
}
