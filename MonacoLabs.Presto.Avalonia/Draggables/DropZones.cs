using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;

namespace MonacoLabs.Presto.Draggables;

/// <summary>The drop zones of each <see cref="TopLevel"/>, in registration order, so the last one registered is topmost.</summary>
internal static class DropZones
{
    private static readonly ConditionalWeakTable<TopLevel, List<Control>> s_zones = new();

    private static readonly AttachedProperty<List<Control>?> RegistrationProperty =
        AvaloniaProperty.RegisterAttached<Control, List<Control>?>("Registration", typeof(DropZones));

    /// <summary>Raised when a registered zone detaches, loses its group or is disabled.</summary>
    internal static event Action<Control>? Left;

    internal static List<Control>? Of(TopLevel topLevel) => s_zones.TryGetValue(topLevel, out var zones) ? zones : null;

    internal static bool IsUsable(Control zone, string group) =>
        Drop.GetGroup(zone) == group && Drop.GetIsEnabled(zone) && zone.IsEffectivelyVisible;

    internal static void Register(Control zone)
    {
        if (TopLevel.GetTopLevel(zone) is not { } topLevel) return;
        var zones = s_zones.GetOrCreateValue(topLevel);
        zones.Add(zone);
        zone.SetValue(RegistrationProperty, zones);
    }

    internal static void Unregister(Control zone)
    {
        if (zone.GetValue(RegistrationProperty) is not { } zones) return;
        zones.Remove(zone);
        zone.ClearValue(RegistrationProperty);
        Left?.Invoke(zone);
    }

    internal static void Leave(Control zone)
    {
        if (zone.GetValue(RegistrationProperty) is not null) Left?.Invoke(zone);
    }
}
