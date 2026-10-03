using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Monaco.Presto;
using Monaco.Presto.AvaloniaBridge;
using PrestoSample.Showcase;

namespace PrestoSample.Builder;

/// <summary>Three library recipes read <see cref="Recipes.Tokens"/>, so picking a <see cref="MotionTokens"/> preset restyles every motion on the stage at once.</summary>
public partial class MotionTokensPage : ShowcasePage
{
    public MotionTokensPage()
    {
        InitializeComponent();
        TokensBox.SelectionChanged += (_, _) => SettingsChanged();
    }

    public IReadOnlyList<Animation> Played { get; private set; } = [];

    protected override void OnAttached()
    {
        RecipeSettings.Preserve(Scope);
        Play();
    }

    private void Play()
    {
        foreach (var played in Played) played.Revert();
        TokensCode.Code = RecipeSettings.Apply(TokensBox);
        var (rows, rowsCode) = Snippet.Of(Recipes.FadeUp(stagger: true).Animate(Rows.Children));
        var (panel, panelCode) = Snippet.Of(Panel.Animate(Recipes.SlideIn(Edge.Right)));
        var (fab, fabCode) = Snippet.Of(Fab.Animate(Recipes.PopIn(delay: Recipes.Tokens.Long)));
        Played = [rows, panel, fab];
        PlayCode.Code = $"{rowsCode};\n{panelCode};\n{fabCode};";
    }

    private void SettingsChanged()
    {
        if (this.IsAttachedToVisualTree()) Play();
    }

    private void Replay(object? sender, RoutedEventArgs e) => Play();
}
