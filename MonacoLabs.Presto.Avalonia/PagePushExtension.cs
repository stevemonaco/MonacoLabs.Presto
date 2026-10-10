using Avalonia.Markup.Xaml;

namespace MonacoLabs.Presto;

/// <summary><c>{presto:PagePush Edge=Right}</c>: <see cref="Recipes.PagePush"/>.</summary>
public sealed class PagePushExtension : MarkupExtension
{
    /// <summary>The side the incoming page slides over from.</summary>
    public Edge Edge { get; set; }

    /// <inheritdoc/>
    public override object ProvideValue(IServiceProvider serviceProvider) => Recipes.PagePush(Edge);
}