using Monaco.Presto.AvaloniaBridge.Lottie.Prebuilt;

namespace Monaco.Presto.AvaloniaBridge.Lottie;

/// <summary><c>{lottie:PlayLottie}</c>: <see cref="LottieRecipes.PlayLottie"/>, which has no exit form. <see cref="RecipeExtension.Duration"/> and <see cref="RecipeExtension.Ease"/> override the frame timing when set.</summary>
public sealed class PlayLottieExtension : RecipeExtension
{
    /// <summary>The marker to play, or the whole composition when unset.</summary>
    public string? Segment { get; set; }

    /// <summary>Whether it repeats forever.</summary>
    public bool Loop { get; set; } = true;

    /// <summary>Whether it plays the segment from its end to its start.</summary>
    public bool Reverse { get; set; }

    /// <summary>The playback speed, 1 being the composition's frame rate.</summary>
    public double Speed { get; set; } = 1;

    /// <inheritdoc/>
    protected override AnimationBuilder Build(bool exit, EaseValue? ease)
    {
        var builder = LottieRecipes.PlayLottie(Segment, Loop, Reverse, Speed, Delay);
        if (Duration is { } duration) builder.Duration(duration);
        return ease is { } value ? builder.Ease(value) : builder;
    }
}
