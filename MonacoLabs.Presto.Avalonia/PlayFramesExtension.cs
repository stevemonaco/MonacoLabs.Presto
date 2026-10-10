namespace MonacoLabs.Presto;

/// <summary><c>{presto:PlayFrames}</c>: <see cref="Recipes.PlayFrames"/>, which has no exit form. <see cref="RecipeExtension.Duration"/> and <see cref="RecipeExtension.Ease"/> override the frame timing when set.</summary>
public sealed class PlayFramesExtension : RecipeExtension
{
    /// <summary>The frames per second.</summary>
    public double Fps { get; set; } = 24;

    /// <summary>Whether it repeats forever.</summary>
    public bool Loop { get; set; } = true;

    /// <summary>Whether it plays from the last frame to the first.</summary>
    public bool Reverse { get; set; }

    /// <inheritdoc/>
    protected override AnimationBuilder Build(bool exit, EaseValue? ease)
    {
        var builder = Recipes.PlayFrames(Fps, Loop, Reverse, Delay);
        if (Duration is { } duration) builder.Duration(duration);
        return ease is { } value ? builder.Ease(value) : builder;
    }
}
