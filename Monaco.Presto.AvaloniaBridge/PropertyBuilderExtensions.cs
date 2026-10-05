using System.Globalization;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Monaco.Presto.AvaloniaBridge.Targets;

namespace Monaco.Presto.AvaloniaBridge;

/// <summary>
/// Typed names for the <see cref="PropertyBuilder{TSelf}"/> properties: the transform, effect and reveal shorthands, the <see cref="PrestoImageBase"/> colour filters, the Avalonia properties
/// animated most often, the Draw/Text/Data pseudo-properties and any <see cref="AvaloniaProperty"/>. Each writes the
/// key the <see cref="AvaloniaAnimationParams"/> indexers write, and installs the bridge first when nothing else has.
/// </summary>
public static class PropertyBuilderExtensions
{
    /// <summary>Animates the <c>"X"</c> translation to <paramref name="to"/>.</summary>
    public static T X<T>(this T builder, TweenValue to) where T : PropertyBuilder<T> => Init(builder).Property("X", to);

    /// <summary>Animates the <c>"X"</c> translation with its own tween settings.</summary>
    public static T X<T>(this T builder, TweenValue? to = null, TweenValue? from = null, TweenValue? duration = null,
        TweenValue? delay = null, EaseValue? ease = null, Composition? composition = null,
        Func<double, double>? modifier = null) where T : PropertyBuilder<T> =>
        Init(builder).Property("X", to, from, duration, delay, ease, composition, modifier);

    /// <summary>Animates the <c>"X"</c> translation to a value computed per target.</summary>
    public static T X<T>(this T builder, FunctionValue to) where T : PropertyBuilder<T> => Init(builder).Property("X", to);

    /// <summary>Animates the <c>"X"</c> translation through the keyframes <paramref name="keyframes"/> adds.</summary>
    public static T X<T>(this T builder, Action<TweenKeyframesBuilder> keyframes) where T : PropertyBuilder<T> => Init(builder).Property("X", keyframes);

    /// <summary>Animates the <c>"Y"</c> translation to <paramref name="to"/>.</summary>
    public static T Y<T>(this T builder, TweenValue to) where T : PropertyBuilder<T> => Init(builder).Property("Y", to);

    /// <summary>Animates the <c>"Y"</c> translation with its own tween settings.</summary>
    public static T Y<T>(this T builder, TweenValue? to = null, TweenValue? from = null, TweenValue? duration = null,
        TweenValue? delay = null, EaseValue? ease = null, Composition? composition = null,
        Func<double, double>? modifier = null) where T : PropertyBuilder<T> =>
        Init(builder).Property("Y", to, from, duration, delay, ease, composition, modifier);

    /// <summary>Animates the <c>"Y"</c> translation to a value computed per target.</summary>
    public static T Y<T>(this T builder, FunctionValue to) where T : PropertyBuilder<T> => Init(builder).Property("Y", to);

    /// <summary>Animates the <c>"Y"</c> translation through the keyframes <paramref name="keyframes"/> adds.</summary>
    public static T Y<T>(this T builder, Action<TweenKeyframesBuilder> keyframes) where T : PropertyBuilder<T> => Init(builder).Property("Y", keyframes);

    /// <summary>Animates the <c>"Rotate"</c> angle to <paramref name="to"/>.</summary>
    public static T Rotate<T>(this T builder, TweenValue to) where T : PropertyBuilder<T> => Init(builder).Property("Rotate", to);

    /// <summary>Animates the <c>"Rotate"</c> angle with its own tween settings.</summary>
    public static T Rotate<T>(this T builder, TweenValue? to = null, TweenValue? from = null, TweenValue? duration = null,
        TweenValue? delay = null, EaseValue? ease = null, Composition? composition = null,
        Func<double, double>? modifier = null) where T : PropertyBuilder<T> =>
        Init(builder).Property("Rotate", to, from, duration, delay, ease, composition, modifier);

    /// <summary>Animates the <c>"Rotate"</c> angle to a value computed per target.</summary>
    public static T Rotate<T>(this T builder, FunctionValue to) where T : PropertyBuilder<T> => Init(builder).Property("Rotate", to);

    /// <summary>Animates the <c>"Rotate"</c> angle through the keyframes <paramref name="keyframes"/> adds.</summary>
    public static T Rotate<T>(this T builder, Action<TweenKeyframesBuilder> keyframes) where T : PropertyBuilder<T> => Init(builder).Property("Rotate", keyframes);
    /// <summary>Animates the <c>"RotateX"</c> angle to <paramref name="to"/>.</summary>
    public static T RotateX<T>(this T builder, TweenValue to) where T : PropertyBuilder<T> => Init(builder).Property("RotateX", to);

    /// <summary>Animates the <c>"RotateX"</c> angle with its own tween settings.</summary>
    public static T RotateX<T>(this T builder, TweenValue? to = null, TweenValue? from = null, TweenValue? duration = null,
        TweenValue? delay = null, EaseValue? ease = null, Composition? composition = null,
        Func<double, double>? modifier = null) where T : PropertyBuilder<T> =>
        Init(builder).Property("RotateX", to, from, duration, delay, ease, composition, modifier);

    /// <summary>Animates the <c>"RotateX"</c> angle to a value computed per target.</summary>
    public static T RotateX<T>(this T builder, FunctionValue to) where T : PropertyBuilder<T> => Init(builder).Property("RotateX", to);

    /// <summary>Animates the <c>"RotateX"</c> angle through the keyframes <paramref name="keyframes"/> adds.</summary>
    public static T RotateX<T>(this T builder, Action<TweenKeyframesBuilder> keyframes) where T : PropertyBuilder<T> => Init(builder).Property("RotateX", keyframes);
    /// <summary>Animates the <c>"RotateY"</c> angle to <paramref name="to"/>.</summary>
    public static T RotateY<T>(this T builder, TweenValue to) where T : PropertyBuilder<T> => Init(builder).Property("RotateY", to);

    /// <summary>Animates the <c>"RotateY"</c> angle with its own tween settings.</summary>
    public static T RotateY<T>(this T builder, TweenValue? to = null, TweenValue? from = null, TweenValue? duration = null,
        TweenValue? delay = null, EaseValue? ease = null, Composition? composition = null,
        Func<double, double>? modifier = null) where T : PropertyBuilder<T> =>
        Init(builder).Property("RotateY", to, from, duration, delay, ease, composition, modifier);

    /// <summary>Animates the <c>"RotateY"</c> angle to a value computed per target.</summary>
    public static T RotateY<T>(this T builder, FunctionValue to) where T : PropertyBuilder<T> => Init(builder).Property("RotateY", to);

    /// <summary>Animates the <c>"RotateY"</c> angle through the keyframes <paramref name="keyframes"/> adds.</summary>
    public static T RotateY<T>(this T builder, Action<TweenKeyframesBuilder> keyframes) where T : PropertyBuilder<T> => Init(builder).Property("RotateY", keyframes);
    /// <summary>Animates the <c>"Perspective"</c> depth to <paramref name="to"/>.</summary>
    public static T Perspective<T>(this T builder, TweenValue to) where T : PropertyBuilder<T> => Init(builder).Property("Perspective", to);

    /// <summary>Animates the <c>"Perspective"</c> depth with its own tween settings.</summary>
    public static T Perspective<T>(this T builder, TweenValue? to = null, TweenValue? from = null, TweenValue? duration = null,
        TweenValue? delay = null, EaseValue? ease = null, Composition? composition = null,
        Func<double, double>? modifier = null) where T : PropertyBuilder<T> =>
        Init(builder).Property("Perspective", to, from, duration, delay, ease, composition, modifier);

    /// <summary>Animates the <c>"Perspective"</c> depth to a value computed per target.</summary>
    public static T Perspective<T>(this T builder, FunctionValue to) where T : PropertyBuilder<T> => Init(builder).Property("Perspective", to);

    /// <summary>Animates the <c>"Perspective"</c> depth through the keyframes <paramref name="keyframes"/> adds.</summary>
    public static T Perspective<T>(this T builder, Action<TweenKeyframesBuilder> keyframes) where T : PropertyBuilder<T> => Init(builder).Property("Perspective", keyframes);

    /// <summary>Animates the uniform <c>"Scale"</c> to <paramref name="to"/>.</summary>
    public static T Scale<T>(this T builder, TweenValue to) where T : PropertyBuilder<T> => Init(builder).Property("Scale", to);

    /// <summary>Animates the uniform <c>"Scale"</c> with its own tween settings.</summary>
    public static T Scale<T>(this T builder, TweenValue? to = null, TweenValue? from = null, TweenValue? duration = null,
        TweenValue? delay = null, EaseValue? ease = null, Composition? composition = null,
        Func<double, double>? modifier = null) where T : PropertyBuilder<T> =>
        Init(builder).Property("Scale", to, from, duration, delay, ease, composition, modifier);

    /// <summary>Animates the uniform <c>"Scale"</c> to a value computed per target.</summary>
    public static T Scale<T>(this T builder, FunctionValue to) where T : PropertyBuilder<T> => Init(builder).Property("Scale", to);

    /// <summary>Animates the uniform <c>"Scale"</c> through the keyframes <paramref name="keyframes"/> adds.</summary>
    public static T Scale<T>(this T builder, Action<TweenKeyframesBuilder> keyframes) where T : PropertyBuilder<T> => Init(builder).Property("Scale", keyframes);

    /// <summary>Animates the <c>"ScaleX"</c> scale to <paramref name="to"/>.</summary>
    public static T ScaleX<T>(this T builder, TweenValue to) where T : PropertyBuilder<T> => Init(builder).Property("ScaleX", to);

    /// <summary>Animates the <c>"ScaleX"</c> scale with its own tween settings.</summary>
    public static T ScaleX<T>(this T builder, TweenValue? to = null, TweenValue? from = null, TweenValue? duration = null,
        TweenValue? delay = null, EaseValue? ease = null, Composition? composition = null,
        Func<double, double>? modifier = null) where T : PropertyBuilder<T> =>
        Init(builder).Property("ScaleX", to, from, duration, delay, ease, composition, modifier);

