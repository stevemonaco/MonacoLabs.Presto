using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Logging;
using Avalonia.Media;
using Avalonia.Metadata;
using Avalonia.VisualTree;
using MonacoLabs.Presto.Gestures;
using Path = Avalonia.Controls.Shapes.Path;

namespace MonacoLabs.Presto.Controls;

/// <summary>
/// Pull to refresh over its <see cref="Content"/>: dragging the content down from its top uncovers an arrow that turns and a
/// ring that draws as the pull nears <see cref="Threshold"/>. Let go past it, and <see cref="Refresh"/> runs with the content
/// held at the threshold and the ring spinning, then the content springs back.
/// </summary>
/// <remarks>
/// <para>
/// The pull is the pointer's travel <c>d</c> damped to <c>r(1 − e^(−d / r))</c>, with <c>r</c> 2.2 times the threshold, so
/// it slows as it goes and never reaches <c>r</c>. The damping is applied to the travel, not to the drawn pull, so a release
/// starts from where the content is drawn and lands exactly on the threshold. A pull starts only on a drag mostly downward,
/// and not while a <see cref="ScrollViewer"/> under the pointer is scrolled away from its top, which keeps the drag.
/// </para>
/// <para>
/// Under <see cref="PrestoDefaults.ReducedMotion"/>, read at each release, the content lands at once, the arrow hides and
/// shows at once, and the ring holds still while the refresh runs.
/// </para>
/// </remarks>
public sealed class PullToRefresh : Control, IGestureMember
{
    private const double Reach = 2.2;
    private const double RingShare = .8;

    /// <summary>The content that is pulled, usually a list or a <see cref="ScrollViewer"/>.</summary>
    public static readonly StyledProperty<Control?> ContentProperty =
        AvaloniaProperty.Register<PullToRefresh, Control?>(nameof(Content));

    /// <summary>How far, in DIPs, the content has to be pulled for a release to refresh; 64 by default.</summary>
    public static readonly StyledProperty<double> ThresholdProperty =
        AvaloniaProperty.Register<PullToRefresh, double>(nameof(Threshold), 64);

    /// <summary>The brush the arrow and the ring are drawn in.</summary>
    public static readonly StyledProperty<IBrush?> ForegroundProperty =
        TextElement.ForegroundProperty.AddOwner<PullToRefresh>();

    private readonly Decorator _host = new();
    private readonly Panel _indicator = new() { VerticalAlignment = VerticalAlignment.Top, IsHitTestVisible = false };
    private readonly Panel _track = new();
    private readonly Ellipse _ring = new() { Width = 28, Height = 28, StrokeThickness = 3, StrokeLineCap = PenLineCap.Round };
    private readonly Path _arrow = new()
    {
        Data = Geometry.Parse("M6,1 L6,11 M2,7 L6,11 L10,7"),
        StrokeThickness = 2,
        StrokeLineCap = PenLineCap.Round,
        StrokeJoin = PenLineJoin.Round,
        HorizontalAlignment = HorizontalAlignment.Center,
        VerticalAlignment = VerticalAlignment.Center,
    };
    private readonly MotionValue _pull = new(Engine.Current, [], 0);
    private readonly GestureGate _gate;
    private Animatable? _animate;
    private IPointer? _pointer;
    private Point _pressedAt;
    private ScrollViewer? _scroller;
    private bool _dragging;
    private double _grabbedAt;
    private double _pulled;
    private bool _refreshing;

    static PullToRefresh() => ClipToBoundsProperty.OverrideDefaultValue<PullToRefresh>(true);

    /// <summary>Creates a pull to refresh with no content.</summary>
    public PullToRefresh()
    {
        _ring.Bind(Shape.StrokeProperty, this.GetObservable(ForegroundProperty));
        _arrow.Bind(Shape.StrokeProperty, this.GetObservable(ForegroundProperty));
        var progress = _pull.Map(p => Math.Clamp(p / Threshold, 0, 1));
        Follow.SetDraw(_ring, progress.Map(p => p * RingShare));
        Follow.SetRotate(_arrow, progress.Map(p => p * 180));
        _indicator.Children.AddRange([_ring, _arrow]);
        _track.Children.AddRange([_indicator, _host]);
        PlaceIndicator();
        _gate = GestureGate.SameAxis(Orientation.Vertical) with { Accepts = travel => travel.Y > 0 && _scroller is not { Offset.Y: > 0 } };
        VisualChildren.Add(_track);
        LogicalChildren.Add(_track);
        AddHandler(PointerPressedEvent, HandlePressed, handledEventsToo: true);
        AddHandler(PointerMovedEvent, HandleMoved, handledEventsToo: true);
        AddHandler(PointerReleasedEvent, HandleReleased, handledEventsToo: true);
        PointerCaptureLost += HandleCaptureLost;
    }

    /// <inheritdoc cref="ContentProperty"/>
    [Content]
    public Control? Content
    {
        get => GetValue(ContentProperty);
        set => SetValue(ContentProperty, value);
    }

