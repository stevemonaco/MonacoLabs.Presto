namespace MonacoLabs.Presto.Compositor;

/// <summary>How much of a layout change a <see cref="LayoutTransition"/> eases (COMPOSITOR-PLAN §4.4).</summary>
public enum LayoutAnimateMode
{
    /// <summary>Eases the move and lets a resize land in one frame.</summary>
    Position,

    /// <summary>
    /// Eases the resize too, by inverting it onto the composition scale about the control's top left corner. The control's
    /// content scales with it; <see cref="PositionAndSizeCorrected"/> keeps the direct children at their size.
    /// </summary>
    PositionAndSize,

    /// <summary>
    /// <see cref="PositionAndSize"/>, with each direct visual child counter-scaled on the render thread so it keeps its size
    /// and its place in the growing control, taking its descendants with it. What the control draws itself still scales:
    /// its background, border, shadow and corner radius, which is not a composition property. Use
    /// <see cref="Animate.ContentSizeProperty"/> for rounded surfaces.
    /// </summary>
    PositionAndSizeCorrected,
}
