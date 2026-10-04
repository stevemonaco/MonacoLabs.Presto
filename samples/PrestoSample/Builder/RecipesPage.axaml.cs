using System.Globalization;
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
    #region snippet RecipesPop
    private static readonly AnimationBuilder Pop = Presto.Build()
        .Scale(k => k.To(1.35).To(1))
        .Y(k => k.To(-18).To(0))
        .Ease(Eases.OutQuad);
    #endregion

    private string _recipe = "";

    public RecipesPage() => InitializeComponent();

    public Animation? Played { get; private set; }

    public Animation? Staggered { get; private set; }

    protected override void OnAttached()
    {
        SetDuration(DurationSlider.Value);
        Stagger();
    }

    private void SetDuration(double duration)
    {
        Pop.Duration(duration);
        _recipe = $"{Snippets.RecipesPop}\n\nPop.Duration({duration.ToString(CultureInfo.InvariantCulture)});";
        RecipeCode.Code = _recipe;
    }

    private void Stagger() =>
        (Staggered, StaggeredCode.Code) = Snippet.Of(Pop.With(b => b.Delay(Presto.Stagger(60))).Animate(Row.Children));

    private void PlayOne(object? sender, RoutedEventArgs e)
    {
        var button = (Button)sender!;
        (Played, var code) = Snippet.Of(button.Animate(Pop));
        RecipeCode.Code = $"{_recipe}\n\n{code};";
    }

    private void PlayStaggered(object? sender, RoutedEventArgs e) => Stagger();

    private void DurationChanged(object? sender, RangeBaseValueChangedEventArgs e) => SetDuration(e.NewValue);
}
