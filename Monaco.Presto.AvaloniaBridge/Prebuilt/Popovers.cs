using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.VisualTree;

namespace Monaco.Presto.AvaloniaBridge.Prebuilt;

/// <summary>
/// Popup recipes for <see cref="Popover.OpenProperty"/> and <see cref="Popover.CloseProperty"/>: the presenter scales and
/// slides from the edge that faces its anchor, using transform and opacity only. Each becomes a
/// <see cref="MotionTokens.Short"/> fade while <see cref="PrestoDefaults.ReducedMotion"/> is set.
/// </summary>
/// <remarks>
/// A platform that flips a popup to fit does not report it. As it opens, before its host is placed, the edge is predicted
/// from the popup's <see cref="Popup.Placement"/>, which each owner copies there, flipped where the presenter would not fit
/// beside its anchor in the window, or the screen for a native popup. A flyout's presenter is not hosted yet as it opens, so
/// it is taken to be hosted as it last was, and natively the first time; as it closes it is read from the presenter's and the
/// anchor's rects, falling back on the placement where they overlap, as at a pointer. Arguments left null come from
/// <see cref="Recipes.Tokens"/>: the duration is <see cref="MotionTokens.Short"/>, the travel half of
/// <see cref="MotionTokens.Distance"/> and the scale <see cref="MotionTokens.Scale"/>.
/// </remarks>
public static class Popovers
{
    /// <summary>Fades in while growing from <paramref name="scale"/> and sliding <paramref name="distance"/> DIPs out of its anchor.</summary>
    public static AnimationBuilder PopoverIn(double? distance = null, double? scale = null, double? duration = null, double? delay = null, EaseValue? ease = null)
    {
        var tokens = Recipes.Tokens;
        var travel = distance ?? tokens.Distance / 2;
        return Recipes.Start(tokens, duration ?? tokens.Short, delay, ease ?? tokens.Ease, stagger: false)
            .Opacity(from: 0)
            .Scale(from: scale ?? tokens.Scale)
            .Property("X", from: Travel("X", travel, placed: false))
            .Property("Y", from: Travel("Y", travel, placed: false))
            .WhenReduced(reduced => reduced.Opacity(from: 0).Duration(tokens.Short));
    }

    /// <summary>Fades out while shrinking to <paramref name="scale"/> and sliding <paramref name="distance"/> DIPs back into its anchor.</summary>
    public static AnimationBuilder PopoverOut(double? distance = null, double? scale = null, double? duration = null, double? delay = null, EaseValue? ease = null)
    {
        var tokens = Recipes.Tokens;
        var travel = distance ?? tokens.Distance / 2;
        return Recipes.Start(tokens, duration ?? tokens.Short, delay, ease ?? tokens.Ease, stagger: false)
            .Opacity(0)
            .Scale(scale ?? tokens.Scale)
            .Property("X", Travel("X", travel, placed: true))
            .Property("Y", Travel("Y", travel, placed: true))
            .WhenReduced(reduced => reduced.Opacity(0).Duration(tokens.Short));
    }

    /// <summary>
    /// The presenter's edge that faces its popup's anchor, or null for a <see cref="PlacementMode.Center"/> popup over it:
    /// read from where the presenter stands once <paramref name="placed"/>, else predicted as the positioner would flip it.
    /// </summary>
    internal static Edge? AnchoredEdge(object target, bool placed)
    {
        if (target is not Control { Parent: Popup popup } presenter) return Edge.Top;
        var preferred = Preferred(popup.Placement);
        if ((popup.PlacementTarget ?? popup.Parent as Control) is not { } anchor || TopLevel.GetTopLevel(anchor) is not { } top ||
            anchor.TranslatePoint(default, top) is not { } anchorAt) return preferred;
        TopLevel.GetTopLevel(presenter)?.UpdateLayout();
        var at = new Rect(anchorAt, anchor.Bounds.Size);
        if (placed && presenter.GetVisualParent() is { } parent)
        {
            // Through the screen, since a native popup is not in the anchor's window.
            var shown = new Rect(top.PointToClient(parent.PointToScreen(presenter.Bounds.Position)), presenter.Bounds.Size);
            Edge? across = shown.Top >= at.Bottom - 1 ? Edge.Top : shown.Bottom <= at.Top + 1 ? Edge.Bottom : null;
            Edge? along = shown.Left >= at.Right - 1 ? Edge.Left : shown.Right <= at.Left + 1 ? Edge.Right : null;
            if ((preferred is Edge.Left or Edge.Right ? along ?? across : across ?? along) is { } edge) return edge;
        }
        // A flyout opens before its popup hosts the presenter, and an overlay host is placed by a posted job, so only the measure is known.
        var size = presenter.DesiredSize;
        if (preferred is not { } side || popup.Placement is PlacementMode.Pointer || size == default) return preferred;
        // An overlay popup fits its window and a native one its screen; one not hosted yet is taken to be hosted as last time.
        var overlay = TopLevel.GetTopLevel(presenter) is { } host ? host == top : popup.IsUsingOverlayLayer;
        var area = !overlay && top.Screens?.ScreenFromVisual(anchor) is { } screen
            ? new Rect(top.PointToClient(screen.WorkingArea.TopLeft), top.PointToClient(screen.WorkingArea.BottomRight))
            : new Rect(top.ClientSize);
        return side switch
        {
            Edge.Top when at.Bottom + size.Height > area.Bottom && at.Top - size.Height >= area.Top => Edge.Bottom,
            Edge.Bottom when at.Top - size.Height < area.Top && at.Bottom + size.Height <= area.Bottom => Edge.Top,
            Edge.Left when at.Right + size.Width > area.Right && at.Left - size.Width >= area.Left => Edge.Right,
            Edge.Right when at.Left - size.Width < area.Left && at.Right + size.Width <= area.Right => Edge.Left,
            _ => side,
        };
    }

    private static Edge? Preferred(PlacementMode placement) => placement switch
    {
        PlacementMode.Center => null,
        PlacementMode.Top or PlacementMode.TopEdgeAlignedLeft or PlacementMode.TopEdgeAlignedRight => Edge.Bottom,
        PlacementMode.Left or PlacementMode.LeftEdgeAlignedTop or PlacementMode.LeftEdgeAlignedBottom => Edge.Right,
        PlacementMode.Right or PlacementMode.RightEdgeAlignedTop or PlacementMode.RightEdgeAlignedBottom => Edge.Left,
        _ => Edge.Top,
    };

    private static FunctionValue Travel(string axis, double distance, bool placed) => (target, _, _) =>
        AnchoredEdge(target, placed) is { } edge && Recipes.Offset(edge, distance) is var (property, offset) && property == axis ? offset : 0;
}
