using System.Runtime.CompilerServices;
using Avalonia.Controls;

namespace MonacoLabs.Presto.Gestures;

/// <summary>The one pointer gesture driving each control, so a <c>Draggable</c> and a <c>Transformable</c> never fight over a target.</summary>
internal static class GestureOwners
{
    private static readonly ConditionalWeakTable<Control, object> s_owners = new();

    public static void Claim(Control target, object owner)
    {
        if (s_owners.TryGetValue(target, out var current) && current.GetType() != owner.GetType())
        {
            throw new InvalidOperationException(
                $"This {target.GetType().Name} is already driven by a {current.GetType().Name}; revert it before creating a {owner.GetType().Name} on it.");
        }
        s_owners.AddOrUpdate(target, owner);
    }

    public static void Release(Control target, object owner)
    {
        if (s_owners.TryGetValue(target, out var current) && ReferenceEquals(current, owner)) s_owners.Remove(target);
    }
}