    /// <inheritdoc cref="ThresholdProperty"/>
    public double Threshold
    {
        get => GetValue(ThresholdProperty);
        set => SetValue(ThresholdProperty, value);
    }

    /// <inheritdoc cref="ForegroundProperty"/>
    public IBrush? Foreground
    {
        get => GetValue(ForegroundProperty);
        set => SetValue(ForegroundProperty, value);
    }

    /// <summary>
    /// The work a release past <see cref="Threshold"/> runs; the content is held at the threshold until its task ends. A
    /// faulted or canceled task is logged through Avalonia's <see cref="Logger"/>, not rethrown, and the content still springs back.
    /// </summary>
    public Func<Task>? Refresh { get; set; }

    /// <summary>How far the content is pulled down, in DIPs, as it renders.</summary>
    public MotionValue Pull => _pull;

    Visual IGestureMember.Element => this;

    IInputElement IGestureMember.CaptureTarget => this;

    private double Range => Reach * Threshold;

    /// <inheritdoc/>
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ContentProperty) _host.Child = Content;
        else if (change.Property == ThresholdProperty) PlaceIndicator();
    }

    // Above the content's top, so the pull uncovers it.
    private void PlaceIndicator()
    {
        _indicator.Height = Threshold;
        _indicator.Margin = new Thickness(0, -Threshold, 0, 0);
    }

    private Animatable Animator()
    {
        if (_animate is not null) return _animate;
        _animate = _track.CreateAnimatable(new AvaloniaAnimatableParams { ["Y"] = 0 });
        // The animatable's OnUpdate can run before the property renders in a frame, so the render itself is followed.
        var animate = _animate;
        animate.Animations.Values.Single()._onRender += _ => _pull.Assign(animate.Get("Y"));
        return animate;
    }

    private void HandlePressed(object? sender, PointerPressedEventArgs e)
    {
        if (_pointer is not null || _refreshing) return;
        if (e.Pointer.Type == PointerType.Mouse && !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        _pointer = e.Pointer;
        _pressedAt = e.GetPosition(null);
        _scroller = (e.Source as Visual)?.GetSelfAndVisualAncestors().TakeWhile(visual => visual != this).OfType<ScrollViewer>().FirstOrDefault();
        GestureArena.Join(e, this, _gate);
    }

    private void HandleMoved(object? sender, PointerEventArgs e)
    {
        if (!_dragging || e.Pointer != _pointer) return;
        e.Handled = true;
        // Gesture recognizers see handled events too, and an inner scroll would steal the capture.
        e.PreventGestureRecognition();
        _pulled = Range * (1 - Math.Exp(-Math.Max(0, _grabbedAt + (e.GetPosition(null) - _pressedAt).Y) / Range));
        Animator().Set("Y", _pulled, 0);
    }

    private void HandleReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (!_dragging || e.Pointer != _pointer) return;
        e.Handled = true;
        Lift();
    }

    private void HandleCaptureLost(object? sender, PointerCaptureLostEventArgs e)
    {
        if (_dragging && e.Pointer == _pointer) Lift();
    }

    void IGestureMember.Won()
    {
        _dragging = true;
        // Picks a pull still springing back up where it is drawn.
        var drawn = Math.Clamp(_animate?.Get("Y") ?? 0, 0, Range - 1);
        _grabbedAt = -Range * Math.Log(1 - (drawn / Range));
    }

    void IGestureMember.Lost() => _pointer = null;

    private void Lift()
    {
        // Cleared first: dropping the capture raises PointerCaptureLost, which lands back here.
        var pointer = _pointer!;
        _pointer = null;
        _dragging = false;
        if (ReferenceEquals(pointer.Captured, this)) pointer.Capture(null);
        if (_pulled >= Threshold) RunRefresh();
        else Settle(0);
    }

    private async void RunRefresh()
    {
        _refreshing = true;
        var reduced = Engine.Current.Defaults.ReducedMotion;
        var fade = reduced ? 0 : Recipes.Tokens.Short;
        Settle(Threshold);
        _ = _arrow.Animate(new AvaloniaAnimationParams { ["Opacity"] = 0, Duration = fade });
        var spin = reduced ? null : _ring.Animate(new AvaloniaAnimationParams { ["Rotate"] = 360, Duration = 800, Ease = Eases.None, Loop = true });
        try
        {
            if (Refresh is { } work) await work();
        }
        catch (Exception e)
        {
            Logger.TryGet(e is OperationCanceledException ? LogEventLevel.Warning : LogEventLevel.Error, LogArea.Control)
                ?.Log(this, "PullToRefresh.Refresh ended with {Exception}", e);
        }
        finally
        {
            spin?.Revert();
            _ = _arrow.Animate(new AvaloniaAnimationParams { ["Opacity"] = 1, Duration = fade });
            _refreshing = false;
            Settle(0);
        }
    }

    private void Settle(double to)
    {
        if (Engine.Current.Defaults.ReducedMotion)
        {
            Animator().Set("Y", to, 0);
            return;
        }
        var spring = new Spring(bounce: .2, duration: 600);
        Animator().Set("Y", to, spring.SettlingDuration, spring);
    }
}
