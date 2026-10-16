using Avalonia.Controls;
using MonacoLabs.Presto;

namespace PrestoGallery.Builder;

/// <summary>The token preset picker shared by the recipe pages.</summary>
internal static class RecipeSettings
{
    private static readonly MotionTokens[] Presets = [MotionTokens.Standard, MotionTokens.Expressive, MotionTokens.Reduced];

    /// <summary>Restores <see cref="Recipes.Tokens"/> when the scope reverts.</summary>
    public static void Preserve(ControlScope scope) => scope.Add(_ =>
    {
        var tokens = Recipes.Tokens;
        return () => Recipes.Tokens = tokens;
    });

    /// <summary>Applies the picked preset globally and returns the code that sets it and the sidebar's reduced-motion flag.</summary>
    public static string Apply(ComboBox tokens)
    {
        Recipes.Tokens = Presets[tokens.SelectedIndex];
        return $"""
            Recipes.Tokens = MotionTokens.{((ComboBoxItem)tokens.SelectedItem!).Content};
            Presto.Defaults.ReducedMotion = {(Presto.Defaults.ReducedMotion ? "true" : "false")};
            """;
    }
}
