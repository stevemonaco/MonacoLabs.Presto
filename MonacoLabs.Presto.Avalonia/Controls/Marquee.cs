using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using MonacoLabs.Presto.Core;
using MonacoLabs.Presto.Transforms;

namespace MonacoLabs.Presto.Controls;

/// <summary>
/// Continuously scrolling content: the content laid out once at its own length along the scroll axis and repeated end to
/// end, <see cref="Gap"/> apart, on a private strip that covers the marquee at every phase of the loop.
/// </summary>
/// <remarks>
/// The strip moves at <see cref="Speed"/> through engine time, and every change of speed, direction or pause eases over
/// <see cref="MotionTokens.Medium"/>. Copies of data content are built from the same <see cref="ContentControl.ContentTemplate"/>, so they take input; copies
/// of a <see cref="Control"/> content are <see cref="VisualBrush"/> mirrors that take no hits. Screen readers and Tab see
/// the content once.
/// </remarks>
public sealed class Marquee : ContentControl
{
    /// <summary>How fast the content travels, in DIPs per second.</summary>
    public static readonly StyledProperty<double> SpeedProperty =
        AvaloniaProperty.Register<Marquee, double>(nameof(Speed), 60);

    /// <summary>Which way the content travels; Left and Right scroll horizontally, Up and Down vertically.</summary>
    public static readonly StyledProperty<MarqueeDirection> DirectionProperty =
        AvaloniaProperty.Register<Marquee, MarqueeDirection>(nameof(Direction));

    /// <summary>The space between the end of one copy of the content and the start of the next, in DIPs.</summary>
    public static readonly StyledProperty<double> GapProperty =
        AvaloniaProperty.Register<Marquee, double>(nameof(Gap), 24);

    /// <summary>How far, in DIPs, the content fades out toward both ends of the axis; 0, the default, draws no fade.</summary>
    public static readonly StyledProperty<double> FadeEdgesProperty =
        AvaloniaProperty.Register<Marquee, double>(nameof(FadeEdges));

    /// <summary>Whether the marquee stops while the pointer is over it or keyboard focus is inside it.</summary>
    public static readonly StyledProperty<bool> PauseOnHoverProperty =
        AvaloniaProperty.Register<Marquee, bool>(nameof(PauseOnHover), true);

