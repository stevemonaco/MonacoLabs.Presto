using Avalonia;
using Avalonia.Controls;
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
    /// <see cref="Presto.Get(object?, string)"/> with the bridge installed first. Every <c>Get</c>, <c>Set</c> and
    /// <c>Remove</c> below exists for that reason alone: until the Avalonia adapter and codecs are registered, the core
    /// resolves an <see cref="AvaloniaObject"/> through its reflection adapter, which knows nothing of <c>"X"</c> or <c>"Opacity"</c>.
    /// </summary>
    public static object? Get(object? targets, string property)
    {
        EnsureInitialized();
        return Presto.Get(targets, property);
    }

    /// <inheritdoc cref="Presto.Get(object?, string, string)"/>
    public static string? Get(object? targets, string property, string unit)
    {
        EnsureInitialized();
        return Presto.Get(targets, property, unit);
    }

    /// <inheritdoc cref="Presto.Get(object?, string)"/>
    public static object? Get(object? targets, AvaloniaProperty property)
    {
        EnsureInitialized();
        return Presto.Get(targets, AvaloniaObjectTargetAdapter.Encode(property));
    }

    /// <inheritdoc cref="Presto.Set"/>
    public static Animation Set(object? targets, AvaloniaAnimationParams parameters)
    {
        EnsureInitialized();
        return Presto.Set(targets, parameters);
    }

    /// <inheritdoc cref="Presto.Remove"/>
    public static IReadOnlyList<object> Remove(object? targets, Timer? renderable = null, string? property = null)
    {
        EnsureInitialized();
        return Presto.Remove(targets, renderable, property);
    }

    /// <inheritdoc cref="Presto.Remove"/>
    public static IReadOnlyList<object> Remove(object? targets, AvaloniaProperty property, Timer? renderable = null)
    {
        EnsureInitialized();
        return Presto.Remove(targets, renderable, AvaloniaObjectTargetAdapter.Encode(property));
    }

    /// <summary><see cref="Presto.CreateScope"/> rooted on a control, so everything created inside reverts when <paramref name="root"/> leaves the visual tree.</summary>
    public static ControlScope CreateScope(Control root, ScopeParams? parameters = null)
    {
        EnsureInitialized();
        return new ControlScope(root, parameters);
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
