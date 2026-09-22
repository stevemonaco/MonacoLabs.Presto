using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Data;
using Avalonia.Media;

namespace Monaco.Presto.AvaloniaBridge.Transforms;

/// <summary>
/// The <see cref="TransformGroup"/> the transform shorthands animate, one per <see cref="Visual"/>: the Avalonia counterpart
/// of the CSS transform string upstream rebuilds in <c>transforms.js</c>. Only the shorthands an animation touches become
/// children, so separate animations on the same visual aggregate instead of replacing each other.
/// </summary>
/// <remarks>
/// A group's children apply in the order they are listed, so they are added in the reverse of upstream's string order: the
/// translation lands last, leaving rotation, scale and skew acting on the visual's own box. The render transform the visual
/// already had stays as the first child, and the group is written at <see cref="BindingPriority.Animation"/> so releasing the
/// last shorthand uncovers it again.
/// </remarks>
internal sealed class RenderTransformComposer
{
    private static readonly ConditionalWeakTable<Visual, RenderTransformComposer> s_composers = new();

    /// <summary>The value a shorthand holds while nothing animates it, indexed by <see cref="TransformShorthand"/>.</summary>
    private static readonly double[] s_identities = [0, 0, 0, 1, 1, 1, 0, 0];

    private readonly double[] _values = [.. s_identities];
    private readonly TransformGroup _group = new();
    private readonly Transform? _existing;
    private readonly IDisposable? _written;
    private TranslateTransform? _translate;
    private RotateTransform? _rotate;
    private ScaleTransform? _scale;
    private SkewTransform? _skew;
    private int _animated;

    private RenderTransformComposer(Visual visual)
    {
        _existing = visual.RenderTransform switch
        {
            Transform transform => transform,
            { } immutable => new MatrixTransform(immutable.Value),
            null => null,
        };
        _written = visual.SetValue(Visual.RenderTransformProperty, _group, BindingPriority.Animation);
    }

    public static double Get(Visual visual, TransformShorthand shorthand) =>
        (s_composers.TryGetValue(visual, out var composer) ? composer._values : s_identities)[(int)shorthand];

    public static void Set(Visual visual, TransformShorthand shorthand, double value)
    {
        var composer = s_composers.GetValue(visual, static v => new RenderTransformComposer(v));
        composer._values[(int)shorthand] = value;
        if (!composer.Animates(shorthand))
        {
            composer._animated |= 1 << (int)shorthand;
            composer.Rebuild();
        }
        composer.Apply(shorthand, value);
    }

    /// <summary>Releases one shorthand back to its identity value; the last of them takes the whole group with it.</summary>
    public static void Revert(Visual visual, TransformShorthand shorthand)
    {
        if (!s_composers.TryGetValue(visual, out var composer) || !composer.Animates(shorthand)) return;

        var identity = s_identities[(int)shorthand];
        composer._values[(int)shorthand] = identity;
        composer._animated &= ~(1 << (int)shorthand);
        composer.Apply(shorthand, identity);
        if (composer._animated != 0)
        {
            composer.Rebuild();
            return;
        }
        composer._written?.Dispose();
        s_composers.Remove(visual);
    }

    private bool Animates(TransformShorthand shorthand) => (_animated & (1 << (int)shorthand)) != 0;

    private void Rebuild()
    {
        var children = _group.Children;
        children.Clear();
        if (_existing != null) children.Add(_existing);
        if (Animates(TransformShorthand.SkewX) || Animates(TransformShorthand.SkewY)) children.Add(_skew ??= new SkewTransform());
        if (Animates(TransformShorthand.Scale) || Animates(TransformShorthand.ScaleX) || Animates(TransformShorthand.ScaleY)) children.Add(_scale ??= new ScaleTransform());
        if (Animates(TransformShorthand.Rotate)) children.Add(_rotate ??= new RotateTransform());
        if (Animates(TransformShorthand.X) || Animates(TransformShorthand.Y)) children.Add(_translate ??= new TranslateTransform());
    }

    private void Apply(TransformShorthand shorthand, double value)
    {
        switch (shorthand)
        {
            case TransformShorthand.X:
                _translate!.X = value;
                break;
            case TransformShorthand.Y:
                _translate!.Y = value;
                break;
            case TransformShorthand.Rotate:
                _rotate!.Angle = value;
                break;
            case TransformShorthand.Scale or TransformShorthand.ScaleX or TransformShorthand.ScaleY:
                // The uniform scale multiplies the per axis ones, as upstream's "scale(s) scaleX(x)" string does.
                _scale!.ScaleX = _values[(int)TransformShorthand.Scale] * _values[(int)TransformShorthand.ScaleX];
                _scale.ScaleY = _values[(int)TransformShorthand.Scale] * _values[(int)TransformShorthand.ScaleY];
                break;
            case TransformShorthand.SkewX:
                _skew!.AngleX = value;
                break;
            case TransformShorthand.SkewY:
                _skew!.AngleY = value;
                break;
        }
    }
}
