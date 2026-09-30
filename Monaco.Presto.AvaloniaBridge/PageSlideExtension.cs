using Avalonia.Markup.Xaml;

namespace Monaco.Presto.AvaloniaBridge;

/// <summary><c>{presto:PageSlide Edge=Right}</c>: <see cref="Recipes.PageSlide"/>.</summary>
public sealed class PageSlideExtension : MarkupExtension
{
    /// <summary>The side the incoming page slides in from.</summary>
    public Edge Edge { get; set; }

    /// <inheritdoc/>
    public override object ProvideValue(IServiceProvider serviceProvider) => Recipes.PageSlide(Edge);
}