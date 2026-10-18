using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Media;
using MonacoLabs.Presto.Compositor;
using MonacoLabs.Presto.Effects;
using MonacoLabs.Presto.Shapes;
using MonacoLabs.Presto.Text;
using MonacoLabs.Presto.Transforms;
using MonacoLabs.Presto.Values;
using MonacoLabs.Presto.Core;

namespace MonacoLabs.Presto.Targets;

/// <summary>
/// Resolves the properties of an <see cref="AvaloniaObject"/>: the transform shorthands ("X", "Rotate", "ScaleX"), the
/// effect shorthands ("Blur", "ShadowX"), the reveal shorthands ("ClipInset", "ClipCircle", "MaskSweep"), a split <see cref="TextBlock"/>'s warp shorthands ("Warp", "WarpOrigin"), a <see cref="Shape"/>'s "Draw", a <see cref="ScrollViewer"/>'s "ScrollX" and "ScrollY", plain names ("Opacity", "Margin"), qualified or attached names ("Canvas.Left"), and the typed <see cref="AvaloniaProperty"/>
/// instances <see cref="AvaloniaAnimationParams"/> encodes. Any other target is left to the next adapter.
/// </summary>
/// <remarks>
/// A target's property is bound once at <see cref="BindingPriority.Animation"/> and every frame is pushed through that one
/// binding, so a frame costs no value entry of its own and a revert disposes the binding, uncovering whatever value the
/// property had underneath. A direct property binding ignores the priority and writes through the setter, so the value the
/// property held before the first frame is captured and written back on revert instead. A <see cref="double"/> property is
/// bound typed, so a frame pushes it unboxed and what is left per frame is the property-changed args Avalonia raises.
/// <see cref="Visual.OpacityProperty"/> is clamped to 0-1, so an overshooting ease holds at the ends. On a layout-rounded element,
/// <see cref="Layoutable.WidthProperty"/> and <see cref="Layoutable.HeightProperty"/> move from their start in four-device-pixel steps
/// until the last frame, so a centered element's edges move together. A property type with no
/// codec is discrete (<see cref="DiscreteCodec"/>), switching at the midpoint except <see cref="Visual.IsVisibleProperty"/>,
/// which stays true throughout.
/// </remarks>
public sealed class AvaloniaObjectTargetAdapter : ITargetAdapter
{
    private static readonly Dictionary<(Type Type, string Name), AvaloniaProperty> s_resolved = [];
    private static readonly Dictionary<AvaloniaProperty, AvaloniaPropertyAccessor> s_accessors = [];
    private static readonly IPropertyAccessor?[] s_transforms = new IPropertyAccessor?[Enum.GetValues<TransformShorthand>().Length];
    private static readonly IPropertyAccessor?[] s_effects = new IPropertyAccessor?[Enum.GetValues<EffectShorthand>().Length];
    private static readonly IPropertyAccessor?[] s_clips = new IPropertyAccessor?[Enum.GetValues<ClipShorthand>().Length];
    private static readonly IPropertyAccessor?[] s_masks = new IPropertyAccessor?[Enum.GetValues<MaskShorthand>().Length];
    private static readonly IPropertyAccessor?[] s_warps = new IPropertyAccessor?[Enum.GetValues<WarpShorthand>().Length];
    private static IPropertyAccessor? s_draw;
    private static IPropertyAccessor? s_scrollX;
    private static IPropertyAccessor? s_scrollY;

    internal static string Encode(AvaloniaProperty property) => $"{property.OwnerType.Name}.{property.Name}";

