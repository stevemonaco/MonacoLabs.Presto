using System.ComponentModel;
using System.Globalization;

namespace Monaco.Presto.AvaloniaBridge.Lottie;

/// <summary>Converts a URI string to a <see cref="LottieComposition"/> through <see cref="LottieComposition.Load"/>.</summary>
public sealed class LottieCompositionConverter : TypeConverter
{
    /// <inheritdoc/>
    public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType) =>
        sourceType == typeof(string) || base.CanConvertFrom(context, sourceType);

    /// <inheritdoc/>
    public override object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value) =>
        value is string uri ? LottieComposition.Load(new Uri(uri, UriKind.RelativeOrAbsolute)) : base.ConvertFrom(context, culture, value);
}
