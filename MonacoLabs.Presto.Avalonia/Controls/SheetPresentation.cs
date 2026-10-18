namespace MonacoLabs.Presto.Controls;

/// <summary>How a <see cref="Sheet"/> presents its content while open.</summary>
public enum SheetPresentation
{
    /// <summary>Slides out from the sheet's <see cref="Sheet.Edge"/> over its scrim.</summary>
    Edge,

    /// <summary>Opens in the sheet's <see cref="Sheet.Flyout"/> at its <see cref="Sheet.Anchor"/>, or at its edge while the anchor is not visible.</summary>
    Anchored,
}
