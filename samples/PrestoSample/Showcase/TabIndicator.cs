using Avalonia;
using Avalonia.VisualTree;

namespace PrestoSample.Showcase;

/// <summary>Measures where a tab strip's selection indicator belongs.</summary>
public static class TabIndicator
{
    /// <summary>The span under <paramref name="tab"/>, inset at both ends, in <paramref name="rail"/>'s coordinates.</summary>
    public static Rect Under(Visual tab, Visual rail, double inset)
    {
        // Translated from the parent so a pressed tab's render transform does not shift the span.
        var at = tab.GetVisualParent()?.TranslatePoint(tab.Bounds.Position, rail) ?? default;
        return new Rect(at.X + inset, 0, tab.Bounds.Width - (2 * inset), 0);
    }
}
