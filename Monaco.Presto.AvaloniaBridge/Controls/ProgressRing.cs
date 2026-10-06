using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Rendering;
using Monaco.Presto.AvaloniaBridge.Shapes;
using Path = Avalonia.Controls.Shapes.Path;

namespace Monaco.Presto.AvaloniaBridge.Controls;

/// <summary>
/// A determinate ring that draws <see cref="Value"/> clockwise from the top, whole or split into <see cref="Segments"/>
/// arcs. With a <see cref="HoldDuration"/> it is also a hold-to-confirm control: pressing it fills it, and
/// <see cref="Confirmed"/> is raised when it is full.
/// </summary>
/// <remarks>
/// <para>
/// It takes the size it is given, so set its <c>Width</c> and <c>Height</c>. <see cref="Value"/> is drawn as it is. To ease to each new value, bind it through <c>{presto:Follow}</c> or
/// <see cref="Follow.Bind(AvaloniaObject, AvaloniaProperty, Avalonia.Data.BindingBase, FollowParams?)"/>, which snap
/// under <see cref="PrestoDefaults.ReducedMotion"/>; to draw a <see cref="MotionValue"/>, bind it with
/// <see cref="Follow.Bind(AvaloniaObject, AvaloniaProperty, MotionValue, FollowParams?)"/>.
/// </para>
/// <para>
/// A hold, by the pointer or by Space or Enter while focused, fills the ring linearly from its value over the share of
/// <see cref="HoldDuration"/> left, under reduced motion too, since the wait is the confirmation. Letting go rewinds it to
/// its value over <see cref="MotionTokens.Short"/>, at once under reduced motion.
/// </para>
/// </remarks>
public sealed class ProgressRing : Control, ICustomHitTest
{
    /// <summary>The share drawn, 0 to 1.</summary>
    public static readonly StyledProperty<double> ValueProperty =
        AvaloniaProperty.Register<ProgressRing, double>(nameof(Value));

    /// <summary>How many arcs the ring is split into; 1 by default.</summary>
    public static readonly StyledProperty<int> SegmentsProperty =
        AvaloniaProperty.Register<ProgressRing, int>(nameof(Segments), 1, validate: count => count > 0);

    /// <summary>The space between two segments, in degrees; 6 by default.</summary>
    public static readonly StyledProperty<double> GapProperty =
        AvaloniaProperty.Register<ProgressRing, double>(nameof(Gap), 6);

    /// <summary>The stroke thickness; 4 by default.</summary>
    public static readonly StyledProperty<double> ThicknessProperty =
        AvaloniaProperty.Register<ProgressRing, double>(nameof(Thickness), 4);

    /// <summary>The drawn part; white by default.</summary>
    public static readonly StyledProperty<IBrush> FillProperty =
        AvaloniaProperty.Register<ProgressRing, IBrush>(nameof(Fill), Brushes.White);

    /// <summary>The track under it; 40 % white by default.</summary>
    public static readonly StyledProperty<IBrush> TrackProperty =
        AvaloniaProperty.Register<ProgressRing, IBrush>(nameof(Track), new ImmutableSolidColorBrush(Color.FromArgb(0x66, 0xFF, 0xFF, 0xFF)));

    /// <summary>Milliseconds a hold takes to fill an empty ring; 0, the default, takes no holds.</summary>
    public static readonly StyledProperty<double> HoldDurationProperty =
        AvaloniaProperty.Register<ProgressRing, double>(nameof(HoldDuration));

    private Path[] _fills = [];
    private Size _built;
    private Animation? _hold;
    private double _rest;


    /// <summary>Creates an empty ring.</summary>
    public ProgressRing() => Build();

    /// <summary>Raised when a hold fills the ring.</summary>
    public event EventHandler? Confirmed;

