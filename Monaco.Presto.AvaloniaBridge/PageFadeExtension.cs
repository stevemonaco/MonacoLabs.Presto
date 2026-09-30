using Avalonia.Markup.Xaml;

namespace Monaco.Presto.AvaloniaBridge;

/// <summary><c>{presto:PageFade}</c>: <see cref="Recipes.PageFade"/>.</summary>
public sealed class PageFadeExtension : MarkupExtension
{
    /// <summary>The duration in ms, or <see cref="MotionTokens.Medium"/> when unset.</summary>
    public double? Duration { get; set; }

    /// <inheritdoc/>
    public override object ProvideValue(IServiceProvider serviceProvider) => Recipes.PageFade(Duration);
}