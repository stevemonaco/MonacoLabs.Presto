using Avalonia.Animation;
using Avalonia.Animation.Easings;

namespace MonacoLabs.Presto.Compositor;

/// <summary>
/// One composition property's key frames and timings, compiled out of a Presto <see cref="Animation"/>. It names no
/// composition type, so the compiler can be exercised without a window.
/// </summary>
/// <remarks>
/// <c>Keys</c> are positions in the cycle from 0 to 1, <c>Values</c> the channel values of each key frame — (x, y, 0) for a
/// translation, (x, y, 1) for a scale, one angle in radians, one opacity — and <c>Easings</c> the easing of each key frame,
/// which eases the segment ending on it. <c>Duration</c> is one cycle: an iteration plus any loop delay, and the return
/// iteration too when an alternating loop has one.
/// </remarks>
internal readonly record struct CompiledTrack(
    CompositorTrackKind Kind,
    float[] Keys,
    double[][] Values,
    Easing[] Easings,
    TimeSpan Duration,
    TimeSpan Delay,
    int IterationCount,
    bool Forever,
    PlaybackDirection Direction);
