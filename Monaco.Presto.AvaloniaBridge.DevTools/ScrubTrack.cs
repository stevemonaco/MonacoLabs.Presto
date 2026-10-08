using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Media;

namespace Monaco.Presto.AvaloniaBridge.DevTools;

/// <summary>
/// A root's timing drawn on one axis of <see cref="Timer.CurrentTime"/>: its delay, each iteration and loop delay, a timeline's
/// labels and child spans as a Gantt, the selection's span and the playhead. Pressing and dragging scrubs through the owner.
/// </summary>
/// <remarks>An infinitely looping root shows only its current iteration.</remarks>
internal sealed class ScrubTrack : Control
{
    private const double BarHeight = 10;
    private const double GanttRow = 7;
    private const double LabelHeight = 12;

    private static readonly IBrush s_delay = new SolidColorBrush(Colors.Gray, 0.35);
    private static readonly IBrush s_iteration = new SolidColorBrush(Colors.SteelBlue, 0.8);
    private static readonly IBrush s_child = new SolidColorBrush(Colors.SteelBlue, 0.55);
    private static readonly Pen s_highlight = new(Brushes.Gold, 1.5);
    private static readonly Pen s_playhead = new(Brushes.OrangeRed, 2);
    private static readonly Pen s_label = new(new SolidColorBrush(Colors.Gray, 0.8));

    private readonly RootInspector _owner;
    private Timer? _root;
    private bool _scrubbing;

    static ScrubTrack() => AffectsRender<ScrubTrack>(TextElement.ForegroundProperty);

    internal ScrubTrack(RootInspector owner)
    {
        _owner = owner;
        Cursor = new Cursor(StandardCursorType.SizeWestEast);
    }

    /// <summary>The selected child's or tween's span, in time from the start of the root's iteration.</summary>
    internal (double Start, double End)? Highlight { get; private set; }

    /// <summary>Where <paramref name="timer"/>'s iteration starts in its root's iteration time.</summary>
    internal static double Origin(Timer timer) => timer.Parent is { } parent ? Origin(parent) + timer.Offset + timer.Delay : 0;

    internal void Update(Timer root, (double Start, double End)? highlight)
    {
        var shapeChanged = root != _root;
        _root = root;
        Highlight = highlight;
        if (shapeChanged) InvalidateMeasure();
        InvalidateVisual();
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var rows = 0;
        if (_root != null) foreach (var _ in _root.Children) rows++;
        return new Size(0, BarHeight + (rows * GanttRow) + LabelHeight + 4);
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (_root is null || !_owner.BeginScrub()) return;
        _scrubbing = true;
        e.Pointer.Capture(this);
        _owner.ScrubTo(TimeAt(e.GetPosition(this).X));
        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (_scrubbing) _owner.ScrubTo(TimeAt(e.GetPosition(this).X));
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        e.Pointer.Capture(null);
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        if (!_scrubbing) return;
        _scrubbing = false;
        _owner.EndScrub();
    }

    public override void Render(DrawingContext context)
    {
        if (_root is not { } root) return;
        var (start, end) = Window(root);
        var scale = Bounds.Width / Math.Max(end - start, 1e-6);
        var height = Bounds.Height;
        double X(double time) => (Math.Clamp(time, start, end) - start) * scale;
        void Span(IBrush brush, double from, double to, double top, double size) =>
            context.FillRectangle(brush, new Rect(X(from), top, Math.Max(X(to) - X(from), 1), size));

        Span(s_delay, -root.Delay, 0, 0, BarHeight);
        var cycle = root.IterationDuration + root.LoopDelay;
        for (var i = Math.Max(0, Math.Floor(start / cycle)); i < root.IterationCount && i * cycle < end; i++)
        {
            var iteration = i * cycle;
            Span(s_iteration, iteration, iteration + root.IterationDuration, 0, BarHeight);
            if (root.LoopDelay > 0) Span(s_delay, iteration + root.IterationDuration, iteration + cycle, 0, BarHeight);
        }

        var origin = root.CurrentIteration * cycle;
        var top = BarHeight + 2;
        foreach (var child in root.Children)
        {
            Span(s_child, origin + child.Offset, origin + child.Offset + ((child.Delay + child.Duration) / child.Speed), top, GanttRow - 2);
            top += GanttRow;
        }
        if (root is Timeline { Labels.Count: > 0 } timeline && TextElement.GetForeground(this) is { } foreground)
        {
            foreach (var (name, time) in timeline.Labels)
            {
                var x = X(origin + time);
                context.DrawLine(s_label, new Point(x, 0), new Point(x, height));
                var text = new FormattedText(name, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, Typeface.Default, 9, foreground);
                context.DrawText(text, new Point(x + 2, height - LabelHeight));
            }
        }
        if (Highlight is var (from, to)) context.DrawRectangle(null, s_highlight, new Rect(X(origin + from), 0, Math.Max(X(origin + to) - X(origin + from), 1), height));
        var playhead = X(root.CurrentTime);
        context.DrawLine(s_playhead, new Point(playhead, 0), new Point(playhead, height));
    }

    private static (double Start, double End) Window(Timer root)
    {
        if (!double.IsPositiveInfinity(root.IterationCount)) return (-root.Delay, root.Duration);
        var iteration = root.CurrentIteration * (root.IterationDuration + root.LoopDelay);
        return (root.CurrentIteration == 0 ? -root.Delay : iteration, iteration + root.IterationDuration);
    }

    private double TimeAt(double x)
    {
        var (start, end) = Window(_root!);
        return start + (Math.Clamp(x / Bounds.Width, 0, 1) * (end - start));
    }
}
