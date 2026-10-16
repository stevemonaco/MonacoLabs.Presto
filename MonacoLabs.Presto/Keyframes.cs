namespace MonacoLabs.Presto;

/// <summary>
/// The <c>keyframes</c> parameter in either upstream form: an ordered <see cref="Keyframe"/> array whose frames split the
/// animation duration, or a dictionary keyed by percentage offset (<c>"0%"</c>, <c>"21.5%"</c>, <c>"100%"</c>).
/// </summary>
public sealed class Keyframes
{
    private Keyframes(Keyframe[]? durationBased, Dictionary<string, Keyframe>? percentageBased)
    {
        DurationBased = durationBased;
        PercentageBased = percentageBased;
    }

    internal Keyframe[]? DurationBased { get; }

    internal Dictionary<string, Keyframe>? PercentageBased { get; }

    /// <summary>The duration-based form: the frames run in order, each taking its own <see cref="Keyframe.Duration"/> or an equal share of the animation's.</summary>
    public static implicit operator Keyframes(Keyframe[] frames) => new(frames, null);

    /// <summary>
    /// The percentage-based form: each key places its frame at that fraction of the animation's duration, and the frames
    /// are sorted by offset. A frame may set its <see cref="Keyframe.Ease"/> but no other setting.
    /// </summary>
    /// <exception cref="ArgumentException">A frame sets <see cref="Keyframe.Duration"/>, <see cref="Keyframe.Delay"/>,
    /// <see cref="Keyframe.Modifier"/> or <see cref="Keyframe.Composition"/>.</exception>
    public static implicit operator Keyframes(Dictionary<string, Keyframe> frames)
    {
        foreach (var (key, frame) in frames) RejectSettings(key, frame);
        return new(null, frames);
    }

    internal static void RejectSettings(string key, Keyframe frame)
    {
        var setting =
            frame.Duration != null ? nameof(Keyframe.Duration) :
            frame.Delay != null ? nameof(Keyframe.Delay) :
            frame.Modifier != null ? nameof(Keyframe.Modifier) :
            frame.Composition != null ? nameof(Keyframe.Composition) :
            null;
        if (setting != null) throw new ArgumentException($"Percentage keyframe \"{key}\" sets {setting}; a percentage frame takes only property values and Ease.");
    }
}
