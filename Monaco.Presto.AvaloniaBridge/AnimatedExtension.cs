using Avalonia;
using Avalonia.Data;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;
using Monaco.Presto.AvaloniaBridge.Bindings;

namespace Monaco.Presto.AvaloniaBridge;

/// <summary>
/// Wraps a one-way binding so its property animates to each new value instead of jumping to it, as in
/// <c>Width="{presto:Animated {Binding Level}, Spring=Gentle}"</c>; the code form is <see cref="AnimatedBindingExtensions.BindAnimated"/>.
/// </summary>
/// <remarks>It works on a control's attribute and inside control and data templates, but not in a style <c>Setter</c>, which every matched control shares.</remarks>
public sealed class AnimatedExtension(BindingBase binding) : MarkupExtension
{
    /// <inheritdoc cref="AnimatedBindingParams.Duration"/>
    public double? Duration { get; set; }

    /// <summary>An ease in the <see cref="EaseParser"/> grammar, such as <c>"outQuint"</c>.</summary>
    public string? Ease { get; set; }

    /// <inheritdoc cref="AnimatedBindingParams.Spring"/>
    public string? Spring { get; set; }

    /// <inheritdoc cref="AnimatedBindingParams.Bounce"/>
    public double? Bounce { get; set; }

    /// <inheritdoc cref="AnimatedBindingParams.Builder"/>
    public AnimationBuilder? Builder { get; set; }

    /// <inheritdoc cref="AnimatedBindingParams.Token"/>
    public MotionTokens? Token { get; set; }

    /// <inheritdoc cref="AnimatedBindingParams.AnimateInitial"/>
    public bool AnimateInitial { get; set; }

    /// <inheritdoc cref="AnimatedBindingParams.WhenReduced"/>
    public AnimationBuilder? WhenReduced { get; set; }

    /// <inheritdoc/>
    /// <exception cref="InvalidOperationException">It is not set on an Avalonia property of an object, such as in a style <c>Setter</c> or from code.</exception>
    /// <exception cref="ArgumentException">The wrapped binding is <c>TwoWay</c> or <c>OneWayToSource</c>, or the timing conflicts.</exception>
    public override object ProvideValue(IServiceProvider serviceProvider) => (serviceProvider?.GetService(typeof(IProvideValueTarget)) as IProvideValueTarget) switch
    {
        { TargetObject: Setter } => throw new InvalidOperationException(
            "{presto:Animated} cannot be used in a style Setter, because one Setter is shared by every control it matches. Set it on the property inside a ControlTemplate or DataTemplate instead."),
        { TargetObject: AvaloniaObject target, TargetProperty: AvaloniaProperty property } => new AnimatedBinding(target, property, binding, Options(), followsItems: true).ToBinding(),
        _ => throw new InvalidOperationException("{presto:Animated} must be set on an Avalonia property in XAML; from code, use BindAnimated."),
    };

    private AnimatedBindingParams Options() => new()
    {
        Duration = Duration,
        Ease = Ease is { } name ? (EaseValue?)EaseParser.Parse(name) : null,
        Spring = Spring,
        Bounce = Bounce,
        Builder = Builder,
        Token = Token,
        AnimateInitial = AnimateInitial,
        WhenReduced = WhenReduced,
    };
}
