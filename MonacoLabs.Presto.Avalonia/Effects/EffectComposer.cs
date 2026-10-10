using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Data;
using Avalonia.Media;

namespace MonacoLabs.Presto.Effects;

/// <summary>
/// The effect the effect shorthands animate, one per <see cref="Visual"/>: a bridge-owned <see cref="BlurEffect"/> or
/// <see cref="DropShadowEffect"/> written at <see cref="BindingPriority.Animation"/> and mutated in place, so releasing the
/// last shorthand uncovers the visual's own <see cref="Visual.Effect"/>. A user effect of the same kind supplies the starting
/// values; without one a blur reads 0, and a shadow reads black, fully opaque, unblurred and at no offset, so animating
/// <c>ShadowBlur</c> alone shows it.
/// </summary>
/// <remarks>
/// A visual holds one effect, so a blur and a shadow cannot animate it at once; one after the other switches the kind. An
/// effect at rest, a blur of 0 or a shadow whose opacity ended at 0, is removed rather than left costing an offscreen pass.
/// </remarks>
internal sealed class EffectComposer
{
    private static readonly ConditionalWeakTable<Visual, EffectComposer> s_composers = new();

    private BlurEffect? _blur;
    private DropShadowEffect? _shadow;
    private readonly ExclusiveKinds<EffectShorthand> _kinds = new(
        EffectShorthand.ShadowX, EffectShorthand.ShadowOpacity, "a visual holds one effect. Put the blur on an inner element, or use BoxShadow on a Border.");
    private IDisposable? _written;
    private bool _faded;
    private double? _followed;

    public static object Identity(EffectShorthand shorthand) => Read(null, shorthand);

    public static object Get(Visual visual, EffectShorthand shorthand) =>
        !s_composers.TryGetValue(visual, out var composer) ? Read(visual.GetBaseValue(Visual.EffectProperty).GetValueOrDefault(), shorthand)
        : composer._kinds.Shows(shorthand) ? Read(composer._kinds.IsKindB ? composer._shadow : composer._blur, shorthand)
        : shorthand == EffectShorthand.Blur && composer._followed is { } radius ? radius
        : Read(visual.GetBaseValue(Visual.EffectProperty).GetValueOrDefault(), shorthand);

    /// <inheritdoc cref="ExclusiveKinds{TShorthand}.Claim"/>
    public static void Claim(Visual visual, EffectShorthand shorthand) =>
        s_composers.GetValue(visual, static _ => new EffectComposer())._kinds.Claim(visual, shorthand);

    /// <summary>Writes one frame; <paramref name="ended"/> says whether its tween has reached its end, which a shadow must before fading out removes it.</summary>
    public static void Set(Visual visual, EffectShorthand shorthand, double value, bool ended)
    {
        var composer = Begin(visual, shorthand);
        composer.Apply(shorthand, value);
        if (shorthand == EffectShorthand.ShadowOpacity) composer._faded = value <= 0 && ended;
        composer.Show(visual);
    }

    public static void SetColor(Visual visual, Color color)
    {
        var composer = Begin(visual, EffectShorthand.ShadowColor);
        composer._shadow!.Color = color;
        composer.Show(visual);
    }

    /// <summary>Makes <paramref name="radius"/> the base blur: shown while no animation holds the effect, and restored when the last one is reverted.</summary>
    public static void Follow(Visual visual, double radius)
    {
        var composer = s_composers.GetValue(visual, static _ => new EffectComposer());
        composer._followed = radius;
        if (!composer._kinds.AnimatesAny) composer.ShowFollowed(visual);
    }

    /// <summary>Drops the followed blur; unless an animation holds the effect, the user's effect shows again.</summary>
    public static void Unfollow(Visual visual)
    {
        if (!s_composers.TryGetValue(visual, out var composer) || composer._followed == null) return;
        composer._followed = null;
        if (composer._kinds.AnimatesAny) return;
        composer.Hide();
        s_composers.Remove(visual);
    }

