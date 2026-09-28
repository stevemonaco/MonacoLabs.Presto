using System.Globalization;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Monaco.Presto.AvaloniaBridge.Targets;

namespace Monaco.Presto.AvaloniaBridge;

/// <summary>
/// Typed names for the <see cref="PropertyBuilder{TSelf}"/> properties: the transform shorthands, the Avalonia properties
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