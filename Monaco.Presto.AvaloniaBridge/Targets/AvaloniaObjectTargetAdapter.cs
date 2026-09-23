using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls.Shapes;
using Avalonia.Data;
using Monaco.Presto.AvaloniaBridge.Compositor;
using Monaco.Presto.AvaloniaBridge.Shapes;
using Monaco.Presto.AvaloniaBridge.Transforms;
using Monaco.Presto.Core;
using Monaco.Presto.Targets;
using Monaco.Presto.Values;

namespace Monaco.Presto.AvaloniaBridge.Targets;

/// <summary>
/// Resolves the properties of an <see cref="AvaloniaObject"/>: the transform shorthands ("X", "Rotate", "ScaleX"), a
/// <see cref="Shape"/>'s "Draw", plain names ("Opacity", "Margin"), qualified or attached names ("Canvas.Left"), and the typed <see cref="AvaloniaProperty"/>
/// instances <see cref="AvaloniaAnimationParams"/> encodes. Any other target is left to the next adapter.
/// </summary>
/// <remarks>
/// A target's property is bound once at <see cref="BindingPriority.Animation"/> and every frame is pushed through that one
/// binding, so a frame costs no value entry of its own and a revert disposes the binding, uncovering whatever value the
/// property had underneath. A direct property binding ignores the priority and writes through the setter, so the value the
/// property held before the first frame is captured and written back on revert instead.
/// </remarks>
public sealed class AvaloniaObjectTargetAdapter : ITargetAdapter
{
    private static readonly Dictionary<(Type Type, string Name), AvaloniaProperty> s_resolved = [];
    private static readonly Dictionary<AvaloniaProperty, IPropertyAccessor> s_accessors = [];
    private static readonly IPropertyAccessor?[] s_transforms = new IPropertyAccessor?[Enum.GetValues<TransformShorthand>().Length];
    private static IPropertyAccessor? s_draw;

    internal static string Encode(AvaloniaProperty property) => $"{property.OwnerType.Name}.{property.Name}";

    /// <summary>
    /// A shorthand moves the visual in device independent pixels and turns it in degrees, unless the value carries a unit of
    /// its own (<c>"1turn"</c>, <c>".5rad"</c>); every other name is looked up in the <see cref="AvaloniaPropertyRegistry"/>.
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="target"/>'s type has no Avalonia property registered under <paramref name="property"/>.</exception>
    /// <exception cref="NotSupportedException">That property's type has no codec registered in <see cref="Presto.Codecs"/>.</exception>
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
            if (visual is Shape && property == "Draw") return s_draw ??= new DrawAccessor();
        }

        var type = target.GetType();
        if (ResolveProperty(type, property) is not { } resolved)
        {
            throw new ArgumentException($"{type} has no registered Avalonia property named '{property}'.", nameof(property));
        }

        if (!s_accessors.TryGetValue(resolved, out var accessor))
        {
            s_accessors[resolved] = accessor = new AvaloniaPropertyAccessor(resolved);
        }
        return accessor;
    }

    /// <summary>
    /// Every spelling of one property — <c>"Opacity"</c>, <c>"Visual.Opacity"</c>, <see cref="Visual.OpacityProperty"/> — resolves
    /// to the same <see cref="AvaloniaProperty"/> and so shares its encoded name, which is what tweens are keyed by.
    /// </summary>
    public string? Canonicalise(object target, string property)
    {
        if (target is not AvaloniaObject) return null;
        if (target is Visual && (TransformShorthands.Parse(property) != null || (target is Shape && property == "Draw"))) return property;
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

    private static AvaloniaProperty? ResolveProperty(Type targetType, string name)
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

    private sealed class AvaloniaPropertyAccessor : IPropertyAccessor
    {
        private readonly AvaloniaProperty _property;
        private readonly Func<double, object>? _number;
        private readonly bool _isString;
        private readonly ConditionalWeakTable<AvaloniaObject, AnimatedValue> _animated = new();
        private readonly ConditionalWeakTable<AvaloniaObject, StrongBox<object?>>? _original;

        public AvaloniaPropertyAccessor(AvaloniaProperty property)
        {
            _property = property;
            // A direct property's write returns no disposable, so its pre-animation value has to be kept for Revert.
            if (property.IsDirect) _original = new ConditionalWeakTable<AvaloniaObject, StrongBox<object?>>();
            var type = property.PropertyType;
            if (type == typeof(double)) _number = static n => n;
            else if (type == typeof(float)) _number = static n => (float)n;
            else if (type == typeof(int)) _number = static n => (int)Helpers.Round(n, 0);
            else if (type == typeof(string)) _isString = true;
            else if (Presto.Codecs.TryGetValue(type, out var codec)) Codec = codec;
            else throw new NotSupportedException($"{property.OwnerType}.{property.Name} is a {type}, which has no codec registered in Presto.Codecs.");
        }

        public IValueCodec? Codec { get; }

        public object? GetValue(object target) => ((AvaloniaObject)target).GetValue(_property);

        public void SetValue(object target, double number, Tween tween)
        {
            var animated = (AvaloniaObject)target;
            if (_original != null && !_original.TryGetValue(animated, out _)) _original.Add(animated, new StrongBox<object?>(animated.GetValue(_property)));
            var value = _isString ? tween.ComposeString(number) : _number != null ? _number(number) : Codec!.Encode(number, tween);
            if (!_animated.TryGetValue(animated, out var source)) _animated.Add(animated, source = new AnimatedValue(animated, _property));
            source.Push(value);
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
