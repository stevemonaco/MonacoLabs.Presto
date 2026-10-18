namespace MonacoLabs.Presto;

/// <summary>An edge of a scroller's viewport that <see cref="Scroll.CueEdgeProperty"/> cues.</summary>
public enum ScrollEdge
{
    /// <summary>Content lies above the viewport.</summary>
    Top,

    /// <summary>Content lies below the viewport.</summary>
    Bottom,

    /// <summary>Content lies left of the viewport.</summary>
    Left,

    /// <summary>Content lies right of the viewport.</summary>
    Right,
}
