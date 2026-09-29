using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Monaco.Presto;
using Monaco.Presto.AvaloniaBridge;
using PrestoSample.Showcase;

namespace PrestoSample.Builder;

/// <summary>One static builder reused on any clicked button and staggered over the row through <c>With</c>; the slider changes the recipe, not what already runs.</summary>
public partial class RecipesPage : ShowcasePage
{
    private static readonly AnimationBuilder Pop;
    private static readonly string Declaration;

    static RecipesPage()
    {
        (Pop, var code) = Snippet.Of(Presto.Build()
            .Scale(k => k.To(1.35).To(1))
            .Y(k => k.To(-18).To(0))
            .Ease(Eases.OutQuad));
        Declaration = Snippet.Declare("static readonly AnimationBuilder Pop", code);
    }

    public RecipesPage() => InitializeComponent();

    public Animation? Played { get; private set; }

    public Animation? Staggered { get; private set; }

    protected override void OnAttached()
    {
        Pop.Duration(DurationSlider.Value);
        RecipeCode.Text = Declaration;
        Stagger();
    }

    private void Stagger() =>
        (Staggered, StaggeredCode.Text) = Snippet.Of(Pop.With(b => b.Delay(Presto.Stagger(60))).Animate(Row.Children));

    private void PlayOne(object? sender, RoutedEventArgs e)
    {
        var button = (Button)sender!;
        (Played, var code) = Snippet.Of(button.Animate(Pop));
        RecipeCode.Text = $"{Declaration}\n\n{code};";
    }

    private void PlayStaggered(object? sender, RoutedEventArgs e) => Stagger();

    private void DurationChanged(object? sender, RangeBaseValueChangedEventArgs e) => Pop.Duration(e.NewValue);
}