    /// <inheritdoc cref="ValueProperty"/>
    public double Value
    {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    /// <inheritdoc cref="SegmentsProperty"/>
    public int Segments
    {
        get => GetValue(SegmentsProperty);
        set => SetValue(SegmentsProperty, value);
    }

    /// <inheritdoc cref="GapProperty"/>
    public double Gap
    {
        get => GetValue(GapProperty);
        set => SetValue(GapProperty, value);
    }

    /// <inheritdoc cref="ThicknessProperty"/>
    public double Thickness
    {
        get => GetValue(ThicknessProperty);
        set => SetValue(ThicknessProperty, value);
    }

    /// <inheritdoc cref="FillProperty"/>
    public IBrush Fill
    {
        get => GetValue(FillProperty);
        set => SetValue(FillProperty, value);
    }

    /// <inheritdoc cref="TrackProperty"/>
    public IBrush Track
    {
        get => GetValue(TrackProperty);
        set => SetValue(TrackProperty, value);
    }

    /// <inheritdoc cref="HoldDurationProperty"/>
    public double HoldDuration
    {
        get => GetValue(HoldDurationProperty);
        set => SetValue(HoldDurationProperty, value);
    }

    /// <inheritdoc/>
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ValueProperty) Draw();
        else if (change.Property == SegmentsProperty || change.Property == FillProperty || change.Property == TrackProperty) Build();
        else if (change.Property == GapProperty || change.Property == ThicknessProperty) Reshape();
        else if (change.Property == HoldDurationProperty) Focusable = HoldDuration > 0;
    }

    /// <inheritdoc/>
    protected override Size MeasureOverride(Size availableSize)
    {
        foreach (var child in VisualChildren) ((Control)child).Measure(availableSize);
        return default;
    }

    /// <inheritdoc/>
    protected override Size ArrangeOverride(Size finalSize)
    {
        if (finalSize != _built)
        {
            _built = finalSize;
            ShapeArcs();
        }
        foreach (var child in VisualChildren) ((Control)child).Arrange(new Rect(finalSize));
        return finalSize;
    }

    // The paths are hit only on their strokes, so a press in the ring's hole would miss it.
    bool ICustomHitTest.HitTest(Point point) => new Rect(Bounds.Size).Contains(point);

    /// <inheritdoc/>
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (HoldDuration <= 0 || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        e.Pointer.Capture(this);
        e.Handled = true;
        Hold();
    }

    /// <inheritdoc/>
    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        LetGo();
    }

    /// <inheritdoc/>
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (HoldDuration <= 0 || e.Key is not (Key.Space or Key.Enter)) return;
        e.Handled = true;
        if (_hold is null) Hold();
    }

    /// <inheritdoc/>
    protected override void OnKeyUp(KeyEventArgs e)
    {
        base.OnKeyUp(e);
        if (e.Key is Key.Space or Key.Enter) LetGo();
    }

    private void Build()
    {
        VisualChildren.Clear();
        _fills = new Path[Segments];
        for (var i = 0; i < Segments; i++) VisualChildren.Add(Arc(Track));
        for (var i = 0; i < Segments; i++) VisualChildren.Add(_fills[i] = Arc(Fill));
        Reshape();
    }

    private void Reshape()
    {
        _built = default;
        InvalidateArrange();
    }

    private Path Arc(IBrush brush)
    {
        var arc = new Path { Stroke = brush, StrokeLineCap = PenLineCap.Round };
        arc.Bind(Shape.StrokeThicknessProperty, this.GetObservable(ThicknessProperty));
        return arc;
    }

    private void ShapeArcs()
    {
        var radius = Math.Max(0, (Math.Min(_built.Width, _built.Height) - Thickness) / 2);
        var centre = new Point(_built.Width / 2, _built.Height / 2);
        var count = _fills.Length;
        var span = 360d / count;
        var gap = count > 1 ? Math.Min(Gap, span) : 0;
        for (var i = 0; i < count; i++)
        {
            var geometry = ArcGeometry(centre, radius, (i * span) + (gap / 2), ((i + 1) * span) - (gap / 2));
            ((Path)VisualChildren[i]).Data = geometry;
            _fills[i].Data = geometry;
        }
        Draw();
    }

    private void Draw()
    {
        var count = _fills.Length;
        for (var i = 0; i < count; i++) DrawAccessor.Apply(_fills[i], 0, Math.Clamp((Value * count) - i, 0, 1));
    }

    private static StreamGeometry ArcGeometry(Point centre, double radius, double from, double to)
    {
        Point At(double degrees) => centre + new Vector(Math.Sin(degrees * Math.PI / 180), -Math.Cos(degrees * Math.PI / 180)) * radius;

        var geometry = new StreamGeometry();
        using var context = geometry.Open();
        // Two halves, so a whole ring's arc never ends where it starts and neither half needs the large-arc flag.
        context.BeginFigure(At(from), false);
        context.ArcTo(At((from + to) / 2), new Size(radius, radius), 0, false, SweepDirection.Clockwise);
        context.ArcTo(At(to), new Size(radius, radius), 0, false, SweepDirection.Clockwise);
        context.EndFigure(false);
        return geometry;
    }

    private void Hold()
    {
        _rest = Value;
        _hold = this.Animate(new AvaloniaAnimationParams
        {
            [nameof(Value)] = 1,
            Duration = HoldDuration * Math.Max(0, 1 - Value),
            Ease = Eases.None,
            OnComplete = _ => Confirmed?.Invoke(this, EventArgs.Empty),
        });
    }

    private void LetGo()
    {
        if (_hold is null) return;
        _hold.Pause();
        _hold = null;
        this.Animate(new AvaloniaAnimationParams
        {
            [nameof(Value)] = _rest,
            Duration = Engine.Current.Defaults.ReducedMotion ? 0 : Recipes.Tokens.Short,
            Ease = Recipes.Tokens.Ease,
            OnComplete = rewind => rewind.Revert(),
        });
    }
}
