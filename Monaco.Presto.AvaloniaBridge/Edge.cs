namespace Monaco.Presto.AvaloniaBridge;

/// <summary>
/// A side: of an element's slot, which <see cref="Recipes.SlideIn"/> arrives from and <see cref="Recipes.SlideOut"/> leaves
/// towards, or of a host, which a <see cref="Controls.Sheet"/> slides out from.
/// </summary>
public enum Edge
{
    /// <summary>The left side.</summary>
    Left,

    /// <summary>The top side.</summary>
    Top,

    /// <summary>The right side.</summary>
    Right,

    /// <summary>The bottom side.</summary>
    Bottom,
}
