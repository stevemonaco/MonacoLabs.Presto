using Avalonia.Interactivity;
using MonacoLabs.Presto;
using PrestoGallery.Showcase;

namespace PrestoGallery.Builder;

/// <summary>
/// Animations attached from XAML: entrances on <c>Presence.Arrive</c> from a static recipe and a markup extension, and a
/// looping emphasis on <c>Animate.OnAttached</c> from a binding.
/// </summary>
public partial class XamlPage : ShowcasePage
{
    #region snippet XamlFadeUp
    public static readonly AnimationBuilder FadeUp = Presto.Build()
        .Opacity(from: 0, to: 1)
        .Y(from: 32, to: 0)
        .Duration(700)
        .Ease(Eases.OutQuad);
    #endregion

    public XamlPage()
    {
        InitializeComponent();
    }

    private void Replay(object? sender, RoutedEventArgs e)
    {
        var panels = Demos.Children.ToList();
        Demos.Children.Clear();
        Demos.Children.AddRange(panels);
    }
}
