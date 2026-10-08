using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;
using Monaco.Presto.AvaloniaBridge.Compositor;
using Monaco.Presto.AvaloniaBridge.Draggables;
using Monaco.Presto.AvaloniaBridge.Events;
using Monaco.Presto.AvaloniaBridge.Gestures;
using Monaco.Presto.AvaloniaBridge.Platform;
using Monaco.Presto.AvaloniaBridge.Shapes;
using Monaco.Presto.AvaloniaBridge.Targets;
using Monaco.Presto.AvaloniaBridge.Text;
using Monaco.Presto.AvaloniaBridge.Values;
using Monaco.Presto.Values;
using Path = Avalonia.Controls.Shapes.Path;

namespace Monaco.Presto.AvaloniaBridge;

/// <summary>Installs the bridge into the <see cref="Presto"/> statics, and mirrors the factories that need Avalonia.</summary>
public static class PrestoAvalonia
{
    internal static bool Initialized;
    private static readonly List<SystemMotionReader> s_systemMotionReaders = [];
    private static bool s_reducedMotionHooked;

    /// <summary>
    /// What an animation on <see cref="Presto.Engine"/> does once every visual it animates has left the visual tree, unless
    /// <see cref="AvaloniaAnimationParams.WhenDetached"/> says otherwise. An animation with a target that is not a visual, nor
    /// a brush or transform the bridge made for one, always continues.
    /// </summary>
    public static WhenDetached WhenDetached { get; set; } = WhenDetached.Pause;

    /// <summary>
    /// Points the engine at an <see cref="AvaloniaFrameSource"/>, registers the Avalonia adapter and codecs, starts
    /// setting <see cref="Engine.Hidden"/> while the app cannot be seen and applies <see cref="WhenDetached"/>, the first
    /// time it is called; later calls do nothing.
    /// </summary>
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
        Presto.Codecs[typeof(GridLength)] = new GridLengthCodec();
        HiddenSignal.Install();
        Presto.Engine.TimerCreated = DetachTracker.Track;
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

    /// <summary>
    /// Whether the animation can run on the render thread: no setting the compositor lacks and a composition counterpart for
    /// every property. Otherwise the reason names the first setting (<c>"Keyframes"</c>, <c>"OnUpdate"</c>…) or property (<c>"Width"</c>) it lacks.
    /// </summary>
    public static bool CanAnimateOnCompositor(AnimationParams parameters, [NotNullWhen(false)] out string? reason)
    {
        var p = parameters.Resolve(Engine.Current);
        reason = UnsupportedOnCompositor(p);
        if (reason != null) return false;
        foreach (var name in p.Properties.Keys)
        {
            if (CompositorProperty.Find(name) != null) continue;
            reason = name;
            return false;
        }
        return true;
    }

    /// <inheritdoc cref="CanAnimateOnCompositor(AnimationParams, out string?)"/>
    public static bool CanAnimateOnCompositor(AnimationBuilder builder, [NotNullWhen(false)] out string? reason) =>
        CanAnimateOnCompositor(builder.ToParams(), out reason);

    /// <summary>Animates <paramref name="targets"/> on the thread <paramref name="thread"/> picks.</summary>
    /// <exception cref="NotSupportedException"><paramref name="thread"/> is <see cref="AnimationThread.Compositor"/> and the compositor has no counterpart for a property or setting.</exception>
    public static ThreadedAnimation Animate(object? targets, AnimationParams parameters, AnimationThread thread)
    {
        EnsureInitialized();
        return thread == AnimationThread.Compositor || (thread == AnimationThread.PreferCompositor && CanAnimateOnCompositor(parameters, out _))
            ? new ThreadedAnimation(null, RunOnCompositor(targets, parameters))
            : new ThreadedAnimation(Presto.Animate(targets, parameters), null);
    }

    /// <summary>Animates <paramref name="targets"/> with a snapshot of <paramref name="builder"/> on the thread <paramref name="thread"/> picks.</summary>
    /// <inheritdoc cref="Animate(object?, AnimationParams, AnimationThread)" path="/exception"/>
    public static ThreadedAnimation Animate(object? targets, AnimationBuilder builder, AnimationThread thread) =>
        Animate(targets, builder.ToParams(), thread);

    /// <inheritdoc cref="Animate(object?, AnimationBuilder, AnimationThread)"/>
    public static ThreadedAnimation Animate(object? targets, Action<AnimationBuilder> build, AnimationThread thread) =>
        Animate(targets, Presto.Build(build), thread);

