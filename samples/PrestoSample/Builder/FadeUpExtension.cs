using Avalonia.Markup.Xaml;
using Monaco.Presto;
using Monaco.Presto.AvaloniaBridge;

namespace PrestoSample.Builder;

/// <summary>A fade-up recipe the markup parameterises; every element gets a fresh builder.</summary>
public sealed class FadeUpExtension : MarkupExtension
{
    /// <summary>The source text of the builder <see cref="ProvideValue"/> returns.</summary>
    public static string Code { get; private set; } = "";

    public double Distance { get; set; } = 24;

    public double Delay { get; set; }

    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        (var builder, Code) = Snippet.Of(Presto.Build()
            .Opacity(from: 0, to: 1)
            .Y(from: Distance, to: 0)
            .Delay(Delay)
            .Duration(500)
            .Ease(Eases.OutQuad));
        return builder;
    }
}
