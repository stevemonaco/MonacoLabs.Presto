using Avalonia.Media;
using MonacoLabs.Presto.Values;

namespace MonacoLabs.Presto;

/// <summary><c>{presto:Recolor To=…}</c>: <see cref="Recipes.Recolor"/>, which has no exit form.</summary>
public sealed class RecolorExtension : RecipeExtension
{
    /// <summary>The color it ends on.</summary>
    public Color To { get; set; } = Colors.DodgerBlue;

    /// <summary>The color property, <c>Background</c> by default.</summary>
    public string Property { get; set; } = "Background";

    /// <summary>The space the path is mixed in, or <see cref="MotionTokens.ColorSpace"/> when unset.</summary>
    public ColorSpace? Space { get; set; }

    /// <summary>The hue arc in <see cref="ColorSpace.Oklch"/>, or <see cref="MotionTokens.Hue"/> when unset.</summary>
    public HueInterpolation? Hue { get; set; }

    /// <inheritdoc/>
    protected override bool HasExitForm => false;

    /// <inheritdoc/>
    protected override AnimationBuilder Build(bool exit, EaseValue? ease) => Recipes.Recolor(To, Property, Space, Hue, Duration, Delay, ease);
}