    /// <summary>Releases one shorthand back to the user's value; the last of them takes the whole effect with it, uncovering any followed blur.</summary>
    public static void Revert(Visual visual, EffectShorthand shorthand)
    {
        if (!s_composers.TryGetValue(visual, out var composer) || !composer._kinds.Animates(shorthand)) return;

        if (composer._kinds.Release(shorthand))
        {
            if (shorthand == EffectShorthand.ShadowOpacity) composer._faded = false;
            composer.Reset(visual.GetBaseValue(Visual.EffectProperty).GetValueOrDefault(), shorthand);
            composer.Show(visual);
            return;
        }
        composer.Hide();
        if (composer._followed != null) composer.ShowFollowed(visual);
        else s_composers.Remove(visual);
    }

    private static object Read(IEffect? effect, EffectShorthand shorthand) => shorthand switch
    {
        EffectShorthand.Blur => (effect as IBlurEffect)?.Radius ?? 0d,
        EffectShorthand.ShadowX => (effect as IDropShadowEffect)?.OffsetX ?? 0d,
        EffectShorthand.ShadowY => (effect as IDropShadowEffect)?.OffsetY ?? 0d,
        EffectShorthand.ShadowBlur => (effect as IDropShadowEffect)?.BlurRadius ?? 0d,
        EffectShorthand.ShadowColor => (effect as IDropShadowEffect)?.Color ?? Colors.Black,
        _ => (effect as IDropShadowEffect)?.Opacity ?? 1d,
    };

    private static EffectComposer Begin(Visual visual, EffectShorthand shorthand)
    {
        var composer = s_composers.GetValue(visual, static _ => new EffectComposer());
        if (!composer._kinds.Begin(shorthand)) return composer;

        composer.Hide();
        composer._faded = false;
        var isShadow = composer._kinds.IsKindB;
        if (isShadow) composer._shadow ??= new DropShadowEffect();
        else composer._blur ??= new BlurEffect();
        var user = visual.GetBaseValue(Visual.EffectProperty).GetValueOrDefault();
        var last = isShadow ? EffectShorthand.ShadowOpacity : EffectShorthand.Blur;
        for (var each = isShadow ? EffectShorthand.ShadowX : EffectShorthand.Blur; each <= last; each++) composer.Reset(user, each);
        return composer;
    }

    private void Reset(IEffect? user, EffectShorthand shorthand)
    {
        if (Read(user, shorthand) is Color color) _shadow!.Color = color;
        else Apply(shorthand, (double)Read(user, shorthand));
    }

    private void Apply(EffectShorthand shorthand, double value)
    {
        switch (shorthand)
        {
            case EffectShorthand.Blur:
                _blur!.Radius = value;
                break;
            case EffectShorthand.ShadowX:
                _shadow!.OffsetX = value;
                break;
            case EffectShorthand.ShadowY:
                _shadow!.OffsetY = value;
                break;
            case EffectShorthand.ShadowBlur:
                _shadow!.BlurRadius = value;
                break;
            case EffectShorthand.ShadowOpacity:
                _shadow!.Opacity = Math.Clamp(value, 0, 1);
                break;
        }
    }

    private void Show(Visual visual)
    {
        if (_kinds.IsKindB ? _faded : _blur!.Radius <= 0) Hide();
        else _written ??= visual.SetValue(Visual.EffectProperty, _kinds.IsKindB ? _shadow : _blur, BindingPriority.Animation);
    }

    private void ShowFollowed(Visual visual)
    {
        var blur = _blur ??= new BlurEffect();
        blur.Radius = _followed!.Value;
        if (blur.Radius <= 0) Hide();
        else _written ??= visual.SetValue(Visual.EffectProperty, blur, BindingPriority.Animation);
    }

    private void Hide()
    {
        _written?.Dispose();
        _written = null;
    }
}
