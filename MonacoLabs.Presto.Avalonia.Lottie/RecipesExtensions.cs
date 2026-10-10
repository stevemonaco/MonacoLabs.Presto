using MonacoLabs.Presto.Lottie.Prebuilt;

namespace MonacoLabs.Presto.Lottie;

/// <summary>Adds the Lottie recipes to the <see cref="Recipes"/> catalog.</summary>
public static class RecipesExtensions
{
    extension(Recipes)
    {
        /// <inheritdoc cref="LottieRecipes.PlayLottie"/>
        public static AnimationBuilder PlayLottie(string? segment = null, bool loop = true, bool reverse = false, double speed = 1, double? delay = null) =>
            LottieRecipes.PlayLottie(segment, loop, reverse, speed, delay);
    }
}
