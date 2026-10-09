namespace Monaco.Presto.AvaloniaBridge;

/// <summary>How <see cref="TextWarp"/> deforms the outline pieces of a split <see cref="Avalonia.Controls.TextBlock"/>.</summary>
public enum TextWarpKind
{
    /// <summary>Vertical sine displacement along x, travelling with <c>WarpPhase</c>.</summary>
    Wave,

    /// <summary>Bends the text's center line into an arc whose sweep grows with the amount.</summary>
    Bend,

    /// <summary>Fisheye magnification within <c>WarpRadius</c> of <c>WarpOrigin</c>; a negative amount pinches.</summary>
    Bulge,

    /// <summary>Rotation about <c>WarpOrigin</c> proportional to the distance from it.</summary>
    Twist,

    /// <summary>Downward drips, longer at seeded columns, growing with the amount.</summary>
    Melt,

    /// <summary>Keystone: the top edge shrinks, or grows for a negative amount, relative to the bottom.</summary>
    Perspective,

    /// <summary>Each point springs back to rest after the motion of <c>WarpOrigin</c> kicks it.</summary>
    Jelly,
}
