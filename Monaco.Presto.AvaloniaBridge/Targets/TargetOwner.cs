using System.Runtime.CompilerServices;
using Avalonia;
using Monaco.Presto.Diagnostics;

namespace Monaco.Presto.AvaloniaBridge.Targets;

/// <summary>
/// Maps a tween target back to the <see cref="Visual"/> it draws on, for inspectors: a visual is its own owner, and the
/// brushes and transforms the bridge creates for a visual are recorded as they are made, but only while
/// <see cref="PrestoDiagnostics.Active"/>. Split text pieces are visuals inside their block, so they need no record.
/// </summary>
internal static class TargetOwner
{
    private static readonly ConditionalWeakTable<object, WeakReference<Visual>> s_owners = new();

    public static Visual? Find(object target) =>
        target as Visual ?? (s_owners.TryGetValue(target, out var owner) && owner.TryGetTarget(out var visual) ? visual : null);

    public static void Record(object owned, object owner)
    {
        if (PrestoDiagnostics.Active && owner is Visual visual) s_owners.AddOrUpdate(owned, new WeakReference<Visual>(visual));
    }
}
