using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Data;
using Monaco.Presto.AvaloniaBridge.Transforms;
using Monaco.Presto.Core;
using Monaco.Presto.Targets;
using Monaco.Presto.Values;

namespace Monaco.Presto.AvaloniaBridge.Targets;

/// <summary>
/// Resolves the properties of an <see cref="AvaloniaObject"/>: the transform shorthands ("X", "Rotate", "ScaleX"), plain
/// names ("Opacity", "Margin"), qualified or attached names ("Canvas.Left"), and the typed <see cref="AvaloniaProperty"/>
/// instances <see cref="AvaloniaAnimationParams"/> encodes. Any other target is left to the next adapter.
/// </summary>
/// <remarks>
/// Every frame is written at <see cref="BindingPriority.Animation"/> and the entry that write returns is kept per target, so
/// the next frame replaces it instead of stacking a new one and a revert removes it, uncovering whatever value the property
/// had underneath. A direct property has no such entry, so the value it held before the first frame is captured and written
/// back on revert instead.
/// </remarks>
public sealed class AvaloniaObjectTargetAdapter : ITargetAdapter
{
    private static readonly Dictionary<(Type Type, string Name), AvaloniaProperty> s_resolved = [];
    private static readonly Dictionary<AvaloniaProperty, IPropertyAccessor> s_accessors = [];
    private static readonly IPropertyAccessor?[] s_transforms = new IPropertyAccessor?[Enum.GetValues<TransformShorthand>().Length];

    internal static string Encode(AvaloniaProperty property) => $"{property.OwnerType.Name}.{property.Name}";

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
        private readonly bool _isAngle = shorthand is TransformShorthand.Rotate or TransformShorthand.SkewX or TransformShorthand.SkewY;
        private readonly DecomposedValue _angle = new();

        public IValueCodec? Codec => null;

        public object? GetValue(object target)
        {
            var value = RenderTransformComposer.Get((Visual)target, shorthand);
            // Read back with the unit so a "1turn" end value converts instead of being taken as 360 turns.
            return _isAngle ? JsNumber.Format(value) + "deg" : value;
        }

        public void SetValue(object target, double number, Tween tween) =>
            RenderTransformComposer.Set((Visual)target, shorthand, _isAngle ? ToDegrees(number, tween._unit) : number);

        public void Revert(object target, object? originalValue) => RenderTransformComposer.Revert((Visual)target, shorthand);

        private double ToDegrees(double number, string? unit)
        {
            if (unit is null or "deg") return number;
            _angle.Kind = ValueKind.Unit;
            _angle.Number = number;
            _angle.Unit = unit;
            UnitConverter.Convert(_angle, "deg");
            return _angle.Number;
        }
    }

    private sealed class AvaloniaPropertyAccessor : IPropertyAccessor
    {
        private readonly AvaloniaProperty _property;
        private readonly Func<double, object>? _number;
        private readonly ConditionalWeakTable<AvaloniaObject, IDisposable> _written = new();
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
            else if (Presto.Codecs.TryGetValue(type, out var codec)) Codec = codec;
            else throw new NotSupportedException($"{property.OwnerType}.{property.Name} is a {type}, which has no codec registered in Presto.Codecs.");
        }

        public IValueCodec? Codec { get; }

        public object? GetValue(object target) => ((AvaloniaObject)target).GetValue(_property);

        public void SetValue(object target, double number, Tween tween)
        {
            var animated = (AvaloniaObject)target;
            _written.TryGetValue(animated, out var previous);
            if (_original != null && !_original.TryGetValue(animated, out _)) _original.Add(animated, new StrongBox<object?>(animated.GetValue(_property)));
            var value = _number != null ? _number(number) : Codec!.Encode(number, tween);
            if (animated.SetValue(_property, value, BindingPriority.Animation) is { } written) _written.AddOrUpdate(animated, written);
            // Disposed only once the new value is in place, so the property never drops back to its base value in between.
            previous?.Dispose();
        }

        public void Revert(object target, object? originalValue)
        {
            var animated = (AvaloniaObject)target;
            if (_original != null)
            {
                if (!_original.TryGetValue(animated, out var original)) return;
                _original.Remove(animated);
                animated.SetValue(_property, original.Value);
                return;
            }
            if (!_written.TryGetValue(animated, out var written)) return;
            _written.Remove(animated);
            written.Dispose();
        }
    }
}
