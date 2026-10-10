using Avalonia.Markup.Xaml;

namespace MonacoLabs.Presto;

/// <summary><c>{presto:PageFadeThrough}</c>: <see cref="Recipes.PageFadeThrough"/>.</summary>
public sealed class PageFadeThroughExtension : MarkupExtension
{
    /// <summary>The whole transition's duration in ms, or <see cref="MotionTokens.Medium"/> when unset.</summary>
    public double? Duration { get; set; }

    /// <inheritdoc/>
    public override object ProvideValue(IServiceProvider serviceProvider) => Recipes.PageFadeThrough(Duration);
}