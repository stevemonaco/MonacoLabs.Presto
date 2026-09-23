using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Media;
using Avalonia.VisualTree;
using Monaco.Presto.AvaloniaBridge.Draggables;
using Monaco.Presto.AvaloniaBridge.Events;
using Monaco.Presto.AvaloniaBridge.Shapes;
using Monaco.Presto.AvaloniaBridge.Targets;
using Monaco.Presto.AvaloniaBridge.Text;
using Monaco.Presto.AvaloniaBridge.Values;
using Path = Avalonia.Controls.Shapes.Path;

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

        Presto.Codecs[typeof(Geometry)] = new GeometryCodec();

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

    /// <summary>
    /// Upstream <c>svg.createDrawable</c>: puts <paramref name="shape"/> into the stroke dash state that reveals only the
    /// contour between <paramref name="start"/> and <paramref name="end"/> (both 0-1), and returns it ready to animate
    /// through its "Draw" property (<c>["Draw"] = "0 1"</c>).
    /// </summary>
    public static Shape CreateDrawable(Shape shape, double start = 0, double end = 0)
    {
        EnsureInitialized();
        DrawAccessor.Apply(shape, start, end);
        return shape;
    }

    /// <summary>
    /// Upstream <c>svg.createMotionPath</c>: the "X", "Y" and "Rotate" tweens that ride <paramref name="path"/>'s contour,
    /// with <paramref name="offset"/> (0-1) shifting where along it the animation starts — clamping at the two ends when it
    /// is 0, wrapping around otherwise.
    /// </summary>
    /// <remarks>
    /// The values are points of the path's own geometry, with no coordinate conversion: the animated control has to sit in
    /// the same container as <paramref name="path"/>, at that container's origin.
    /// </remarks>
    public static (TweenParams X, TweenParams Y, TweenParams Rotate) CreateMotionPath(Shape path, double offset = 0)
    {
        EnsureInitialized();
        var geometry = path.RenderedGeometry ?? throw new ArgumentException($"{path.GetType()} has no geometry to follow.", nameof(path));
        var length = geometry.ContourLength;
        var shift = offset * length;
        var clamp = offset == 0;

        double Distance(double progress)
        {
            var distance = progress + shift;
            return clamp ? Math.Clamp(distance, 0, length) : ((distance % length) + length) % length;
        }

        return (
            new TweenParams { From = 0, To = length, Modifier = p => geometry.TryGetPointAtDistance(Distance(p), out var point) ? point.X : 0 },
            new TweenParams { From = 0, To = length, Modifier = p => geometry.TryGetPointAtDistance(Distance(p), out var point) ? point.Y : 0 },
            new TweenParams
            {
                From = 0,
                To = length,
                Modifier = p => geometry.TryGetPointAndTangentAtDistance(Distance(p), out _, out var tangent)
                    ? Math.Atan2(tangent.Y, tangent.X) * 180 / Math.PI
                    : 0,
            });
    }

    /// <summary>
    /// Upstream <c>svg.morphTo</c>: the from/to pair that morphs a <see cref="Path"/>'s <see cref="Path.Data"/> into
    /// <paramref name="destination"/>, by resampling both contours at <paramref name="precision"/> points per unit of the
    /// longer one and interpolating the two point by point.
    /// </summary>
    /// <remarks>
    /// The morph chains off the tween it follows, so one added to a timeline, or a later keyframe, starts from the shape the
    /// previous one ended on rather than from the path's own data. Both geometries are read in their own coordinates, so the
    /// path must not stretch its data. A <c>Polyline</c>'s or <c>Polygon</c>'s <c>Points</c> is a list of points rather than
    /// a <see cref="Geometry"/> and cannot be morphed; use a <see cref="Path"/>.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="precision"/> is zero or negative. Upstream reads that as its raw-attribute morph, which needs the
    /// source path string an Avalonia <see cref="Geometry"/> does not carry.
    /// </exception>
    public static ChainedFunctionValue MorphTo(Geometry destination, double precision = GeometryCodec.DefaultPrecision)
    {
        EnsureInitialized();
        if (precision <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(precision),
                precision,
                "MorphTo resamples both contours, so it needs a positive precision. Upstream's precision: 0 morphs the raw points attribute instead, which has no Avalonia equivalent.");
        }

        return (target, _, _, previous) =>
        {
            // Upstream writes prevTween._value back to the element before sampling it; here that value parses into the geometry to sample.
            var source = previous?._value is string data ? Geometry.Parse(data) : (target as Path)?.Data;
            if (source == null) throw new ArgumentException($"Can't morph a {target.GetType()}. MorphTo animates the Data of a Path.", nameof(target));
            var points = GeometryCodec.PointCount(Math.Max(source.ContourLength, destination.ContourLength), precision);
            return TweenValue.FromTo(GeometryCodec.Sample(source, points), GeometryCodec.Sample(destination, points));
        };
    }

    /// <summary>
    /// Upstream <c>splitText</c>: takes <paramref name="textBlock"/>'s text apart into the per-line, per-word and
    /// per-character controls <paramref name="parameters"/> asks for, and keeps them in step with the block's width.
    /// </summary>
    public static TextSplitter SplitText(TextBlock textBlock, SplitTextParams? parameters = null)
    {
        EnsureInitialized();
        return new TextSplitter(textBlock, parameters ?? new SplitTextParams());
    }

    /// <summary>
    /// Upstream <c>scrambleText</c>: the value of a string property (<c>["Text"] = PrestoAvalonia.ScrambleText()</c>) that
    /// scrambles the target's text with random characters and settles it character by character.
    /// </summary>
    /// <remarks>
    /// The scramble chains off the tween it follows, so one added to a timeline, or a later keyframe, starts from the text
    /// the previous one ended on; without <see cref="ScrambleTextParams.Text"/> it then settles back on that same text.
    /// </remarks>
    public static ChainedFunctionValue ScrambleText(ScrambleTextParams? parameters = null)
    {
        EnsureInitialized();
        return TextScrambler.Create(parameters ?? new ScrambleTextParams());
    }

    /// <summary>
    /// Upstream <c>createDraggable</c>: makes <paramref name="target"/> draggable by pointer, moving it through the
    /// transform shorthands "X" and "Y" unless <paramref name="parameters"/> maps the axes elsewhere.
    /// </summary>
    public static Draggable CreateDraggable(Control target, DraggableParams? parameters = null)
    {
        EnsureInitialized();
        return new Draggable(target, parameters ?? new DraggableParams());
    }

    /// <summary>
    /// Upstream <c>onScroll</c>: watches a control crossing a <see cref="ScrollViewer"/>'s thresholds, and drives
    /// whatever is handed to <see cref="ScrollObserver.Link"/> from the scroll position.
    /// </summary>
    public static ScrollObserver OnScroll(ScrollObserverParams? parameters = null)
    {
        EnsureInitialized();
        return new ScrollObserver(parameters ?? new ScrollObserverParams());
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