    /// <summary>Whether the marquee is stopped; bind it two-way to a pause button.</summary>
    public static readonly StyledProperty<bool> IsPausedProperty =
        AvaloniaProperty.Register<Marquee, bool>(nameof(IsPaused), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Whether the marquee always scrolls or only while its content is longer than it.</summary>
    public static readonly StyledProperty<MarqueeMode> ModeProperty =
        AvaloniaProperty.Register<Marquee, MarqueeMode>(nameof(Mode));

    private readonly Strip _strip;
    private readonly ContentPresenter _content;
    private readonly Velocity _velocity = new();
    private Animatable? _speed;
    private Timer? _timer;
    private double _target;
    private double _offset;
    private double _time;
    private bool _attached;
    private bool _reduced;
    private bool _keyboardFocus;
    private bool _mirrors;
    private bool _scrolling;
    private double _length;
    private double _period;
    private (double Viewport, double Fade, bool Horizontal) _mask;

    static Marquee()
    {
        ClipToBoundsProperty.OverrideDefaultValue<Marquee>(true);
        AffectsMeasure<Marquee>(DirectionProperty);
        AffectsArrange<Marquee>(GapProperty, FadeEdgesProperty, ModeProperty, HorizontalContentAlignmentProperty, VerticalContentAlignmentProperty);
    }

    /// <summary>Creates an empty marquee.</summary>
    public Marquee()
    {
        // Local, so a theme's ContentControl template cannot replace the strip.
        Template = null;
        _content = CreatePresenter();
        _strip = new Strip(this) { Children = { _content } };
        VisualChildren.Add(_strip);
        LogicalChildren.Add(_strip);
        AddHandler(GotFocusEvent, HandleGotFocus);
    }

    /// <inheritdoc cref="SpeedProperty"/>
    public double Speed
    {
        get => GetValue(SpeedProperty);
        set => SetValue(SpeedProperty, value);
    }

    /// <inheritdoc cref="DirectionProperty"/>
    public MarqueeDirection Direction
    {
        get => GetValue(DirectionProperty);
        set => SetValue(DirectionProperty, value);
    }

    /// <inheritdoc cref="GapProperty"/>
    public double Gap
    {
        get => GetValue(GapProperty);
        set => SetValue(GapProperty, value);
    }

    /// <inheritdoc cref="FadeEdgesProperty"/>
    public double FadeEdges
    {
        get => GetValue(FadeEdgesProperty);
        set => SetValue(FadeEdgesProperty, value);
    }

    /// <inheritdoc cref="PauseOnHoverProperty"/>
    public bool PauseOnHover
    {
        get => GetValue(PauseOnHoverProperty);
        set => SetValue(PauseOnHoverProperty, value);
    }

    /// <inheritdoc cref="IsPausedProperty"/>
    public bool IsPaused
    {
        get => GetValue(IsPausedProperty);
        set => SetValue(IsPausedProperty, value);
    }

    /// <inheritdoc cref="ModeProperty"/>
    public MarqueeMode Mode
    {
        get => GetValue(ModeProperty);
        set => SetValue(ModeProperty, value);
    }

    private bool Horizontal => IsHorizontal(Direction);

    private TransformShorthand Shorthand => Horizontal ? TransformShorthand.X : TransformShorthand.Y;

    private bool Held => IsPaused || (PauseOnHover && (IsPointerOver || _keyboardFocus));

    /// <inheritdoc/>
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == DirectionProperty && IsHorizontal(change.GetOldValue<MarqueeDirection>()) != Horizontal)
        {
            RenderTransformComposer.Revert(_strip, Horizontal ? TransformShorthand.Y : TransformShorthand.X);
            _offset = 0;
        }
        else if (change.Property == IsPointerOverProperty || change.Property == IsKeyboardFocusWithinProperty)
        {
            _keyboardFocus &= IsKeyboardFocusWithin;
            Run();
        }
        else if (change.Property == SpeedProperty || change.Property == IsPausedProperty || change.Property == PauseOnHoverProperty)
        {
            Run();
        }
    }

    /// <inheritdoc/>
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _attached = true;
        CheckReducedMotion();
        Run();
    }

    /// <inheritdoc/>
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _attached = false;
        _timer?.Pause();
    }

    /// <inheritdoc/>
    protected override Size MeasureOverride(Size availableSize)
    {
        _strip.Measure(availableSize);
        var size = _content.DesiredSize;
        return Horizontal
            ? size.WithWidth(Math.Min(size.Width, availableSize.Width))
            : size.WithHeight(Math.Min(size.Height, availableSize.Height));
    }

    /// <inheritdoc/>
    protected override Size ArrangeOverride(Size finalSize)
    {
        CheckReducedMotion();
        var horizontal = Horizontal;
        var viewport = horizontal ? finalSize.Width : finalSize.Height;
        _length = horizontal ? _content.DesiredSize.Width : _content.DesiredSize.Height;
        _period = _length + Gap;
        _scrolling = !_reduced && _length > 0 && _period > 0 && (Mode == MarqueeMode.Always || _length > viewport);
        SetCopies(_scrolling ? (int)Math.Ceiling(viewport / _period) : 0);
        _strip.Arrange(new Rect(finalSize));
        UpdateMask(viewport, horizontal);
        Run();
        return finalSize;
    }

    private static bool IsHorizontal(MarqueeDirection direction) => direction is MarqueeDirection.Left or MarqueeDirection.Right;

    private void CheckReducedMotion()
    {
        var reduced = Engine.Current.Defaults.ReducedMotion;
        if (reduced == _reduced) return;
        _reduced = reduced;
        InvalidateArrange();
        Run();
    }

    // Aims the speed channel at the speed the marquee should show, then starts, rests or stops the strip.
    private void Run()
    {
        if (!_attached) return;
        var target = Held ? 0 : Direction is MarqueeDirection.Left or MarqueeDirection.Up ? -Speed : Speed;
        if (_speed is null)
        {
            _velocity.Value = target;
            _speed = Presto.CreateAnimatable(_velocity, new AnimatableParams { [nameof(Velocity.Value)] = Recipes.Tokens.Medium * Globals.TimeScale });
        }
        else if (target != _target)
        {
            _speed.Set(nameof(Velocity.Value), target, Recipes.Tokens.Medium * Globals.TimeScale, Recipes.Tokens.Ease);
        }
        _target = target;
        if (!_scrolling || _reduced)
        {
            _offset = 0;
            RenderTransformComposer.Revert(_strip, Shorthand);
            _timer?.Pause();
            return;
        }
        Write();
        if (target == 0 && _velocity.Value == 0)
        {
            _timer?.Pause();
            return;
        }
        _timer ??= Presto.CreateTimer(new TimerParams { Duration = Constants.MaxValue, Autoplay = false, OnUpdate = Advance });
        if (!_timer.Paused) return;
        // Resume moves a timer a frame ahead, so the first frame after it only takes the time.
        _time = double.NaN;
        _timer.Resume();
    }

    private void Advance(Timer timer)
    {
        var time = timer.CurrentTime;
        var elapsed = double.IsNaN(_time) ? 0 : time - _time;
        _time = time;
        CheckReducedMotion();
        if (timer.Paused) return;
        var velocity = _velocity.Value;
        // Speed is DIPs per second, so seconds mode does not rescale it.
        _offset += velocity * elapsed / (Constants.K * Globals.TimeScale);
        Write();
        if (_target == 0 && velocity == 0) timer.Pause();
    }

    // Wraps the offset into the period behind the strip's direction, where the copies cover the viewport.
    private void Write()
    {
        var forward = Direction is MarqueeDirection.Right or MarqueeDirection.Down;
        var offset = _offset % _period;
        if (forward ? offset < 0 : offset > 0) offset += forward ? _period : -_period;
        _offset = offset;
        RenderTransformComposer.Set(_strip, Shorthand, offset);
    }

    private void HandleGotFocus(object? sender, FocusChangedEventArgs e)
    {
        _keyboardFocus = e.NavigationMethod is NavigationMethod.Tab or NavigationMethod.Directional;
        Run();
    }

    private ContentPresenter CreatePresenter() => new()
    {
        [!ContentPresenter.ContentProperty] = this[!ContentProperty],
        [!ContentPresenter.ContentTemplateProperty] = this[!ContentTemplateProperty],
    };

    private void SetCopies(int count)
    {
        var children = _strip.Children;
        var mirrors = Content is Control;
        if (mirrors != _mirrors)
        {
            children.RemoveRange(1, children.Count - 1);
            _mirrors = mirrors;
        }
        if (children.Count - 1 > count) children.RemoveRange(count + 1, children.Count - 1 - count);
        while (children.Count - 1 < count)
        {
            Control copy = _mirrors
                ? new Border
                {
                    Background = new VisualBrush(_content) { Stretch = Stretch.None, AlignmentX = AlignmentX.Left, AlignmentY = AlignmentY.Top },
                    IsHitTestVisible = false,
                }
                : CreatePresenter();
            AutomationProperties.SetAccessibilityView(copy, AccessibilityView.Raw);
            KeyboardNavigation.SetTabNavigation(copy, KeyboardNavigationMode.None);
            children.Add(copy);
        }
    }

    // The mask's stops are relative, so only a new viewport, fade or axis changes them.
    private void UpdateMask(double viewport, bool horizontal)
    {
        var mask = (viewport, FadeEdges, horizontal);
        if (mask == _mask) return;
        _mask = mask;
        if (FadeEdges <= 0 || viewport <= 0)
        {
            ClearValue(OpacityMaskProperty);
            return;
        }
        var ramp = Math.Min(FadeEdges, viewport / 2) / viewport;
        OpacityMask = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(horizontal ? 1 : 0, horizontal ? 0 : 1, RelativeUnit.Relative),
            GradientStops =
            {
                new GradientStop(Colors.Transparent, 0),
                new GradientStop(Colors.Black, ramp),
                new GradientStop(Colors.Black, 1 - ramp),
                new GradientStop(Colors.Transparent, 1),
            },
        };
    }

    private sealed class Strip(Marquee marquee) : Panel
    {
        protected override Size MeasureOverride(Size availableSize)
        {
            var constraint = marquee.Horizontal
                ? availableSize.WithWidth(double.PositiveInfinity)
                : availableSize.WithHeight(double.PositiveInfinity);
            foreach (var child in Children) child.Measure(constraint);
            return Children[0].DesiredSize;
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            if (!marquee._scrolling)
            {
                var (at, length) = Fitted(finalSize);
                Place(Children[0], finalSize, at, length);
                return finalSize;
            }
            var slot = marquee.Direction is MarqueeDirection.Right or MarqueeDirection.Down ? -1 : 0;
            for (var i = 1; i < Children.Count; i++, slot++)
            {
                if (slot == 0) slot++;
                Place(Children[i], finalSize, slot * marquee._period, marquee._length);
            }
            Place(Children[0], finalSize, 0, marquee._length);
            return finalSize;
        }

        private (double At, double Length) Fitted(Size finalSize)
        {
            var viewport = marquee.Horizontal ? finalSize.Width : finalSize.Height;
            if (marquee._length > viewport) return (0, marquee._length);
            var alignment = marquee.Horizontal
                ? marquee.HorizontalContentAlignment switch
                {
                    HorizontalAlignment.Center => 0.5,
                    HorizontalAlignment.Right => 1,
                    HorizontalAlignment.Stretch => double.NaN,
                    _ => 0,
                }
                : marquee.VerticalContentAlignment switch
                {
                    VerticalAlignment.Center => 0.5,
                    VerticalAlignment.Bottom => 1,
                    VerticalAlignment.Stretch => double.NaN,
                    _ => 0,
                };
            return double.IsNaN(alignment) ? (0, viewport) : ((viewport - marquee._length) * alignment, marquee._length);
        }

        private void Place(Control child, Size finalSize, double at, double length) =>
            child.Arrange(marquee.Horizontal
                ? new Rect(at, 0, length, finalSize.Height)
                : new Rect(0, at, finalSize.Width, length));
    }

    private sealed class Velocity
    {
        public double Value { get; set; }
    }
}
