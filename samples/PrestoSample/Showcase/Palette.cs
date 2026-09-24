using Avalonia.Media;
using Avalonia.Media.Immutable;

namespace PrestoSample.Showcase;

/// <summary>
/// The gallery's colour literals as brushes for controls; a compositor scene, which cannot resolve
/// <c>StaticResource</c>, reads the <see cref="ISolidColorBrush.Color"/> off one.
/// </summary>
public static class Palette
{
    public static readonly ImmutableSolidColorBrush Background = new(Color.FromRgb(0x0B, 0x0E, 0x14));
    public static readonly ImmutableSolidColorBrush Surface = new(Color.FromRgb(0x14, 0x1A, 0x24));
    public static readonly ImmutableSolidColorBrush SurfaceHigh = new(Color.FromRgb(0x1D, 0x25, 0x31));
    public static readonly ImmutableSolidColorBrush Border = new(Color.FromRgb(0x2A, 0x34, 0x42));
    public static readonly ImmutableSolidColorBrush Ink = new(Color.FromRgb(0xE6, 0xEC, 0xF5));
    public static readonly ImmutableSolidColorBrush InkDim = new(Color.FromRgb(0x9A, 0xA8, 0xBF));
    public static readonly ImmutableSolidColorBrush AccentDeep = new(Color.FromRgb(0x16, 0x30, 0x5E));
    public static readonly ImmutableSolidColorBrush AccentDim = new(Color.FromRgb(0x2B, 0x57, 0x9F));
    public static readonly ImmutableSolidColorBrush Accent = new(Color.FromRgb(0x4C, 0x8D, 0xFF));
    public static readonly ImmutableSolidColorBrush AccentBright = new(Color.FromRgb(0x82, 0xB4, 0xFF));
    public static readonly ImmutableSolidColorBrush AccentPale = new(Color.FromRgb(0xC3, 0xDB, 0xFF));
    public static readonly ImmutableSolidColorBrush Aqua = new(Color.FromRgb(0x2D, 0xD4, 0xBF));
    public static readonly ImmutableSolidColorBrush Coral = new(Color.FromRgb(0xFF, 0x6B, 0x7A));

    /// <summary>The accent hue from deepest to palest, for staggering a set of visuals across the ramp.</summary>
    public static readonly IReadOnlyList<ImmutableSolidColorBrush> AccentBrushRamp =
        [AccentDeep, AccentDim, Accent, AccentBright, AccentPale];

    /// <summary><see cref="AccentBrushRamp"/> as colours, for a scene that draws its own brushes.</summary>
    public static readonly IReadOnlyList<Color> AccentRamp = [.. AccentBrushRamp.Select(brush => brush.Color)];
}
