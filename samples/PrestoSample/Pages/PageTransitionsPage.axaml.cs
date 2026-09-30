using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Monaco.Presto;
using Monaco.Presto.AvaloniaBridge;
using PrestoSample.Builder;
using PrestoSample.Showcase;

namespace PrestoSample.Pages;

/// <summary>
/// <see cref="PrestoPageTransition"/> on the three Avalonia page hosts: a <c>TransitioningContentControl</c> under each page
/// recipe or a custom builder pair with its code printed, a swipeable <c>Carousel</c> that scrubs a slide, and a
/// <c>NavigationPage</c> that pushes and pops.
/// </summary>
public partial class PageTransitionsPage : UserControl
{
    private const int PageCount = 4;

    private static readonly (PrestoPageTransition Value, string Code) Custom = Snippet.Of(new PrestoPageTransition
    {
        Enter = Presto.Build().Opacity(from: 0).Scale(from: 1.08).Rotate(from: -3)
            .Duration(450).Ease(Eases.OutQuint),
        Exit = Presto.Build().Opacity(0).Scale(0.94)
            .Duration(200).Ease(Eases.InQuad),
    });

    private int _page;

    public PageTransitionsPage()
    {
        InitializeComponent();
        Host.Content = Card(_page);
        RecipePicker.SelectedIndex = 3;
        Swiper.ItemsSource = Enumerable.Range(0, PageCount).Select(Card).ToList();
        _ = Stack.PushAsync(StackPage(0));
    }

    private void RecipeChanged(object? sender, SelectionChangedEventArgs e)
    {
        OptionPicker.ItemsSource = RecipePicker.SelectedIndex switch
        {
            2 => Enum.GetValues<PageAxis>(),
            3 or 4 => new[] { Edge.Right, Edge.Left, Edge.Bottom, Edge.Top },
            _ => null,
        };
        OptionPicker.IsVisible = OptionPicker.ItemsSource != null;
        OptionPicker.SelectedIndex = OptionPicker.IsVisible ? 0 : -1;
        Apply();
    }

    private void OptionChanged(object? sender, SelectionChangedEventArgs e) => Apply();

    private void Apply()
    {
        var (transition, call, markup) = (RecipePicker.SelectedIndex, OptionPicker.SelectedItem) switch
        {
            (0, _) => (Recipes.PageFade(), "Recipes.PageFade()", "{presto:PageFade}"),
            (1, _) => (Recipes.PageFadeThrough(), "Recipes.PageFadeThrough()", "{presto:PageFadeThrough}"),
            (2, PageAxis axis) => (Recipes.PageSharedAxis(axis), $"Recipes.PageSharedAxis(PageAxis.{axis})", $"{{presto:PageSharedAxis Axis={axis}}}"),
            (3, Edge edge) => (Recipes.PageSlide(edge), $"Recipes.PageSlide(Edge.{edge})", $"{{presto:PageSlide Edge={edge}}}"),
            (4, Edge edge) => (Recipes.PagePush(edge), $"Recipes.PagePush(Edge.{edge})", $"{{presto:PagePush Edge={edge}}}"),
            (5, _) => (Custom.Value, Custom.Code, null),
            // The option picker is between item sources.
            _ => default((PrestoPageTransition?, string, string?)),
        };
        if (transition == null) return;
        Host.PageTransition = transition;
        HostCode.Text = $"host.PageTransition = {call};"
            + (markup == null ? "" : $"\n\n<TransitioningContentControl\n    ClipToBounds=\"True\"\n    PageTransition=\"{markup}\" />");
    }

    private void NextClicked(object? sender, RoutedEventArgs e) => Go(1);

    private void BackClicked(object? sender, RoutedEventArgs e) => Go(-1);

    private void Go(int step)
    {
        _page = (_page + step + PageCount) % PageCount;
        Host.IsTransitionReversed = step < 0;
        Host.Content = Card(_page);
    }

    // The sleep waits a frame so the swap has started the transition, and runs below Render priority so that frame is committed first.
    private void FreezeClicked(object? sender, RoutedEventArgs e)
    {
        Go(1);
        TopLevel.GetTopLevel(this)?.RequestAnimationFrame(_ => Dispatcher.UIThread.Post(() => Thread.Sleep(2000), DispatcherPriority.Background));
    }

    private void PreviousSlideClicked(object? sender, RoutedEventArgs e) => Swiper.Previous();

    private void NextSlideClicked(object? sender, RoutedEventArgs e) => Swiper.Next();

    private void PushClicked(object? sender, RoutedEventArgs e) => _ = Stack.PushAsync(StackPage(Stack.StackDepth));

    private void PopClicked(object? sender, RoutedEventArgs e)
    {
        if (Stack.CanGoBack) _ = Stack.PopAsync();
    }

    private static ContentPage StackPage(int depth) => new() { Header = $"Level {depth + 1}", Content = Card(depth % PageCount) };

    private static Border Card(int index) => new()
    {
        Background = Palette.Categorical[index],
        CornerRadius = new(10),
        Child = new TextBlock
        {
            Text = $"Page {index + 1}",
            FontSize = 32,
            FontWeight = FontWeight.Bold,
            Foreground = Palette.Background,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        },
    };
}