    /// <summary>
    /// A shorthand moves the visual in device independent pixels and turns it in degrees, unless the value carries a unit of
    /// its own (<c>"1turn"</c>, <c>".5rad"</c>); every other name is looked up in the <see cref="AvaloniaPropertyRegistry"/>.
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="target"/>'s type has no Avalonia property registered under <paramref name="property"/>.</exception>
    public IPropertyAccessor? Resolve(object target, string property)
    {
        if (target is not AvaloniaObject) return null;

        if (target is Visual visual)
        {
            AvaloniaFrameSource.Seed(visual);
            if (TransformShorthands.Parse(property) is { } shorthand)
            {
                return s_transforms[(int)shorthand] ??= new TransformAccessor(shorthand);
            }
            if (EffectShorthands.Parse(property) is { } effect)
            {
                EffectComposer.Claim(visual, effect);
                return s_effects[(int)effect] ??= new EffectAccessor(effect);
            }
            if (EffectShorthands.ParseClip(property) is { } clip)
            {
                ClipComposer.Claim(visual, clip);
                return s_clips[(int)clip] ??= new ClipAccessor(clip);
            }
            if (EffectShorthands.ParseMask(property) is { } mask) return s_masks[(int)mask] ??= new MaskAccessor(mask);
            if (visual is TextBlock && WarpComposer.Parse(property) is { } warp) return s_warps[(int)warp] ??= new WarpAccessor(warp);
            if (visual is Shape && property == "Draw") return s_draw ??= new DrawAccessor();
            if (visual is ScrollViewer && property == "ScrollX") return s_scrollX ??= new ScrollOffsetAccessor(horizontal: true);
            if (visual is ScrollViewer && property == "ScrollY") return s_scrollY ??= new ScrollOffsetAccessor(horizontal: false);
        }

        var type = target.GetType();
        if (ResolveProperty(type, property) is not { } resolved)
        {
            throw new ArgumentException($"{type} has no registered Avalonia property named '{property}'.", nameof(property));
        }

        s_accessors.TryGetValue(resolved, out var accessor);
        if (accessor is not { CodecIsCurrent: true }) s_accessors[resolved] = accessor = new AvaloniaPropertyAccessor(resolved, accessor);
        return accessor;
    }

    /// <summary>
    /// Every spelling of one property — <c>"Opacity"</c>, <c>"Visual.Opacity"</c>, <see cref="Visual.OpacityProperty"/> — resolves
    /// to the same <see cref="AvaloniaProperty"/> and so shares its encoded name, which is what tweens are keyed by.
    /// </summary>
    public string? Canonicalize(object target, string property)
    {
        if (target is not AvaloniaObject) return null;
        if (target is Visual && (TransformShorthands.Parse(property) != null || EffectShorthands.Parse(property) != null || EffectShorthands.ParseClip(property) != null || EffectShorthands.ParseMask(property) != null || (target is TextBlock && WarpComposer.Parse(property) != null) || (target is Shape && property == "Draw") || (target is ScrollViewer && property is "ScrollX" or "ScrollY"))) return property;
        return ResolveProperty(target.GetType(), property) is { } resolved ? Encode(resolved) : null;
    }

    /// <summary>Cancels the compositor animations of <paramref name="targets"/>, which no tween removal reaches.</summary>
    public void Removed(IReadOnlyList<object> targets, string? property)
    {
        CompositorTrackKind? kind = null;
        if (property != null)
        {
            if (CompositorProperty.Find(property) is not { } resolved) return;
            kind = resolved.Kind;
        }

        for (var i = 0; i < targets.Count; i++)
        {
            if (targets[i] is Visual visual) CompositorRegistry.Release(visual, kind);
        }
    }

    internal static AvaloniaProperty? ResolveProperty(Type targetType, string name)
    {
        if (s_resolved.TryGetValue((targetType, name), out var cached)) return cached;
        var resolved = Find(targetType, name);
        if (resolved != null) s_resolved[(targetType, name)] = resolved;
        return resolved;
    }

    private static AvaloniaProperty? Find(Type targetType, string name)
    {
        var dot = name.IndexOf('.');
        // FindRegistered throws on a qualified name, and attached properties are only listed separately.
        if (dot < 0) return AvaloniaPropertyRegistry.Instance.FindRegistered(targetType, name);

        var owner = name[..dot];
        var property = name[(dot + 1)..];
        return Match(AvaloniaPropertyRegistry.Instance.GetRegisteredAttached(targetType), owner, property)
            ?? Match(AvaloniaPropertyRegistry.Instance.GetRegistered(targetType), owner, property);
    }

