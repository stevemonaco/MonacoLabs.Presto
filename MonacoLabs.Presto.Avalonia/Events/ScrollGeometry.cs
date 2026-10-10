using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.VisualTree;

namespace MonacoLabs.Presto.Events;

/// <summary>The offsets that bring an element into view in each of its ancestor scrollers.</summary>
internal static class ScrollGeometry
{
    /// <summary>
    /// Every ancestor scroller of <paramref name="element"/> that must move, innermost first, with the offset that shows it there. Each outer
    /// scroller aims at the part of the element its inner one will show, measured from layout slots so render transforms do not count.
    /// </summary>
    public static List<(ScrollViewer Scroller, Vector To)> Targets(Control element, ScrollAlignment alignment, Thickness margin)
    {
        var targets = new List<(ScrollViewer, Vector)>();
        var rect = new Rect(element.Bounds.Size);
        for (Visual visual = element; visual.GetVisualParent() is { } parent; visual = parent)
        {
            rect = rect.Translate(visual.Bounds.Position);
            if (parent is not ScrollContentPresenter { TemplatedParent: ScrollViewer scroller }) continue;
            rect = rect.Translate(scroller.Offset);
            var to = Target(scroller, rect, alignment, margin);
            if (to != scroller.Offset) targets.Add((scroller, to));
            rect = rect.Translate(-to).Intersect(new Rect(scroller.Viewport));
        }
        return targets;
    }

    /// <summary>Where <paramref name="element"/>'s layout slot sits in <paramref name="scroller"/>'s content, its offset added back.</summary>
    public static Rect Slot(Control element, ScrollViewer scroller)
    {
        var rect = new Rect(element.Bounds.Size);
        for (Visual visual = element; visual.GetVisualParent() is { } parent; visual = parent)
        {
            rect = rect.Translate(visual.Bounds.Position);
            if (parent is ScrollContentPresenter { TemplatedParent: var owner } && owner == scroller) break;
        }
        return rect.Translate(scroller.Offset);
    }

    /// <summary>The offset that places <paramref name="rect"/>, in content coordinates, as <paramref name="alignment"/> says.</summary>
    public static Vector Target(ScrollViewer scroller, Rect rect, ScrollAlignment alignment, Thickness margin)
    {
        rect = rect.Inflate(margin);
        var offset = scroller.Offset;
        var viewport = scroller.Viewport;
        var max = scroller.ScrollBarMaximum;
        return new Vector(
            Axis(rect.Left, rect.Right, offset.X, viewport.Width, max.X, alignment),
            Axis(rect.Top, rect.Bottom, offset.Y, viewport.Height, max.Y, alignment));
    }

    private static double Axis(double start, double end, double offset, double viewport, double max, ScrollAlignment alignment)
    {
        var target = alignment switch
        {
            ScrollAlignment.Start => start,
            ScrollAlignment.Center => (start + end - viewport) / 2,
            ScrollAlignment.End => end - viewport,
            _ when start >= offset && end <= offset + viewport => offset,
            _ when start < offset || end - start > viewport => start,
            _ => end - viewport,
        };
        return Math.Clamp(target, 0, max);
    }
}