    /// <summary>Animates the <c>"ScaleX"</c> scale to a value computed per target.</summary>
    public static T ScaleX<T>(this T builder, FunctionValue to) where T : PropertyBuilder<T> => Init(builder).Property("ScaleX", to);

    /// <summary>Animates the <c>"ScaleX"</c> scale through the keyframes <paramref name="keyframes"/> adds.</summary>
    public static T ScaleX<T>(this T builder, Action<TweenKeyframesBuilder> keyframes) where T : PropertyBuilder<T> => Init(builder).Property("ScaleX", keyframes);

    /// <summary>Animates the <c>"ScaleY"</c> scale to <paramref name="to"/>.</summary>
    public static T ScaleY<T>(this T builder, TweenValue to) where T : PropertyBuilder<T> => Init(builder).Property("ScaleY", to);

    /// <summary>Animates the <c>"ScaleY"</c> scale with its own tween settings.</summary>
    public static T ScaleY<T>(this T builder, TweenValue? to = null, TweenValue? from = null, TweenValue? duration = null,
        TweenValue? delay = null, EaseValue? ease = null, Composition? composition = null,
        Func<double, double>? modifier = null) where T : PropertyBuilder<T> =>
        Init(builder).Property("ScaleY", to, from, duration, delay, ease, composition, modifier);

    /// <summary>Animates the <c>"ScaleY"</c> scale to a value computed per target.</summary>
    public static T ScaleY<T>(this T builder, FunctionValue to) where T : PropertyBuilder<T> => Init(builder).Property("ScaleY", to);

    /// <summary>Animates the <c>"ScaleY"</c> scale through the keyframes <paramref name="keyframes"/> adds.</summary>
    public static T ScaleY<T>(this T builder, Action<TweenKeyframesBuilder> keyframes) where T : PropertyBuilder<T> => Init(builder).Property("ScaleY", keyframes);

    /// <summary>Animates the <c>"SkewX"</c> angle to <paramref name="to"/>.</summary>
    public static T SkewX<T>(this T builder, TweenValue to) where T : PropertyBuilder<T> => Init(builder).Property("SkewX", to);

    /// <summary>Animates the <c>"SkewX"</c> angle with its own tween settings.</summary>
    public static T SkewX<T>(this T builder, TweenValue? to = null, TweenValue? from = null, TweenValue? duration = null,
        TweenValue? delay = null, EaseValue? ease = null, Composition? composition = null,
        Func<double, double>? modifier = null) where T : PropertyBuilder<T> =>
        Init(builder).Property("SkewX", to, from, duration, delay, ease, composition, modifier);

    /// <summary>Animates the <c>"SkewX"</c> angle to a value computed per target.</summary>
    public static T SkewX<T>(this T builder, FunctionValue to) where T : PropertyBuilder<T> => Init(builder).Property("SkewX", to);

    /// <summary>Animates the <c>"SkewX"</c> angle through the keyframes <paramref name="keyframes"/> adds.</summary>
    public static T SkewX<T>(this T builder, Action<TweenKeyframesBuilder> keyframes) where T : PropertyBuilder<T> => Init(builder).Property("SkewX", keyframes);

    /// <summary>Animates the <c>"SkewY"</c> angle to <paramref name="to"/>.</summary>
    public static T SkewY<T>(this T builder, TweenValue to) where T : PropertyBuilder<T> => Init(builder).Property("SkewY", to);

    /// <summary>Animates the <c>"SkewY"</c> angle with its own tween settings.</summary>
    public static T SkewY<T>(this T builder, TweenValue? to = null, TweenValue? from = null, TweenValue? duration = null,
        TweenValue? delay = null, EaseValue? ease = null, Composition? composition = null,
        Func<double, double>? modifier = null) where T : PropertyBuilder<T> =>
        Init(builder).Property("SkewY", to, from, duration, delay, ease, composition, modifier);

    /// <summary>Animates the <c>"SkewY"</c> angle to a value computed per target.</summary>
    public static T SkewY<T>(this T builder, FunctionValue to) where T : PropertyBuilder<T> => Init(builder).Property("SkewY", to);

    /// <summary>Animates the <c>"SkewY"</c> angle through the keyframes <paramref name="keyframes"/> adds.</summary>
    public static T SkewY<T>(this T builder, Action<TweenKeyframesBuilder> keyframes) where T : PropertyBuilder<T> => Init(builder).Property("SkewY", keyframes);

    /// <summary>Animates <c>"Opacity"</c> to <paramref name="to"/>.</summary>
    public static T Opacity<T>(this T builder, TweenValue to) where T : PropertyBuilder<T> => Init(builder).Property("Opacity", to);

    /// <summary>Animates <c>"Opacity"</c> with its own tween settings.</summary>
    public static T Opacity<T>(this T builder, TweenValue? to = null, TweenValue? from = null, TweenValue? duration = null,
        TweenValue? delay = null, EaseValue? ease = null, Composition? composition = null,
        Func<double, double>? modifier = null) where T : PropertyBuilder<T> =>
        Init(builder).Property("Opacity", to, from, duration, delay, ease, composition, modifier);

    /// <summary>Animates <c>"Opacity"</c> to a value computed per target.</summary>
    public static T Opacity<T>(this T builder, FunctionValue to) where T : PropertyBuilder<T> => Init(builder).Property("Opacity", to);

    /// <summary>Animates <c>"Opacity"</c> through the keyframes <paramref name="keyframes"/> adds.</summary>
    public static T Opacity<T>(this T builder, Action<TweenKeyframesBuilder> keyframes) where T : PropertyBuilder<T> => Init(builder).Property("Opacity", keyframes);

    /// <summary>Animates <c>"Width"</c> to <paramref name="to"/>.</summary>
    public static T Width<T>(this T builder, TweenValue to) where T : PropertyBuilder<T> => Init(builder).Property("Width", to);

    /// <summary>Animates <c>"Width"</c> with its own tween settings.</summary>
    public static T Width<T>(this T builder, TweenValue? to = null, TweenValue? from = null, TweenValue? duration = null,
        TweenValue? delay = null, EaseValue? ease = null, Composition? composition = null,
        Func<double, double>? modifier = null) where T : PropertyBuilder<T> =>
        Init(builder).Property("Width", to, from, duration, delay, ease, composition, modifier);

    /// <summary>Animates <c>"Width"</c> to a value computed per target.</summary>
    public static T Width<T>(this T builder, FunctionValue to) where T : PropertyBuilder<T> => Init(builder).Property("Width", to);

    /// <summary>Animates <c>"Width"</c> through the keyframes <paramref name="keyframes"/> adds.</summary>
    public static T Width<T>(this T builder, Action<TweenKeyframesBuilder> keyframes) where T : PropertyBuilder<T> => Init(builder).Property("Width", keyframes);

    /// <summary>Animates <c>"Height"</c> to <paramref name="to"/>.</summary>
    public static T Height<T>(this T builder, TweenValue to) where T : PropertyBuilder<T> => Init(builder).Property("Height", to);

    /// <summary>Animates <c>"Height"</c> with its own tween settings.</summary>
    public static T Height<T>(this T builder, TweenValue? to = null, TweenValue? from = null, TweenValue? duration = null,
        TweenValue? delay = null, EaseValue? ease = null, Composition? composition = null,
        Func<double, double>? modifier = null) where T : PropertyBuilder<T> =>
        Init(builder).Property("Height", to, from, duration, delay, ease, composition, modifier);

    /// <summary>Animates <c>"Height"</c> to a value computed per target.</summary>
    public static T Height<T>(this T builder, FunctionValue to) where T : PropertyBuilder<T> => Init(builder).Property("Height", to);

    /// <summary>Animates <c>"Height"</c> through the keyframes <paramref name="keyframes"/> adds.</summary>
    public static T Height<T>(this T builder, Action<TweenKeyframesBuilder> keyframes) where T : PropertyBuilder<T> => Init(builder).Property("Height", keyframes);

    /// <summary>Animates <c>"Background"</c> to <paramref name="to"/>.</summary>
    public static T Background<T>(this T builder, TweenValue to) where T : PropertyBuilder<T> => Init(builder).Property("Background", to);

    /// <summary>Animates <c>"Background"</c> with its own tween settings.</summary>
    public static T Background<T>(this T builder, TweenValue? to = null, TweenValue? from = null, TweenValue? duration = null,
        TweenValue? delay = null, EaseValue? ease = null, Composition? composition = null,
        Func<double, double>? modifier = null) where T : PropertyBuilder<T> =>
        Init(builder).Property("Background", to, from, duration, delay, ease, composition, modifier);

    /// <summary>Animates <c>"Background"</c> to a value computed per target.</summary>
    public static T Background<T>(this T builder, FunctionValue to) where T : PropertyBuilder<T> => Init(builder).Property("Background", to);

    /// <summary>Animates <c>"Background"</c> through the keyframes <paramref name="keyframes"/> adds.</summary>
    public static T Background<T>(this T builder, Action<TweenKeyframesBuilder> keyframes) where T : PropertyBuilder<T> => Init(builder).Property("Background", keyframes);
    /// <summary>Animates <c>"BoxShadow"</c> to <paramref name="to"/>, a <see cref="BoxShadows"/> string such as <c>"0 4 16 0 #40000000"</c>.</summary>
    public static T BoxShadow<T>(this T builder, TweenValue to) where T : PropertyBuilder<T> => Init(builder).Property("BoxShadow", to);

    /// <summary>Animates <c>"BoxShadow"</c> with its own tween settings.</summary>
    public static T BoxShadow<T>(this T builder, TweenValue? to = null, TweenValue? from = null, TweenValue? duration = null,
        TweenValue? delay = null, EaseValue? ease = null, Composition? composition = null,
        Func<double, double>? modifier = null) where T : PropertyBuilder<T> =>
        Init(builder).Property("BoxShadow", to, from, duration, delay, ease, composition, modifier);

    /// <summary>Animates <c>"BoxShadow"</c> to a value computed per target.</summary>
    public static T BoxShadow<T>(this T builder, FunctionValue to) where T : PropertyBuilder<T> => Init(builder).Property("BoxShadow", to);

