namespace MonacoLabs.Presto;

/// <summary>How a new tween interacts with running tweens on the same target property.</summary>
public enum Composition
{
    /// <summary>Overrides the running tweens on that property, the default.</summary>
    Replace = 0,

    /// <summary>Leaves the running tweens on that property alone, so each writes in turn; the fastest mode, since nothing is composed.</summary>
    Overlap = 1,

    /// <summary>Sums onto the running tweens on that property through the engine's additive pass instead of overriding them.</summary>
    /// <remarks>
    /// While an additive tween is live, the additive pass owns the property and overwrites a <see cref="Replace"/> tween on it
    /// in the same frame. An additive tween's <c>To</c> sets the base the additive group composes from.
    /// </remarks>
    Additive = 2,
}
