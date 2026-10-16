using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.VisualTree;
using MonacoLabs.Presto;
using MonacoLabs.Presto.Values;
using PrestoGallery.Showcase;

namespace PrestoGallery.Builder;

/// <summary>Three library recipes read <see cref="Recipes.Tokens"/>, so picking a <see cref="MotionTokens"/> preset restyles every motion on the stage at once.</summary>
public partial class MotionTokensPage : ShowcasePage
{
    #region snippet MotionTokensShades
    private static readonly PrestoColor Accent = PrestoColor.Parse("#2563eb");
    private static readonly PrestoColor Hover = Accent.ShiftOklch(dl: 0.06);
    private static readonly PrestoColor Pressed = Accent.ShiftOklch(dl: -0.08);
    private static readonly PrestoColor Muted = Accent.WithOklch(c: 0.04);
    #endregion

    public MotionTokensPage()
    {
        InitializeComponent();
        TokensBox.SelectionChanged += (_, _) => SettingsChanged();
        SpaceBox.SelectionChanged += (_, _) => SettingsChanged();
        HueBox.SelectionChanged += (_, _) => SettingsChanged();
        StateCard.PointerEntered += (_, _) => Shade(Hover, nameof(Hover));
        StateCard.PointerExited += (_, _) => Shade(Accent, nameof(Accent));
        StateCard.PointerPressed += (_, _) => Shade(Pressed, nameof(Pressed));
        StateCard.PointerReleased += (_, _) =>
        {
            if (StateCard.IsPointerOver) Shade(Hover, nameof(Hover));
            else Shade(Accent, nameof(Accent));
        };
        MutedBox.IsCheckedChanged += (_, _) => Shade(Accent, nameof(Accent));
    }

    public IReadOnlyList<Animation> Played { get; private set; } = [];

    public Animation? ColorState { get; private set; }

    protected override void OnAttached()
    {
        RecipeSettings.Preserve(Scope);
        StateCard.Background = new SolidColorBrush(Accent.ToAvalonia());
        Play();
        StateCode.Code = Snippets.MotionTokensShades;
    }

    private void Play()
    {
        foreach (var played in Played) played.Revert();
        ApplyTokens();
        var (rows, rowsCode) = Snippet.Of(Recipes.FadeUp(stagger: true).Animate(Rows.Children));
        var (panel, panelCode) = Snippet.Of(Panel.Animate(Recipes.SlideIn(Edge.Right)));
        var (fab, fabCode) = Snippet.Of(Fab.Animate(Recipes.PopIn(delay: Recipes.Tokens.Long)));
        Played = [rows, panel, fab];
        PlayCode.Code = $"{rowsCode};\n{panelCode};\n{fabCode};";
    }

    private void ApplyTokens()
    {
        var preset = RecipeSettings.Apply(TokensBox);
        var (space, hue) = ((ColorSpace)SpaceBox.SelectedIndex, (HueInterpolation)HueBox.SelectedIndex);
        Recipes.Tokens = Recipes.Tokens with { ColorSpace = space, Hue = hue };
        TokensCode.Code = $"{preset}\nRecipes.Tokens = Recipes.Tokens with {{ ColorSpace = ColorSpace.{space}, Hue = HueInterpolation.{hue} }};";
    }

    private void Shade(PrestoColor state, string name)
    {
        if (MutedBox.IsChecked == true) (state, name) = (Muted, nameof(Muted));
        var from = ((ISolidColorBrush)StateCard.Background!).Color.ToPresto();
        string code;
        (ColorState, code) = Snippet.Of(StateCard.Animate(Presto.Build()
            .Background(k => k.Color(from, state, Recipes.Tokens.ColorSpace, Recipes.Tokens.Hue,
                duration: Recipes.Tokens.Medium, ease: Recipes.Tokens.Ease))));
        StateCode.Code = $"{Snippets.MotionTokensShades}\n\nvar state = {name};\n{code};";
    }

    private void SettingsChanged()
    {
        if (this.IsAttachedToVisualTree()) Play();
    }

    private void Replay(object? sender, RoutedEventArgs e) => Play();
}
