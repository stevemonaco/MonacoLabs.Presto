using Avalonia.Media;
using Monaco.Presto.Core;
using Monaco.Presto.Values;

namespace Monaco.Presto.AvaloniaBridge;

/// <summary>Converts between Presto colors (0-255 channels, 0-1 alpha) and Avalonia's byte channels.</summary>
public static class ColorExtensions
{
    /// <summary>Rounds the channels as Presto writes them, and the 0-1 alpha to the nearest byte.</summary>
    public static Color ToAvalonia(this PrestoColor color) =>
        Color.FromArgb(Channel(color.A * 255), Channel(color.R), Channel(color.G), Channel(color.B));

    public static PrestoColor ToPresto(this Color color) => new(color.R, color.G, color.B, color.A / 255d);

    private static byte Channel(double value) => (byte)Helpers.Clamp(Helpers.Round(value, 0), 0, 255);
}