    /// <summary>Animates <c>"BoxShadow"</c> through the keyframes <paramref name="keyframes"/> adds.</summary>
    public static T BoxShadow<T>(this T builder, Action<TweenKeyframesBuilder> keyframes) where T : PropertyBuilder<T> => Init(builder).Property("BoxShadow", keyframes);

    /// <summary>Animates <c>"Foreground"</c> to <paramref name="to"/>.</summary>
    public static T Foreground<T>(this T builder, TweenValue to) where T : PropertyBuilder<T> => Init(builder).Property("Foreground", to);

    /// <summary>Animates <c>"Foreground"</c> with its own tween settings.</summary>
    public static T Foreground<T>(this T builder, TweenValue? to = null, TweenValue? from = null, TweenValue? duration = null,
        TweenValue? delay = null, EaseValue? ease = null, Composition? composition = null,
        Func<double, double>? modifier = null) where T : PropertyBuilder<T> =>
        Init(builder).Property("Foreground", to, from, duration, delay, ease, composition, modifier);

    /// <summary>Animates <c>"Foreground"</c> to a value computed per target.</summary>
    public static T Foreground<T>(this T builder, FunctionValue to) where T : PropertyBuilder<T> => Init(builder).Property("Foreground", to);

    /// <summary>Animates <c>"Foreground"</c> through the keyframes <paramref name="keyframes"/> adds.</summary>
    public static T Foreground<T>(this T builder, Action<TweenKeyframesBuilder> keyframes) where T : PropertyBuilder<T> => Init(builder).Property("Foreground", keyframes);

    /// <summary>Animates <c>"CornerRadius"</c> to <paramref name="to"/>.</summary>
    public static T CornerRadius<T>(this T builder, TweenValue to) where T : PropertyBuilder<T> => Init(builder).Property("CornerRadius", to);

    /// <summary>Animates <c>"CornerRadius"</c> with its own tween settings.</summary>
    public static T CornerRadius<T>(this T builder, TweenValue? to = null, TweenValue? from = null, TweenValue? duration = null,
        TweenValue? delay = null, EaseValue? ease = null, Composition? composition = null,
        Func<double, double>? modifier = null) where T : PropertyBuilder<T> =>
        Init(builder).Property("CornerRadius", to, from, duration, delay, ease, composition, modifier);

    /// <summary>Animates <c>"CornerRadius"</c> to a value computed per target.</summary>
    public static T CornerRadius<T>(this T builder, FunctionValue to) where T : PropertyBuilder<T> => Init(builder).Property("CornerRadius", to);

    /// <summary>Animates <c>"CornerRadius"</c> through the keyframes <paramref name="keyframes"/> adds.</summary>
    public static T CornerRadius<T>(this T builder, Action<TweenKeyframesBuilder> keyframes) where T : PropertyBuilder<T> => Init(builder).Property("CornerRadius", keyframes);

    /// <summary>Animates <c>"BorderThickness"</c> to <paramref name="to"/>.</summary>
    public static T BorderThickness<T>(this T builder, TweenValue to) where T : PropertyBuilder<T> => Init(builder).Property("BorderThickness", to);

    /// <summary>Animates <c>"BorderThickness"</c> with its own tween settings.</summary>
    public static T BorderThickness<T>(this T builder, TweenValue? to = null, TweenValue? from = null, TweenValue? duration = null,
        TweenValue? delay = null, EaseValue? ease = null, Composition? composition = null,
        Func<double, double>? modifier = null) where T : PropertyBuilder<T> =>
        Init(builder).Property("BorderThickness", to, from, duration, delay, ease, composition, modifier);

    /// <summary>Animates <c>"BorderThickness"</c> to a value computed per target.</summary>
    public static T BorderThickness<T>(this T builder, FunctionValue to) where T : PropertyBuilder<T> => Init(builder).Property("BorderThickness", to);

    /// <summary>Animates <c>"BorderThickness"</c> through the keyframes <paramref name="keyframes"/> adds.</summary>
    public static T BorderThickness<T>(this T builder, Action<TweenKeyframesBuilder> keyframes) where T : PropertyBuilder<T> => Init(builder).Property("BorderThickness", keyframes);

    /// <summary>Animates <c>"Margin"</c> to <paramref name="to"/>.</summary>
    public static T Margin<T>(this T builder, TweenValue to) where T : PropertyBuilder<T> => Init(builder).Property("Margin", to);

    /// <summary>Animates <c>"Margin"</c> with its own tween settings.</summary>
    public static T Margin<T>(this T builder, TweenValue? to = null, TweenValue? from = null, TweenValue? duration = null,
        TweenValue? delay = null, EaseValue? ease = null, Composition? composition = null,
        Func<double, double>? modifier = null) where T : PropertyBuilder<T> =>
        Init(builder).Property("Margin", to, from, duration, delay, ease, composition, modifier);

    /// <summary>Animates <c>"Margin"</c> to a value computed per target.</summary>
    public static T Margin<T>(this T builder, FunctionValue to) where T : PropertyBuilder<T> => Init(builder).Property("Margin", to);

    /// <summary>Animates <c>"Margin"</c> through the keyframes <paramref name="keyframes"/> adds.</summary>
    public static T Margin<T>(this T builder, Action<TweenKeyframesBuilder> keyframes) where T : PropertyBuilder<T> => Init(builder).Property("Margin", keyframes);

    /// <summary>Animates <c>"Padding"</c> to <paramref name="to"/>.</summary>
    public static T Padding<T>(this T builder, TweenValue to) where T : PropertyBuilder<T> => Init(builder).Property("Padding", to);

    /// <summary>Animates <c>"Padding"</c> with its own tween settings.</summary>
    public static T Padding<T>(this T builder, TweenValue? to = null, TweenValue? from = null, TweenValue? duration = null,
        TweenValue? delay = null, EaseValue? ease = null, Composition? composition = null,
        Func<double, double>? modifier = null) where T : PropertyBuilder<T> =>
        Init(builder).Property("Padding", to, from, duration, delay, ease, composition, modifier);

    /// <summary>Animates <c>"Padding"</c> to a value computed per target.</summary>
    public static T Padding<T>(this T builder, FunctionValue to) where T : PropertyBuilder<T> => Init(builder).Property("Padding", to);

    /// <summary>Animates <c>"Padding"</c> through the keyframes <paramref name="keyframes"/> adds.</summary>
    public static T Padding<T>(this T builder, Action<TweenKeyframesBuilder> keyframes) where T : PropertyBuilder<T> => Init(builder).Property("Padding", keyframes);

    /// <summary>Animates the <c>"Draw"</c> slice of a drawable shape to <paramref name="to"/>.</summary>
    public static T Draw<T>(this T builder, TweenValue to) where T : PropertyBuilder<T> => Init(builder).Property("Draw", to);

    /// <summary>Animates the <c>"Draw"</c> slice of a drawable shape with its own tween settings.</summary>
    public static T Draw<T>(this T builder, TweenValue? to = null, TweenValue? from = null, TweenValue? duration = null,
        TweenValue? delay = null, EaseValue? ease = null, Composition? composition = null,
        Func<double, double>? modifier = null) where T : PropertyBuilder<T> =>
        Init(builder).Property("Draw", to, from, duration, delay, ease, composition, modifier);

    /// <summary>Animates the <c>"Draw"</c> slice of a drawable shape to a value computed per target.</summary>
    public static T Draw<T>(this T builder, FunctionValue to) where T : PropertyBuilder<T> => Init(builder).Property("Draw", to);

    /// <summary>Animates the <c>"Draw"</c> slice of a drawable shape through the keyframes <paramref name="keyframes"/> adds.</summary>
    public static T Draw<T>(this T builder, Action<TweenKeyframesBuilder> keyframes) where T : PropertyBuilder<T> => Init(builder).Property("Draw", keyframes);

    /// <summary>Animates <c>"Text"</c> to <paramref name="to"/>.</summary>
    public static T Text<T>(this T builder, TweenValue to) where T : PropertyBuilder<T> => Init(builder).Property("Text", to);

    /// <summary>Animates <c>"Text"</c> with its own tween settings.</summary>
    public static T Text<T>(this T builder, TweenValue? to = null, TweenValue? from = null, TweenValue? duration = null,
        TweenValue? delay = null, EaseValue? ease = null, Composition? composition = null,
        Func<double, double>? modifier = null,
        Func<double, string>? textModifier = null) where T : PropertyBuilder<T> =>
        Init(builder).Property("Text", to, from, duration, delay, ease, composition, modifier, textModifier);

    /// <summary>Animates <c>"Text"</c> to a value computed per target.</summary>
    public static T Text<T>(this T builder, FunctionValue to) where T : PropertyBuilder<T> => Init(builder).Property("Text", to);

    /// <summary>Animates <c>"Text"</c> through the keyframes <paramref name="keyframes"/> adds.</summary>
    public static T Text<T>(this T builder, Action<TweenKeyframesBuilder> keyframes) where T : PropertyBuilder<T> => Init(builder).Property("Text", keyframes);

    /// <summary>Animates a path's <c>"Data"</c> geometry to <paramref name="to"/>.</summary>
    public static T Data<T>(this T builder, TweenValue to) where T : PropertyBuilder<T> => Init(builder).Property("Data", to);

    /// <summary>Animates a path's <c>"Data"</c> geometry with its own tween settings.</summary>
    public static T Data<T>(this T builder, TweenValue? to = null, TweenValue? from = null, TweenValue? duration = null,
        TweenValue? delay = null, EaseValue? ease = null, Composition? composition = null,
        Func<double, double>? modifier = null) where T : PropertyBuilder<T> =>
        Init(builder).Property("Data", to, from, duration, delay, ease, composition, modifier);

    /// <summary>Animates a path's <c>"Data"</c> geometry to a value computed per target.</summary>
    public static T Data<T>(this T builder, FunctionValue to) where T : PropertyBuilder<T> => Init(builder).Property("Data", to);

    /// <summary>Animates a path's <c>"Data"</c> geometry through the keyframes <paramref name="keyframes"/> adds.</summary>
    public static T Data<T>(this T builder, Action<TweenKeyframesBuilder> keyframes) where T : PropertyBuilder<T> => Init(builder).Property("Data", keyframes);
    /// <summary>Animates the <c>"Blur"</c> radius to <paramref name="to"/>.</summary>
    public static T Blur<T>(this T builder, TweenValue to) where T : PropertyBuilder<T> => Init(builder).Property("Blur", to);

