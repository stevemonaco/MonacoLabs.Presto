using Avalonia;
using Avalonia.Media;

namespace Monaco.Presto.AvaloniaBridge;

/// <summary>The frame model <see cref="SpriteSheet"/> and <see cref="ImageSequence"/> share: which frame a <c>Frame</c> position shows, and the tween that plays them, and how a frame is sized and drawn.</summary>
internal static class FramePlayer
{
    /// <summary>The frame shown at <paramref name="frame"/>: floored and held on the last frame, or wrapped when <paramref name="wrap"/> is set.</summary>
    public static int Resolve(double frame, int frameCount, bool wrap)
    {
        if (frameCount <= 0 || double.IsNaN(frame)) return 0;
        var index = Math.Floor(frame);
        if (wrap) return (int)(((index % frameCount) + frameCount) % frameCount);
        return (int)Math.Clamp(index, 0, frameCount - 1);
    }

    /// <summary>Tweens <paramref name="target"/>'s <c>Frame</c> linearly from 0 to its frame count, giving each frame 1000 / <paramref name="fps"/> ms.</summary>
    public static Animation Play(AvaloniaObject target, double fps, bool loop, bool reverse) =>
        Prebuilt.Sprites.PlayFrames(fps, loop, reverse).Animate(target);

    /// <summary>Draws <paramref name="sourceRect"/> of <paramref name="image"/> stretched into <paramref name="bounds"/> and centred, as <see cref="Avalonia.Controls.Image"/> does.</summary>
    public static void Draw(DrawingContext context, IImage image, Rect sourceRect, Size bounds, Stretch stretch, StretchDirection direction)
    {
        if (bounds.Width <= 0 || bounds.Height <= 0) return;
        var viewport = new Rect(bounds);
        var scale = stretch.CalculateScaling(bounds, sourceRect.Size, direction);
        var destination = viewport.CenterRect(new Rect(sourceRect.Size * scale)).Intersect(viewport);
        var source = sourceRect.CenterRect(new Rect(destination.Size / scale));
        // Filtered scaling samples up to half a source pixel past the rect, which would pull in a neighbouring sheet cell.
        if (scale != new Vector(1, 1) && sourceRect != new Rect(image.Size))
            source = source.Intersect(sourceRect.Deflate(0.5));
        context.DrawImage(image, source, destination);
    }

    /// <summary>The size a <paramref name="frame"/>-sized control takes in <paramref name="available"/>; a null <paramref name="direction"/> allows any scaling, as arrange does.</summary>
    public static Size Measure(Size available, Size frame, Stretch stretch, StretchDirection? direction) =>
        stretch.CalculateSize(available, frame, direction ?? StretchDirection.Both);
}
