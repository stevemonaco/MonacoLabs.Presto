using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace Monaco.Presto.AvaloniaBridge.Tests;

/// <summary><c>{presto:Follow}</c> on attributes, with each kind of timing, over a view-model <see cref="MotionValue"/>, and inside control and data templates.</summary>
public sealed partial class FollowXamlView : UserControl
{
    public FollowXamlView() => InitializeComponent();
}

/// <summary>What XAML hands a markup extension, for calling <see cref="MarkupExtension.ProvideValue"/> from a test.</summary>
public sealed class ProvidedTarget(object targetObject, object targetProperty) : IServiceProvider, IProvideValueTarget
{
    public object TargetObject => targetObject;

    public object TargetProperty => targetProperty;

    public object? GetService(Type serviceType) => serviceType == typeof(IProvideValueTarget) ? this : null;
}
