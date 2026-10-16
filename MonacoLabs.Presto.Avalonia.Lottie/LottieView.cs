using Avalonia;
using Avalonia.Media;
using Avalonia.Rendering.Composition;
using MonacoLabs.Presto.Controls;

namespace MonacoLabs.Presto.Lottie;

/// <summary>
/// Shows one <see cref="Frame"/> of a Lottie <see cref="Composition"/>, sized and stretched as an image and drawn through the
/// base's color filters, so tweening, seeking or binding <see cref="Frame"/> or <see cref="Progress"/> plays it.
/// </summary>
/// <remarks>The inherited <see cref="PrestoImageBase.Source"/> is not used.</remarks>
public sealed class LottieView : PrestoImageBase
{
    /// <summary>Defines the <see cref="Composition"/> property.</summary>
    public static readonly StyledProperty<LottieComposition?> CompositionProperty =
        AvaloniaProperty.Register<LottieView, LottieComposition?>(nameof(Composition));

    /// <summary>Defines the <see cref="Frame"/> property.</summary>
    public static readonly StyledProperty<double> FrameProperty =
        AvaloniaProperty.Register<LottieView, double>(nameof(Frame),
            coerce: static (o, v) => ((LottieView)o).Composition is { } c ? Math.Clamp(v, c.InPoint, c.OutPoint) : v);

    /// <summary>Defines the <see cref="Progress"/> property.</summary>
    public static readonly StyledProperty<double> ProgressProperty =
        AvaloniaProperty.Register<LottieView, double>(nameof(Progress), coerce: static (_, v) => Math.Clamp(v, 0, 1));

    /// <summary>Defines the <see cref="RenderThread"/> property.</summary>
    public static readonly StyledProperty<bool> RenderThreadProperty =
        AvaloniaProperty.Register<LottieView, bool>(nameof(RenderThread));

    private LottiePlayerHandle? _player;
    private CompositionCustomVisual? _visual;
    private LottieVisualHandler.PlayMessage? _playRequest;
    private bool _syncing;

    static LottieView()
    {
        AffectsRender<LottieView>(CompositionProperty, FrameProperty, ProgressProperty);
        AffectsMeasure<LottieView>(CompositionProperty);
    }

    /// <summary>The Lottie animation to show.</summary>
    public LottieComposition? Composition
    {
        get => GetValue(CompositionProperty);
        set => SetValue(CompositionProperty, value);
    }

    /// <summary>The composition frame shown, fractional frames included, clamped to <see cref="LottieComposition.InPoint"/>..<see cref="LottieComposition.OutPoint"/>.</summary>
    public double Frame
    {
        get => GetValue(FrameProperty);
        set => SetValue(FrameProperty, value);
    }

    /// <summary><see cref="Frame"/> as a fraction from 0 at <see cref="LottieComposition.InPoint"/> to 1 at <see cref="LottieComposition.OutPoint"/>.</summary>
    public double Progress
    {
        get => GetValue(ProgressProperty);
        set => SetValue(ProgressProperty, value);
    }

    /// <summary>
    /// Draws the composition on the render thread, where <see cref="PlayOnRenderThread"/> plays it, so a spinner keeps moving
    /// while the UI thread is blocked.
    /// </summary>
    /// <remarks>
    /// Render-thread playback does not update <see cref="Frame"/>. Writing <see cref="Frame"/> or <see cref="Composition"/>
    /// still seeks the drawing, and stops render-thread playback. The Browser renders on the UI thread, so nothing
    /// survives a blocked UI thread there.
    /// </remarks>
    public bool RenderThread
    {
        get => GetValue(RenderThreadProperty);
        set => SetValue(RenderThreadProperty, value);
    }

    private double InPoint => Composition?.InPoint ?? 0;

    private double OutPoint => Composition?.OutPoint ?? 0;

    private double FrameCount => OutPoint - InPoint;

    private double FrameRate => Composition?.FrameRate ?? 0;

    /// <inheritdoc/>
    protected override IImage? CurrentImage => Composition;

    /// <summary>Tweens <see cref="Frame"/> linearly from <see cref="LottieComposition.InPoint"/> to <see cref="LottieComposition.OutPoint"/>, or back when <paramref name="reverse"/> is set, at the composition's frame rate times <paramref name="speed"/>.</summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="speed"/> is not above 0.</exception>
    public Animation Play(bool loop = false, bool reverse = false, double speed = 1)
    {
        ThrowIfNotPositive(speed);
        return PlayRange(Segment(null, reverse), loop, speed);
    }

    /// <summary>Plays the segment of the marker named <paramref name="marker"/>, ignoring case, as <see cref="Play(bool, bool, double)"/> plays the whole composition.</summary>
    /// <exception cref="ArgumentException">The composition has no such marker.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="speed"/> is not above 0.</exception>
    public Animation Play(string marker, bool loop = false, bool reverse = false, double speed = 1)
    {
        ThrowIfNotPositive(speed);
        return PlayRange(Segment(marker, reverse), loop, speed);
    }

    /// <summary>Tweens <see cref="Frame"/> linearly from <paramref name="from"/> to <paramref name="to"/>, backwards when <paramref name="to"/> is the lower.</summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="speed"/> is not above 0.</exception>
    public Animation PlaySegment(double from, double to, bool loop = false, double speed = 1)
    {
        ThrowIfNotPositive(speed);
        return PlayRange(new(null, from, to), loop, speed);
    }

