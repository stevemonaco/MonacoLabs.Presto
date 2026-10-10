using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Metadata;
using MonacoLabs.Presto.Draggables;
using MonacoLabs.Presto.Core;

namespace MonacoLabs.Presto.Controls;

/// <summary>
/// A carousel: its <see cref="Slides"/> side by side, each as wide as the pager, on a strip that a horizontal drag throws
/// through <see cref="Inertia"/> and snaps to a slide. Left, Right, Home and End page it, <see cref="GoTo"/> springs it to
/// a slide, and it can loop and autoplay.
/// </summary>
/// <remarks>
/// <para>
/// Autoplay advances one slide every <see cref="AutoplayInterval"/>, wrapping to the first, and holds while the pointer is
/// over the pager, while it is dragged and while the keyboard has focus inside it. Paging by hand starts the interval over.
/// <see cref="OffsetOf"/> gives each slide's distance from the center, for parallax layers through <see cref="Follow"/>.
/// </para>
/// <para>
/// Under <see cref="PrestoDefaults.ReducedMotion"/>, read at each move, a release or <see cref="GoTo"/> lands on its slide at
/// once, and autoplay still pages, the same way, while <see cref="AutoplayProgress"/> stays at 0.
/// </para>
/// </remarks>
public sealed class Pager : Control
{
    private static readonly EaseValue s_travel = Eases.Spring(bounce: .2, duration: 700);
    private static readonly Inertia s_inertia = new(0);

