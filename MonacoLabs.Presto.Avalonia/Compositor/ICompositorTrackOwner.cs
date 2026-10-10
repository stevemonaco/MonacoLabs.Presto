using Avalonia;

namespace MonacoLabs.Presto.Compositor;

/// <summary>Something the <see cref="CompositorRegistry"/> can hand a composition property back from: an animation or a layout transition.</summary>
internal interface ICompositorTrackOwner
{
    /// <summary>Lets go of <paramref name="kind"/> on <paramref name="visual"/>, which another owner is taking over.</summary>
    void Release(Visual visual, CompositorTrackKind kind);
}
