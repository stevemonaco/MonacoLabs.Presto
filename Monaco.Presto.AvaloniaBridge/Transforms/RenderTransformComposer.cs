using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Data;
using Avalonia.Media;
using Avalonia.Threading;
using Monaco.Presto.AvaloniaBridge.Compositor;

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
/// last shorthand uncovers it again. A transition that displaces the group, such as Fluent's pressed scale on a button, is
/// pushed back underneath it, and the style value it animates to becomes the first child instead.
/// </remarks>
internal sealed class RenderTransformComposer
{
    private static readonly ConditionalWeakTable<Visual, RenderTransformComposer> s_composers = new();

    /// <summary>The value a shorthand holds while nothing animates it, indexed by <see cref="TransformShorthand"/>.</summary>
    private static readonly double[] s_identities = [0, 0, 0, 1, 1, 1, 0, 0];

    private readonly double[] _values = [.. s_identities];
    private readonly TransformGroup _group = new();
    private Transform? _existing;
    private IDisposable? _written;
    private TranslateTransform? _translate;
    private RotateTransform? _rotate;
    private ScaleTransform? _scale;
    private SkewTransform? _skew;
    private int _animated;
    private bool _reasserting;

    private RenderTransformComposer(Visual visual)
    {
        _existing = AsTransform(visual.RenderTransform);
        _written = visual.SetValue(Visual.RenderTransformProperty, _group, BindingPriority.Animation);
        visual.PropertyChanged += OnPropertyChanged;
    }

    public static double Get(Visual visual, TransformShorthand shorthand) =>
        (s_composers.TryGetValue(visual, out var composer) ? composer._values : s_identities)[(int)shorthand];

    /// <summary>Whether a running animation drives a shorthand of <paramref name="visual"/>; one that has ended only holds its value.</summary>
    public static bool Animates(Visual visual)
    {
        if (!s_composers.TryGetValue(visual, out var composer)) return false;
        for (var i = 0; i < s_identities.Length; i++)
        {
            var shorthand = (TransformShorthand)i;
            if (composer.Animates(shorthand) && Presto.GetAnimations(visual, shorthand.ToString()).Count != 0) return true;
        }
        return false;
    }

    public static void Set(Visual visual, TransformShorthand shorthand, double value)
    {
        var composer = s_composers.GetValue(visual, static v => new RenderTransformComposer(v));
        composer._values[(int)shorthand] = value;
        if (!composer.Animates(shorthand))
        {
            CompositorRegistry.WarnIfCompositorDrives(visual, shorthand);
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
        visual.PropertyChanged -= composer.OnPropertyChanged;
        composer._written?.Dispose();
        s_composers.Remove(visual);
    }

    /// <summary>
    /// Runs <paramref name="action"/> with <paramref name="visual"/>'s group emptied, so it sees the visual untransformed.
    /// The group's children change and the property does not, so nothing re-asserts the group in between.
    /// </summary>
    /// <returns>The group's transform, or null, with <paramref name="action"/> not run, when the visual's render transform is not a composer's.</returns>
    public static Matrix? Untransformed(Visual visual, Action action)
    {
        if (!s_composers.TryGetValue(visual, out var composer) || !ReferenceEquals(visual.RenderTransform, composer._group)) return null;
        var matrix = composer._group.Value;
        var children = composer._group.Children;
        var saved = children.ToArray();
        children.Clear();
        try
        {
            action();
        }
        finally
        {
            children.AddRange(saved);
        }
        return matrix;
    }

    private static Transform? AsTransform(ITransform? transform) => transform switch
    {
        Transform mutable => mutable,
        { } immutable => new MatrixTransform(immutable.Value),
        null => null,
    };

    private void OnPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        var visual = (Visual)sender!;
        if (_reasserting || e.Property != Visual.RenderTransformProperty || ReferenceEquals(visual.RenderTransform, _group)) return;
        var displaced = _written;
        // Detaching removes a transition mid re-evaluation, where each re-assert raises another change instead of winning.
        _reasserting = true;
        try
        {
            _written = visual.SetValue(Visual.RenderTransformProperty, _group, BindingPriority.Animation);
            displaced?.Dispose();
        }
        finally
        {
            _reasserting = false;
        }
        // The transition starts before the style's new value is committed as the base value.
        Dispatcher.UIThread.Post(() =>
        {
            if (!s_composers.TryGetValue(visual, out var composer) || composer != this) return;
            _existing = AsTransform(visual.GetBaseValue(Visual.RenderTransformProperty).GetValueOrDefault());
            Rebuild();
        }, DispatcherPriority.Send);
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