    /// <summary>Animates the <c>"Blur"</c> radius with its own tween settings.</summary>
    public static T Blur<T>(this T builder, TweenValue? to = null, TweenValue? from = null, TweenValue? duration = null,
        TweenValue? delay = null, EaseValue? ease = null, Composition? composition = null,
        Func<double, double>? modifier = null) where T : PropertyBuilder<T> =>
        Init(builder).Property("Blur", to, from, duration, delay, ease, composition, modifier);

    /// <summary>Animates the <c>"Blur"</c> radius to a value computed per target.</summary>
    public static T Blur<T>(this T builder, FunctionValue to) where T : PropertyBuilder<T> => Init(builder).Property("Blur", to);

    /// <summary>Animates the <c>"Blur"</c> radius through the keyframes <paramref name="keyframes"/> adds.</summary>
    public static T Blur<T>(this T builder, Action<TweenKeyframesBuilder> keyframes) where T : PropertyBuilder<T> => Init(builder).Property("Blur", keyframes);
    /// <summary>Animates the <c>"ShadowX"</c> drop-shadow offset to <paramref name="to"/>.</summary>
    public static T ShadowX<T>(this T builder, TweenValue to) where T : PropertyBuilder<T> => Init(builder).Property("ShadowX", to);

    /// <summary>Animates the <c>"ShadowX"</c> drop-shadow offset with its own tween settings.</summary>
    public static T ShadowX<T>(this T builder, TweenValue? to = null, TweenValue? from = null, TweenValue? duration = null,
        TweenValue? delay = null, EaseValue? ease = null, Composition? composition = null,
        Func<double, double>? modifier = null) where T : PropertyBuilder<T> =>
        Init(builder).Property("ShadowX", to, from, duration, delay, ease, composition, modifier);

    /// <summary>Animates the <c>"ShadowX"</c> drop-shadow offset to a value computed per target.</summary>
    public static T ShadowX<T>(this T builder, FunctionValue to) where T : PropertyBuilder<T> => Init(builder).Property("ShadowX", to);

    /// <summary>Animates the <c>"ShadowX"</c> drop-shadow offset through the keyframes <paramref name="keyframes"/> adds.</summary>
    public static T ShadowX<T>(this T builder, Action<TweenKeyframesBuilder> keyframes) where T : PropertyBuilder<T> => Init(builder).Property("ShadowX", keyframes);
    /// <summary>Animates the <c>"ShadowY"</c> drop-shadow offset to <paramref name="to"/>.</summary>
    public static T ShadowY<T>(this T builder, TweenValue to) where T : PropertyBuilder<T> => Init(builder).Property("ShadowY", to);

    /// <summary>Animates the <c>"ShadowY"</c> drop-shadow offset with its own tween settings.</summary>
    public static T ShadowY<T>(this T builder, TweenValue? to = null, TweenValue? from = null, TweenValue? duration = null,
        TweenValue? delay = null, EaseValue? ease = null, Composition? composition = null,
        Func<double, double>? modifier = null) where T : PropertyBuilder<T> =>
        Init(builder).Property("ShadowY", to, from, duration, delay, ease, composition, modifier);

    /// <summary>Animates the <c>"ShadowY"</c> drop-shadow offset to a value computed per target.</summary>
    public static T ShadowY<T>(this T builder, FunctionValue to) where T : PropertyBuilder<T> => Init(builder).Property("ShadowY", to);

    /// <summary>Animates the <c>"ShadowY"</c> drop-shadow offset through the keyframes <paramref name="keyframes"/> adds.</summary>
    public static T ShadowY<T>(this T builder, Action<TweenKeyframesBuilder> keyframes) where T : PropertyBuilder<T> => Init(builder).Property("ShadowY", keyframes);
    /// <summary>Animates the <c>"ShadowBlur"</c> drop-shadow radius to <paramref name="to"/>.</summary>
    public static T ShadowBlur<T>(this T builder, TweenValue to) where T : PropertyBuilder<T> => Init(builder).Property("ShadowBlur", to);

    /// <summary>Animates the <c>"ShadowBlur"</c> drop-shadow radius with its own tween settings.</summary>
    public static T ShadowBlur<T>(this T builder, TweenValue? to = null, TweenValue? from = null, TweenValue? duration = null,
        TweenValue? delay = null, EaseValue? ease = null, Composition? composition = null,
        Func<double, double>? modifier = null) where T : PropertyBuilder<T> =>
        Init(builder).Property("ShadowBlur", to, from, duration, delay, ease, composition, modifier);

    /// <summary>Animates the <c>"ShadowBlur"</c> drop-shadow radius to a value computed per target.</summary>
    public static T ShadowBlur<T>(this T builder, FunctionValue to) where T : PropertyBuilder<T> => Init(builder).Property("ShadowBlur", to);

    /// <summary>Animates the <c>"ShadowBlur"</c> drop-shadow radius through the keyframes <paramref name="keyframes"/> adds.</summary>
    public static T ShadowBlur<T>(this T builder, Action<TweenKeyframesBuilder> keyframes) where T : PropertyBuilder<T> => Init(builder).Property("ShadowBlur", keyframes);
    /// <summary>Animates the <c>"ShadowColor"</c> drop-shadow color to <paramref name="to"/>.</summary>
    public static T ShadowColor<T>(this T builder, TweenValue to) where T : PropertyBuilder<T> => Init(builder).Property("ShadowColor", to);

    /// <summary>Animates the <c>"ShadowColor"</c> drop-shadow color with its own tween settings.</summary>
    public static T ShadowColor<T>(this T builder, TweenValue? to = null, TweenValue? from = null, TweenValue? duration = null,
        TweenValue? delay = null, EaseValue? ease = null, Composition? composition = null,
        Func<double, double>? modifier = null) where T : PropertyBuilder<T> =>
        Init(builder).Property("ShadowColor", to, from, duration, delay, ease, composition, modifier);

    /// <summary>Animates the <c>"ShadowColor"</c> drop-shadow color to a value computed per target.</summary>
    public static T ShadowColor<T>(this T builder, FunctionValue to) where T : PropertyBuilder<T> => Init(builder).Property("ShadowColor", to);

    /// <summary>Animates the <c>"ShadowColor"</c> drop-shadow color through the keyframes <paramref name="keyframes"/> adds.</summary>
    public static T ShadowColor<T>(this T builder, Action<TweenKeyframesBuilder> keyframes) where T : PropertyBuilder<T> => Init(builder).Property("ShadowColor", keyframes);
    /// <summary>Animates the <c>"ShadowOpacity"</c> drop-shadow opacity to <paramref name="to"/>.</summary>
    public static T ShadowOpacity<T>(this T builder, TweenValue to) where T : PropertyBuilder<T> => Init(builder).Property("ShadowOpacity", to);

    /// <summary>Animates the <c>"ShadowOpacity"</c> drop-shadow opacity with its own tween settings.</summary>
    public static T ShadowOpacity<T>(this T builder, TweenValue? to = null, TweenValue? from = null, TweenValue? duration = null,
        TweenValue? delay = null, EaseValue? ease = null, Composition? composition = null,
        Func<double, double>? modifier = null) where T : PropertyBuilder<T> =>
        Init(builder).Property("ShadowOpacity", to, from, duration, delay, ease, composition, modifier);

    /// <summary>Animates the <c>"ShadowOpacity"</c> drop-shadow opacity to a value computed per target.</summary>
    public static T ShadowOpacity<T>(this T builder, FunctionValue to) where T : PropertyBuilder<T> => Init(builder).Property("ShadowOpacity", to);

    /// <summary>Animates the <c>"ShadowOpacity"</c> drop-shadow opacity through the keyframes <paramref name="keyframes"/> adds.</summary>
    public static T ShadowOpacity<T>(this T builder, Action<TweenKeyframesBuilder> keyframes) where T : PropertyBuilder<T> => Init(builder).Property("ShadowOpacity", keyframes);
    /// <summary>Animates the <c>"ClipInset"</c> reveal to <paramref name="to"/>.</summary>
    public static T ClipInset<T>(this T builder, TweenValue to) where T : PropertyBuilder<T> => Init(builder).Property("ClipInset", to);

    /// <summary>Animates the <c>"ClipInset"</c> reveal with its own tween settings.</summary>
    public static T ClipInset<T>(this T builder, TweenValue? to = null, TweenValue? from = null, TweenValue? duration = null,
        TweenValue? delay = null, EaseValue? ease = null, Composition? composition = null,
        Func<double, double>? modifier = null) where T : PropertyBuilder<T> =>
        Init(builder).Property("ClipInset", to, from, duration, delay, ease, composition, modifier);

    /// <summary>Animates the <c>"ClipInset"</c> reveal to a value computed per target.</summary>
    public static T ClipInset<T>(this T builder, FunctionValue to) where T : PropertyBuilder<T> => Init(builder).Property("ClipInset", to);

    /// <summary>Animates the <c>"ClipInset"</c> reveal through the keyframes <paramref name="keyframes"/> adds.</summary>
    public static T ClipInset<T>(this T builder, Action<TweenKeyframesBuilder> keyframes) where T : PropertyBuilder<T> => Init(builder).Property("ClipInset", keyframes);
    /// <summary>Animates the <c>"ClipCircle"</c> reveal radius to <paramref name="to"/>.</summary>
    public static T ClipCircle<T>(this T builder, TweenValue to) where T : PropertyBuilder<T> => Init(builder).Property("ClipCircle", to);

    /// <summary>Animates the <c>"ClipCircle"</c> reveal radius with its own tween settings.</summary>
    public static T ClipCircle<T>(this T builder, TweenValue? to = null, TweenValue? from = null, TweenValue? duration = null,
        TweenValue? delay = null, EaseValue? ease = null, Composition? composition = null,
        Func<double, double>? modifier = null) where T : PropertyBuilder<T> =>
        Init(builder).Property("ClipCircle", to, from, duration, delay, ease, composition, modifier);

