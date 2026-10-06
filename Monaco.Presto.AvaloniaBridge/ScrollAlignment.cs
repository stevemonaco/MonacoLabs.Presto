namespace Monaco.Presto.AvaloniaBridge;

/// <summary>Where <see cref="PrestoAvalonia.ScrollIntoView(Avalonia.Controls.Control, ScrollAlignment, Avalonia.Thickness, AnimationBuilder?)"/> places an element in each viewport.</summary>
public enum ScrollAlignment
{
    /// <summary>Scrolls the least distance that shows the element, or none when it is already in view; the default.</summary>
    Nearest,

    /// <summary>Aligns the element's leading edge with the viewport's.</summary>
    Start,

    /// <summary>Centers the element in the viewport.</summary>
    Center,

    /// <summary>Aligns the element's trailing edge with the viewport's.</summary>
    End,
}
