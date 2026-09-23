namespace Monaco.Presto;

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

    /// <summary>The percentage-based form: each key places its frame at that fraction of the animation's duration, and the frames are sorted by offset.</summary>
    public static implicit operator Keyframes(Dictionary<string, Keyframe> frames) => new(null, frames);
}
