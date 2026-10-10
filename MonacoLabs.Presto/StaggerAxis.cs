namespace MonacoLabs.Presto;

/// <summary>The single grid axis a stagger measures along, instead of the distance to its origin.</summary>
public enum StaggerAxis
{
    /// <summary>Columns, signed so targets right of the origin take positive values.</summary>
    X,

    /// <summary>Rows, signed so targets below the origin take positive values.</summary>
    Y,

    /// <summary>Layers, signed so targets behind the origin take positive values.</summary>
    Z,
}
