using System.Diagnostics;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Logging;
using Monaco.Presto.AvaloniaBridge.Transforms;

namespace Monaco.Presto.AvaloniaBridge.Compositor;

/// <summary>
/// Upstream's <c>WAAPIAnimationsLookups</c>: which <see cref="ICompositorTrackOwner"/> drives each composition property of
/// each visual. Starting a track takes the property over from whoever held it, exactly as <c>addWAAPIAnimation</c> removes
/// the animation it replaces, and an animation left with no tracks cancels.
/// </summary>
/// <remarks>
/// Compositor-only, as upstream's lookups are: UI-thread tweens compose through the core's own replace lookups and know
/// nothing of this. A finished track keeps holding its end value on the server, upstream's <c>persist</c>, so entries live
/// until the animation is canceled, reverted or replaced rather than until it completes.
/// </remarks>
internal static class CompositorRegistry
{
    private static readonly int s_kinds = Enum.GetValues<CompositorTrackKind>().Length;
    private static readonly ConditionalWeakTable<Visual, ICompositorTrackOwner?[]> s_held = new();

    /// <summary>Hands <paramref name="kind"/> on <paramref name="visual"/> to <paramref name="owner"/>, releasing whoever held it.</summary>
    public static void Take(Visual visual, CompositorTrackKind kind, ICompositorTrackOwner owner)
    {
        var held = s_held.GetValue(visual, static _ => new ICompositorTrackOwner?[s_kinds]);
        // A layout transition retakes the property it already holds on every layout pass, which must not release itself.
        if (!ReferenceEquals(held[(int)kind], owner)) held[(int)kind]?.Release(visual, kind);
        held[(int)kind] = owner;
    }

    /// <summary>Whoever drives <paramref name="kind"/> on <paramref name="visual"/>, if anyone.</summary>
    public static ICompositorTrackOwner? Holder(Visual visual, CompositorTrackKind kind) =>
        s_held.TryGetValue(visual, out var held) ? held[(int)kind] : null;

    /// <summary>Drops <paramref name="owner"/>'s claim on <paramref name="kind"/>, if it still holds it.</summary>
    public static void Drop(Visual visual, CompositorTrackKind kind, ICompositorTrackOwner owner)
    {
        if (s_held.TryGetValue(visual, out var held) && held[(int)kind] == owner) held[(int)kind] = null;
    }

    /// <summary>Releases every compositor animation driving <paramref name="visual"/>, or only <paramref name="kind"/> when one is named.</summary>
    public static void Release(Visual visual, CompositorTrackKind? kind)
    {
        if (!s_held.TryGetValue(visual, out var held)) return;
        for (var i = 0; i < held.Length; i++)
        {
            if (kind == null || (int)kind == i) held[i]?.Release(visual, (CompositorTrackKind)i);
        }
    }

    /// <summary>Warns when <paramref name="kind"/> starts on a visual the transform shorthands already drive.</summary>
    [Conditional("DEBUG")]
    public static void WarnIfShorthandsDrive(Visual visual, CompositorTrackKind kind)
    {
        if (kind != CompositorTrackKind.Opacity && RenderTransformComposer.Animates(visual)) Warn(visual, kind.ToString());
    }

    /// <summary>Warns when <paramref name="shorthand"/> starts on a visual a compositor animation already drives.</summary>
    [Conditional("DEBUG")]
    public static void WarnIfCompositorDrives(Visual visual, TransformShorthand shorthand)
    {
        if (DrivesTransform(visual)) Warn(visual, shorthand.ToString());
    }

    private static bool DrivesTransform(Visual visual)
    {
        if (!s_held.TryGetValue(visual, out var held)) return false;
        for (var i = 0; i < held.Length; i++)
        {
            if (held[i] != null && (CompositorTrackKind)i != CompositorTrackKind.Opacity) return true;
        }
        return false;
    }

    private static void Warn(Visual visual, string property) =>
        Logger.TryGet(LogEventLevel.Warning, LogArea.Animations)?.Log(
            visual,
            "Presto animates '{Property}' of this {Type} on the compositor and on the UI thread at once; the render transform and the composition transform multiply rather than replace each other.",
            property,
            visual.GetType().Name);
}
