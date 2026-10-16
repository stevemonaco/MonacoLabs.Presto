using System.Globalization;

namespace MonacoLabs.Presto.Controls;

/// <summary>
/// How <see cref="FilteredImage"/> swaps its source: the kind of transition, its timing and the kind's tuning. XAML can give it as a
/// string, a kind and an optional duration in ms such as <c>"Dissolve"</c> or <c>"Wipe 800"</c>, or as an element with properties.
/// </summary>
public sealed record ImageTransition
{
    /// <summary>The kind of transition; defaults to <see cref="ImageTransitionKind.Crossfade"/>.</summary>
    public ImageTransitionKind Kind { get; init; }

    /// <summary>The timing (duration, ease, delay); null plays 600 ms with <see cref="Eases.InOutQuad"/>.</summary>
    public AnimationBuilder? Builder { get; init; }

    /// <summary>The <see cref="ImageTransitionKind.Wipe"/> direction in degrees, 0 sweeping left to right, as <c>MaskSweepAngle</c>.</summary>
    public double Angle { get; init; }

    /// <summary>The width of the <see cref="ImageTransitionKind.Wipe"/> edge as a fraction of the sweep; defaults to 0.2.</summary>
    public double Feather { get; init; } = 0.2;

    /// <summary>The block size in DIPs <see cref="ImageTransitionKind.Pixelate"/> reaches mid-way; defaults to 24.</summary>
    public double PeakPixelate { get; init; } = 24;

    /// <summary>The displacement in DIPs <see cref="ImageTransitionKind.Ripple"/> reaches mid-way; defaults to 20.</summary>
    public double PeakRipple { get; init; } = 20;

    internal AnimationBuilder Timing => Builder ?? Eased(600);

    /// <summary>Parses a kind and an optional duration in ms, such as <c>"Wipe 800"</c>, eased with <see cref="Eases.InOutQuad"/>.</summary>
    /// <exception cref="FormatException"><paramref name="s"/> is not a kind name optionally followed by a number.</exception>
    public static ImageTransition Parse(string s)
    {
        var parts = s.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        // Enum.TryParse also takes numbers and comma lists; only a kind's own name is accepted.
        if (parts.Length is 0 or > 2 || !Enum.TryParse<ImageTransitionKind>(parts[0], true, out var kind)
            || !kind.ToString().Equals(parts[0], StringComparison.OrdinalIgnoreCase))
            throw new FormatException($"\"{s}\" is not an image transition kind optionally followed by a duration in ms.");
        return parts.Length == 1 ? new() { Kind = kind } : new() { Kind = kind, Builder = Eased(double.Parse(parts[1], CultureInfo.InvariantCulture)) };
    }

    private static AnimationBuilder Eased(double duration) => Presto.Build().Duration(duration).Ease(Eases.InOutQuad);
}
