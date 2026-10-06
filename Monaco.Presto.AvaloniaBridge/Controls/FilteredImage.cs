using Avalonia;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace Monaco.Presto.AvaloniaBridge.Controls;

/// <summary>
/// An <see cref="Avalonia.Controls.Image"/> with animatable CSS-style colour filters such as <see cref="PrestoImageBase.Grayscale"/>
/// and <see cref="PrestoImageBase.HueRotate"/>, which can swap its source through an <see cref="ImageTransition"/>.
/// </summary>
public sealed class FilteredImage : PrestoImageBase
{
    /// <summary>Defines the <see cref="SourceTransition"/> property.</summary>
    public static readonly StyledProperty<ImageTransition?> SourceTransitionProperty =
        AvaloniaProperty.Register<FilteredImage, ImageTransition?>(nameof(SourceTransition));

    /// <summary>
    /// How far the running transition has come, from 0 (the outgoing image) to 1 (only <see cref="PrestoImageBase.Source"/>).
    /// Internal: only <see cref="TransitionTo(IImage?, ImageTransition)"/> and <see cref="SourceTransition"/> animate it, and a revert returns it to its default of 1.
    /// </summary>
    internal static readonly StyledProperty<double> TransitionProgressProperty =
        AvaloniaProperty.Register<FilteredImage, double>(nameof(TransitionProgress), 1);

    private IImage? _incoming;
    private IImage? _outgoing;
    private Raster? _snapshot;
    private ImageTransition? _transition;
    private Animation? _animation;
    private bool _swapping;

    static FilteredImage() => AffectsRender<FilteredImage>(TransitionProgressProperty);

    /// <summary>
    /// The transition played from the old image whenever <see cref="PrestoImageBase.Source"/> changes while attached, including
    /// through a binding; null, the default, snaps as <see cref="Avalonia.Controls.Image"/> does.
    /// </summary>
    public ImageTransition? SourceTransition
    {
        get => GetValue(SourceTransitionProperty);
        set => SetValue(SourceTransitionProperty, value);
    }

    internal double TransitionProgress => GetValue(TransitionProgressProperty);

    internal IImage? Outgoing => _outgoing;

    /// <summary>
    /// Sets <see cref="PrestoImageBase.Source"/> to <paramref name="source"/> now and plays <paramref name="transition"/> from the
    /// previous image, or from what is on screen when a transition is already running. Reverting the animation snaps to
    /// <paramref name="source"/>; the same source with no transition running changes nothing and only runs the timing.
    /// </summary>
    public Animation TransitionTo(IImage? source, ImageTransition transition)
    {
        if (_transition is null && source == Source) return this.Animate(transition.Timing);

        var previous = Source;
        _swapping = true;
        try
        {
            Source = source;
        }
        finally
        {
            _swapping = false;
        }
        return Begin(previous, transition);
    }

    /// <inheritdoc cref="TransitionTo(IImage?, ImageTransition)"/>
    public Animation TransitionTo(IImage? source, ImageTransitionKind kind = ImageTransitionKind.Crossfade, AnimationBuilder? builder = null) =>
        TransitionTo(source, new ImageTransition { Kind = kind, Builder = builder });

    /// <inheritdoc cref="TransitionTo(IImage?, ImageTransition)"/>
    public Animation TransitionTo(IImage? source, ImageTransitionKind kind, Action<AnimationBuilder> build) =>
        TransitionTo(source, kind, Presto.Build(build));

    // Source has already changed, so a snapshot draws the composite from _incoming.
    private Animation Begin(IImage? previous, ImageTransition transition)
    {
        _animation?.Cancel();
        var snapshot = _transition is not null && Bounds.Width > 0 && Bounds.Height > 0 ? Snapshot() : null;
        var outgoing = snapshot?.Bitmap ?? previous;
        Release();
        (_incoming, _outgoing, _snapshot, _transition) = (Source, outgoing, snapshot, transition);
        return _animation = this.Animate(transition.Timing.With(b => b.Property(TransitionProgressProperty, from: 0.0, to: 1.0)));
    }

    /// <inheritdoc/>
    protected override IImage? CurrentImage => _incoming;

    /// <inheritdoc/>
    public override void Render(DrawingContext context)
    {
        if (_transition is not { } transition || TransitionProgress >= 1)
        {
            base.Render(context);
            return;
        }

        var p = TransitionProgress;
        var incoming = ImageEffect;
        // A snapshot already shows the filters it was taken with.
        var outgoing = _snapshot is null ? incoming
            : new ImageEffect(ColorMatrix.Identity, 0, incoming.RippleScale, 1, 0, incoming.DissolveScale, incoming.DissolveSoftness, incoming.Seed);
        switch (transition.Kind)
        {
            case ImageTransitionKind.Dissolve:
                DrawOutgoing(context, outgoing);
                DrawIncoming(context, incoming with { Dissolve = 1 - p });
                break;
            case ImageTransitionKind.Wipe:
                DrawOutgoing(context, outgoing);
                DrawIncoming(context, incoming with { Wipe = p, WipeAngle = transition.Angle, WipeFeather = transition.Feather });
                break;
            case ImageTransitionKind.Pixelate:
                var blocks = 1 + (transition.PeakPixelate - 1) * (1 - Math.Abs(2 * p - 1));
                if (p < 0.5) DrawOutgoing(context, outgoing with { Pixelate = blocks });
                else DrawIncoming(context, incoming with { Pixelate = blocks });
                break;
            default:
                if (transition.Kind == ImageTransitionKind.Ripple)
                {
                    var ripple = transition.PeakRipple * Math.Sin(Math.PI * p);
                    (outgoing, incoming) = (outgoing with { Ripple = ripple }, incoming with { Ripple = ripple });
                }
                DrawOutgoing(context, outgoing);
                using (context.PushOpacity(p)) DrawIncoming(context, incoming);
                break;
        }
    }

    // A snapshot is the size of the control, so it fills it rather than being placed by Stretch.
    private void DrawOutgoing(DrawingContext context, in ImageEffect effect)
    {
        if (_outgoing is null) return;
        var fill = _snapshot is not null;
        DrawImage(context, _outgoing, SourceRect(_outgoing), fill ? Stretch.Fill : Stretch, fill ? StretchDirection.Both : StretchDirection, effect, _snapshot);
    }

    private void DrawIncoming(DrawingContext context, in ImageEffect effect)
    {
        if (_incoming is { } source) DrawImage(context, source, SourceRect(source), Stretch, StretchDirection, effect);
    }

    /// <inheritdoc/>
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == TransitionProgressProperty && TransitionProgress >= 1) Release();
        else if (change.Property == SourceProperty && !_swapping)
        {
            if (SourceTransition is { } transition && change.OldValue is IImage previous && this.IsAttachedToVisualTree()) Begin(previous, transition);
            else _incoming = Source;
        }
    }

    /// <inheritdoc/>
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        if (_transition is not null) _animation?.Complete();
    }

    private void Release()
    {
        _snapshot?.Dispose();
        (_outgoing, _snapshot, _transition) = (null, null, null);
    }
}
