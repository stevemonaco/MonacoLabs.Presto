using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Data;
using Avalonia.Media;
using Avalonia.Threading;
using Monaco.Presto.AvaloniaBridge.Compositor;
using Monaco.Presto.AvaloniaBridge.Targets;
using Monaco.Presto.Diagnostics;

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
    private static readonly double[] s_identities = [0, 0, 0, 1, 1, 1, 0, 0, 0, 0, 0];

    private readonly double[] _values = [.. s_identities];
    private readonly double[] _bases = new double[s_identities.Length];
    private readonly TransformGroup _group = new();
    private Transform? _existing;
    private IDisposable? _written;
    private TranslateTransform? _translate;
    private RotateTransform? _rotate;
    private ScaleTransform? _scale;
    private SkewTransform? _skew;
    private Rotate3DTransform? _rotate3D;
    private int _animated;
    private int _followed;
    private bool _reasserting;

    private RenderTransformComposer(Visual visual)
    {
        _existing = AsTransform(visual.RenderTransform);
        _written = visual.SetValue(Visual.RenderTransformProperty, _group, BindingPriority.Animation);
        visual.PropertyChanged += OnPropertyChanged;
    }

    public static double Identity(TransformShorthand shorthand) => s_identities[(int)shorthand];

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
        if (!composer.Animates(shorthand)) composer.Include(visual, shorthand, ref composer._animated);
        composer.Apply(shorthand, value);
    }

    /// <summary>Makes <paramref name="value"/> the base of one shorthand: shown while no animation holds it, and restored when one is reverted.</summary>
    public static void Follow(Visual visual, TransformShorthand shorthand, double value)
    {
        var composer = s_composers.GetValue(visual, static v => new RenderTransformComposer(v));
        composer._bases[(int)shorthand] = value;
        if (!composer.Follows(shorthand)) composer.Include(visual, shorthand, ref composer._followed);
        if (composer.Animates(shorthand)) return;
        composer._values[(int)shorthand] = value;
        composer.Apply(shorthand, value);
    }

    /// <summary>Releases one shorthand from its animation, back to its followed base or else its identity value; the last of them takes the whole group with it.</summary>
    public static void Revert(Visual visual, TransformShorthand shorthand)
    {
        if (!s_composers.TryGetValue(visual, out var composer) || !composer.Animates(shorthand)) return;
        composer._animated &= ~(1 << (int)shorthand);
        composer.Release(visual, shorthand);
    }

    /// <summary>Drops one shorthand's followed base; unless an animation holds it, it returns to its identity value.</summary>
    public static void Unfollow(Visual visual, TransformShorthand shorthand)
    {
        if (!s_composers.TryGetValue(visual, out var composer) || !composer.Follows(shorthand)) return;
        composer._followed &= ~(1 << (int)shorthand);
        if (!composer.Animates(shorthand)) composer.Release(visual, shorthand);
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
            Rebuild(visual);
        }, DispatcherPriority.Send);
    }

    private bool Animates(TransformShorthand shorthand) => (_animated & (1 << (int)shorthand)) != 0;

    private bool Follows(TransformShorthand shorthand) => (_followed & (1 << (int)shorthand)) != 0;

    private bool Shows(TransformShorthand shorthand) => ((_animated | _followed) & (1 << (int)shorthand)) != 0;

    private void Include(Visual visual, TransformShorthand shorthand, ref int mask)
    {
        var shown = Shows(shorthand);
        mask |= 1 << (int)shorthand;
        if (shown) return;
        CompositorRegistry.WarnIfCompositorDrives(visual, shorthand);
        Rebuild(visual);
    }

    private void Release(Visual visual, TransformShorthand shorthand)
    {
        var value = Follows(shorthand) ? _bases[(int)shorthand] : s_identities[(int)shorthand];
        _values[(int)shorthand] = value;
        Apply(shorthand, value);
        if ((_animated | _followed) != 0)
        {
            Rebuild(visual);
            return;
        }
        visual.PropertyChanged -= OnPropertyChanged;
        _written?.Dispose();
        s_composers.Remove(visual);
    }

    private void Rebuild(Visual visual)
    {
        var children = _group.Children;
        children.Clear();
        if (_existing != null) children.Add(_existing);
        if (Shows(TransformShorthand.SkewX) || Shows(TransformShorthand.SkewY)) children.Add(_skew ??= new SkewTransform());
        if (Shows(TransformShorthand.Scale) || Shows(TransformShorthand.ScaleX) || Shows(TransformShorthand.ScaleY)) children.Add(_scale ??= new ScaleTransform());
        if (Shows(TransformShorthand.RotateX) || Shows(TransformShorthand.RotateY) || Shows(TransformShorthand.Perspective)) children.Add(_rotate3D ??= new Rotate3DTransform());
        if (Shows(TransformShorthand.Rotate)) children.Add(_rotate ??= new RotateTransform());
        if (Shows(TransformShorthand.X) || Shows(TransformShorthand.Y)) children.Add(_translate ??= new TranslateTransform());
        if (!PrestoDiagnostics.Active) return;
        TargetOwner.Record(_group, visual);
        for (var i = 0; i < children.Count; i++) TargetOwner.Record(children[i], visual);
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
            case TransformShorthand.RotateX:
                _rotate3D!.AngleX = value;
                break;
            case TransformShorthand.RotateY:
                _rotate3D!.AngleY = value;
                break;
            case TransformShorthand.Perspective:
                _rotate3D!.Depth = value;
                break;
        }
    }
}
