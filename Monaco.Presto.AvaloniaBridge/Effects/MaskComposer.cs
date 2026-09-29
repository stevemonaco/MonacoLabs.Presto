using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Data;
using Avalonia.Media;
using Monaco.Presto.Values;

namespace Monaco.Presto.AvaloniaBridge.Effects;

/// <summary>
/// The mask the <c>MaskSweep</c> reveal animates, one per <see cref="Visual"/>: a bridge-owned <see cref="LinearGradientBrush"/>,
/// opaque behind a soft edge and transparent ahead of it, written to <see cref="Visual.OpacityMask"/> at
/// <see cref="BindingPriority.Animation"/> and mutated in place, so releasing the last shorthand uncovers the visual's own mask.
/// </summary>
/// <remarks>
/// MOTION-KIT §3.2 gives <c>MaskSweep</c> a progress, an angle and a feather, which one scalar cannot carry, so
/// <c>MaskSweep</c> is the progress (0 hidden, 1 shown) and the companions <c>MaskSweepAngle</c> (degrees, 0 sweeping left to
/// right) and <c>MaskSweepFeather</c> (the edge's width as a fraction of the sweep, default 0.2) tune it. The edge is placed
/// by the brush's start and end points over fixed stops, since Skia pins stop offsets to 0-1 and the edge runs past both
/// ends. A sweep that ends at 1 is removed.
/// </remarks>
internal sealed class MaskComposer
{
    private const double DefaultFeather = 0.2;
    private static readonly ConditionalWeakTable<Visual, MaskComposer> s_composers = new();

    private readonly LinearGradientBrush _brush = new() { GradientStops = { new GradientStop(Colors.Black, 0), new GradientStop(Colors.Transparent, 1) } };
    private double _progress = 1;
    private double _angle;
    private double _feather = DefaultFeather;
    private IDisposable? _written;
    private bool _revealed;
    private int _animated;

    public static object Identity(MaskShorthand shorthand) => shorthand switch
    {
        MaskShorthand.MaskSweep => 1d,
        MaskShorthand.MaskSweepAngle => 0d,
        _ => DefaultFeather,
    };

    public static object Get(Visual visual, MaskShorthand shorthand)
    {
        if (!s_composers.TryGetValue(visual, out var composer) || !composer.Animates(shorthand)) return Identity(shorthand);
        return shorthand switch
        {
            MaskShorthand.MaskSweep => composer._progress,
            // Read back with the unit so a "1turn" end value converts instead of being taken as 360 turns.
            MaskShorthand.MaskSweepAngle => JsNumber.Format(composer._angle) + "deg",
            _ => composer._feather,
        };
    }

    /// <summary>Writes one frame of <paramref name="shorthand"/>; <paramref name="ended"/> says whether the tween has reached its end.</summary>
    public static void Set(Visual visual, MaskShorthand shorthand, double value, bool ended)
    {
        var composer = s_composers.GetValue(visual, static _ => new MaskComposer());
        composer._animated |= 1 << (int)shorthand;
        composer.Assign(shorthand, value, ended);
        composer.Show(visual);
    }

    /// <summary>Releases one shorthand back to its identity; the last of them takes the whole mask with it.</summary>
    public static void Revert(Visual visual, MaskShorthand shorthand)
    {
        if (!s_composers.TryGetValue(visual, out var composer) || !composer.Animates(shorthand)) return;

        composer._animated &= ~(1 << (int)shorthand);
        if (composer._animated != 0)
        {
            composer.Assign(shorthand, (double)Identity(shorthand), ended: false);
            composer.Show(visual);
            return;
        }
        composer.Hide();
        s_composers.Remove(visual);
    }

    private bool Animates(MaskShorthand shorthand) => (_animated & (1 << (int)shorthand)) != 0;

    private void Assign(MaskShorthand shorthand, double value, bool ended)
    {
        switch (shorthand)
        {
            case MaskShorthand.MaskSweep:
                _progress = value;
                _revealed = ended && value >= 1;
                break;
            case MaskShorthand.MaskSweepAngle:
                _angle = value;
                break;
            default:
                _feather = value;
                break;
        }
    }

    private void Show(Visual visual)
    {
        if (_revealed)
        {
            Hide();
            return;
        }
        Layout();
        _written ??= visual.SetValue(Visual.OpacityMaskProperty, _brush, BindingPriority.Animation);
        // A mask mutated in place is not observed; the renderer re-reads Visual.OpacityMask only when the visual is dirty.
        visual.InvalidateVisual();
    }

    private void Hide()
    {
        _written?.Dispose();
        _written = null;
    }

    private void Layout()
    {
        // A zero-width edge would make start and end coincide, a degenerate gradient.
        var feather = Math.Max(_feather, 1e-3);
        var edge = _progress * (1 + feather) - feather / 2;
        var (sin, cos) = Math.SinCos(_angle * Math.PI / 180);
        // The visual's extent along the angled axis, in relative units.
        var extent = Math.Abs(cos) + Math.Abs(sin);
        _brush.StartPoint = At(edge - feather / 2);
        _brush.EndPoint = At(edge + feather / 2);

        RelativePoint At(double along) => new(0.5 + cos * (along - 0.5) * extent, 0.5 + sin * (along - 0.5) * extent, RelativeUnit.Relative);
    }
}