    private static AvaloniaProperty? Match(IReadOnlyList<AvaloniaProperty> properties, string owner, string name)
    {
        for (var i = 0; i < properties.Count; i++)
        {
            if (properties[i].Name == name && properties[i].OwnerType.Name == owner) return properties[i];
        }
        return null;
    }

    private sealed class TransformAccessor(TransformShorthand shorthand) : IPropertyAccessor
    {
        private readonly bool _isAngle = TransformShorthands.IsAngle(shorthand);

        public IValueCodec? Codec => null;

        public object? GetValue(object target)
        {
            var value = RenderTransformComposer.Get((Visual)target, shorthand);
            // Read back with the unit so a "1turn" end value converts instead of being taken as 360 turns.
            return _isAngle ? JsNumber.Format(value) + "deg" : value;
        }

        public void SetValue(object target, double number, Tween tween) =>
            RenderTransformComposer.Set((Visual)target, shorthand, _isAngle ? TransformShorthands.ToDegrees(number, tween._unit) : number);

        public void Revert(object target, object? originalValue) => RenderTransformComposer.Revert((Visual)target, shorthand);
    }

    private sealed class EffectAccessor(EffectShorthand shorthand) : IPropertyAccessor
    {
        public IValueCodec? Codec { get; } = shorthand == EffectShorthand.ShadowColor ? Presto.Codecs[typeof(Color)] : null;

        public object? GetValue(object target) => EffectComposer.Get((Visual)target, shorthand);

        public void SetValue(object target, double number, Tween tween)
        {
            // Rebuilt here rather than through the codec, whose Encode boxes the color.
            if (shorthand == EffectShorthand.ShadowColor) EffectComposer.SetColor((Visual)target, PrestoColor.FromChannels(tween._numbers).ToAvalonia());
            else EffectComposer.Set((Visual)target, shorthand, number, tween._currentTime >= tween._changeDuration);
        }

        public void Revert(object target, object? originalValue) => EffectComposer.Revert((Visual)target, shorthand);
    }

    private sealed class ClipAccessor(ClipShorthand shorthand) : IPropertyAccessor
    {
        public IValueCodec? Codec { get; } = shorthand switch
        {
            ClipShorthand.ClipInset => new ClipInsetCodec(),
            ClipShorthand.ClipCircleCenter => Presto.Codecs[typeof(RelativePoint)],
            _ => null,
        };

        public object? GetValue(object target) => ClipComposer.Get((Visual)target, shorthand);

        public void SetValue(object target, double number, Tween tween)
        {
            var visual = (Visual)target;
            var ended = tween._currentTime >= tween._changeDuration;
            switch (shorthand)
            {
                case ClipShorthand.ClipInset:
                    ClipComposer.SetInset(visual, tween._numbers, tween._strings, ended);
                    break;
                case ClipShorthand.ClipCircle:
                    ClipComposer.SetRadius(visual, number, ended);
                    break;
                default:
                    ClipComposer.SetCenter(visual, RelativePointCodec.Compose(tween));
                    break;
            }
        }

        public void Revert(object target, object? originalValue) => ClipComposer.Revert((Visual)target, shorthand);
    }

    private sealed class MaskAccessor(MaskShorthand shorthand) : IPropertyAccessor
    {
        public IValueCodec? Codec => null;

        public object? GetValue(object target) => MaskComposer.Get((Visual)target, shorthand);

        public void SetValue(object target, double number, Tween tween) =>
            MaskComposer.Set((Visual)target, shorthand, shorthand == MaskShorthand.MaskSweepAngle ? TransformShorthands.ToDegrees(number, tween._unit) : number,
                tween._currentTime >= tween._changeDuration);

        public void Revert(object target, object? originalValue) => MaskComposer.Revert((Visual)target, shorthand);
    }

    private sealed class WarpAccessor(WarpShorthand shorthand) : IPropertyAccessor
    {
        public IValueCodec? Codec { get; } = shorthand == WarpShorthand.WarpOrigin ? Presto.Codecs[typeof(Point)] : null;

        public object? GetValue(object target) => WarpComposer.Get((TextBlock)target, shorthand);

