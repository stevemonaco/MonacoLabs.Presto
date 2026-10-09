namespace Monaco.Presto.AvaloniaBridge.Text;

/// <summary>Where <see cref="GlyphPoints.Sample"/> puts its points; combine both for an outline and its fill.</summary>
[Flags]
public enum GlyphSampling
{
    /// <summary>Along every contour of the outline.</summary>
    Edge = 1,

    /// <summary>Inside the outline, holes excluded.</summary>
    Fill = 2,
}
