using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace Monaco.Presto.AvaloniaBridge.Controls;

/// <summary>
/// Shows one image of <see cref="Frames"/>, sized to the first frame and stretched and aligned as <see cref="Image"/> does.
/// The inherited <see cref="PrestoImageBase.Source"/> is ignored.
/// </summary>
public class ImageSequence : PrestoImageBase
{
    /// <summary>Defines the <see cref="Frames"/> property.</summary>
    public static readonly StyledProperty<IReadOnlyList<IImage>?> FramesProperty =
        AvaloniaProperty.Register<ImageSequence, IReadOnlyList<IImage>?>(nameof(Frames));

    /// <summary>Defines the <see cref="Frame"/> property.</summary>
    public static readonly StyledProperty<double> FrameProperty =
        AvaloniaProperty.Register<ImageSequence, double>(nameof(Frame));

    /// <summary>Defines the <see cref="Wrap"/> property.</summary>
    public static readonly StyledProperty<bool> WrapProperty =
        AvaloniaProperty.Register<ImageSequence, bool>(nameof(Wrap));

    /// <summary>Defines the <see cref="CurrentFrame"/> property.</summary>
    public static readonly DirectProperty<ImageSequence, int> CurrentFrameProperty =
        AvaloniaProperty.RegisterDirect<ImageSequence, int>(nameof(CurrentFrame), static o => o.CurrentFrame);

    private int _currentFrame;

    static ImageSequence()
    {
        AffectsRender<ImageSequence>(FramesProperty, CurrentFrameProperty);
        AffectsMeasure<ImageSequence>(FramesProperty);
    }

    /// <summary>The frames, in playing order.</summary>
    public IReadOnlyList<IImage>? Frames
    {
        get => GetValue(FramesProperty);
        set => SetValue(FramesProperty, value);
    }

    /// <inheritdoc cref="SpriteSheet.Frame"/>
    public double Frame
    {
        get => GetValue(FrameProperty);
        set => SetValue(FrameProperty, value);
    }

    /// <inheritdoc cref="SpriteSheet.Wrap"/>
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

    /// <summary>The number of <see cref="Frames"/>.</summary>
    public int FrameCount => Frames?.Count ?? 0;

    /// <inheritdoc cref="SpriteSheet.Play"/>
    public Animation Play(double fps = 24, bool loop = true, bool reverse = false) =>
        FramePlayer.Play(this, fps, loop, reverse);

    /// <summary>
    /// Loads <paramref name="count"/> frames by formatting <paramref name="uriFormat"/> (such as
    /// <c>"avares://App/Assets/walk_{0:000}.png"</c>) with each index from <paramref name="start"/>.
    /// </summary>
    /// <remarks>
    /// Every frame is decoded up front on the calling thread and held in memory, about <c>count · width · height · 4</c>
    /// bytes; lazy or streamed decoding is out of scope.
    /// </remarks>
    public static IReadOnlyList<IImage> FromAssets(string uriFormat, int count, int start = 0)
    {
        var frames = new IImage[count];
        for (var i = 0; i < count; i++)
        {
            using var stream = AssetLoader.Open(new Uri(string.Format(CultureInfo.InvariantCulture, uriFormat, start + i)));
            frames[i] = new Bitmap(stream);
        }
        return frames;
    }

    /// <inheritdoc/>
    protected override IImage? CurrentImage => FrameCount > 0 ? Frames![CurrentFrame] : null;

    private protected override IImage? MeasuredImage => FrameCount > 0 ? Frames![0] : null;

    /// <inheritdoc/>
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == FrameProperty || change.Property == WrapProperty || change.Property == FramesProperty)
            CurrentFrame = FramePlayer.Resolve(Frame, FrameCount, Wrap);
    }
}
