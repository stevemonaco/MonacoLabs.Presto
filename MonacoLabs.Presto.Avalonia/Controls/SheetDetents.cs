using System.Collections.ObjectModel;
using Avalonia.Controls;

namespace MonacoLabs.Presto.Controls;

/// <summary>
/// The extents a <see cref="Sheet"/> rests at, from closed to fully open: a star length is a fraction of the host's extent
/// along the sheet's axis, a pixel length is DIPs, and <c>Auto</c> is the content's own extent. XAML gives them as a
/// string, such as <c>"0,0.5,1"</c> or <c>"200,1*"</c>.
/// </summary>
public sealed class SheetDetents(IList<GridLength> detents) : ReadOnlyCollection<GridLength>(detents)
{
    /// <summary>Parses comma- or space-separated lengths, where a plain number up to 1 is a fraction, as <c>n*</c> is.</summary>
    public static SheetDetents Parse(string s) =>
        new(GridLength.ParseLengths(s).Select(d => d.IsAbsolute && d.Value <= 1 ? new GridLength(d.Value, GridUnitType.Star) : d).ToList());
}
