using Avalonia;
using Avalonia.Controls;

namespace Monaco.Presto.AvaloniaBridge.Tests;

/// <summary>A view using all three <see cref="Connected"/> properties: a bound key, an <c>x:Static</c> motion and a bound anchor.</summary>
public sealed partial class ConnectedXamlView : UserControl
{
    public ConnectedXamlView() => InitializeComponent();
}

public static class ConnectedRecipes
{
    public static readonly AnimationBuilder Held = Presto.Build().Duration(600000).Ease(Eases.None);
}

public sealed class ConnectedXamlViewModel(int id, Thickness photoMargin)
{
    public int Id => id;

    public Thickness PhotoMargin => photoMargin;
}
