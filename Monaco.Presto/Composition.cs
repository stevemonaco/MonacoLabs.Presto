namespace Monaco.Presto;

/// <summary>How a new tween interacts with running tweens on the same target property (upstream <c>compositionTypes</c>).</summary>
public enum Composition
{
    /// <summary>Overrides the running tweens on that property, the default (upstream <c>'replace'</c>).</summary>
    Replace = 0,

    /// <summary>Leaves the running tweens alone; the fastest mode, since nothing is composed (upstream <c>'none'</c>).</summary>
    None = 1,

    /// <summary>Adds to the running tweens on that property instead of overriding them (upstream <c>'blend'</c>).</summary>
    /// <remarks>
    /// While a blended tween is live, the additive pass owns the property and overwrites a <see cref="Replace"/> tween on it
    /// in the same frame. A blended tween's <c>To</c> sets the base the additive group composes from.
    /// </remarks>
    Blend = 2,
}
