using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace MonacoLabs.Presto.Tests;

/// <summary>Sets <see cref="Animate.OnAttachedProperty"/> through each XAML spelling: <c>x:Static</c>, a markup extension and a binding.</summary>
public sealed partial class AttachedXamlView : UserControl
{
    public AttachedXamlView() => InitializeComponent();
}

public static class AttachedRecipes
{
    public static readonly AnimationBuilder SlideTo100 = Presto.Build().X(100).Duration(100).Ease(Eases.None);
}

public sealed class SlideToExtension : MarkupExtension
{
    public double Distance { get; set; }

    public override object ProvideValue(IServiceProvider serviceProvider) =>
        Presto.Build().X(Distance).Duration(100).Ease(Eases.None);
}

public sealed class AttachedXamlViewModel
{
    public AnimationBuilder Motion { get; } = Presto.Build().X(60).Duration(100).Ease(Eases.None);
}
