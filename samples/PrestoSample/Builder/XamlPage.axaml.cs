using Avalonia.Interactivity;
using PrestoSample.Showcase;

namespace PrestoSample.Builder;

/// <summary>
/// <c>Animate.OnAttached</c> set three ways from XAML: a static recipe, a markup extension and a binding. The XAML
/// snippets are constants because XAML has no caller-argument capture; a test pins each to this page's markup.
/// </summary>
public partial class XamlPage : ShowcasePage
{
    public const string StaticXaml = """
        <Border Classes="swatch" Width="64" Height="64" presto:Animate.OnAttached="{x:Static local:Motions.FadeUp}" />
        """;

    public const string ExtensionXaml = """
        <TextBlock Classes="row" Text="Inbox" presto:Animate.OnAttached="{local:FadeUp Distance=48, Delay=0}" />
        <TextBlock Classes="row" Text="Drafts" presto:Animate.OnAttached="{local:FadeUp Distance=48, Delay=100}" />
        <TextBlock Classes="row" Text="Sent" presto:Animate.OnAttached="{local:FadeUp Distance=48, Delay=200}" />
        <TextBlock Classes="row" Text="Archive" presto:Animate.OnAttached="{local:FadeUp Distance=48, Delay=300}" />
        """;

    public const string BindingXaml = """
        <ToggleSwitch IsChecked="{Binding IsAlert}" OffContent="Idle" OnContent="Alert" />
        <PathIcon Data="{StaticResource StarIcon}" Width="48" Height="48" Foreground="{x:Static showcase:Palette.Amber}"
                  presto:Animate.OnAttached="{Binding Emphasis}" />
        """;

    public XamlPage()
    {
        InitializeComponent();
        const string field = "public static readonly AnimationBuilder";
        StaticCode.Text = $"// Motions.cs\n{Snippet.Declare($"{field} {nameof(Motions.FadeUp)}", Motions.FadeUpCode)}";
        ExtensionCode.Text = $"// FadeUpExtension.ProvideValue, once per element\nreturn {FadeUpExtension.Code};";
        BindingCode.Text = $"// EmphasisViewModel: IsAlert swaps Emphasis between these two\n" +
            $"{Snippet.Declare($"{field} {nameof(Motions.Float)}", Motions.FloatCode)}\n\n{Snippet.Declare($"{field} {nameof(Motions.Shake)}", Motions.ShakeCode)}";
    }

    private void Replay(object? sender, RoutedEventArgs e)
    {
        var panels = Demos.Children.ToList();
        Demos.Children.Clear();
        Demos.Children.AddRange(panels);
    }
}
