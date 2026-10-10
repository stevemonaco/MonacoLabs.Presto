namespace MonacoLabs.Presto.Controls;

/// <summary>How a <see cref="PagerIndicator"/> shows where its <see cref="Pager"/> is.</summary>
public enum PagerIndicatorMode
{
    /// <summary>A dot per slide; the selected one widens into a pill.</summary>
    Dots,

    /// <summary>A bar that fills as the pager moves through its slides.</summary>
    Progress,
}