    internal static CompositorAnimation RunOnCompositor(object? targets, AnimationParams p)
    {
        p = p.Resolve(Engine.Current);
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
    /// A move that starts while <see cref="PrestoDefaults.ReducedMotion"/> is set lands in one frame, as
    /// <see cref="Recipes.Reflow"/>'s reduced form does.
    /// </remarks>
    public static LayoutTransition AnimateLayout(
        Control control,
        double duration,
        EaseValue? ease = null,
        LayoutAnimateMode mode = LayoutAnimateMode.Position)
    {
        EnsureInitialized();
        return new LayoutTransition(control, first: null, firstOnScreen: false, new LayoutMotion(duration, Ease: ease, LandsWhenReduced: true), mode, once: false);
    }

    /// <summary>
    /// <see cref="AnimateLayout(Control, double, EaseValue?, LayoutAnimateMode)"/> with the motion taken from
    /// <paramref name="builder"/>: its duration, delay and ease, and per-axis eases given as <c>.X(ease: …)</c>,
    /// <c>.Y(ease: …)</c> and <c>.Scale(ease: …)</c>. Easing X and Y differently curves the move into an arc. From XAML, set
    /// <see cref="Animate.LayoutProperty"/> or <see cref="Animate.ItemLayoutProperty"/>.
    /// </summary>
    /// <exception cref="NotSupportedException">
    /// The builder gives X, Y or Scale a value, which is layout's to decide, or sets anything else a layout transition cannot honor.
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
    /// Eases the real layout size of <paramref name="host"/> over <paramref name="duration"/> milliseconds whenever its content
    /// changes size, clipping the content meanwhile, on the UI thread. From XAML, set <see cref="Animate.ContentSizeProperty"/>.
    /// </summary>
    /// <remarks>
    /// Unlike a <see cref="LayoutAnimateMode.PositionAndSize"/> layout transition nothing is scaled, so corner radius, shadow
    /// and text stay true, but the host is laid out again every frame, which suits a panel rather than hundreds of rows. Its
    /// first laid-out pass records its size without easing, and a resize that starts while
    /// <see cref="PrestoDefaults.ReducedMotion"/> is set lands in one frame.
    /// </remarks>
    public static ContentSizeTransition AnimateContentSize(Control host, double duration, EaseValue? ease = null)
    {
        EnsureInitialized();
        return new ContentSizeTransition(host, new LayoutMotion(duration, Ease: ease, LandsWhenReduced: true), record: true);
    }

    /// <summary>
    /// <see cref="AnimateContentSize(Control, double, EaseValue?)"/> with the duration, delay and ease taken from
    /// <paramref name="builder"/>. From XAML, set <see cref="Animate.ContentSizeProperty"/>.
    /// </summary>
    /// <exception cref="NotSupportedException">The builder animates a property, which is layout's to decide, or sets anything else a layout transition cannot honor.</exception>
    public static ContentSizeTransition AnimateContentSize(Control host, AnimationBuilder builder)
    {
        EnsureInitialized();
        return new ContentSizeTransition(host, ContentSizeTransition.Motion(builder), record: true);
    }

    /// <inheritdoc cref="AnimateContentSize(Control, AnimationBuilder)"/>
    public static ContentSizeTransition AnimateContentSize(Control host, Action<AnimationBuilder> build) =>
        AnimateContentSize(host, Presto.Build(build));

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
    /// A move that starts while <see cref="PrestoDefaults.ReducedMotion"/> is set lands in one frame.
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
        return new LayoutTransition(target, LayoutTransition.MeasureDrawn(source), firstOnScreen: true, new LayoutMotion(duration, Ease: ease, LandsWhenReduced: true), mode, once);
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
    /// A move that starts while <see cref="PrestoDefaults.ReducedMotion"/> is set lands in one frame.
    /// </summary>
    /// <returns>The transition, or <c>null</c> when nothing live is prepared under the key.</returns>
    public static LayoutTransition? AnimateSharedElement(
        string key,
        Control target,
        double duration,
        EaseValue? ease = null,
        LayoutAnimateMode mode = LayoutAnimateMode.PositionAndSize,
        bool once = true) =>
        AnimateSharedElement(key, target, new LayoutMotion(duration, Ease: ease, LandsWhenReduced: true), mode, once);

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
    /// is 0, wrapping around otherwise — and <paramref name="autoRotate"/> degrees added to the tangent angle.
    /// </summary>
    /// <remarks>
    /// The values are points of the path's own geometry, with no coordinate conversion: the animated control has to sit in
    /// the same container as <paramref name="path"/>, at that container's origin.
    /// </remarks>
    public static (TweenParams X, TweenParams Y, TweenParams Rotate) CreateMotionPath(Shape path, double offset = 0, double autoRotate = 0)
    {
        EnsureInitialized();
        var geometry = path.RenderedGeometry ?? throw new ArgumentException($"{path.GetType()} has no geometry to follow.", nameof(path));
        var length = geometry.ContourLength;
        var distance = MotionPath.OffsetDistance(length, offset);
        return (
            new TweenParams { From = 0, To = length, Modifier = p => geometry.TryGetPointAtDistance(distance(p), out var point) ? point.X : 0 },
            new TweenParams { From = 0, To = length, Modifier = p => geometry.TryGetPointAtDistance(distance(p), out var point) ? point.Y : 0 },
            new TweenParams
            {
                From = 0,
                To = length,
                Modifier = p => geometry.TryGetPointAndTangentAtDistance(distance(p), out _, out var tangent)
                    ? Math.Atan2(tangent.Y, tangent.X) * 180 / Math.PI + autoRotate
                    : 0,
            });
    }

    /// <summary>
    /// The "X", "Y" and "Rotate" tweens that ride a <see cref="MotionPath"/> through <paramref name="points"/>, with the
    /// <paramref name="offset"/> and <paramref name="autoRotate"/> of <see cref="CreateMotionPath(Shape, double, double)"/>.
    /// </summary>
    /// <remarks>The animated control has to sit at the origin of the container the points are in.</remarks>
    public static (TweenParams X, TweenParams Y, TweenParams Rotate) CreateMotionPath(
        IReadOnlyList<Point> points, double curviness = 1, bool closed = false, double offset = 0, double autoRotate = 0)
    {
        EnsureInitialized();
        return MotionPath.Through(ToTuples(points), curviness, closed).Tweens(offset, autoRotate);
    }

    /// <summary>The geometry of the <see cref="MotionPath"/> through <paramref name="points"/>, for drawing its track.</summary>
    public static StreamGeometry MotionPathGeometry(IReadOnlyList<Point> points, double curviness = 1, bool closed = false)
    {
        var bezier = MotionPath.Through(ToTuples(points), curviness, closed).BezierPoints;
        var geometry = new StreamGeometry();
        using var context = geometry.Open();
        context.BeginFigure(new Point(bezier[0].X, bezier[0].Y), false);
        for (var i = 1; i < bezier.Count; i += 3)
        {
            context.CubicBezierTo(
                new Point(bezier[i].X, bezier[i].Y), new Point(bezier[i + 1].X, bezier[i + 1].Y), new Point(bezier[i + 2].X, bezier[i + 2].Y));
        }

        context.EndFigure(closed);
        return geometry;
    }

    private static (double X, double Y)[] ToTuples(IReadOnlyList<Point> points)
    {
        var tuples = new (double X, double Y)[points.Count];
        for (var i = 0; i < tuples.Length; i++) tuples[i] = (points[i].X, points[i].Y);
        return tuples;
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
    /// With <see cref="ScrollObserverParams.Pin"/> set, the target is held in place while its thresholds pass, moved in the
    /// same notification as the scroll so it never trails it by a frame.
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

    /// <summary>
    /// Eases <paramref name="scroller"/> to <paramref name="offset"/> through the <c>"ScrollX"</c>/<c>"ScrollY"</c>
    /// shorthands, for the axes that move, with the settings, callbacks and other properties of <paramref name="builder"/>.
    /// </summary>
    /// <remarks>
    /// Without a duration or ease from the builder it takes <see cref="MotionTokens.Short"/> plus 150 ms per viewport
    /// traveled, capped at <see cref="MotionTokens.Long"/>, on <see cref="MotionTokens.Ease"/>. The offset is held to the
    /// extent every frame, and reaches <paramref name="offset"/> if the extent grows to fit it. Any scroll it did not write
    /// stops it, a later scroll to replaces it, and a <see cref="ScrollObserverParams.Snap"/> waits until it has ended.
    /// Under <see cref="PrestoDefaults.ReducedMotion"/> it jumps, unless the builder has a <c>WhenReduced</c> form.
    /// </remarks>
    public static Animation ScrollTo(ScrollViewer scroller, Vector offset, AnimationBuilder? builder = null)
    {
        EnsureInitialized();
        var p = builder?.ToParams() ?? new AnimationParams();
        var from = scroller.Offset;
        var max = scroller.ScrollBarMaximum;
        var to = new Vector(Math.Clamp(offset.X, 0, max.X), Math.Clamp(offset.Y, 0, max.Y));
        var properties = new OrderedDictionary<string, TweenValue>(p.Properties);
        if (to.X != from.X) properties["ScrollX"] = ScrollMotion.Tween(scroller, horizontal: true, from.X, offset.X);
        if (to.Y != from.Y) properties["ScrollY"] = ScrollMotion.Tween(scroller, horizontal: false, from.Y, offset.Y);
        return Presto.Animate(scroller, ScrollMotion.Apply(p with { Properties = properties }, ScrollMotion.Viewports(to - from, scroller.Viewport)));
    }

    /// <inheritdoc cref="ScrollTo(ScrollViewer, Vector, AnimationBuilder?)"/>
    public static Animation ScrollTo(ScrollViewer scroller, Vector offset, Action<AnimationBuilder> build) =>
        ScrollTo(scroller, offset, Presto.Build(build));

    /// <summary>
    /// <see cref="ScrollTo(ScrollViewer, Vector, AnimationBuilder?)"/> along one <paramref name="axis"/>, leaving the other
    /// where it is: <c>ScrollTo(scroller, 0)</c> is back to top.
    /// </summary>
    public static Animation ScrollTo(ScrollViewer scroller, double offset, ScrollAxis axis = ScrollAxis.Vertical, AnimationBuilder? builder = null) =>
        ScrollTo(scroller, axis == ScrollAxis.Horizontal ? scroller.Offset.WithX(offset) : scroller.Offset.WithY(offset), builder);

    /// <inheritdoc cref="ScrollTo(ScrollViewer, double, ScrollAxis, AnimationBuilder?)"/>
    public static Animation ScrollTo(ScrollViewer scroller, double offset, Action<AnimationBuilder> build, ScrollAxis axis = ScrollAxis.Vertical) =>
        ScrollTo(scroller, offset, axis, Presto.Build(build));

    /// <summary>
    /// Eases every ancestor <see cref="ScrollViewer"/> of <paramref name="element"/> at once, innermost first, so the element
    /// sits in each viewport as <paramref name="alignment"/> says, <paramref name="margin"/> clear of its edges.
    /// </summary>
    /// <remarks>
    /// Each outer scroller aims at the part of the element its inner one will show. One animation drives them all with the
    /// motion of <see cref="ScrollTo(ScrollViewer, Vector, AnimationBuilder?)"/> over the largest distance, and a scroll on
    /// one scroller stops only that scroller. When nothing needs to scroll, the returned animation has already completed.
    /// </remarks>
    public static Animation ScrollIntoView(Control element, ScrollAlignment alignment = ScrollAlignment.Nearest, Thickness margin = default, AnimationBuilder? builder = null)
    {
        EnsureInitialized();
        var p = builder?.ToParams() ?? new AnimationParams();
        var moves = ScrollGeometry.Targets(element, alignment, margin);
        var properties = new OrderedDictionary<string, TweenValue>(p.Properties);
        var viewports = 0d;
        foreach (var (scroller, to) in moves)
        {
            viewports = Math.Max(viewports, ScrollMotion.Viewports(to - scroller.Offset, scroller.Viewport));
            if (to.X != scroller.Offset.X) properties["ScrollX"] = (FunctionValue)((_, i, _) => Tween(i, horizontal: true));
            if (to.Y != scroller.Offset.Y) properties["ScrollY"] = (FunctionValue)((_, i, _) => Tween(i, horizontal: false));
        }
        var animation = Presto.Animate(moves.ConvertAll(move => move.Scroller), ScrollMotion.Apply(p with { Properties = properties }, viewports));
        if (moves.Count == 0) animation.Complete();
        return animation;

        TweenValue Tween(int index, bool horizontal)
        {
            var (scroller, to) = moves[index];
            return ScrollMotion.Tween(scroller, horizontal, ScrollWriter.Along(scroller.Offset, horizontal), ScrollWriter.Along(to, horizontal));
        }
    }

    /// <inheritdoc cref="ScrollIntoView(Control, ScrollAlignment, Thickness, AnimationBuilder?)"/>
    public static Animation ScrollIntoView(Control element, Action<AnimationBuilder> build, ScrollAlignment alignment = ScrollAlignment.Nearest, Thickness margin = default) =>
        ScrollIntoView(element, alignment, margin, Presto.Build(build));

    /// <summary>
    /// Eases <paramref name="list"/> until the item at <paramref name="index"/> sits in its viewport as <paramref name="alignment"/>
    /// says, realized or not, with the motion of <see cref="ScrollTo(ScrollViewer, Vector, AnimationBuilder?)"/>.
    /// </summary>
    /// <remarks>
    /// On a <see cref="VirtualizingStackPanel"/> an item more than two viewports away is first jumped to from a viewport short of
    /// it, then the last stretch is animated, aimed each frame at the item's measured position once it is realized and at the
    /// item itself when inserts or removals move its index; it ends exactly aligned. Any other panel scrolls its container into
    /// view. User input stops it, and under <see cref="PrestoDefaults.ReducedMotion"/> it jumps.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is not an index of <paramref name="list"/>'s items.</exception>
    public static Animation ScrollToIndex(ItemsControl list, int index, ScrollAlignment alignment = ScrollAlignment.Start, AnimationBuilder? builder = null)
    {
        EnsureInitialized();
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(index, list.ItemCount);
        if (list.ItemsPanelRoot is not VirtualizingStackPanel panel)
            return ScrollIntoView(list.ContainerFromIndex(index) ?? throw new InvalidOperationException("The list has no container for that index yet."), alignment, default, builder);
        var scroller = panel.FindAncestorOfType<ScrollViewer>() ?? throw new InvalidOperationException("The list has no ScrollViewer.");
        return new IndexScroller(list, panel, scroller, index, alignment).Start(builder?.ToParams() ?? new AnimationParams());
    }

    /// <inheritdoc cref="ScrollToIndex(ItemsControl, int, ScrollAlignment, AnimationBuilder?)"/>
    public static Animation ScrollToIndex(ItemsControl list, int index, Action<AnimationBuilder> build, ScrollAlignment alignment = ScrollAlignment.Start) =>
        ScrollToIndex(list, index, alignment, Presto.Build(build));

    /// <summary>
    /// A <see cref="MotionValue"/> holding <paramref name="scroller"/>'s offset along <paramref name="axis"/>, rounded to whole
    /// pixels, or with <paramref name="progress"/> set, how far through its scrollable extent it is, 0 to 1.
    /// </summary>
    /// <remarks>
    /// Each call makes its own value, fed from the <see cref="ScrollContainer"/> the scroller's observers share and refreshed
    /// when its extent or viewport changes. It stops at its last value once the scroller leaves the visual tree or the ambient
    /// scope reverts, and reattaching does not revive it, so make another.
    /// </remarks>
    public static MotionValue ScrollValue(ScrollViewer scroller, ScrollAxis axis = ScrollAxis.Vertical, bool progress = false)
    {
        EnsureInitialized();
        var value = ScrollContainer.AddValue(scroller, axis, progress);
        return MotionValueSource.Listen(scroller, value, () => ScrollContainer.Containers[scroller].RemoveValue(value));
    }

    /// <summary>
    /// A <see cref="MotionValue"/> holding the pointer's position over <paramref name="control"/> along <paramref name="axis"/>,
    /// in DIPs from its top left, or with <paramref name="relativeToBounds"/> set, as a fraction of its size.
    /// </summary>
    /// <remarks>Updated as the pointer enters and moves; it stops and is not revived as <see cref="ScrollValue"/> describes.</remarks>
    public static MotionValue PointerValue(Control control, Orientation axis, bool relativeToBounds = false)
    {
        EnsureInitialized();
        var value = new MotionValue(Engine.Current, [], 0);
        EventHandler<PointerEventArgs> handle = (_, e) =>
        {
            var point = e.GetPosition(control);
            var (position, size) = axis == Orientation.Horizontal ? (point.X, control.Bounds.Width) : (point.Y, control.Bounds.Height);
            value.Assign(!relativeToBounds ? position : size > 0 ? position / size : 0);
        };
        control.PointerEntered += handle;
        control.PointerMoved += handle;
        return MotionValueSource.Listen(control, value, () =>
        {
            control.PointerEntered -= handle;
            control.PointerMoved -= handle;
        });
    }

    /// <summary>A <see cref="MotionValue"/> holding <paramref name="target"/>'s <paramref name="property"/>, which must convert to <c>double</c>.</summary>
    /// <remarks>
    /// Updated whenever the property changes; it stops and is not revived as <see cref="ScrollValue"/> describes, a target
    /// that is not a <see cref="Visual"/> only when the scope reverts.
    /// </remarks>
    public static MotionValue ValueOf(AvaloniaObject target, AvaloniaProperty property)
    {
        EnsureInitialized();
        var value = new MotionValue(Engine.Current, [], Convert.ToDouble(target.GetValue(property), CultureInfo.InvariantCulture));
        EventHandler<AvaloniaPropertyChangedEventArgs> handle = (_, e) =>
        {
            if (e.Property == property) value.Assign(Convert.ToDouble(target.GetValue(property), CultureInfo.InvariantCulture));
        };
        target.PropertyChanged += handle;
        return MotionValueSource.Listen(target, value, () => target.PropertyChanged -= handle);
    }

    /// <summary><see cref="Presto.CreateScope"/> rooted on a control, so everything created inside reverts when <paramref name="root"/> leaves the visual tree.</summary>
    public static ControlScope CreateScope(Control root, ScopeParams? parameters = null)
    {
        EnsureInitialized();
        return new ControlScope(root, parameters);
    }

    /// <summary>
    /// Keeps <see cref="PrestoDefaults.ReducedMotion"/> on the <see cref="Engine.Current"/> engine in step with the OS
    /// reduce-motion setting until the returned handle is disposed, which stops following and leaves the flag as it is.
    /// </summary>
    /// <param name="probe">Reads the setting in place of the built-in one; <c>null</c> from it means unknown and leaves the flag alone.</param>
    /// <param name="changed">Called with the new value whenever following changes the flag.</param>
    /// <remarks>
    /// The built-in probe reads Windows, macOS, iOS and the browser, and returns unknown elsewhere, Android included. The
    /// setting is read now, again whenever a window or the app is activated, on browser change events, and on
    /// <see cref="RefreshSystemReducedMotion"/>.
    /// </remarks>
    public static IDisposable FollowSystemReducedMotion(Func<bool?>? probe = null, Action<bool>? changed = null)
    {
        var engine = Engine.Current;
        probe ??= ReducedMotionProbe.Read;
        return ReadSystemReducedMotion(() =>
        {
            if (probe() is not { } reduced || reduced == engine.Defaults.ReducedMotion) return;
            engine.Defaults.ReducedMotion = reduced;
            changed?.Invoke(reduced);
        });
    }

    /// <summary>
    /// Reports the OS reduce-motion setting now and whenever it changes until the returned handle is disposed, without
    /// touching <see cref="PrestoDefaults.ReducedMotion"/>; it is read at the same moments <see cref="FollowSystemReducedMotion"/> reads it.
    /// </summary>
    /// <param name="changed">Called with the setting; <c>null</c> means the platform cannot read it.</param>
    /// <param name="probe">Reads the setting in place of the built-in one.</param>
    public static IDisposable WatchSystemReducedMotion(Action<bool?> changed, Func<bool?>? probe = null)
    {
        probe ??= ReducedMotionProbe.Read;
        var reported = false;
        bool? last = null;
        return ReadSystemReducedMotion(() =>
        {
            var reduced = probe();
            if (reported && reduced == last) return;
            reported = true;
            last = reduced;
            changed(reduced);
        });
    }

    /// <summary>Reads the OS reduce-motion setting again for every <see cref="FollowSystemReducedMotion"/> and <see cref="WatchSystemReducedMotion"/> still active.</summary>
    public static void RefreshSystemReducedMotion()
    {
        foreach (var reader in s_systemMotionReaders.ToArray()) reader.Read();
    }

    private static SystemMotionReader ReadSystemReducedMotion(Action read)
    {
        if (!s_reducedMotionHooked)
        {
            s_reducedMotionHooked = true;
            WindowBase.IsActiveProperty.Changed.AddClassHandler<WindowBase>((window, _) =>
            {
                if (window.IsActive) RefreshSystemReducedMotion();
            });
            if (Application.Current?.TryGetFeature<IActivatableLifetime>() is { } lifetime) lifetime.Activated += (_, _) => RefreshSystemReducedMotion();
            ReducedMotionProbe.Listen(RefreshSystemReducedMotion);
        }
        var reader = new SystemMotionReader(read);
        s_systemMotionReaders.Add(reader);
        read();
        return reader;
    }

    private sealed class SystemMotionReader(Action read) : IDisposable
    {
        public void Read() => read();

        public void Dispose() => s_systemMotionReaders.Remove(this);
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
