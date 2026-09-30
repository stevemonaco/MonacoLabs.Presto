namespace Monaco.Presto.AvaloniaBridge.Gestures;

/// <summary>When the mouse wheel zooms a <c>Transformable</c> about the cursor.</summary>
public enum WheelZoom
{
    /// <summary>The wheel never zooms.</summary>
    None,

    /// <summary>Only Ctrl+wheel zooms, leaving a plain wheel to an ancestor scroller (the default).</summary>
    WithControl,

    /// <summary>Every wheel event zooms.</summary>
    Always,
}
