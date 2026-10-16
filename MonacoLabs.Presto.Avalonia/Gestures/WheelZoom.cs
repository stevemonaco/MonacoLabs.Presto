namespace MonacoLabs.Presto.Gestures;

/// <summary>When the mouse wheel zooms a <c>Transformable</c> about the cursor.</summary>
public enum WheelZoom
{
    /// <summary>The wheel never zooms.</summary>
    None,

    /// <summary>Only Ctrl+wheel zooms, leaving a plain wheel to an ancestor scroller (the default).</summary>
    WithControl,

    /// <summary>Every wheel event with a vertical delta zooms; a horizontal-only one is left to an ancestor scroller.</summary>
    Always,
}
