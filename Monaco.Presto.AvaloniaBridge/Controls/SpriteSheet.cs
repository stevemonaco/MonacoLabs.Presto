using Avalonia;
using Avalonia.Media;

namespace Monaco.Presto.AvaloniaBridge.Controls;

/// <summary>
/// Shows one frame of a <see cref="PrestoImageBase.Source"/> image cut into a <see cref="Columns"/> by <see cref="Rows"/> grid, read left
/// to right then top to bottom, and sized, stretched and aligned as a single frame.
/// </summary>
public class SpriteSheet : PrestoImageBase
{
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
        AffectsRender<SpriteSheet>(CurrentFrameProperty, ColumnsProperty, RowsProperty, FrameSizeProperty);
        AffectsMeasure<SpriteSheet>(ColumnsProperty, RowsProperty, FrameSizeProperty);
    }

    /// <summary>Grid columns in <see cref="PrestoImageBase.Source"/>; defaults to 1.</summary>
    public int Columns
    {
        get => GetValue(ColumnsProperty);
        set => SetValue(ColumnsProperty, value);
    }

    /// <summary>Grid rows in <see cref="PrestoImageBase.Source"/>; defaults to 1.</summary>
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
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == FrameProperty || change.Property == WrapProperty || change.Property == FrameCountProperty
            || change.Property == ColumnsProperty || change.Property == RowsProperty)
            CurrentFrame = FramePlayer.Resolve(Frame, EffectiveFrameCount, Wrap);
    }

    /// <inheritdoc/>
    protected override Rect SourceRect(IImage source)
    {
        var cell = new Size(source.Size.Width / Columns, source.Size.Height / Rows);
        var index = CurrentFrame;
        return new Rect(new Point(index % Columns * cell.Width, index / Columns * cell.Height), FrameSize ?? cell);
    }
}