    /// <summary>Animates the <c>"ClipCircle"</c> reveal radius to a value computed per target.</summary>
    public static T ClipCircle<T>(this T builder, FunctionValue to) where T : PropertyBuilder<T> => Init(builder).Property("ClipCircle", to);

    /// <summary>Animates the <c>"ClipCircle"</c> reveal radius through the keyframes <paramref name="keyframes"/> adds.</summary>
    public static T ClipCircle<T>(this T builder, Action<TweenKeyframesBuilder> keyframes) where T : PropertyBuilder<T> => Init(builder).Property("ClipCircle", keyframes);
    /// <summary>Animates the <c>"ClipCircleCenter"</c> reveal center to <paramref name="to"/>.</summary>
    public static T ClipCircleCenter<T>(this T builder, TweenValue to) where T : PropertyBuilder<T> => Init(builder).Property("ClipCircleCenter", to);

    /// <summary>Animates the <c>"ClipCircleCenter"</c> reveal center with its own tween settings.</summary>
    public static T ClipCircleCenter<T>(this T builder, TweenValue? to = null, TweenValue? from = null, TweenValue? duration = null,
        TweenValue? delay = null, EaseValue? ease = null, Composition? composition = null,
        Func<double, double>? modifier = null) where T : PropertyBuilder<T> =>
        Init(builder).Property("ClipCircleCenter", to, from, duration, delay, ease, composition, modifier);

    /// <summary>Animates the <c>"ClipCircleCenter"</c> reveal center to a value computed per target.</summary>
    public static T ClipCircleCenter<T>(this T builder, FunctionValue to) where T : PropertyBuilder<T> => Init(builder).Property("ClipCircleCenter", to);

    /// <summary>Animates the <c>"ClipCircleCenter"</c> reveal center through the keyframes <paramref name="keyframes"/> adds.</summary>
    public static T ClipCircleCenter<T>(this T builder, Action<TweenKeyframesBuilder> keyframes) where T : PropertyBuilder<T> => Init(builder).Property("ClipCircleCenter", keyframes);
    /// <summary>Animates the <c>"MaskSweep"</c> reveal to <paramref name="to"/>.</summary>
    public static T MaskSweep<T>(this T builder, TweenValue to) where T : PropertyBuilder<T> => Init(builder).Property("MaskSweep", to);

    /// <summary>Animates the <c>"MaskSweep"</c> reveal with its own tween settings.</summary>
    public static T MaskSweep<T>(this T builder, TweenValue? to = null, TweenValue? from = null, TweenValue? duration = null,
        TweenValue? delay = null, EaseValue? ease = null, Composition? composition = null,
        Func<double, double>? modifier = null) where T : PropertyBuilder<T> =>
        Init(builder).Property("MaskSweep", to, from, duration, delay, ease, composition, modifier);

    /// <summary>Animates the <c>"MaskSweep"</c> reveal to a value computed per target.</summary>
    public static T MaskSweep<T>(this T builder, FunctionValue to) where T : PropertyBuilder<T> => Init(builder).Property("MaskSweep", to);

    /// <summary>Animates the <c>"MaskSweep"</c> reveal through the keyframes <paramref name="keyframes"/> adds.</summary>
    public static T MaskSweep<T>(this T builder, Action<TweenKeyframesBuilder> keyframes) where T : PropertyBuilder<T> => Init(builder).Property("MaskSweep", keyframes);
    /// <summary>Animates the <c>"MaskSweepAngle"</c> sweep direction to <paramref name="to"/>.</summary>
    public static T MaskSweepAngle<T>(this T builder, TweenValue to) where T : PropertyBuilder<T> => Init(builder).Property("MaskSweepAngle", to);

    /// <summary>Animates the <c>"MaskSweepAngle"</c> sweep direction with its own tween settings.</summary>
    public static T MaskSweepAngle<T>(this T builder, TweenValue? to = null, TweenValue? from = null, TweenValue? duration = null,
        TweenValue? delay = null, EaseValue? ease = null, Composition? composition = null,
        Func<double, double>? modifier = null) where T : PropertyBuilder<T> =>
        Init(builder).Property("MaskSweepAngle", to, from, duration, delay, ease, composition, modifier);

    /// <summary>Animates the <c>"MaskSweepAngle"</c> sweep direction to a value computed per target.</summary>
    public static T MaskSweepAngle<T>(this T builder, FunctionValue to) where T : PropertyBuilder<T> => Init(builder).Property("MaskSweepAngle", to);

    /// <summary>Animates the <c>"MaskSweepAngle"</c> sweep direction through the keyframes <paramref name="keyframes"/> adds.</summary>
    public static T MaskSweepAngle<T>(this T builder, Action<TweenKeyframesBuilder> keyframes) where T : PropertyBuilder<T> => Init(builder).Property("MaskSweepAngle", keyframes);
    /// <summary>Animates the <c>"MaskSweepFeather"</c> soft edge to <paramref name="to"/>.</summary>
    public static T MaskSweepFeather<T>(this T builder, TweenValue to) where T : PropertyBuilder<T> => Init(builder).Property("MaskSweepFeather", to);

    /// <summary>Animates the <c>"MaskSweepFeather"</c> soft edge with its own tween settings.</summary>
    public static T MaskSweepFeather<T>(this T builder, TweenValue? to = null, TweenValue? from = null, TweenValue? duration = null,
        TweenValue? delay = null, EaseValue? ease = null, Composition? composition = null,
        Func<double, double>? modifier = null) where T : PropertyBuilder<T> =>
        Init(builder).Property("MaskSweepFeather", to, from, duration, delay, ease, composition, modifier);

    /// <summary>Animates the <c>"MaskSweepFeather"</c> soft edge to a value computed per target.</summary>
    public static T MaskSweepFeather<T>(this T builder, FunctionValue to) where T : PropertyBuilder<T> => Init(builder).Property("MaskSweepFeather", to);

    /// <summary>Animates the <c>"MaskSweepFeather"</c> soft edge through the keyframes <paramref name="keyframes"/> adds.</summary>
    public static T MaskSweepFeather<T>(this T builder, Action<TweenKeyframesBuilder> keyframes) where T : PropertyBuilder<T> => Init(builder).Property("MaskSweepFeather", keyframes);

    /// <summary>Animates the image brightness filter (<see cref="PrestoImageBase.Brightness"/>) to <paramref name="to"/>.</summary>
    public static T Brightness<T>(this T builder, TweenValue to) where T : PropertyBuilder<T> => builder.Property(PrestoImageBase.BrightnessProperty, to);

    /// <summary>Animates the image brightness filter with its own tween settings.</summary>
    public static T Brightness<T>(this T builder, TweenValue? to = null, TweenValue? from = null, TweenValue? duration = null,
        TweenValue? delay = null, EaseValue? ease = null, Composition? composition = null,
        Func<double, double>? modifier = null) where T : PropertyBuilder<T> =>
        builder.Property(PrestoImageBase.BrightnessProperty, to, from, duration, delay, ease, composition, modifier);

    /// <summary>Animates the image brightness filter to a value computed per target.</summary>
    public static T Brightness<T>(this T builder, FunctionValue to) where T : PropertyBuilder<T> => builder.Property(PrestoImageBase.BrightnessProperty, to);

    /// <summary>Animates the image brightness filter through the keyframes <paramref name="keyframes"/> adds.</summary>
    public static T Brightness<T>(this T builder, Action<TweenKeyframesBuilder> keyframes) where T : PropertyBuilder<T> => builder.Property(PrestoImageBase.BrightnessProperty, keyframes);

    /// <summary>Animates the image contrast filter (<see cref="PrestoImageBase.Contrast"/>) to <paramref name="to"/>.</summary>
    public static T Contrast<T>(this T builder, TweenValue to) where T : PropertyBuilder<T> => builder.Property(PrestoImageBase.ContrastProperty, to);

    /// <summary>Animates the image contrast filter with its own tween settings.</summary>
    public static T Contrast<T>(this T builder, TweenValue? to = null, TweenValue? from = null, TweenValue? duration = null,
        TweenValue? delay = null, EaseValue? ease = null, Composition? composition = null,
        Func<double, double>? modifier = null) where T : PropertyBuilder<T> =>
        builder.Property(PrestoImageBase.ContrastProperty, to, from, duration, delay, ease, composition, modifier);

    /// <summary>Animates the image contrast filter to a value computed per target.</summary>
    public static T Contrast<T>(this T builder, FunctionValue to) where T : PropertyBuilder<T> => builder.Property(PrestoImageBase.ContrastProperty, to);

    /// <summary>Animates the image contrast filter through the keyframes <paramref name="keyframes"/> adds.</summary>
    public static T Contrast<T>(this T builder, Action<TweenKeyframesBuilder> keyframes) where T : PropertyBuilder<T> => builder.Property(PrestoImageBase.ContrastProperty, keyframes);

    /// <summary>Animates the image saturation filter (<see cref="PrestoImageBase.Saturate"/>) to <paramref name="to"/>.</summary>
    public static T Saturate<T>(this T builder, TweenValue to) where T : PropertyBuilder<T> => builder.Property(PrestoImageBase.SaturateProperty, to);

    /// <summary>Animates the image saturation filter with its own tween settings.</summary>
    public static T Saturate<T>(this T builder, TweenValue? to = null, TweenValue? from = null, TweenValue? duration = null,
        TweenValue? delay = null, EaseValue? ease = null, Composition? composition = null,
        Func<double, double>? modifier = null) where T : PropertyBuilder<T> =>
        builder.Property(PrestoImageBase.SaturateProperty, to, from, duration, delay, ease, composition, modifier);

    /// <summary>Animates the image saturation filter to a value computed per target.</summary>
    public static T Saturate<T>(this T builder, FunctionValue to) where T : PropertyBuilder<T> => builder.Property(PrestoImageBase.SaturateProperty, to);

    /// <summary>Animates the image saturation filter through the keyframes <paramref name="keyframes"/> adds.</summary>
    public static T Saturate<T>(this T builder, Action<TweenKeyframesBuilder> keyframes) where T : PropertyBuilder<T> => builder.Property(PrestoImageBase.SaturateProperty, keyframes);

