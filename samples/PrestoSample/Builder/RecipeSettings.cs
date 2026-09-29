using Avalonia.Controls;
using Monaco.Presto;
using Monaco.Presto.AvaloniaBridge;

namespace PrestoSample.Builder;

/// <summary>The token preset and reduced-motion picker shared by the recipe pages.</summary>
internal static class RecipeSettings
{
    private static readonly MotionTokens[] Presets = [MotionTokens.Standard, MotionTokens.Expressive, MotionTokens.Reduced];

    /// <summary>Restores <see cref="Recipes.Tokens"/> and <see cref="PrestoDefaults.ReducedMotion"/> when the scope reverts.</summary>
    public static void Preserve(ControlScope scope) => scope.Add(_ =>
    {
        var (tokens, reduced) = (Recipes.Tokens, Presto.Defaults.ReducedMotion);
        return () => (Recipes.Tokens, Presto.Defaults.ReducedMotion) = (tokens, reduced);
    });

    /// <summary>Applies the picked preset and flag globally and returns the code that does the same.</summary>
    public static string Apply(ComboBox tokens, ToggleSwitch reduced)
    {
        Recipes.Tokens = Presets[tokens.SelectedIndex];
        Presto.Defaults.ReducedMotion = reduced.IsChecked == true;
        return $"""
            Recipes.Tokens = MotionTokens.{((ComboBoxItem)tokens.SelectedItem!).Content};
            Presto.Defaults.ReducedMotion = {(Presto.Defaults.ReducedMotion ? "true" : "false")};
            """;
    }
}
