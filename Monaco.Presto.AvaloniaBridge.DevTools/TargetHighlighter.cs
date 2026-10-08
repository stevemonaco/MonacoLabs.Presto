using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Monaco.Presto.AvaloniaBridge.Targets;

namespace Monaco.Presto.AvaloniaBridge.DevTools;

/// <summary>Outlines the visuals the hovered and the selected rows animate, from the window's <see cref="AdornerLayer"/> while there is anything to outline.</summary>
internal sealed class TargetHighlighter : Control
{
    private static readonly Pen s_hovered = new(Brushes.DeepSkyBlue, 2);
    private static readonly Pen s_selected = new(Brushes.Orange, 2, DashStyle.Dash);

    private readonly List<Visual> _visuals = [];
    private readonly List<Rect> _outlines = [];
    private int _hoveredCount;

    internal TargetHighlighter() => IsHitTestVisible = false;

    /// <summary>The outlined bounds in top-level coordinates, those of the hovered row first.</summary>
    internal IReadOnlyList<Rect> Outlines => _outlines;

    /// <summary>Re-measures the outlines; the overlay calls it once per frame, since animated targets move.</summary>
    internal void Update(TopLevel? top, TickableRow? hovered, TickableRow? selected)
    {
        _visuals.Clear();
        _outlines.Clear();
        if (top != null)
        {
            Collect(hovered, top);
            _hoveredCount = _outlines.Count;
            if (selected != hovered) Collect(selected, top);
        }
        if (_outlines.Count > 0) Show(top!);
        else Hide();
        InvalidateVisual();
    }

    internal void Hide() => (Parent as AdornerLayer)?.Children.Remove(this);

    public override void Render(DrawingContext context)
    {
        if (TopLevel.GetTopLevel(this)?.TransformToVisual(this) is not { } toThis) return;
        using var _ = context.PushTransform(toThis);
        for (var i = 0; i < _outlines.Count; i++) context.DrawRectangle(null, i < _hoveredCount ? s_hovered : s_selected, _outlines[i]);
    }

    private void Show(TopLevel top)
    {
        if (Parent != null || AdornerLayer.GetAdornerLayer(top) is not { } layer) return;
        layer.Children.Add(this);
    }

    private void Collect(TickableRow? row, TopLevel top)
    {
        if (row?.Tween is { } tween) Add(tween.Target, top);
        else if (row?.Timer is { } timer)
        {
            foreach (var each in TickableRow.AllTweens(timer)) Add(each.Target, top);
        }
    }

    private void Add(object target, TopLevel top)
    {
        if (TargetOwner.Find(target) is not { } visual || _visuals.Contains(visual) || visual.TransformToVisual(top) is not { } matrix) return;
        _visuals.Add(visual);
        _outlines.Add(new Rect(visual.Bounds.Size).TransformToAABB(matrix));
    }
}
