using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Media;
using Avalonia.VisualTree;
using Monaco.Presto.AvaloniaBridge.Compositor;
using Monaco.Presto.AvaloniaBridge.Draggables;
using Monaco.Presto.AvaloniaBridge.Events;
using Monaco.Presto.AvaloniaBridge.Gestures;
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
        Presto.Adapters.Add(new CompositorScratch.Adapter());

        Presto.Codecs[typeof(Color)] = new ColorCodec();

        Presto.Codecs[typeof(IBrush)] = new BrushCodec();
        Presto.Codecs[typeof(BoxShadows)] = new BoxShadowsCodec();

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

    /// <inheritdoc cref="Presto.Set(object?, AnimationParams)"/>
    public static Animation Set(object? targets, AvaloniaAnimationParams parameters)
    {
        EnsureInitialized();
        return Presto.Set(targets, parameters);
    }

    /// <summary>
    /// Upstream <c>waapi.animate</c>: animates the transform and opacity of <paramref name="targets"/> on the render thread,
    /// so the animation carries on while the UI thread is busy.
    /// </summary>
    /// <remarks>
    /// Only "X", "Y", "Rotate", "Scale", "ScaleX", "ScaleY" and "Opacity" have composition counterparts; anything else throws.
    /// Every target has to be a <see cref="Visual"/>, and one outside the visual tree starts when it is attached to one.
    /// Each target gets its own playback mirror and composition animations, so this is meant for a handful of visuals, not
    /// thousands.
    /// </remarks>
    [OverloadResolutionPriority(1)]
    public static CompositorAnimation AnimateOnCompositor(object? targets, CompositorAnimationParams parameters)
    {
        EnsureInitialized();
        return new CompositorAnimation(targets, parameters);
    }

    /// <summary>Runs a snapshot of <paramref name="builder"/> on the render thread, as the <see cref="CompositorAnimationParams"/> overload does.</summary>
    /// <exception cref="NotSupportedException">The builder set something the compositor has no counterpart for.</exception>
    public static CompositorAnimation AnimateOnCompositor(object? targets, AnimationBuilder builder) =>
        RunOnCompositor(targets, builder.ToParams());

    /// <summary>Whether <paramref name="p"/> can run on the render thread: no setting the compositor lacks and a composition counterpart for every property.</summary>
    internal static bool FitsCompositor(AnimationParams p)
    {
        if (UnsupportedOnCompositor(p) != null) return false;
        foreach (var name in p.Properties.Keys)
        {
            if (CompositorProperty.Find(name) == null) return false;
        }
        return true;
    }

    internal static CompositorAnimation RunOnCompositor(object? targets, AnimationParams p)
    {
        if (UnsupportedOnCompositor(p) is { } unsupported) throw new NotSupportedException($"{unsupported} has no counterpart on the compositor.");

        var parameters = new CompositorAnimationParams
        {
            Duration = p.Duration,
            Delay = p.Delay,
            Ease = p.Ease,
            Loop = p.Loop,
            LoopDelay = p.LoopDelay,
            Alternate = p.Alternate,
            Reversed = p.Reversed,
            Autoplay = p.Autoplay,
            PlaybackRate = p.PlaybackRate,
            OnBegin = p.OnBegin,
            OnComplete = p.OnComplete,
            IsReducedMotion = p.IsReducedMotion,
        };
        foreach (var (name, value) in p.Properties) parameters.Properties[name] = value;
        return AnimateOnCompositor(targets, parameters);
    }

    private static string? UnsupportedOnCompositor(AnimationParams p) =>
        p.Keyframes != null ? nameof(p.Keyframes) :
        p.FrameRate != null ? nameof(p.FrameRate) :
        p.PlaybackEase != null ? nameof(p.PlaybackEase) :
        p.Composition != null ? nameof(p.Composition) :
        p.Modifier != null ? nameof(p.Modifier) :
        p.Id != null ? nameof(p.Id) :
        p.Priority != new TimerParams().Priority ? nameof(p.Priority) :
        p.OnBeforeUpdate != null ? nameof(p.OnBeforeUpdate) :
        p.OnUpdate != null ? nameof(p.OnUpdate) :
        p.OnLoop != null ? nameof(p.OnLoop) :
        p.OnPause != null ? nameof(p.OnPause) :
        p.OnRender != null ? nameof(p.OnRender) :
        null;

    /// <inheritdoc cref="AnimateOnCompositor(object?, AnimationBuilder)"/>
    public static CompositorAnimation AnimateOnCompositor(object? targets, Action<AnimationBuilder> build) =>
        AnimateOnCompositor(targets, Presto.Build(build));

    /// <summary>
    /// Upstream <c>createLayout</c>: eases <paramref name="control"/> into its new place over <paramref name="duration"/>
    /// milliseconds whenever layout moves it, on the render thread (COMPOSITOR-PLAN §4.4). From XAML, set
    /// <see cref="Animate.LayoutProperty"/> or <see cref="Animate.ItemLayoutProperty"/>.
    /// </summary>
    /// <remarks>
    /// The move is inverted onto the composition transform, so <see cref="Visual.Bounds"/> is still right in the layout pass
    /// that moved it and only the pixels lag behind. A control outside the visual tree starts when it is attached to one, and
    /// its first laid-out pass records where it is without easing, which is why a control appearing is not a layout move.
    /// </remarks>
    public static LayoutTransition AnimateLayout(
        Control control,
        double duration,
        EaseValue? ease = null,
        LayoutAnimateMode mode = LayoutAnimateMode.Position)
    {
        EnsureInitialized();
        return new LayoutTransition(control, first: null, firstOnScreen: false, new LayoutMotion(duration, Ease: ease), mode, once: false);
    }

    /// <summary>
    /// <see cref="AnimateLayout(Control, double, EaseValue?, LayoutAnimateMode)"/> with the motion taken from
    /// <paramref name="builder"/>: its duration, delay and ease, and per-axis eases given as <c>.X(ease: …)</c>,
    /// <c>.Y(ease: …)</c> and <c>.Scale(ease: …)</c>. Easing X and Y differently curves the move into an arc. From XAML, set
    /// <see cref="Animate.LayoutProperty"/> or <see cref="Animate.ItemLayoutProperty"/>.
    /// </summary>
    /// <exception cref="NotSupportedException">
    /// The builder gives X, Y or Scale a value, which is layout's to decide, or sets anything else a layout transition cannot honour.
    /// </exception>
    public static LayoutTransition AnimateLayout(Control control, AnimationBuilder builder, LayoutAnimateMode mode = LayoutAnimateMode.Position)
    {
        EnsureInitialized();
        return new LayoutTransition(control, first: null, firstOnScreen: false, LayoutMotion.From(builder), mode, once: false);
    }

    /// <inheritdoc cref="AnimateLayout(Control, AnimationBuilder, LayoutAnimateMode)"/>
    public static LayoutTransition AnimateLayout(Control control, Action<AnimationBuilder> build, LayoutAnimateMode mode = LayoutAnimateMode.Position) =>
        AnimateLayout(control, Presto.Build(build), mode);

    /// <summary>
    /// Eases <paramref name="target"/> from where <paramref name="source"/> stands right now into the slot layout gives it
    /// next, which is how an item moves between two containers (COMPOSITOR-PLAN §4.4 step 6).
    /// </summary>
    /// <remarks>
    /// <paramref name="source"/> is measured where it is drawn on screen — scroll offsets, render-transform translation and
    /// any live transition included — so a dropped drag, a control re-armed mid-move or an item in a scrolled list flies from
    /// where it is seen. The two have to share a window. By default a one-shot, off again once the move is under way;
    /// passing <paramref name="once"/> false keeps easing later layout changes as
    /// <see cref="AnimateLayout(Control, double, EaseValue?, LayoutAnimateMode)"/> does. Passing the same control twice reparents it.
    /// </remarks>
    public static LayoutTransition AnimateSharedElement(
        Control source,
        Control target,
        double duration,
        EaseValue? ease = null,
        LayoutAnimateMode mode = LayoutAnimateMode.PositionAndSize,
        bool once = true)
    {
        EnsureInitialized();
        return new LayoutTransition(target, LayoutTransition.MeasureDrawn(source), firstOnScreen: true, new LayoutMotion(duration, Ease: ease), mode, once);
    }

    /// <summary>
    /// <see cref="AnimateSharedElement(Control, Control, double, EaseValue?, LayoutAnimateMode, bool)"/> with the motion
    /// taken from <paramref name="builder"/>, as <see cref="AnimateLayout(Control, AnimationBuilder, LayoutAnimateMode)"/> takes it.
    /// </summary>
    /// <inheritdoc cref="AnimateLayout(Control, AnimationBuilder, LayoutAnimateMode)" path="/exception"/>
    public static LayoutTransition AnimateSharedElement(
        Control source,
        Control target,
        AnimationBuilder builder,
        LayoutAnimateMode mode = LayoutAnimateMode.PositionAndSize,
        bool once = true)
    {
        EnsureInitialized();
        return new LayoutTransition(target, LayoutTransition.MeasureDrawn(source), firstOnScreen: true, LayoutMotion.From(builder), mode, once);
    }

    /// <inheritdoc cref="AnimateSharedElement(Control, Control, AnimationBuilder, LayoutAnimateMode, bool)"/>
    public static LayoutTransition AnimateSharedElement(
        Control source,
        Control target,
        Action<AnimationBuilder> build,
        LayoutAnimateMode mode = LayoutAnimateMode.PositionAndSize,
        bool once = true) =>
        AnimateSharedElement(source, target, Presto.Build(build), mode, once);

    /// <summary>
    /// Records where <paramref name="source"/> is drawn under <paramref name="key"/>, so a keyed
    /// <see cref="AnimateSharedElement(string, Control, double, EaseValue?, LayoutAnimateMode, bool)"/> can fly a control
    /// from there later, after <paramref name="source"/> and its view are gone: the Toolkit's <c>PrepareToAnimate</c>.
    /// </summary>
    /// <remarks>
    /// Only the rectangle is kept, never the control. Preparing a key again replaces it, and an entry that no keyed start
    /// takes within three seconds is discarded, so a navigation that never completed cannot launch a later one from a stale place.
    /// </remarks>
    /// <returns><c>false</c>, and nothing prepared under the key, when <paramref name="source"/> is not on screen.</returns>
    public static bool PrepareSharedElement(string key, Control source) => LayoutTransition.Prepare(key, source);

    /// <summary>
    /// Flies <paramref name="target"/> from where <see cref="PrepareSharedElement"/> recorded <paramref name="key"/> into
    /// the slot layout gives it next, taking the entry: the Toolkit's <c>GetAnimation(key)?.TryStart(target)</c>.
    /// </summary>
    /// <returns>The transition, or <c>null</c> when nothing live is prepared under the key.</returns>
    public static LayoutTransition? AnimateSharedElement(
        string key,
        Control target,
        double duration,
        EaseValue? ease = null,
        LayoutAnimateMode mode = LayoutAnimateMode.PositionAndSize,
        bool once = true) =>
        AnimateSharedElement(key, target, new LayoutMotion(duration, Ease: ease), mode, once);

    /// <summary>The keyed <see cref="AnimateSharedElement(string, Control, double, EaseValue?, LayoutAnimateMode, bool)"/> with its motion taken from <paramref name="builder"/>.</summary>
    /// <inheritdoc cref="AnimateSharedElement(string, Control, double, EaseValue?, LayoutAnimateMode, bool)" path="/returns"/>
    /// <inheritdoc cref="AnimateLayout(Control, AnimationBuilder, LayoutAnimateMode)" path="/exception"/>
    public static LayoutTransition? AnimateSharedElement(
        string key,
        Control target,
        AnimationBuilder builder,
        LayoutAnimateMode mode = LayoutAnimateMode.PositionAndSize,
        bool once = true) =>
        AnimateSharedElement(key, target, LayoutMotion.From(builder), mode, once);

    /// <inheritdoc cref="AnimateSharedElement(string, Control, AnimationBuilder, LayoutAnimateMode, bool)"/>
    public static LayoutTransition? AnimateSharedElement(
        string key,
        Control target,
        Action<AnimationBuilder> build,
        LayoutAnimateMode mode = LayoutAnimateMode.PositionAndSize,
        bool once = true) =>
        AnimateSharedElement(key, target, Presto.Build(build), mode, once);

    private static LayoutTransition? AnimateSharedElement(string key, Control target, LayoutMotion motion, LayoutAnimateMode mode, bool once)
    {
        EnsureInitialized();
        return LayoutTransition.TakePrepared(key) is { } from
            ? new LayoutTransition(target, from, firstOnScreen: true, motion, mode, once)
            : null;
    }

    /// <summary>
    /// Plays <paramref name="exit"/> on a ghost of <paramref name="control"/> drawn where the control is now, so code that
    /// removes the control itself can do so in the same frame: <see cref="Presence.ExitProperty"/> for imperative removal.
    /// </summary>
    /// <returns>The ghost's animation, whose completion or cancellation removes the ghost; <c>null</c> when <paramref name="control"/> is not on screen.</returns>
    public static Animation? AnimateExit(Control control, AnimationBuilder exit)
    {
        EnsureInitialized();
        if (control.GetVisualParent() is not { } parent || TopLevel.GetTopLevel(control) is not { } top) return null;
        return parent.TranslatePoint(control.Bounds.Position, top) is { } position ? Presence.Ghost(control, parent, position, exit) : null;
    }

    /// <inheritdoc cref="AnimateExit(Control, AnimationBuilder)"/>
    public static Animation? AnimateExit(Control control, Action<AnimationBuilder> exit) => AnimateExit(control, Presto.Build(exit));

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

    /// <inheritdoc cref="Presto.GetAnimations"/>
    public static IReadOnlyList<Animation> GetAnimations(object target, AvaloniaProperty property) =>
        Presto.GetAnimations(target, AvaloniaObjectTargetAdapter.Encode(property));

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
    /// Upstream <c>svg.morphTo</c>: the from/to pair that morphs the target's <see cref="Geometry"/> <c>Data</c> property
    /// (<see cref="Path.Data"/>, <see cref="PathIcon.Data"/>) into <paramref name="destination"/>, by resampling both
    /// contours at <paramref name="precision"/> points per unit of the longer one and interpolating the two point by point.
    /// </summary>
    /// <remarks>
    /// The morph chains off the tween it follows, so one added to a timeline, or a later keyframe, starts from the shape the
    /// previous one ended on rather than from the target's own data. Both geometries are read in their own coordinates, so the
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
            var source = previous?._value is string data ? Geometry.Parse(data) : Presto.Get(target, "Data") as Geometry;
            if (source == null) throw new ArgumentException($"Can't morph a {target.GetType()}. MorphTo animates a Geometry Data property.", nameof(target));
            var points = MorphPointCount(source, destination, precision);
            return TweenValue.FromTo(GeometryCodec.Sample(source, points), GeometryCodec.Sample(destination, points));
        };
    }

    /// <summary>The number of points <see cref="MorphTo"/> resamples both <paramref name="from"/> and <paramref name="to"/> at.</summary>
    public static int MorphPointCount(Geometry from, Geometry to, double precision = GeometryCodec.DefaultPrecision) =>
        GeometryCodec.PointCount(Math.Max(from.ContourLength, to.ContourLength), precision);

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
    /// Makes <paramref name="target"/> pan with one pointer and pan, pinch-zoom and rotate with two, through the transform
    /// shorthands "X", "Y", "Scale" and "Rotate".
    /// </summary>
    /// <exception cref="InvalidOperationException">A live <see cref="Draggable"/> already drives <paramref name="target"/>.</exception>
    public static Transformable CreateTransformable(Control target, TransformableParams? parameters = null)
    {
        EnsureInitialized();
        return new Transformable(target, parameters ?? new TransformableParams());
    }

    /// <summary>
    /// Upstream <c>onScroll</c>: watches a control crossing a <see cref="ScrollViewer"/>'s thresholds, and drives
    /// whatever is handed to <see cref="ScrollObserver.Link"/> from the scroll position.
    /// </summary>
    /// <remarks>
    /// Nothing is pinned while its thresholds pass: Avalonia has no <c>position: sticky</c>, so a pinned stage is the
    /// caller's overlay. Put it beside the <see cref="ScrollViewer"/> in a <see cref="Panel"/> with
    /// <c>IsHitTestVisible="False"</c>, so the wheel still reaches the scroller, and show and hide it from
    /// <see cref="ScrollObserverParams.OnEnter"/> and <see cref="ScrollObserverParams.OnLeave"/>.
    /// </remarks>
    public static ScrollObserver OnScroll(ScrollObserverParams? parameters = null)
    {
        EnsureInitialized();
        return new ScrollObserver(parameters ?? new ScrollObserverParams());
    }

    /// <summary>Measures every resolved observer of <paramref name="scroller"/> again now, instead of 250ms after its size changes.</summary>
    public static void RefreshScroll(ScrollViewer scroller)
    {
        if (ScrollContainer.Containers.TryGetValue(scroller, out var container)) container.Refresh();
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
