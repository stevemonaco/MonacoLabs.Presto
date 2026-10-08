using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Data;

namespace Monaco.Presto.AvaloniaBridge.Targets;

/// <summary>
/// Holds a visual's <see cref="Visual.IsVisible"/> at <see cref="BindingPriority.Animation"/> on behalf of any number of
/// owners. The owner that last changed its value wins; once every owner releases, the binding is disposed and the value
/// underneath shows again.
/// </summary>
internal sealed class VisibilityHold
{
    private static readonly ConditionalWeakTable<Visual, VisibilityHold> s_holds = new();
    private readonly List<(object Owner, bool Visible)> _owners = [];
    private readonly AnimatedValue<bool> _value;

    private VisibilityHold(Visual visual) => _value = new AnimatedValue<bool>(visual, Visual.IsVisibleProperty);

    public static void Hold(Visual visual, object owner, bool visible)
    {
        if (!s_holds.TryGetValue(visual, out var hold)) s_holds.Add(visual, hold = new VisibilityHold(visual));
        var index = hold.IndexOf(owner);
        if (index >= 0)
        {
            if (hold._owners[index].Visible == visible) return;
            hold._owners.RemoveAt(index);
        }
        hold._owners.Add((owner, visible));
        hold._value.Push(visible);
    }

    public static void Release(Visual visual, object owner)
    {
        if (!s_holds.TryGetValue(visual, out var hold) || hold.IndexOf(owner) is not (>= 0 and var index)) return;
        hold._owners.RemoveAt(index);
        if (hold._owners.Count > 0)
        {
            hold._value.Push(hold._owners[^1].Visible);
            return;
        }
        s_holds.Remove(visual);
        hold._value.Dispose();
    }

    private int IndexOf(object owner)
    {
        for (var i = 0; i < _owners.Count; i++)
        {
            if (ReferenceEquals(_owners[i].Owner, owner)) return i;
        }
        return -1;
    }
}