    /// <summary>Animates the image grayscale filter (<see cref="PrestoImageBase.Grayscale"/>) to <paramref name="to"/>.</summary>
    public static T Grayscale<T>(this T builder, TweenValue to) where T : PropertyBuilder<T> => builder.Property(PrestoImageBase.GrayscaleProperty, to);

    /// <summary>Animates the image grayscale filter with its own tween settings.</summary>
    public static T Grayscale<T>(this T builder, TweenValue? to = null, TweenValue? from = null, TweenValue? duration = null,
        TweenValue? delay = null, EaseValue? ease = null, Composition? composition = null,
        Func<double, double>? modifier = null) where T : PropertyBuilder<T> =>
        builder.Property(PrestoImageBase.GrayscaleProperty, to, from, duration, delay, ease, composition, modifier);

    /// <summary>Animates the image grayscale filter to a value computed per target.</summary>
    public static T Grayscale<T>(this T builder, FunctionValue to) where T : PropertyBuilder<T> => builder.Property(PrestoImageBase.GrayscaleProperty, to);

    /// <summary>Animates the image grayscale filter through the keyframes <paramref name="keyframes"/> adds.</summary>
    public static T Grayscale<T>(this T builder, Action<TweenKeyframesBuilder> keyframes) where T : PropertyBuilder<T> => builder.Property(PrestoImageBase.GrayscaleProperty, keyframes);

    /// <summary>Animates the image sepia filter (<see cref="PrestoImageBase.Sepia"/>) to <paramref name="to"/>.</summary>
    public static T Sepia<T>(this T builder, TweenValue to) where T : PropertyBuilder<T> => builder.Property(PrestoImageBase.SepiaProperty, to);

    /// <summary>Animates the image sepia filter with its own tween settings.</summary>
    public static T Sepia<T>(this T builder, TweenValue? to = null, TweenValue? from = null, TweenValue? duration = null,
        TweenValue? delay = null, EaseValue? ease = null, Composition? composition = null,
        Func<double, double>? modifier = null) where T : PropertyBuilder<T> =>
        builder.Property(PrestoImageBase.SepiaProperty, to, from, duration, delay, ease, composition, modifier);

    /// <summary>Animates the image sepia filter to a value computed per target.</summary>
    public static T Sepia<T>(this T builder, FunctionValue to) where T : PropertyBuilder<T> => builder.Property(PrestoImageBase.SepiaProperty, to);

    /// <summary>Animates the image sepia filter through the keyframes <paramref name="keyframes"/> adds.</summary>
    public static T Sepia<T>(this T builder, Action<TweenKeyframesBuilder> keyframes) where T : PropertyBuilder<T> => builder.Property(PrestoImageBase.SepiaProperty, keyframes);

    /// <summary>Animates the image hue rotation, in degrees, (<see cref="PrestoImageBase.HueRotate"/>) to <paramref name="to"/>.</summary>
    public static T HueRotate<T>(this T builder, TweenValue to) where T : PropertyBuilder<T> => builder.Property(PrestoImageBase.HueRotateProperty, to);

    /// <summary>Animates the image hue rotation, in degrees, with its own tween settings.</summary>
    public static T HueRotate<T>(this T builder, TweenValue? to = null, TweenValue? from = null, TweenValue? duration = null,
        TweenValue? delay = null, EaseValue? ease = null, Composition? composition = null,
        Func<double, double>? modifier = null) where T : PropertyBuilder<T> =>
        builder.Property(PrestoImageBase.HueRotateProperty, to, from, duration, delay, ease, composition, modifier);

    /// <summary>Animates the image hue rotation, in degrees, to a value computed per target.</summary>
    public static T HueRotate<T>(this T builder, FunctionValue to) where T : PropertyBuilder<T> => builder.Property(PrestoImageBase.HueRotateProperty, to);

    /// <summary>Animates the image hue rotation, in degrees, through the keyframes <paramref name="keyframes"/> adds.</summary>
    public static T HueRotate<T>(this T builder, Action<TweenKeyframesBuilder> keyframes) where T : PropertyBuilder<T> => builder.Property(PrestoImageBase.HueRotateProperty, keyframes);

    /// <summary>Animates the image invert filter (<see cref="PrestoImageBase.Invert"/>) to <paramref name="to"/>.</summary>
    public static T Invert<T>(this T builder, TweenValue to) where T : PropertyBuilder<T> => builder.Property(PrestoImageBase.InvertProperty, to);

    /// <summary>Animates the image invert filter with its own tween settings.</summary>
    public static T Invert<T>(this T builder, TweenValue? to = null, TweenValue? from = null, TweenValue? duration = null,
        TweenValue? delay = null, EaseValue? ease = null, Composition? composition = null,
        Func<double, double>? modifier = null) where T : PropertyBuilder<T> =>
        builder.Property(PrestoImageBase.InvertProperty, to, from, duration, delay, ease, composition, modifier);

    /// <summary>Animates the image invert filter to a value computed per target.</summary>
    public static T Invert<T>(this T builder, FunctionValue to) where T : PropertyBuilder<T> => builder.Property(PrestoImageBase.InvertProperty, to);

    /// <summary>Animates the image invert filter through the keyframes <paramref name="keyframes"/> adds.</summary>
    public static T Invert<T>(this T builder, Action<TweenKeyframesBuilder> keyframes) where T : PropertyBuilder<T> => builder.Property(PrestoImageBase.InvertProperty, keyframes);

    /// <summary>Animates the image tint filter (<see cref="PrestoImageBase.Tint"/>) to <paramref name="to"/>.</summary>
    public static T Tint<T>(this T builder, TweenValue to) where T : PropertyBuilder<T> => builder.Property(PrestoImageBase.TintProperty, to);

    /// <summary>Animates the image tint filter with its own tween settings.</summary>
    public static T Tint<T>(this T builder, TweenValue? to = null, TweenValue? from = null, TweenValue? duration = null,
        TweenValue? delay = null, EaseValue? ease = null, Composition? composition = null,
        Func<double, double>? modifier = null) where T : PropertyBuilder<T> =>
        builder.Property(PrestoImageBase.TintProperty, to, from, duration, delay, ease, composition, modifier);

    /// <summary>Animates the image tint filter to a value computed per target.</summary>
    public static T Tint<T>(this T builder, FunctionValue to) where T : PropertyBuilder<T> => builder.Property(PrestoImageBase.TintProperty, to);

    /// <summary>Animates the image tint filter through the keyframes <paramref name="keyframes"/> adds.</summary>
    public static T Tint<T>(this T builder, Action<TweenKeyframesBuilder> keyframes) where T : PropertyBuilder<T> => builder.Property(PrestoImageBase.TintProperty, keyframes);

    /// <summary>Animates the image tint color (<see cref="PrestoImageBase.TintColor"/>) to <paramref name="to"/>.</summary>
    public static T TintColor<T>(this T builder, TweenValue to) where T : PropertyBuilder<T> => builder.Property(PrestoImageBase.TintColorProperty, to);

    /// <summary>Animates the image tint color with its own tween settings.</summary>
    public static T TintColor<T>(this T builder, TweenValue? to = null, TweenValue? from = null, TweenValue? duration = null,
        TweenValue? delay = null, EaseValue? ease = null, Composition? composition = null,
        Func<double, double>? modifier = null) where T : PropertyBuilder<T> =>
        builder.Property(PrestoImageBase.TintColorProperty, to, from, duration, delay, ease, composition, modifier);

    /// <summary>Animates the image tint color to a value computed per target.</summary>
    public static T TintColor<T>(this T builder, FunctionValue to) where T : PropertyBuilder<T> => builder.Property(PrestoImageBase.TintColorProperty, to);

    /// <summary>Animates the image tint color through the keyframes <paramref name="keyframes"/> adds.</summary>
    public static T TintColor<T>(this T builder, Action<TweenKeyframesBuilder> keyframes) where T : PropertyBuilder<T> => builder.Property(PrestoImageBase.TintColorProperty, keyframes);

    /// <summary>Animates the image duotone filter (<see cref="PrestoImageBase.Duotone"/>) to <paramref name="to"/>.</summary>
    public static T Duotone<T>(this T builder, TweenValue to) where T : PropertyBuilder<T> => builder.Property(PrestoImageBase.DuotoneProperty, to);

    /// <summary>Animates the image duotone filter with its own tween settings.</summary>
    public static T Duotone<T>(this T builder, TweenValue? to = null, TweenValue? from = null, TweenValue? duration = null,
        TweenValue? delay = null, EaseValue? ease = null, Composition? composition = null,
        Func<double, double>? modifier = null) where T : PropertyBuilder<T> =>
        builder.Property(PrestoImageBase.DuotoneProperty, to, from, duration, delay, ease, composition, modifier);

    /// <summary>Animates the image duotone filter to a value computed per target.</summary>
    public static T Duotone<T>(this T builder, FunctionValue to) where T : PropertyBuilder<T> => builder.Property(PrestoImageBase.DuotoneProperty, to);

    /// <summary>Animates the image duotone filter through the keyframes <paramref name="keyframes"/> adds.</summary>
    public static T Duotone<T>(this T builder, Action<TweenKeyframesBuilder> keyframes) where T : PropertyBuilder<T> => builder.Property(PrestoImageBase.DuotoneProperty, keyframes);

    /// <summary>Animates the image duotone shadow color (<see cref="PrestoImageBase.DuotoneShadow"/>) to <paramref name="to"/>.</summary>
    public static T DuotoneShadow<T>(this T builder, TweenValue to) where T : PropertyBuilder<T> => builder.Property(PrestoImageBase.DuotoneShadowProperty, to);

    /// <summary>Animates the image duotone shadow color with its own tween settings.</summary>
    public static T DuotoneShadow<T>(this T builder, TweenValue? to = null, TweenValue? from = null, TweenValue? duration = null,
        TweenValue? delay = null, EaseValue? ease = null, Composition? composition = null,
        Func<double, double>? modifier = null) where T : PropertyBuilder<T> =>
        builder.Property(PrestoImageBase.DuotoneShadowProperty, to, from, duration, delay, ease, composition, modifier);

    /// <summary>Animates the image duotone shadow color to a value computed per target.</summary>
    public static T DuotoneShadow<T>(this T builder, FunctionValue to) where T : PropertyBuilder<T> => builder.Property(PrestoImageBase.DuotoneShadowProperty, to);

