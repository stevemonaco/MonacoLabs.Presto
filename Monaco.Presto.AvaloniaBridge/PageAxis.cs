namespace Monaco.Presto.AvaloniaBridge;

/// <summary>The axis <see cref="Recipes.PageSharedAxis"/> moves pages along.</summary>
public enum PageAxis
{
    /// <summary>Horizontally: the incoming page arrives from the right.</summary>
    X,

    /// <summary>Vertically: the incoming page arrives from below.</summary>
    Y,

    /// <summary>In depth: the incoming page grows into place and the outgoing one grows away.</summary>
    Z,
}