    /// <summary>
    /// Plays the marker named <paramref name="segment"/>, or the whole composition when null, on the render thread while
    /// <see cref="RenderThread"/> is set, timed as <see cref="Play(string, bool, bool, double)"/> times it.
    /// </summary>
    /// <remarks>The request is kept, so it starts once the view is attached with <see cref="RenderThread"/> set and restarts each time that happens again.</remarks>
    /// <exception cref="ArgumentException">The composition has no such marker.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="speed"/> is not above 0.</exception>
    public void PlayOnRenderThread(string? segment = null, bool loop = true, double speed = 1)
    {
        ThrowIfNotPositive(speed);
        var range = Segment(segment, false);
        _playRequest = new(range.From, range.To, Milliseconds(range, speed), loop);
        _visual?.SendHandlerMessage(_playRequest);
    }

    /// <summary>Stops render-thread playback, holding the frame it reached.</summary>
    public void StopRenderThread()
    {
        _playRequest = null;
        _visual?.SendHandlerMessage(LottieVisualHandler.StopMessage);
    }

    internal LottieSegment Segment(string? marker, bool reverse)
    {
        var segment = marker is null ? new LottieSegment(null, InPoint, OutPoint)
            : Composition?.TryGetSegment(marker, out var named) == true ? named
            : throw new ArgumentException(
                $"The Lottie composition has no marker '{marker}'. Markers: {string.Join(", ", Composition?.Segments.Select(s => s.Name) ?? [])}.", nameof(marker));
        return reverse ? new(segment.Name, segment.To, segment.From) : segment;
    }

    internal static void ThrowIfNotPositive(double speed)
    {
        if (!(speed > 0))
            throw new ArgumentOutOfRangeException(nameof(speed), speed, "Speed must be above 0.");
    }

    internal double Milliseconds(LottieSegment segment, double speed) =>
        FrameRate > 0 ? Math.Abs(segment.To - segment.From) / FrameRate * 1000 / speed : 0;

    /// <inheritdoc/>
    public override void Render(DrawingContext context)
    {
        var composition = Composition;
        var (placed, _) = composition is null ? default : Place(composition.Size, Stretch, StretchDirection);
        var bounds = placed.Intersect(new Rect(Bounds.Size));
        if (_visual is { } visual)
        {
            visual.SendHandlerMessage(new LottieVisualHandler.SceneMessage(composition, Frame, placed, bounds, ImageEffect));
            return;
        }
        if (composition is null || bounds.Width <= 0 || bounds.Height <= 0) return;
        _player ??= new LottiePlayerHandle(composition);
        var operation = new LottieDrawOperation(_player.Retain(), Frame, placed, bounds, ImageEffect);
        Draw(context, operation);
        // An immediate context, such as a Presence ghost's RenderTargetBitmap, renders synchronously and never disposes.
        if (operation.Rendered) operation.Dispose();
    }

    /// <inheritdoc/>
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == CompositionProperty)
        {
            _playRequest = null;
            ReleasePlayer();
            Sync(FrameProperty, InPoint);
            Sync(ProgressProperty, 0);
        }
        else if (change.Property == FrameProperty)
        {
            _playRequest = null;
            if (FrameCount > 0) Sync(ProgressProperty, (Frame - InPoint) / FrameCount);
        }
        else if (change.Property == ProgressProperty) Sync(FrameProperty, InPoint + Progress * FrameCount);
        else if (change.Property == RenderThreadProperty)
        {
            if (RenderThread) AttachVisual();
            else DetachVisual();
        }
        else if (change.Property == BoundsProperty && _visual is { } visual) visual.Size = new Vector(Bounds.Width, Bounds.Height);
    }

    /// <inheritdoc/>
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (RenderThread) AttachVisual();
    }

    /// <inheritdoc/>
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        ReleasePlayer();
        DetachVisual();
    }

    private void AttachVisual()
    {
        if (ElementComposition.GetElementVisual(this) is not { } element) return;
        ReleasePlayer();
        _visual = element.Compositor.CreateCustomVisual(new LottieVisualHandler());
        _visual.Size = new Vector(Bounds.Width, Bounds.Height);
        ElementComposition.SetElementChildVisual(this, _visual);
        if (_playRequest is { } play) _visual.SendHandlerMessage(play);
        InvalidateVisual();
    }

    private void DetachVisual()
    {
        if (_visual is not { } visual) return;
        visual.SendHandlerMessage(LottieVisualHandler.DetachMessage);
        ElementComposition.SetElementChildVisual(this, null);
        _visual = null;
        InvalidateVisual();
    }

    private Animation PlayRange(LottieSegment segment, bool loop, double speed) =>
        Prebuilt.LottieRecipes.Frames(segment.From, segment.To, Milliseconds(segment, speed), loop).Animate(this);

    private void Sync(StyledProperty<double> property, double value)
    {
        if (_syncing) return;
        _syncing = true;
        try
        {
            SetCurrentValue(property, value);
        }
        finally
        {
            _syncing = false;
        }
    }

    private void ReleasePlayer()
    {
        _player?.Dispose();
        _player = null;
    }
}
