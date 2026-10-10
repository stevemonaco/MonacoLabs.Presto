using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace MonacoLabs.Presto.Tests;

/// <summary>A shown 200 by 200 scroller over a 300 unit lead, a 100 unit target and a trail, stacked along one axis.</summary>
internal sealed record ScrollStage(Window Window, ScrollViewer Scroller, Border Target, Border Trail)
{
    public const double ViewportSize = 200;
    public const double Lead = 300;
    public const double TargetSize = 100;

    public StackPanel Panel => (StackPanel)Scroller.Content!;

    public static ScrollStage Show(Orientation orientation = Orientation.Vertical, double trail = Lead, Border? target = null)
    {
        target ??= new Border { Background = Brushes.Red };
        var leadBorder = new Border();
        var trailBorder = new Border();
        var horizontal = orientation == Orientation.Horizontal;
        foreach (var (control, size) in new[] { (leadBorder, Lead), (target, TargetSize), (trailBorder, trail) })
        {
            if (horizontal) control.Width = size; else control.Height = size;
        }
        var panel = new StackPanel { Orientation = orientation, Children = { leadBorder, target, trailBorder } };
        var scroller = new ScrollViewer
        {
            Width = ViewportSize,
            Height = ViewportSize,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            HorizontalScrollBarVisibility = horizontal ? ScrollBarVisibility.Auto : ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = horizontal ? ScrollBarVisibility.Disabled : ScrollBarVisibility.Auto,
            Content = panel,
        };
        var window = new Window { Content = scroller, Width = 300, Height = 300 };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return new ScrollStage(window, scroller, target, trailBorder);
    }

    public static void ScrollTo(ScrollViewer scroller, double offset, Orientation orientation = Orientation.Vertical)
    {
        scroller.Offset = orientation == Orientation.Horizontal ? new Vector(offset, 0) : new Vector(0, offset);
        Dispatcher.UIThread.RunJobs();
    }

    public static void DragThumb(Window window, ScrollViewer scroller, double by)
    {
        var bar = scroller.GetVisualDescendants().OfType<ScrollBar>().Single(bar => bar.Orientation == Orientation.Vertical);
        var thumb = bar.GetVisualDescendants().OfType<Thumb>().Single();
        var grab = thumb.TranslatePoint(new Point(thumb.Bounds.Width / 2, thumb.Bounds.Height / 2), window)!.Value;
        window.MouseDown(grab, MouseButton.Left);
        window.MouseMove(grab + new Vector(0, by));
        window.MouseUp(grab + new Vector(0, by), MouseButton.Left);
    }
}
