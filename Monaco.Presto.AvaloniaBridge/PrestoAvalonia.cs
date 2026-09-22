using Avalonia;
using Avalonia.Media;
using Avalonia.VisualTree;
using Monaco.Presto.AvaloniaBridge.Targets;
using Monaco.Presto.AvaloniaBridge.Values;

namespace Monaco.Presto.AvaloniaBridge;

/// <summary>Installs the bridge into the <see cref="Presto"/> statics, and mirrors the factories that need Avalonia.</summary>
public static class PrestoAvalonia
{
    internal static bool Initialized;

    /// <summary>Points the engine at an <see cref="AvaloniaFrameSource"/> and registers the Avalonia adapter and codecs the first time it is called; later calls do nothing.</summary>
    public static void EnsureInitialized()
    {
        if (Initialized) return;
        Initialized = true;
        Presto.Engine.FrameSource = new AvaloniaFrameSource();
        Presto.Adapters.Add(new AvaloniaObjectTargetAdapter());

        Presto.Codecs[typeof(Color)] = new ColorCodec();

        Presto.Codecs[typeof(IBrush)] = new BrushCodec();

        Presto.Codecs[typeof(Thickness)] = new StructCodec<Thickness>(
            static t => [t.Left, t.Top, t.Right, t.Bottom],
            static n => new Thickness(n[0], n[1], n[2], n[3]));
        Presto.Codecs[typeof(CornerRadius)] = new StructCodec<CornerRadius>(
            static c => [c.TopLeft, c.TopRight, c.BottomRight, c.BottomLeft],
            static n => new CornerRadius(n[0], n[1], n[2], n[3]));
        Presto.Codecs[typeof(Point)] = new StructCodec<Point>(
            static p => [p.X, p.Y],
            static n => new Point(n[0], n[1]));
        Presto.Codecs[typeof(Size)] = new StructCodec<Size>(
            static s => [s.Width, s.Height],
            static n => new Size(n[0], n[1]));
        Presto.Codecs[typeof(Rect)] = new StructCodec<Rect>(
            static r => [r.X, r.Y, r.Width, r.Height],
            static n => new Rect(n[0], n[1], n[2], n[3]));
        Presto.Codecs[typeof(Vector)] = new StructCodec<Vector>(
            static v => [v.X, v.Y],
            static n => new Vector(n[0], n[1]));
        Presto.Codecs[typeof(RelativePoint)] = new RelativePointCodec();
    }

    /// <summary>
    /// <see cref="Presto.Stagger"/> with <see cref="StaggerParams.AutoGrid"/> measured from where the targets are laid out
    /// instead of read from <c>X</c>/<c>Y</c>/<c>Z</c> properties.
    /// </summary>
    public static Stagger Stagger(TweenValue value, StaggerParams? parameters = null)
    {
        EnsureInitialized();
        return Presto.Stagger(value, (parameters ?? new StaggerParams()) with { AutoGrid = true, Coordinates = MeasureCenter });
    }

    private static double[]? MeasureCenter(object target)
    {
        if (target is not Visual visual) return null;
        var root = visual;
        while (root.GetVisualParent() is { } parent) root = parent;
        var center = visual.TranslatePoint(new Point(visual.Bounds.Width / 2, visual.Bounds.Height / 2), root) ?? default;
        return [center.X, center.Y];
    }
}
