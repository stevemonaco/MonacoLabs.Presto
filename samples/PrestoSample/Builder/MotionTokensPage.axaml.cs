using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Monaco.Presto;
using Monaco.Presto.AvaloniaBridge;
using PrestoSample.Showcase;

namespace PrestoSample.Builder;

/// <summary>Three recipes built from a <see cref="MotionTokens"/> set; switching the set restyles every motion on the stage at once.</summary>
public partial class MotionTokensPage : ShowcasePage
{
    private static readonly IReadOnlyList<(MotionTokens Tokens, string Code)> TokenSets =
    [
        Snippet.Of(new MotionTokens(Short: 200, Medium: 450, Long: 650,
            Standard: Eases.OutQuad, Emphasized: Eases.OutCubic, Distance: 24, StaggerStep: 50)),
        Snippet.Of(new MotionTokens(Short: 450, Medium: 900, Long: 1200,
            Standard: Eases.OutBack(2.2), Emphasized: Eases.OutElastic(1, .5), Distance: 64, StaggerStep: 90)),
        Snippet.Of(new MotionTokens(Short: 120, Medium: 150, Long: 200,
            Standard: Eases.None, Emphasized: Eases.None, Distance: 0, StaggerStep: 0)),
    ];

    private static readonly Func<MotionTokens, AnimationBuilder> Enter;
    private static readonly Func<MotionTokens, AnimationBuilder> SlideIn;
    private static readonly Func<MotionTokens, AnimationBuilder> Pop;
    private static readonly string Recipes;

    static MotionTokensPage()
    {
        (Enter, var enter) = Snippet.Of<Func<MotionTokens, AnimationBuilder>>(t => Presto.Build()
            .Opacity(from: 0, to: 1)
            .Y(from: t.Distance, to: 0)
            .Duration(t.Medium)
            .Ease(t.Emphasized));
        (SlideIn, var slideIn) = Snippet.Of<Func<MotionTokens, AnimationBuilder>>(t => Presto.Build()
            .Opacity(from: 0, to: 1)
            .X(from: t.Distance * 3, to: 0)
            .Duration(t.Long)
            .Ease(t.Emphasized));
        (Pop, var pop) = Snippet.Of<Func<MotionTokens, AnimationBuilder>>(t => Presto.Build()
            .Scale(k => k.To(1 + (t.Distance / 100)).To(1))
            .Rotate(from: -t.Distance, to: 0)
            .Duration(t.Short * 2)
            .Ease(t.Standard));
        const string recipe = "static readonly Func<MotionTokens, AnimationBuilder>";
        Recipes = string.Join("\n\n",
            Snippet.Declare($"{recipe} Enter", enter),
            Snippet.Declare($"{recipe} SlideIn", slideIn),
            Snippet.Declare($"{recipe} Pop", pop));
    }

    public MotionTokensPage() => InitializeComponent();

    public IReadOnlyList<Animation> Played { get; private set; } = [];

    protected override void OnAttached()
    {
        RecipesCode.Text = Recipes;
        Play();
    }

    private void Play()
    {
        var (tokens, tokensCode) = TokenSets[SelectedSet()];
        var (rows, rowsCode) = Snippet.Of(Enter(tokens).Delay(Presto.Stagger(tokens.StaggerStep)).Animate(Rows.Children));
        var (panel, panelCode) = Snippet.Of(Panel.Animate(SlideIn(tokens)));
        var (fab, fabCode) = Snippet.Of(Fab.Animate(Pop(tokens).Delay(tokens.Long)));
        Played = [rows, panel, fab];
        TokensCode.Text = Snippet.Declare("var tokens", tokensCode);
        PlayCode.Text = $"{rowsCode};\n{panelCode};\n{fabCode};";
    }

    private int SelectedSet() => ExpressiveOption.IsChecked == true ? 1 : ReducedOption.IsChecked == true ? 2 : 0;

    private void TokensChanged(object? sender, RoutedEventArgs e)
    {
        if (sender is RadioButton { IsChecked: true } && this.IsAttachedToVisualTree()) Play();
    }

    private void Replay(object? sender, RoutedEventArgs e) => Play();
}
