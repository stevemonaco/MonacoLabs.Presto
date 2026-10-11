namespace MonacoLabs.Presto.Draggables;

/// <summary>
/// One axis of a <see cref="PrestoAvalonia.CreateDraggable"/>: the property it drags and how that value is treated.
/// Upstream's <c>x: { mapTo, modifier, composition, snap }</c>.
/// </summary>
public sealed record DraggableAxisParams
{
    /// <summary>The property the axis writes: the transform shorthand "X" or "Y" by default, "Canvas.Left" to drag a layout property.</summary>
    public string? MapTo { get; init; }

    /// <summary>Upstream's <c>x: false</c>: the axis is still read through <see cref="Draggable.X"/>, but dragging never writes it.</summary>
    public bool Enabled { get; init; } = true;

    /// <summary>Snaps this axis, overriding <see cref="DraggableParams.Snap"/>.</summary>
    public Func<double, double>? Snap { get; init; }

    /// <summary>Post-processes this axis's value, in place of <see cref="DraggableParams.Modifier"/>.</summary>
    /// <remarks>
    /// Must be idempotent, like rounding or wrapping: from the release on it is re-applied to its own output, so a curve such as a
    /// rubber band jumps at release and settles short of the snap point.
    /// </remarks>
    public Func<double, double>? Modifier { get; init; }

    /// <summary>How the drag composes with other animations of the property this axis writes; it otherwise replaces them.</summary>
    public Composition? Composition { get; init; }
}
