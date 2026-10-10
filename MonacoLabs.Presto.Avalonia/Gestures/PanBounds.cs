namespace MonacoLabs.Presto.Gestures;

/// <summary>How far a <c>Transformable</c> may pan its scaled, rotated content within its container.</summary>
public enum PanBounds
{
    /// <summary>The content covers the container, or is centered on an axis where it is smaller (the default).</summary>
    Contain,

    /// <summary>The content stays within the container.</summary>
    Inside,

    /// <summary>The content pans without bounds.</summary>
    Free,
}
