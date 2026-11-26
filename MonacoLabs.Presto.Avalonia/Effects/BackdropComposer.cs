using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Data;
using Avalonia.Media;

namespace MonacoLabs.Presto.Effects;

/// <summary>
/// The blur the <c>BackdropBlur</c> shorthand animates, one per <see cref="Visual"/>: a bridge-owned <see cref="BlurEffect"/>
/// on <see cref="Visual.BackdropEffect"/> at <see cref="BindingPriority.Animation"/>, mutated in place and removed at radius 0,
/// starting from the user's own backdrop blur.
/// </summary>
internal sealed class BackdropComposer
{
    private static readonly ConditionalWeakTable<Visual, BackdropComposer> s_composers = new();

    private readonly BlurEffect _blur = new();
    private IDisposable? _written;

    public static double Get(Visual visual) =>
        s_composers.TryGetValue(visual, out var composer) ? composer._blur.Radius : UserRadius(visual);

    public static void Set(Visual visual, double radius)
    {
        if (!s_composers.TryGetValue(visual, out var composer))
        {
            composer = new BackdropComposer();
            s_composers.Add(visual, composer);
        }
        composer._blur.Radius = radius;
        if (radius <= 0) composer.Hide();
        else composer._written ??= visual.SetValue(Visual.BackdropEffectProperty, composer._blur, BindingPriority.Animation);
    }

    public static void Revert(Visual visual)
    {
        if (!s_composers.TryGetValue(visual, out var composer)) return;
        composer.Hide();
        s_composers.Remove(visual);
    }

    private static double UserRadius(Visual visual) =>
        (visual.GetBaseValue(Visual.BackdropEffectProperty).GetValueOrDefault() as IBlurEffect)?.Radius ?? 0;

    private void Hide()
    {
        _written?.Dispose();
        _written = null;
    }
}