    /// <summary>Animates the image duotone shadow color through the keyframes <paramref name="keyframes"/> adds.</summary>
    public static T DuotoneShadow<T>(this T builder, Action<TweenKeyframesBuilder> keyframes) where T : PropertyBuilder<T> => builder.Property(PrestoImageBase.DuotoneShadowProperty, keyframes);

    /// <summary>Animates the image duotone highlight color (<see cref="PrestoImageBase.DuotoneHighlight"/>) to <paramref name="to"/>.</summary>
    public static T DuotoneHighlight<T>(this T builder, TweenValue to) where T : PropertyBuilder<T> => builder.Property(PrestoImageBase.DuotoneHighlightProperty, to);

    /// <summary>Animates the image duotone highlight color with its own tween settings.</summary>
    public static T DuotoneHighlight<T>(this T builder, TweenValue? to = null, TweenValue? from = null, TweenValue? duration = null,
        TweenValue? delay = null, EaseValue? ease = null, Composition? composition = null,
        Func<double, double>? modifier = null) where T : PropertyBuilder<T> =>
        builder.Property(PrestoImageBase.DuotoneHighlightProperty, to, from, duration, delay, ease, composition, modifier);

    /// <summary>Animates the image duotone highlight color to a value computed per target.</summary>
    public static T DuotoneHighlight<T>(this T builder, FunctionValue to) where T : PropertyBuilder<T> => builder.Property(PrestoImageBase.DuotoneHighlightProperty, to);

    /// <summary>Animates the image duotone highlight color through the keyframes <paramref name="keyframes"/> adds.</summary>
    public static T DuotoneHighlight<T>(this T builder, Action<TweenKeyframesBuilder> keyframes) where T : PropertyBuilder<T> => builder.Property(PrestoImageBase.DuotoneHighlightProperty, keyframes);

    /// <summary>Animates the image noise dissolve (<see cref="PrestoImageBase.Dissolve"/>) to <paramref name="to"/>.</summary>
    public static T Dissolve<T>(this T builder, TweenValue to) where T : PropertyBuilder<T> => builder.Property(PrestoImageBase.DissolveProperty, to);

    /// <summary>Animates the image noise dissolve with its own tween settings.</summary>
    public static T Dissolve<T>(this T builder, TweenValue? to = null, TweenValue? from = null, TweenValue? duration = null,
        TweenValue? delay = null, EaseValue? ease = null, Composition? composition = null,
        Func<double, double>? modifier = null) where T : PropertyBuilder<T> =>
        builder.Property(PrestoImageBase.DissolveProperty, to, from, duration, delay, ease, composition, modifier);

    /// <summary>Animates the image noise dissolve to a value computed per target.</summary>
    public static T Dissolve<T>(this T builder, FunctionValue to) where T : PropertyBuilder<T> => builder.Property(PrestoImageBase.DissolveProperty, to);

    /// <summary>Animates the image noise dissolve through the keyframes <paramref name="keyframes"/> adds.</summary>
    public static T Dissolve<T>(this T builder, Action<TweenKeyframesBuilder> keyframes) where T : PropertyBuilder<T> => builder.Property(PrestoImageBase.DissolveProperty, keyframes);

    /// <summary>Animates the dissolve noise size in DIPs (<see cref="PrestoImageBase.DissolveScale"/>) to <paramref name="to"/>.</summary>
    public static T DissolveScale<T>(this T builder, TweenValue to) where T : PropertyBuilder<T> => builder.Property(PrestoImageBase.DissolveScaleProperty, to);

    /// <summary>Animates the dissolve noise size in DIPs with its own tween settings.</summary>
    public static T DissolveScale<T>(this T builder, TweenValue? to = null, TweenValue? from = null, TweenValue? duration = null,
        TweenValue? delay = null, EaseValue? ease = null, Composition? composition = null,
        Func<double, double>? modifier = null) where T : PropertyBuilder<T> =>
        builder.Property(PrestoImageBase.DissolveScaleProperty, to, from, duration, delay, ease, composition, modifier);

    /// <summary>Animates the dissolve noise size in DIPs to a value computed per target.</summary>
    public static T DissolveScale<T>(this T builder, FunctionValue to) where T : PropertyBuilder<T> => builder.Property(PrestoImageBase.DissolveScaleProperty, to);

    /// <summary>Animates the dissolve noise size in DIPs through the keyframes <paramref name="keyframes"/> adds.</summary>
    public static T DissolveScale<T>(this T builder, Action<TweenKeyframesBuilder> keyframes) where T : PropertyBuilder<T> => builder.Property(PrestoImageBase.DissolveScaleProperty, keyframes);

    /// <summary>Animates the dissolve edge softness (<see cref="PrestoImageBase.DissolveSoftness"/>) to <paramref name="to"/>.</summary>
    public static T DissolveSoftness<T>(this T builder, TweenValue to) where T : PropertyBuilder<T> => builder.Property(PrestoImageBase.DissolveSoftnessProperty, to);

    /// <summary>Animates the dissolve edge softness with its own tween settings.</summary>
    public static T DissolveSoftness<T>(this T builder, TweenValue? to = null, TweenValue? from = null, TweenValue? duration = null,
        TweenValue? delay = null, EaseValue? ease = null, Composition? composition = null,
        Func<double, double>? modifier = null) where T : PropertyBuilder<T> =>
        builder.Property(PrestoImageBase.DissolveSoftnessProperty, to, from, duration, delay, ease, composition, modifier);

    /// <summary>Animates the dissolve edge softness to a value computed per target.</summary>
    public static T DissolveSoftness<T>(this T builder, FunctionValue to) where T : PropertyBuilder<T> => builder.Property(PrestoImageBase.DissolveSoftnessProperty, to);

    /// <summary>Animates the dissolve edge softness through the keyframes <paramref name="keyframes"/> adds.</summary>
    public static T DissolveSoftness<T>(this T builder, Action<TweenKeyframesBuilder> keyframes) where T : PropertyBuilder<T> => builder.Property(PrestoImageBase.DissolveSoftnessProperty, keyframes);

    /// <summary>Animates the image pixelation block size in DIPs (<see cref="PrestoImageBase.Pixelate"/>) to <paramref name="to"/>.</summary>
    public static T Pixelate<T>(this T builder, TweenValue to) where T : PropertyBuilder<T> => builder.Property(PrestoImageBase.PixelateProperty, to);

    /// <summary>Animates the image pixelation block size with its own tween settings.</summary>
    public static T Pixelate<T>(this T builder, TweenValue? to = null, TweenValue? from = null, TweenValue? duration = null,
        TweenValue? delay = null, EaseValue? ease = null, Composition? composition = null,
        Func<double, double>? modifier = null) where T : PropertyBuilder<T> =>
        builder.Property(PrestoImageBase.PixelateProperty, to, from, duration, delay, ease, composition, modifier);

    /// <summary>Animates the image pixelation block size to a value computed per target.</summary>
    public static T Pixelate<T>(this T builder, FunctionValue to) where T : PropertyBuilder<T> => builder.Property(PrestoImageBase.PixelateProperty, to);

    /// <summary>Animates the image pixelation block size through the keyframes <paramref name="keyframes"/> adds.</summary>
    public static T Pixelate<T>(this T builder, Action<TweenKeyframesBuilder> keyframes) where T : PropertyBuilder<T> => builder.Property(PrestoImageBase.PixelateProperty, keyframes);

    /// <summary>Animates the image noise displacement in DIPs (<see cref="PrestoImageBase.Ripple"/>) to <paramref name="to"/>.</summary>
    public static T Ripple<T>(this T builder, TweenValue to) where T : PropertyBuilder<T> => builder.Property(PrestoImageBase.RippleProperty, to);

    /// <summary>Animates the image noise displacement with its own tween settings.</summary>
    public static T Ripple<T>(this T builder, TweenValue? to = null, TweenValue? from = null, TweenValue? duration = null,
        TweenValue? delay = null, EaseValue? ease = null, Composition? composition = null,
        Func<double, double>? modifier = null) where T : PropertyBuilder<T> =>
        builder.Property(PrestoImageBase.RippleProperty, to, from, duration, delay, ease, composition, modifier);

    /// <summary>Animates the image noise displacement to a value computed per target.</summary>
    public static T Ripple<T>(this T builder, FunctionValue to) where T : PropertyBuilder<T> => builder.Property(PrestoImageBase.RippleProperty, to);

    /// <summary>Animates the image noise displacement through the keyframes <paramref name="keyframes"/> adds.</summary>
    public static T Ripple<T>(this T builder, Action<TweenKeyframesBuilder> keyframes) where T : PropertyBuilder<T> => builder.Property(PrestoImageBase.RippleProperty, keyframes);

    /// <summary>Animates the ripple noise size in DIPs (<see cref="PrestoImageBase.RippleScale"/>) to <paramref name="to"/>.</summary>
    public static T RippleScale<T>(this T builder, TweenValue to) where T : PropertyBuilder<T> => builder.Property(PrestoImageBase.RippleScaleProperty, to);

    /// <summary>Animates the ripple noise size in DIPs with its own tween settings.</summary>
    public static T RippleScale<T>(this T builder, TweenValue? to = null, TweenValue? from = null, TweenValue? duration = null,
        TweenValue? delay = null, EaseValue? ease = null, Composition? composition = null,
        Func<double, double>? modifier = null) where T : PropertyBuilder<T> =>
        builder.Property(PrestoImageBase.RippleScaleProperty, to, from, duration, delay, ease, composition, modifier);

    /// <summary>Animates the ripple noise size in DIPs to a value computed per target.</summary>
    public static T RippleScale<T>(this T builder, FunctionValue to) where T : PropertyBuilder<T> => builder.Property(PrestoImageBase.RippleScaleProperty, to);

    /// <summary>Animates the ripple noise size in DIPs through the keyframes <paramref name="keyframes"/> adds.</summary>
    public static T RippleScale<T>(this T builder, Action<TweenKeyframesBuilder> keyframes) where T : PropertyBuilder<T> => builder.Property(PrestoImageBase.RippleScaleProperty, keyframes);