    /// <summary>The slide the pager rests at or is heading for; setting it springs there.</summary>
    public static readonly StyledProperty<int> SelectedIndexProperty =
        AvaloniaProperty.Register<Pager, int>(nameof(SelectedIndex), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Whether the last slide leads on to the first, for a drag as well as for paging.</summary>
    public static readonly StyledProperty<bool> LoopProperty =
        AvaloniaProperty.Register<Pager, bool>(nameof(Loop));

    /// <summary>Milliseconds between autoplay advances; 0, the default, turns autoplay off.</summary>
    public static readonly StyledProperty<double> AutoplayIntervalProperty =
        AvaloniaProperty.Register<Pager, double>(nameof(AutoplayInterval));

    private readonly Strip _strip;
    private readonly MotionValue _position = new(Engine.Current, [], 0);
    private readonly MotionValue _autoplayProgress = new(Engine.Current, [], 0);
    private Draggable? _drag;
    private Animation? _travel;
    private Timer? _autoplay;
    private double _width;
    private bool _dragging;
    private bool _settling;
    private bool _keyboardFocus;

    static Pager()
    {
        FocusableProperty.OverrideDefaultValue<Pager>(true);
        ClipToBoundsProperty.OverrideDefaultValue<Pager>(true);
    }

    /// <summary>Creates an empty pager.</summary>
    public Pager()
    {
        _strip = new Strip(this);
        VisualChildren.Add(_strip);
        LogicalChildren.Add(_strip);
        AddHandler(GotFocusEvent, HandleGotFocus);
    }

    /// <summary>The slides, in order.</summary>
    [Content]
    public Avalonia.Controls.Controls Slides => _strip.Children;

    /// <inheritdoc cref="SelectedIndexProperty"/>
    public int SelectedIndex
    {
        get => GetValue(SelectedIndexProperty);
        set => SetValue(SelectedIndexProperty, value);
    }

    /// <inheritdoc cref="LoopProperty"/>
    public bool Loop
    {
        get => GetValue(LoopProperty);
        set => SetValue(LoopProperty, value);
    }

    /// <inheritdoc cref="AutoplayIntervalProperty"/>
    public double AutoplayInterval
    {
        get => GetValue(AutoplayIntervalProperty);
        set => SetValue(AutoplayIntervalProperty, value);
    }

    /// <summary>
    /// The slide the strip shows, as it renders, in slide widths: 1.5 is halfway from slide 1 to slide 2. A looping pager
    /// counts on past its last slide rather than wrapping.
    /// </summary>
    public MotionValue Position => _position;

    /// <summary>How far through the current autoplay interval the pager is, from 0 to 1.</summary>
    public MotionValue AutoplayProgress => _autoplayProgress;

    /// <summary>How far <paramref name="slide"/> stands from the center, in slide widths: negative to the left, positive to the right.</summary>
    public MotionValue OffsetOf(int slide) => _position.Map(p => Slot(slide, p) - p);

    /// <summary>Springs to <paramref name="index"/>, which wraps when <see cref="Loop"/> is set and is clamped to the slides otherwise.</summary>
    public void GoTo(int index)
    {
        if (Slides.Count == 0) return;
        Select(Wrap(index));
        RestartAutoplay();
    }

    /// <inheritdoc/>
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == SelectedIndexProperty && !_settling)
        {
            Travel(SelectedIndex);
            RestartAutoplay();
        }
        else if (change.Property == LoopProperty)
        {
            _drag?.Refresh();
            _strip.InvalidateArrange();
        }
        else if (change.Property == AutoplayIntervalProperty && _drag is not null)
        {
            StartAutoplay();
        }
        else if (change.Property == IsPointerOverProperty || change.Property == IsKeyboardFocusWithinProperty)
        {
            _keyboardFocus &= IsKeyboardFocusWithin;
            Hold();
        }
    }

    /// <inheritdoc/>
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _drag = PrestoAvalonia.CreateDraggable(_strip, new DraggableParams
        {
            Trigger = this,
            Y = new DraggableAxisParams { Enabled = false },
            ContainerBounds = _ => Loop
                ? new Rect(-Constants.MaxValue, 0, 2 * Constants.MaxValue, 0)
                : new Rect(MinX, 0, -MinX, 0),
            Snap = x => _width > 0 ? Utils.Snap(x, _width) : x,
            ReleaseInertia = s_inertia,
            OnGrab = _ =>
            {
                _travel?.Pause();
                _dragging = true;
                Hold();
            },
            OnRelease = Released,
            OnSettle = _ => Settle(),
        });
        _drag.Animate.Animations[Presto.Canonicalize(_strip, "X")]._onRender += _ => Moved();
        Place();
        StartAutoplay();
    }

    /// <inheritdoc/>
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _travel?.Pause();
        _autoplay?.Cancel();
        _autoplay = null;
        _drag?.Revert();
        _drag = null;
    }

    /// <inheritdoc/>
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        int? index = e.Key switch
        {
            Key.Left => SelectedIndex - 1,
            Key.Right => SelectedIndex + 1,
            Key.Home => 0,
            Key.End => Slides.Count - 1,
            _ => null,
        };
        if (e.Handled || index is null) return;
        GoTo(index.Value);
        e.Handled = true;
    }

    /// <inheritdoc/>
    protected override Size MeasureOverride(Size availableSize)
    {
        _strip.Measure(availableSize);
        return new Size(
            double.IsInfinity(availableSize.Width) ? _strip.DesiredSize.Width : availableSize.Width,
            double.IsInfinity(availableSize.Height) ? _strip.DesiredSize.Height : availableSize.Height);
    }

    /// <inheritdoc/>
    protected override Size ArrangeOverride(Size finalSize)
    {
        _strip.Arrange(new Rect(finalSize));
        if (finalSize.Width != _width)
        {
            _width = finalSize.Width;
            Place();
        }
        return finalSize;
    }

    private double MinX => -Math.Max(0, Slides.Count - 1) * _width;

    private static int Mod(int value, int count) => ((value % count) + count) % count;

    private int Wrap(int index) => Loop ? Mod(index, Slides.Count) : Math.Clamp(index, 0, Slides.Count - 1);

    // The column a slide is laid out in; a looping pager moves each slide to the copy nearest the page shown.
    private double Slot(int slide, double page)
    {
        var count = Slides.Count;
        return Loop && count > 0 ? slide - (count * Math.Floor(((slide - page) / count) + .5)) : slide;
    }

    private void Moved()
    {
        _position.Assign(_width > 0 ? -_drag!.X / _width : 0);
        if (Loop) _strip.InvalidateArrange();
    }

    private void Select(int index)
    {
        SetSelected(index);
        Travel(index);
    }

    private void SetSelected(int index)
    {
        _settling = true;
        SelectedIndex = index;
        _settling = false;
    }

    private void Travel(int index)
    {
        if (_drag is null || _width <= 0) return;
        _travel?.Pause();
        var x = -Slot(index, _position.Value) * _width;
        if (Engine.Current.Defaults.ReducedMotion) _drag.X = x;
        else _travel = Presto.Animate(_drag, new AnimationParams { ["X"] = x, Ease = s_travel });
    }

    // Lands on the selected slide at once, as the pager is attached or resized.
    private void Place()
    {
        _travel?.Pause();
        if (_drag is null) return;
        _drag.Refresh();
        _drag.X = -Slot(SelectedIndex, _position.Value) * _width;
    }

    private void Released(Draggable drag)
    {
        _dragging = false;
        if (Engine.Current.Defaults.ReducedMotion && _width > 0)
        {
            var thrown = Utils.Snap(drag.X + (drag.Velocity * Math.Cos(drag.Angle) * s_inertia.TimeConstant), _width);
            drag.X = Loop ? thrown : Math.Clamp(thrown, MinX, 0);
            Settle();
        }
        RestartAutoplay();
    }

    private void Settle()
    {
        if (_dragging || Slides.Count == 0) return;
        SetSelected(Wrap((int)Math.Round(_position.Value)));
    }

    private void StartAutoplay()
    {
        _autoplay?.Cancel();
        _autoplay = null;
        _autoplayProgress.Assign(0);
        if (AutoplayInterval <= 0) return;
        _autoplay = Presto.CreateTimer(new TimerParams
        {
            Duration = AutoplayInterval,
            Loop = true,
            OnUpdate = timer => _autoplayProgress.Assign(Engine.Current.Defaults.ReducedMotion ? 0 : timer.IterationProgress),
            OnLoop = _ =>
            {
                if (Slides.Count > 0) Select(Mod(SelectedIndex + 1, Slides.Count));
            },
        });
        Hold();
    }

    private void RestartAutoplay()
    {
        if (_autoplay is null) return;
        _autoplay.Restart();
        _autoplayProgress.Assign(0);
        Hold();
    }

    private void Hold()
    {
        if (_dragging || IsPointerOver || _keyboardFocus) _autoplay?.Pause();
        else _autoplay?.Resume();
    }

    private void HandleGotFocus(object? sender, FocusChangedEventArgs e)
    {
        _keyboardFocus = e.NavigationMethod is NavigationMethod.Tab or NavigationMethod.Directional;
        Hold();
    }

    private sealed class Strip(Pager pager) : Panel
    {
        protected override Size MeasureOverride(Size availableSize)
        {
            var size = default(Size);
            foreach (var slide in Children)
            {
                slide.Measure(availableSize);
                size = new Size(Math.Max(size.Width, slide.DesiredSize.Width), Math.Max(size.Height, slide.DesiredSize.Height));
            }
            return size;
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            var page = pager._position.Value;
            for (var i = 0; i < Children.Count; i++)
            {
                Children[i].Arrange(new Rect(pager.Slot(i, page) * finalSize.Width, 0, finalSize.Width, finalSize.Height));
            }
            return finalSize;
        }
    }
}
