using Avalonia;
using Avalonia.Controls;
using Monaco.Presto;

namespace PrestoSample.Views;

/// <summary>
/// The gallery's sidebar column: collapsing and expanding ease a width this class owns, which each update writes to the
/// column as a <see cref="GridLength"/>, while the content keeps its minimum width and clips rather than reflowing.
/// </summary>
internal sealed class SidebarPane(ColumnDefinition column, Control content)
{
    private readonly double _minWidth = column.MinWidth;
    private double _expanded;

    public bool IsCollapsed { get; private set; }

    /// <summary>The animated width; public so Presto can reach it.</summary>
    public double Width { get; set; }

    public void Toggle()
    {
        // The column keeps its MinWidth only while expanded and at rest, which is when the splitter may have moved it.
        if (!IsCollapsed && column.MinWidth > 0) _expanded = Width = column.ActualWidth;
        IsCollapsed = !IsCollapsed;
        column.MinWidth = 0;
        Presto.Animate(this, new AnimationParams
        {
            ["Width"] = IsCollapsed ? 0 : _expanded,
            Duration = Presto.Defaults.ReducedMotion ? 0 : 280,
            Ease = Eases.OutQuart,
            OnUpdate = _ =>
            {
                column.Width = new GridLength(Width);
                content.Margin = new Thickness(0, 0, Math.Min(0, Width - _minWidth), 0);
            },
            OnComplete = _ =>
            {
                if (!IsCollapsed) column.MinWidth = _minWidth;
            },
        });
    }
}
