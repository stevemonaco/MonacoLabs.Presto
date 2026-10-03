using Monaco.Presto;
using Monaco.Presto.AvaloniaBridge;

namespace PrestoSample.Showcase.Pages;

/// <summary>
/// Connected navigation with no animation code: the view model only changes its current page, and the views' XAML says
/// which elements are the same thing, how they fly and what enters with them.
/// </summary>
public partial class ConnectedXamlPage : ShowcasePage
{
    /// <summary>The photo's flight both ways: X and the scale ease out fast while Y eases in and out, bending the path into an arc.</summary>
    public static readonly AnimationBuilder Arc;
    private static readonly string ArcSource;

    static ConnectedXamlPage() =>
        (Arc, ArcSource) = Snippet.Of(Presto.Build().Duration(620).Ease(Eases.OutQuint).Y(ease: Eases.InOutCubic));

    public ConnectedXamlPage()
    {
        InitializeComponent();
        ArcCode.Code = $"// ConnectedXamlPage.cs\n{Snippet.Declare($"public static readonly AnimationBuilder {nameof(Arc)}", ArcSource)}";
    }
}
