using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace Monaco.Presto.AvaloniaBridge;

/// <summary>
/// Shows one frame of a <see cref="Source"/> image cut into a <see cref="Columns"/> by <see cref="Rows"/> grid, read left
/// to right then top to bottom, and sized, stretched and aligned as a single frame.
/// </summary>
public class SpriteSheet : Control
{
    /// <summary>Defines the <see cref="Source"/> property.</summary>
    public static readonly StyledProperty<IImage?> SourceProperty = Image.SourceProperty.AddOwner<SpriteSheet>();

    /// <summary>Defines the <see cref="Stretch"/> property.</summary>
    public static readonly StyledProperty<Stretch> StretchProperty = Image.StretchProperty.AddOwner<SpriteSheet>();

    /// <summary>Defines the <see cref="StretchDirection"/> property.</summary>
    public static readonly StyledProperty<StretchDirection> StretchDirectionProperty = Image.StretchDirectionProperty.AddOwner<SpriteSheet>();

    /// <summary>Defines the <see cref="Columns"/> property.</summary>
    public static readonly StyledProperty<int> ColumnsProperty =
        AvaloniaProperty.Register<SpriteSheet, int>(nameof(Columns), 1, validate: static v => v > 0);

    /// <summary>Defines the <see cref="Rows"/> property.</summary>
    public static readonly StyledProperty<int> RowsProperty =
        AvaloniaProperty.Register<SpriteSheet, int>(nameof(Rows), 1, validate: static v => v > 0);

    /// <summary>Defines the <see cref="FrameCount"/> property.</summary>
    public static readonly StyledProperty<int?> FrameCountProperty =
        AvaloniaProperty.Register<SpriteSheet, int?>(nameof(FrameCount));

    /// <summary>Defines the <see cref="FrameSize"/> property.</summary>
    public static readonly StyledProperty<Size?> FrameSizeProperty =
        AvaloniaProperty.Register<SpriteSheet, Size?>(nameof(FrameSize));

    /// <summary>Defines the <see cref="Frame"/> property.</summary>
    public static readonly StyledProperty<double> FrameProperty =
        AvaloniaProperty.Register<SpriteSheet, double>(nameof(Frame));

    /// <summary>Defines the <see cref="Wrap"/> property.</summary>
    public static readonly StyledProperty<bool> WrapProperty =
        AvaloniaProperty.Register<SpriteSheet, bool>(nameof(Wrap));

    /// <summary>Defines the <see cref="CurrentFrame"/> property.</summary>
    public static readonly DirectProperty<SpriteSheet, int> CurrentFrameProperty =
        AvaloniaProperty.RegisterDirect<SpriteSheet, int>(nameof(CurrentFrame), static o => o.CurrentFrame);

    private int _currentFrame;

    static SpriteSheet()
    {
        AffectsRender<SpriteSheet>(SourceProperty, StretchProperty, StretchDirectionProperty, CurrentFrameProperty, ColumnsProperty, RowsProperty, FrameSizeProperty);
        AffectsMeasure<SpriteSheet>(SourceProperty, StretchProperty, StretchDirectionProperty, ColumnsProperty, RowsProperty, FrameSizeProperty);
    }

    /// <summary>The sheet image.</summary>
    public IImage? Source
    {
        get => GetValue(SourceProperty);
        set => SetValue(SourceProperty, value);
    }

    /// <summary>How one frame is stretched to fill the control; defaults to <see cref="Avalonia.Media.Stretch.Uniform"/>.</summary>
    public Stretch Stretch
    {
        get => GetValue(StretchProperty);
        set => SetValue(StretchProperty, value);
    }

    /// <summary>Whether one frame may be scaled up, down or both.</summary>
    public StretchDirection StretchDirection
    {
        get => GetValue(StretchDirectionProperty);
        set => SetValue(StretchDirectionProperty, value);
    }

    /// <summary>Grid columns in <see cref="Source"/>; defaults to 1.</summary>
    public int Columns
    {
        get => GetValue(ColumnsProperty);
        set => SetValue(ColumnsProperty, value);
    }

    /// <summary>Grid rows in <see cref="Source"/>; defaults to 1.</summary>
    public int Rows
    {
        get => GetValue(RowsProperty);
        set => SetValue(RowsProperty, value);
    }

    /// <summary>How many grid cells hold frames, for a partial last row; null uses every cell.</summary>
    public int? FrameCount
    {
        get => GetValue(FrameCountProperty);
        set => SetValue(FrameCountProperty, value);
    }

    /// <summary>The frame drawn from the top left of each grid cell, for a sheet whose cells carry margins; null uses the whole cell.</summary>
    public Size? FrameSize
    {
        get => GetValue(FrameSizeProperty);
        set => SetValue(FrameSizeProperty, value);
    }

    /// <summary>The frame position; the frame shown is its floor, held on the last frame or wrapped by <see cref="Wrap"/>.</summary>
    /// <remarks>A double, so a linear tween from 0 to <see cref="EffectiveFrameCount"/> gives every frame equal time.</remarks>
    public double Frame
    {
        get => GetValue(FrameProperty);
        set => SetValue(FrameProperty, value);
    }

    /// <summary>Whether a <see cref="Frame"/> past the last frame wraps back to the first instead of holding the last.</summary>
    public bool Wrap
    {
        get => GetValue(WrapProperty);
        set => SetValue(WrapProperty, value);
    }

    /// <summary>The index of the frame shown.</summary>
    public int CurrentFrame
    {
        get => _currentFrame;
        private set => SetAndRaise(CurrentFrameProperty, ref _currentFrame, value);
    }

    /// <summary><see cref="FrameCount"/>, or every grid cell when it is null.</summary>
    public int EffectiveFrameCount => FrameCount ?? Columns * Rows;

    /// <summary>Plays every frame once per iteration at <paramref name="fps"/>, by tweening <see cref="Frame"/> linearly.</summary>
    public Animation Play(double fps = 24, bool loop = true, bool reverse = false) =>
        FramePlayer.Play(this, fps, loop, reverse);

    /// <inheritdoc/>
    public override void Render(DrawingContext context)
    {
        if (Source is { } source) FramePlayer.Draw(context, source, SourceRect(source), Bounds.Size, Stretch, StretchDirection);
    }

    /// <inheritdoc/>
    protected override Size MeasureOverride(Size availableSize) =>
        Source is { } source ? FramePlayer.Measure(availableSize, SourceRect(source).Size, Stretch, StretchDirection) : default;

    /// <inheritdoc/>
    protected override Size ArrangeOverride(Size finalSize) =>
        Source is { } source ? FramePlayer.Measure(finalSize, SourceRect(source).Size, Stretch, null) : default;

    /// <inheritdoc/>
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == FrameProperty || change.Property == WrapProperty || change.Property == FrameCountProperty
            || change.Property == ColumnsProperty || change.Property == RowsProperty)
            CurrentFrame = FramePlayer.Resolve(Frame, EffectiveFrameCount, Wrap);
    }

    private Rect SourceRect(IImage source)
    {
        var cell = new Size(source.Size.Width / Columns, source.Size.Height / Rows);
        var index = CurrentFrame;
        return new Rect(new Point(index % Columns * cell.Width, index / Columns * cell.Height), FrameSize ?? cell);
    }
}
