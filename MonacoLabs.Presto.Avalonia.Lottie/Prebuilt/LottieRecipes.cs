namespace MonacoLabs.Presto.Lottie.Prebuilt;

/// <summary>Playback recipes for <see cref="LottieView"/>.</summary>
public static class LottieRecipes
{
    /// <summary>
    /// Plays each target's <paramref name="segment"/> marker, or its whole composition when null, as <see cref="LottieView.Play(string, bool, bool, double)"/>
    /// does, reading the composition per target; any other target counts as a zero-length segment. Reduced: jumps to the
    /// frame the segment ends on, without looping.
    /// </summary>
    public static AnimationBuilder PlayLottie(string? segment = null, bool loop = true, bool reverse = false, double speed = 1, double? delay = null)
    {
        LottieSegment Range(object target) => target is LottieView view ? view.Segment(segment, reverse) : default;
        var builder = Frames(
            (FunctionValue)((target, _, _) => Range(target).From),
            (FunctionValue)((target, _, _) => Range(target).To),
            (FunctionValue)((target, _, _) => target is LottieView view ? view.Milliseconds(Range(target), speed) : 0),
            loop);
        return delay is { } ms ? builder.Delay(ms) : builder;
    }

    internal static AnimationBuilder Frames(TweenValue from, TweenValue to, TweenValue duration, bool loop) =>
        Presto.Build()
            .Property("Frame", to: to, from: from)
            .Duration(duration)
            .Ease(Eases.None)
            .Loop(loop)
            .WhenReduced(reduced => reduced.Loop(false).Property("Frame", to).Duration(0));
}
