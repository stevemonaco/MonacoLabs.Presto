namespace PrestoSample.Showcase.Pages;

/// <summary>
/// Connected navigation with no animation code: the view model only changes its current page, and the views' XAML says
/// which elements are the same thing, how they fly and what enters with them.
/// </summary>
public partial class ConnectedXamlPage : ShowcasePage
{
    public ConnectedXamlPage()
    {
        InitializeComponent();
        ArcCode.Code = $"// PhotoFlight.cs\n{Snippets.ConnectedXamlArc}";
    }
}
