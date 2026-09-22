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

    public static implicit operator Keyframes(Keyframe[] frames) => new(frames, null);

    public static implicit operator Keyframes(Dictionary<string, Keyframe> frames) => new(null, frames);
}