        public void SetValue(object target, double number, Tween tween)
        {
            // Read from the channels rather than through the codec, whose Encode boxes the point.
            if (shorthand == WarpShorthand.WarpOrigin) WarpComposer.SetOrigin((TextBlock)target, new Point(tween._numbers[0], tween._numbers[1]));
            else WarpComposer.Set((TextBlock)target, shorthand, number);
        }

        public void Revert(object target, object? originalValue) => WarpComposer.Revert((TextBlock)target, shorthand);
    }

    private sealed class AvaloniaPropertyAccessor : IPropertyAccessor
    {
        private readonly AvaloniaProperty _property;
        private readonly Func<double, object>? _number;
        private readonly bool _isDouble;
        private readonly bool _isOpacity;
        private readonly bool _isSize;
        private readonly bool _isString;
        private readonly ConditionalWeakTable<AvaloniaObject, IDisposable> _animated;
        private readonly ConditionalWeakTable<AvaloniaObject, StrongBox<object?>>? _original;

        // A replacement for a stale codec shares its predecessor's bindings, so a target never holds two for one property.
        public AvaloniaPropertyAccessor(AvaloniaProperty property, AvaloniaPropertyAccessor? previous)
        {
            _property = property;
            _animated = previous?._animated ?? new();
            // A direct property's write returns no disposable, so its pre-animation value has to be kept for Revert.
            if (property.IsDirect) _original = previous?._original ?? new ConditionalWeakTable<AvaloniaObject, StrongBox<object?>>();
            _isOpacity = property == Visual.OpacityProperty;
            _isSize = property == Layoutable.WidthProperty || property == Layoutable.HeightProperty;
            var type = property.PropertyType;
            if (type == typeof(double)) _isDouble = true;
            else if (type == typeof(float)) _number = static n => (float)n;
            else if (type == typeof(int)) _number = static n => (int)Helpers.Round(n, 0);
            else if (type == typeof(string)) _isString = true;
            else if (Presto.Codecs.TryGetValue(type, out var codec)) Codec = codec;
            else Codec = new DiscreteCodec(type, property == Visual.IsVisibleProperty ? DiscreteRule.TrueThroughout : DiscreteRule.Midpoint);
        }

        public IValueCodec? Codec { get; }

        public bool CodecIsCurrent =>
            Codec == null || (Presto.Codecs.TryGetValue(_property.PropertyType, out var registered) ? ReferenceEquals(registered, Codec) : Codec is DiscreteCodec);

        public object? GetValue(object target) => ((AvaloniaObject)target).GetValue(_property);

        public void SetValue(object target, double number, Tween tween)
        {
            var animated = (AvaloniaObject)target;
            if (_original != null && !_original.TryGetValue(animated, out _)) _original.Add(animated, new StrongBox<object?>(animated.GetValue(_property)));
            if (_isDouble) Animated<double>(animated).Push(_isOpacity ? Math.Clamp(number, 0, 1) : _isSize ? Snapped(animated, number, tween) : number);
            else Animated<object?>(animated).Push(_isString ? tween.ComposeString(number) : _number != null ? _number(number) : Codec!.Encode(number, tween));
        }

        private static double Snapped(AvaloniaObject target, double number, Tween tween)
        {
            if (target is not Layoutable { UseLayoutRounding: true } layoutable || tween._currentTime >= tween._changeDuration) return number;
            var scale = LayoutHelper.GetLayoutScale(layoutable);
            return tween._fromNumber + Helpers.Round((number - tween._fromNumber) * scale / 4, 0) * 4 / scale;
        }

        private AnimatedValue<T> Animated<T>(AvaloniaObject target)
        {
            if (!_animated.TryGetValue(target, out var source)) _animated.Add(target, source = new AnimatedValue<T>(target, _property));
            return (AnimatedValue<T>)source;
        }

        public void Revert(object target, object? originalValue)
        {
            var animated = (AvaloniaObject)target;
            if (_animated.TryGetValue(animated, out var source))
            {
                _animated.Remove(animated);
                source.Dispose();
            }
            if (_original == null || !_original.TryGetValue(animated, out var original)) return;
            _original.Remove(animated);
            animated.SetValue(_property, original.Value);
        }
    }
}