    /// <summary>Scrolls a <see cref="Avalonia.Controls.ScrollViewer"/> horizontally to the <c>"ScrollX"</c> offset <paramref name="to"/>.</summary>
    public static T ScrollX<T>(this T builder, TweenValue to) where T : PropertyBuilder<T> => Init(builder).Property("ScrollX", to);

    /// <summary>Scrolls a <see cref="Avalonia.Controls.ScrollViewer"/>'s <c>"ScrollX"</c> offset with its own tween settings.</summary>
    public static T ScrollX<T>(this T builder, TweenValue? to = null, TweenValue? from = null, TweenValue? duration = null,
        TweenValue? delay = null, EaseValue? ease = null, Composition? composition = null,
        Func<double, double>? modifier = null) where T : PropertyBuilder<T> =>
        Init(builder).Property("ScrollX", to, from, duration, delay, ease, composition, modifier);

    /// <summary>Scrolls a <see cref="Avalonia.Controls.ScrollViewer"/>'s <c>"ScrollX"</c> offset to a value computed per target.</summary>
    public static T ScrollX<T>(this T builder, FunctionValue to) where T : PropertyBuilder<T> => Init(builder).Property("ScrollX", to);

    /// <summary>Scrolls a <see cref="Avalonia.Controls.ScrollViewer"/>'s <c>"ScrollX"</c> offset through the keyframes <paramref name="keyframes"/> adds.</summary>
    public static T ScrollX<T>(this T builder, Action<TweenKeyframesBuilder> keyframes) where T : PropertyBuilder<T> => Init(builder).Property("ScrollX", keyframes);

    /// <summary>Scrolls a <see cref="Avalonia.Controls.ScrollViewer"/> vertically to the <c>"ScrollY"</c> offset <paramref name="to"/>.</summary>
    public static T ScrollY<T>(this T builder, TweenValue to) where T : PropertyBuilder<T> => Init(builder).Property("ScrollY", to);

    /// <summary>Scrolls a <see cref="Avalonia.Controls.ScrollViewer"/>'s <c>"ScrollY"</c> offset with its own tween settings.</summary>
    public static T ScrollY<T>(this T builder, TweenValue? to = null, TweenValue? from = null, TweenValue? duration = null,
        TweenValue? delay = null, EaseValue? ease = null, Composition? composition = null,
        Func<double, double>? modifier = null) where T : PropertyBuilder<T> =>
        Init(builder).Property("ScrollY", to, from, duration, delay, ease, composition, modifier);

    /// <summary>Scrolls a <see cref="Avalonia.Controls.ScrollViewer"/>'s <c>"ScrollY"</c> offset to a value computed per target.</summary>
    public static T ScrollY<T>(this T builder, FunctionValue to) where T : PropertyBuilder<T> => Init(builder).Property("ScrollY", to);

    /// <summary>Scrolls a <see cref="Avalonia.Controls.ScrollViewer"/>'s <c>"ScrollY"</c> offset through the keyframes <paramref name="keyframes"/> adds.</summary>
    public static T ScrollY<T>(this T builder, Action<TweenKeyframesBuilder> keyframes) where T : PropertyBuilder<T> => Init(builder).Property("ScrollY", keyframes);

    /// <summary>Animates <c>"Background"</c> to a solid brush of <paramref name="to"/>.</summary>
    public static T Background<T>(this T builder, Color to) where T : PropertyBuilder<T> => Init(builder).Property("Background", SolidBrush(to));

    /// <summary>Animates <c>"Background"</c> to the brush <paramref name="to"/>: a solid brush's color, or a gradient's stops and geometry.</summary>
    public static T Background<T>(this T builder, IBrush to) where T : PropertyBuilder<T> => Init(builder).Property("Background", TweenValue.Of(to));

    /// <summary>Animates <c>"Foreground"</c> to a solid brush of <paramref name="to"/>.</summary>
    public static T Foreground<T>(this T builder, Color to) where T : PropertyBuilder<T> => Init(builder).Property("Foreground", SolidBrush(to));

    /// <summary>Animates <c>"Foreground"</c> to the brush <paramref name="to"/>: a solid brush's color, or a gradient's stops and geometry.</summary>
    public static T Foreground<T>(this T builder, IBrush to) where T : PropertyBuilder<T> => Init(builder).Property("Foreground", TweenValue.Of(to));
    /// <summary>Animates <c>"BoxShadow"</c> to <paramref name="to"/>.</summary>
    public static T BoxShadow<T>(this T builder, BoxShadows to) where T : PropertyBuilder<T> => Init(builder).Property("BoxShadow", TweenValue.Of(to));

    /// <summary>Animates <c>"CornerRadius"</c> to <paramref name="to"/>.</summary>
    public static T CornerRadius<T>(this T builder, CornerRadius to) where T : PropertyBuilder<T> => Init(builder).Property("CornerRadius", TweenValue.Of(to));

    /// <summary>Animates <c>"BorderThickness"</c> to <paramref name="to"/>.</summary>
    public static T BorderThickness<T>(this T builder, Thickness to) where T : PropertyBuilder<T> => Init(builder).Property("BorderThickness", TweenValue.Of(to));

    /// <summary>Animates <c>"Margin"</c> to <paramref name="to"/>.</summary>
    public static T Margin<T>(this T builder, Thickness to) where T : PropertyBuilder<T> => Init(builder).Property("Margin", TweenValue.Of(to));

    /// <summary>Animates <c>"Padding"</c> to <paramref name="to"/>.</summary>
    public static T Padding<T>(this T builder, Thickness to) where T : PropertyBuilder<T> => Init(builder).Property("Padding", TweenValue.Of(to));

    /// <summary>Animates the <c>"Draw"</c> slice of a drawable shape to run from <paramref name="start"/> to <paramref name="end"/>, both 0-1.</summary>
    public static T Draw<T>(this T builder, double start, double end) where T : PropertyBuilder<T> =>
        Init(builder).Property("Draw", string.Create(CultureInfo.InvariantCulture, $"{start} {end}"));

    /// <summary>Morphs a path's <c>"Data"</c> into <paramref name="to"/>.</summary>
    public static T Data<T>(this T builder, Geometry to) where T : PropertyBuilder<T> => Init(builder).Property("Data", TweenValue.Of(to));
    /// <summary>Animates <c>"ShadowColor"</c> to <paramref name="to"/>.</summary>
    public static T ShadowColor<T>(this T builder, Color to) where T : PropertyBuilder<T> => Init(builder).Property("ShadowColor", TweenValue.Of(to));

    /// <summary>Animates the four drop-shadow shorthands to <paramref name="x"/>, <paramref name="y"/>, <paramref name="blur"/> and <paramref name="color"/>.</summary>
    public static T Shadow<T>(this T builder, double x, double y, double blur, Color color) where T : PropertyBuilder<T> =>
        builder.ShadowX(x).ShadowY(y).ShadowBlur(blur).ShadowColor(color);

    /// <summary>Animates <see cref="PrestoImageBase.TintColor"/> to <paramref name="to"/>.</summary>
    public static T TintColor<T>(this T builder, Color to) where T : PropertyBuilder<T> => builder.Property(PrestoImageBase.TintColorProperty, TweenValue.Of(to));

    /// <summary>Animates <see cref="PrestoImageBase.DuotoneShadow"/> to <paramref name="to"/>.</summary>
    public static T DuotoneShadow<T>(this T builder, Color to) where T : PropertyBuilder<T> => builder.Property(PrestoImageBase.DuotoneShadowProperty, TweenValue.Of(to));

    /// <summary>Animates <see cref="PrestoImageBase.DuotoneHighlight"/> to <paramref name="to"/>.</summary>
    public static T DuotoneHighlight<T>(this T builder, Color to) where T : PropertyBuilder<T> => builder.Property(PrestoImageBase.DuotoneHighlightProperty, TweenValue.Of(to));

    /// <summary>Animates the <c>"ClipInset"</c> reveal to the DIP insets <paramref name="to"/>.</summary>
    public static T ClipInset<T>(this T builder, Thickness to) where T : PropertyBuilder<T> => Init(builder).Property("ClipInset", TweenValue.Of(to));

    /// <summary>Animates the <c>"ClipCircleCenter"</c> reveal center to <paramref name="to"/>.</summary>
    public static T ClipCircleCenter<T>(this T builder, RelativePoint to) where T : PropertyBuilder<T> => Init(builder).Property("ClipCircleCenter", TweenValue.Of(to));

    /// <summary>Animates <paramref name="property"/> to <paramref name="to"/>.</summary>
    public static T Property<T>(this T builder, AvaloniaProperty property, TweenValue to) where T : PropertyBuilder<T> =>
        Init(builder).Property(AvaloniaObjectTargetAdapter.Encode(property), to);

    /// <summary>Animates <paramref name="property"/> with its own tween settings.</summary>
    public static T Property<T>(this T builder, AvaloniaProperty property, TweenValue? to = null, TweenValue? from = null,
        TweenValue? duration = null, TweenValue? delay = null, EaseValue? ease = null, Composition? composition = null,
        Func<double, double>? modifier = null, Func<double, string>? textModifier = null) where T : PropertyBuilder<T> =>
        Init(builder).Property(AvaloniaObjectTargetAdapter.Encode(property), to, from, duration, delay, ease, composition, modifier, textModifier);

    /// <summary>Animates <paramref name="property"/> to a value computed per target.</summary>
    public static T Property<T>(this T builder, AvaloniaProperty property, FunctionValue to) where T : PropertyBuilder<T> =>
        Init(builder).Property(AvaloniaObjectTargetAdapter.Encode(property), to);

    /// <summary>Animates <paramref name="property"/> through the keyframes <paramref name="keyframes"/> adds.</summary>
    public static T Property<T>(this T builder, AvaloniaProperty property, Action<TweenKeyframesBuilder> keyframes) where T : PropertyBuilder<T> =>
        Init(builder).Property(AvaloniaObjectTargetAdapter.Encode(property), keyframes);

    private static TweenValue SolidBrush(Color color) => TweenValue.Of(new ImmutableSolidColorBrush(color));

    private static T Init<T>(T builder) where T : PropertyBuilder<T>
    {
        PrestoAvalonia.EnsureInitialized();
        return builder;
    }
}