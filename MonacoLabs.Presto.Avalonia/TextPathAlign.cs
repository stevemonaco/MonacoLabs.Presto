namespace MonacoLabs.Presto;

/// <summary>Which point of a <see cref="TextPath"/> text sits at <see cref="TextPath.OffsetProperty"/> along the contour.</summary>
public enum TextPathAlign
{
    /// <summary>The text starts there.</summary>
    Start,

    /// <summary>The text is centered there.</summary>
    Center,

    /// <summary>The text ends there.</summary>
    End,
}
