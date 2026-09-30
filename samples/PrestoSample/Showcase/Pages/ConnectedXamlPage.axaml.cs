using Monaco.Presto;
using Monaco.Presto.AvaloniaBridge;
using PrestoSample.Builder;

namespace PrestoSample.Showcase.Pages;

/// <summary>
/// Connected navigation with no animation code: the view model only changes its current page, and the views' XAML says
/// which elements are the same thing, how they fly and what enters with them. The XAML snippets are constants because XAML
/// has no caller-argument capture; a test pins each to this page's markup.
/// </summary>
public partial class ConnectedXamlPage : ShowcasePage
{
    public const string CardXaml = """
        <Button Classes="place" CommandParameter="{Binding}"
                Command="{Binding $parent[ItemsControl].((local:DestinationsViewModel)DataContext).OpenCommand}">
            <StackPanel>
                <Border Classes="photo"
                        presto:Connected.Key="{Binding Id, StringFormat=photo-{0}}"
                        presto:Connected.Motion="{x:Static local:ConnectedXamlPage.Arc}">
                    <showcase:Scenery Hue="{Binding Hue}" Index="{Binding Id}" />
                </Border>
                <TextBlock Classes="city" Text="{Binding City}" Margin="2,8,0,0"
                           presto:Connected.Key="{Binding Id, StringFormat=title-{0}}" />
                <TextBlock Classes="caption" Text="{Binding Country}" Margin="2,1,0,0" />
            </StackPanel>
        </Button>
        """;

    public const string DetailXaml = """
        <Grid ColumnDefinitions="330,*" ColumnSpacing="24" Margin="5">
            <Border Classes="photo hero"
                    presto:Connected.Key="{Binding Destination.Id, StringFormat=photo-{0}}"
                    presto:Connected.Motion="{x:Static local:ConnectedXamlPage.Arc}">
                <showcase:Scenery Hue="{Binding Destination.Hue}" Index="{Binding Destination.Id}" />
            </Border>
            <StackPanel Grid.Column="1">
                <TextBlock Classes="city hero" Text="{Binding Destination.City}"
                           presto:Connected.Key="{Binding Destination.Id, StringFormat=title-{0}}" />
                <StackPanel presto:Connected.Anchor="{Binding Destination.Id, StringFormat=photo-{0}}">
                    <TextBlock Classes="caption" Text="{Binding Destination.Country}" FontSize="14" />
                    <TextBlock Classes="blurb" Text="{Binding Destination.Blurb}" Margin="0,12,0,16" />
                    <Button Content="Back" Command="{Binding BackCommand}" />
                </StackPanel>
            </StackPanel>
        </Grid>
        """;

    public const string HostsXaml = """
        <Panel>
            <ContentControl Content="{Binding SwappedPage}" />
            <TransitioningContentControl Content="{Binding FadedPage}" PageTransition="{presto:PageFade Duration=300}" />
        </Panel>
        """;

    /// <summary>The photo's flight both ways: X and the scale ease out fast while Y eases in and out, bending the path into an arc.</summary>
    public static readonly AnimationBuilder Arc;
    private static readonly string ArcSource;

    static ConnectedXamlPage() =>
        (Arc, ArcSource) = Snippet.Of(Presto.Build().Duration(620).Ease(Eases.OutQuint).Y(ease: Eases.InOutCubic));

    public ConnectedXamlPage()
    {
        InitializeComponent();
        ArcCode.Text = $"// ConnectedXamlPage.cs\n{Snippet.Declare($"public static readonly AnimationBuilder {nameof(Arc)}", ArcSource)}";
    }
}
