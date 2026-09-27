namespace Monaco.Presto.AvaloniaBridge.Compositor;

/// <summary>How much of a layout change a <see cref="LayoutTransition"/> eases (COMPOSITOR-PLAN §4.4).</summary>
public enum LayoutAnimateMode
{
    /// <summary>Eases the move and lets a resize land in one frame.</summary>
    Position,

    /// <summary>
    /// Eases the resize too, by inverting it onto the composition scale about the control's top left corner. The control's
    /// content scales with it: Motion counter-scales children, which here would mean writing every child's visual.
    /// </summary>
    PositionAndSize,
}
