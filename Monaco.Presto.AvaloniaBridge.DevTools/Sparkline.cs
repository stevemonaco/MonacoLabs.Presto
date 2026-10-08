using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;

namespace Monaco.Presto.AvaloniaBridge.DevTools;

/// <summary>A line of the latest samples in a fixed ring, scaled to their peak and drawn in the inherited foreground.</summary>
internal sealed class Sparkline : Control
{
    private readonly double[] _samples = new double[120];
    private int _next;
    private int _count;
    private Pen? _pen;

    static Sparkline() => AffectsRender<Sparkline>(TextElement.ForegroundProperty);

    internal void Push(double sample)
    {
        _samples[_next] = sample;
        _next = (_next + 1) % _samples.Length;
        if (_count < _samples.Length) _count++;
        InvalidateVisual();
    }

    public override void Render(DrawingContext context)
    {
        if (_count < 2 || TextElement.GetForeground(this) is not { } brush) return;
        if (_pen?.Brush != brush) _pen = new Pen(brush);
        var peak = 0.01;
        for (var i = 0; i < _count; i++) peak = Math.Max(peak, _samples[i]);
        var length = _samples.Length;
        var dx = Bounds.Width / (length - 1);
        var height = Bounds.Height;
        var oldest = (_next - _count + length) % length;
        var previous = default(Point);
        for (var i = 0; i < _count; i++)
        {
            var point = new Point(i * dx, height - (_samples[(oldest + i) % length] / peak * height));
            if (i > 0) context.DrawLine(_pen, previous, point);
            previous = point;
        }
    }
}
