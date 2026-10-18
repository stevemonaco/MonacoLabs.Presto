using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Media;

namespace MonacoLabs.Presto;

/// <summary>
/// <c>{presto:Burst}</c>: a <see cref="CueAction"/> that calls <see cref="Recipes.Burst"/> on its <see cref="Control"/> for
/// each value except a first watched one.
/// </summary>
public sealed class BurstExtension : MarkupExtension
{
    /// <summary>How many particles to emit.</summary>
    public int Count { get; set; } = 24;

    /// <summary>The particles' color, or the first accent when unset.</summary>
    public Color? Color { get; set; }

    /// <summary>What plays on the target under reduced motion, or <see cref="Recipes.Pulse"/> when unset.</summary>
    public AnimationBuilder? Reduced { get; set; }

    /// <inheritdoc/>
    public override object ProvideValue(IServiceProvider serviceProvider) => new BurstAction(Count, Color, Reduced);

    private sealed class BurstAction(int count, Color? color, AnimationBuilder? reduced) : CueAction
    {
        protected internal override void Invoke(Visual target, object? value, bool first)
        {
            var control = Require<Control>(target, "{presto:Burst}");
            if (!first) Recipes.Burst(control, count, color, reduced);
        }
    }
}
