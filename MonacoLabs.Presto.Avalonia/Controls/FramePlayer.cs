using Avalonia;

namespace MonacoLabs.Presto.Controls;

/// <summary>The frame model <see cref="SpriteSheet"/> and <see cref="ImageSequence"/> share: which frame a <c>Frame</c> position shows, and the tween that plays them.</summary>
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
}
