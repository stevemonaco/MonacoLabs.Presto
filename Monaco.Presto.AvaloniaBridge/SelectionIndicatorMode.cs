namespace Monaco.Presto.AvaloniaBridge;

/// <summary>Where <see cref="Selection.IndicatorProperty"/> draws against the selected item.</summary>
public enum SelectionIndicatorMode
{
    /// <summary>Behind the item, covering it.</summary>
    Fill,

    /// <summary>
    /// A bar along one edge of the item: the bottom edge in a horizontal list, the left edge in a vertical one, as thick as
    /// the indicator's own <c>Height</c> or <c>Width</c>.
    /// </summary>
    Underline,
}
