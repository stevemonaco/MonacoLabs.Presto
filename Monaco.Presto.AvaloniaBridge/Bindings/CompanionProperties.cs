using Avalonia;

namespace Monaco.Presto.AvaloniaBridge.Bindings;

/// <summary>
/// The hidden, untyped attached property a value-driven binding resolves its source on, one per animated property, so
/// the source sees the control's own <c>DataContext</c>, name scope and templated parent.
/// </summary>
internal sealed class CompanionProperties
{
    private static readonly Dictionary<AvaloniaProperty, AttachedProperty<object?>> s_companions = [];

    private CompanionProperties()
    {
    }

    public static AttachedProperty<object?> For(AvaloniaProperty property)
    {
        if (!s_companions.TryGetValue(property, out var companion))
        {
            // Registered on first use, since the animated properties are not known up front.
#pragma warning disable AVP1001
            s_companions[property] = companion = AvaloniaProperty.RegisterAttached<CompanionProperties, AvaloniaObject, object?>($"{property.OwnerType.Name}{property.Name}");
#pragma warning restore AVP1001
        }
        return companion;
    }
}
