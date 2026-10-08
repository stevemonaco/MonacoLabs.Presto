using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;

namespace Monaco.Presto.AvaloniaBridge.DevTools;

/// <summary>A sampled plot of an <see cref="Monaco.Presto.Ease"/>, scaled to include any overshoot, with a dot at the current progress.</summary>
internal sealed class EaseThumbnail : Control
{
    private const int Samples = 32;

    private Ease? _ease;
    private double _progress;
    private Pen? _pen;

    static EaseThumbnail() => AffectsRender<EaseThumbnail>(TextElement.ForegroundProperty);

    internal Ease? Ease
    {
        get => _ease;
        set
        {
            _ease = value;
            InvalidateVisual();
        }
    }

    internal double Progress
    {
        get => _progress;
        set
        {
            if (_progress == value) return;
            _progress = value;
            InvalidateVisual();
        }
    }

    public override void Render(DrawingContext context)
    {
        if (_ease is not { } ease || TextElement.GetForeground(this) is not { } brush) return;
        if (_pen?.Brush != brush) _pen = new Pen(brush);
        double low = 0, high = 1;
        for (var i = 0; i <= Samples; i++)
        {
            var value = ease((double)i / Samples);
            low = Math.Min(low, value);
            high = Math.Max(high, value);
        }
        var width = Bounds.Width;
        var height = Bounds.Height;
        Point At(double progress) => new(progress * width, height - ((ease(progress) - low) / (high - low) * height));
        var previous = At(0);
        for (var i = 1; i <= Samples; i++)
        {
            var point = At((double)i / Samples);
            context.DrawLine(_pen, previous, point);
            previous = point;
        }
        context.DrawEllipse(Brushes.OrangeRed, null, At(_progress), 2.5, 2.5);
    }
}
