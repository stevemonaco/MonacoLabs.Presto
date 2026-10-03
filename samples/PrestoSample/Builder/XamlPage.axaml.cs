using System.Collections.ObjectModel;
using Avalonia.Interactivity;
using Monaco.Presto;
using Monaco.Presto.AvaloniaBridge;
using PrestoSample.Showcase;

namespace PrestoSample.Builder;

/// <summary>
/// Animations attached from XAML: entrances on <c>Presence.Arrive</c> from a static recipe and a markup extension, and a
/// looping emphasis on <c>Animate.OnAttached</c> from a binding.
/// </summary>
public partial class XamlPage : ShowcasePage
{
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
        StaticCode.CSharp = Snippet.Declare($"public static readonly AnimationBuilder {nameof(FadeUp)}", FadeUpCode);
        ExtensionCode.CSharp = "// {presto:Fade Direction=Up, Distance=48, Delay=100} provides, once per element\nRecipes.FadeUp(distance: 48, delay: 100)";
        BindingCode.CSharp = "// EmphasisViewModel: IsAlert swaps Emphasis between these two\n" +
            $"{Snippet.Declare(nameof(EmphasisViewModel.Idle), EmphasisViewModel.Idle.Code)}\n{Snippet.Declare(nameof(EmphasisViewModel.Alert), EmphasisViewModel.Alert.Code)}";
        ReorderCode.CSharp = "// {presto:Reorder} and {presto:Reorder Intensity=2} provide, once per list\nRecipes.ReorderLift()\nRecipes.ReorderLift(intensity: 2)";
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
