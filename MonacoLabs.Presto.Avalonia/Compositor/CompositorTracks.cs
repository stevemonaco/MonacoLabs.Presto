using Avalonia;
using Avalonia.Rendering.Composition;
using Avalonia.Rendering.Composition.Animations;

namespace MonacoLabs.Presto.Compositor;

/// <summary>Puts a <see cref="CompiledTrack"/> on a composition visual, either as a running animation or as one direct write.</summary>
internal static class CompositorTracks
{
    private static readonly TimeSpan s_minimumDuration = TimeSpan.FromMilliseconds(1);

    /// <summary>Builds the composition animation <paramref name="track"/> compiles into, typed after the property it drives.</summary>
    public static KeyFrameAnimation Build(CompositionVisual element, CompiledTrack track)
    {
        KeyFrameAnimation animation;
        if (track.Kind is CompositorTrackKind.Translation or CompositorTrackKind.Scale)
        {
            var vector = element.Compositor.CreateVector3DKeyFrameAnimation();
            for (var i = 0; i < track.Keys.Length; i++)
            {
                var value = track.Values[i];
                vector.InsertKeyFrame(track.Keys[i], new Vector3D(value[0], value[1], value[2]), track.Easings[i]);
            }
            animation = vector;
        }
        else
        {
            var scalar = element.Compositor.CreateScalarKeyFrameAnimation();
            for (var i = 0; i < track.Keys.Length; i++) scalar.InsertKeyFrame(track.Keys[i], (float)track.Values[i][0], track.Easings[i]);
            animation = scalar;
        }

        // The setter validates the duration it is replacing rather than the one coming in, so the 1 ms floor is ours to hold.
        animation.Duration = track.Duration < s_minimumDuration ? s_minimumDuration : track.Duration;
        animation.DelayTime = track.Delay;
        // Shows the from value through the delay, as a UI-thread animation renders it at creation.
        animation.DelayBehavior = AnimationDelayBehavior.SetInitialValueBeforeDelay;
        animation.Direction = track.Direction;
        animation.IterationCount = track.IterationCount;
        animation.IterationBehavior = track.Forever ? AnimationIterationBehavior.Forever : AnimationIterationBehavior.Count;
        return animation;
    }

    /// <summary>Writes one composition property directly, which is also what detaches any animation still holding it.</summary>
    public static void Write(CompositionVisual element, CompositorTrackKind kind, double[] value)
    {
        // The client setter's equality guard drops an unchanged write, so the discarded first write is what puts the real one on the wire.
        switch (kind)
        {
            case CompositorTrackKind.Translation:
                element.Translation = new Vector3D(value[0] + 1, value[1], value[2]);
                element.Translation = new Vector3D(value[0], value[1], value[2]);
                break;
            case CompositorTrackKind.Scale:
                element.Scale = new Vector3D(value[0] + 1, value[1], value[2]);
                element.Scale = new Vector3D(value[0], value[1], value[2]);
                break;
            case CompositorTrackKind.RotationAngle:
                element.RotationAngle = (float)value[0] + 1;
                element.RotationAngle = (float)value[0];
                break;
            default:
                element.Opacity = (float)value[0] + 1;
                element.Opacity = (float)value[0];
                break;
        }
    }
}
