using System.Collections.ObjectModel;
using Avalonia.Interactivity;
using Monaco.Presto;
using Monaco.Presto.AvaloniaBridge;
using PrestoSample.Showcase;

namespace PrestoSample.Builder;

/// <summary>
/// Animations attached from XAML: entrances on <c>Presence.Arrive</c> from a static recipe and a markup extension, and a
/// looping emphasis on <c>Animate.OnAttached</c> from a binding. The XAML snippets are constants because XAML has no
/// caller-argument capture; a test pins each to this page's markup.
/// </summary>
public partial class XamlPage : ShowcasePage
{
    public const string StaticXaml = """
        <Border Classes="swatch" Width="64" Height="64" presto:Presence.Arrive="{x:Static local:XamlPage.FadeUp}" />
        """;

    public const string ExtensionXaml = """
        <TextBlock Classes="row" Text="Inbox" presto:Presence.Arrive="{presto:Fade Direction=Up, Distance=48, Delay=0}" />
        <TextBlock Classes="row" Text="Drafts" presto:Presence.Arrive="{presto:Fade Direction=Up, Distance=48, Delay=100}" />
        <TextBlock Classes="row" Text="Sent" presto:Presence.Arrive="{presto:Fade Direction=Up, Distance=48, Delay=200}" />
        <TextBlock Classes="row" Text="Archive" presto:Presence.Arrive="{presto:Fade Direction=Up, Distance=48, Delay=300}" />
        """;

    public const string BindingXaml = """
        <ToggleSwitch IsChecked="{Binding IsAlert}" OffContent="Idle" OnContent="Alert" />
        <PathIcon Data="{StaticResource StarIcon}" Width="48" Height="48" Foreground="{x:Static showcase:Palette.Amber}"
                  presto:Animate.OnAttached="{Binding Emphasis}" />
        """;

    public const string ReorderXaml = """
        <ListBox x:Name="DefaultLift" Classes="reorder" presto:Reorder.Enabled="True" presto:Reorder.Lift="{presto:Reorder}" presto:Animate.ItemLayout="{presto:Reflow}" />
        <ListBox x:Name="StrongLift" Classes="reorder" presto:Reorder.Enabled="True" presto:Reorder.Lift="{presto:Reorder Intensity=2}" presto:Animate.ItemLayout="{presto:Reflow}" />
        """;

    public static readonly AnimationBuilder FadeUp;
    private static readonly string FadeUpCode;

    static XamlPage() =>
        (FadeUp, FadeUpCode) = Snippet.Of(Presto.Build()
            .Opacity(from: 0, to: 1)
            .Y(from: 32, to: 0)
            .Duration(700)
            .Ease(Eases.OutQuad));

    public XamlPage()
    {
        InitializeComponent();
        StaticCode.Text = Snippet.Declare($"public static readonly AnimationBuilder {nameof(FadeUp)}", FadeUpCode);
        ExtensionCode.Text = "// {presto:Fade Direction=Up, Distance=48, Delay=100} provides, once per element\nRecipes.FadeUp(distance: 48, delay: 100)";
        BindingCode.Text = "// EmphasisViewModel: IsAlert swaps Emphasis between these two\n" +
            $"{Snippet.Declare(nameof(EmphasisViewModel.Idle), EmphasisViewModel.Idle.Code)}\n{Snippet.Declare(nameof(EmphasisViewModel.Alert), EmphasisViewModel.Alert.Code)}";
        ReorderCode.Text = "// {presto:Reorder} and {presto:Reorder Intensity=2} provide, once per list\nRecipes.ReorderLift()\nRecipes.ReorderLift(intensity: 2)";
        DefaultLift.ItemsSource = new ObservableCollection<string>(["Inbox", "Drafts", "Sent", "Archive"]);
        StrongLift.ItemsSource = new ObservableCollection<string>(["Inbox", "Drafts", "Sent", "Archive"]);
    }

    private void Replay(object? sender, RoutedEventArgs e)
    {
        var panels = Demos.Children.ToList();
        Demos.Children.Clear();
        Demos.Children.AddRange(panels);
    }
}
