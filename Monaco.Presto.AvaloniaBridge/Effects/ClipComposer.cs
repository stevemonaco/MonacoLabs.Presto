using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Data;
using Avalonia.Media;
using Monaco.Presto.Values;

namespace Monaco.Presto.AvaloniaBridge.Effects;

/// <summary>
/// The clip the reveal shorthands animate, one per <see cref="Visual"/>: a bridge-owned <see cref="RectangleGeometry"/>
/// (<c>ClipInset</c>) or <see cref="EllipseGeometry"/> (<c>ClipCircle</c>) written at <see cref="BindingPriority.Animation"/>
/// and mutated in place, so releasing the last shorthand uncovers the visual's own <see cref="Visual.Clip"/>. The geometry
/// is laid out from the visual's bounds and follows them while installed, so a <c>%</c> inset or a circle tracks a resize.
/// </summary>
/// <remarks>
/// <c>ClipCircle</c> is a radius fraction, 1 reaching the corner farthest from the center. MOTION-KIT §3.2 gives it a center
/// too, which one scalar cannot carry, so the center is the companion <c>ClipCircleCenter</c>, a <see cref="RelativePoint"/>
/// defaulting to 50%,50%. A visual holds one clip, so an inset and a circle cannot animate it at once; one after the other
/// switches the kind. A reveal that ends at full visibility, every inset 0 or a radius of 1, is removed.
/// </remarks>
internal sealed class ClipComposer
{
    private static readonly ConditionalWeakTable<Visual, ClipComposer> s_composers = new();
    private static readonly string[] s_dips = ["", " ", " ", " ", ""];

    private readonly double[] _inset = new double[4];
    private string[] _insetUnits = s_dips;
    private double _radius = 1;
    private RelativePoint _center = RelativePoint.Center;
    private RectangleGeometry? _rectangle;
    private EllipseGeometry? _ellipse;
    private readonly ExclusiveKinds<ClipShorthand> _kinds = new(
        ClipShorthand.ClipCircle, ClipShorthand.ClipCircleCenter, "a visual holds one clip. Put one reveal on an inner element.");
    private IDisposable? _written;
    private bool _revealed;

    public static object Identity(ClipShorthand shorthand) => shorthand switch
    {
        ClipShorthand.ClipInset => "0",
        ClipShorthand.ClipCircle => 1d,
        _ => RelativePoint.Center,
    };

    public static object Get(Visual visual, ClipShorthand shorthand)
    {
        if (!s_composers.TryGetValue(visual, out var composer) || !composer._kinds.Shows(shorthand)) return Identity(shorthand);
        return shorthand switch
        {
            ClipShorthand.ClipInset => composer.FormatInset(),
            ClipShorthand.ClipCircle => composer._radius,
            _ => composer._center,
        };
    }

    /// <inheritdoc cref="ExclusiveKinds{TShorthand}.Claim"/>
    public static void Claim(Visual visual, ClipShorthand shorthand) =>
        s_composers.GetValue(visual, static _ => new ClipComposer())._kinds.Claim(visual, shorthand);

    /// <summary>Writes one inset frame from its channels and their units; <paramref name="ended"/> says whether the tween has reached its end.</summary>
    public static void SetInset(Visual visual, double[] inset, string[] units, bool ended)
    {
        var composer = Begin(visual, ClipShorthand.ClipInset);
        inset.CopyTo(composer._inset, 0);
        composer._insetUnits = units;
        composer._revealed = ended && inset[0] == 0 && inset[1] == 0 && inset[2] == 0 && inset[3] == 0;
        composer.Show(visual);
    }

    public static void SetRadius(Visual visual, double radius, bool ended)
    {
        var composer = Begin(visual, ClipShorthand.ClipCircle);
        composer._radius = radius;
        composer._revealed = ended && radius >= 1;
        composer.Show(visual);
    }

    public static void SetCenter(Visual visual, RelativePoint center)
    {
        var composer = Begin(visual, ClipShorthand.ClipCircleCenter);
        composer._center = center;
        composer.Show(visual);
    }

    /// <summary>Releases one shorthand back to its identity; the last of them takes the whole clip with it.</summary>
    public static void Revert(Visual visual, ClipShorthand shorthand)
    {
        if (!s_composers.TryGetValue(visual, out var composer) || !composer._kinds.Animates(shorthand)) return;

        if (composer._kinds.Release(shorthand))
        {
            composer.Reset(shorthand);
            composer.Show(visual);
            return;
        }
        composer.Hide(visual);
        s_composers.Remove(visual);
    }

    private static ClipComposer Begin(Visual visual, ClipShorthand shorthand)
    {
        var composer = s_composers.GetValue(visual, static _ => new ClipComposer());
        if (!composer._kinds.Begin(shorthand)) return composer;

        composer.Hide(visual);
        if (composer._kinds.IsKindB) composer._ellipse ??= new EllipseGeometry();
        else composer._rectangle ??= new RectangleGeometry();
        for (var each = ClipShorthand.ClipInset; each <= ClipShorthand.ClipCircleCenter; each++) composer.Reset(each);
        return composer;
    }

    private void Reset(ClipShorthand shorthand)
    {
        _revealed = false;
        switch (shorthand)
        {
            case ClipShorthand.ClipInset:
                Array.Clear(_inset);
                _insetUnits = s_dips;
                break;
            case ClipShorthand.ClipCircle:
                _radius = 1;
                break;
            default:
                _center = RelativePoint.Center;
                break;
        }
    }

    private string FormatInset() =>
        $"{JsNumber.Format(_inset[0])}{_insetUnits[1]}{JsNumber.Format(_inset[1])}{_insetUnits[2]}{JsNumber.Format(_inset[2])}{_insetUnits[3]}{JsNumber.Format(_inset[3])}{_insetUnits[4]}";

    private void Show(Visual visual)
    {
        if (_revealed)
        {
            Hide(visual);
            return;
        }
        Layout(visual);
        if (_written != null) return;
        _written = visual.SetValue(Visual.ClipProperty, _kinds.IsKindB ? _ellipse : _rectangle, BindingPriority.Animation);
        visual.PropertyChanged += HandlePropertyChanged;
    }

    private void Hide(Visual visual)
    {
        if (_written == null) return;
        visual.PropertyChanged -= HandlePropertyChanged;
        _written.Dispose();
        _written = null;
    }

    private void Place(Visual visual)
    {
        var size = visual.Bounds.Size;
        if (_kinds.IsKindB)
        {
            var center = _center.ToPixels(size);
            var corner = new Vector(Math.Max(center.X, size.Width - center.X), Math.Max(center.Y, size.Height - center.Y));
            var radius = Math.Max(0, _radius) * corner.Length;
            _ellipse!.Rect = new Rect(center.X - radius, center.Y - radius, radius * 2, radius * 2);
            return;
        }
        var left = Side(0, size.Width);
        var top = Side(1, size.Height);
        _rectangle!.Rect = new Rect(left, top, Math.Max(0, size.Width - left - Side(2, size.Width)), Math.Max(0, size.Height - top - Side(3, size.Height)));
    }

    private void Layout(Visual visual)
    {
        Place(visual);
        // A clip mutated in place is not observed; the renderer re-reads Visual.Clip only when the visual is dirty.
        visual.InvalidateVisual();
    }

    private double Side(int channel, double extent) => ClipInsetCodec.IsPercent(_insetUnits, channel) ? _inset[channel] * extent / 100 : _inset[channel];

    private void HandlePropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == Visual.BoundsProperty) Layout((Visual)sender!);
    }
}
