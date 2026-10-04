using Monaco.Presto.AvaloniaBridge.Lottie.Prebuilt;

namespace Monaco.Presto.AvaloniaBridge.Lottie;

/// <summary>Adds the Lottie recipes to the <see cref="Recipes"/> catalogue.</summary>
public static class RecipesExtensions
{
    extension(Recipes)
    {
        /// <inheritdoc cref="LottieRecipes.PlayLottie"/>
        public static AnimationBuilder PlayLottie(string? segment = null, bool loop = true, bool reverse = false, double speed = 1, double? delay = null) =>
            LottieRecipes.PlayLottie(segment, loop, reverse, speed, delay);
    }
}
