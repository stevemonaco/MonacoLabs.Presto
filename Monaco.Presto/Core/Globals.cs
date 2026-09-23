namespace Monaco.Presto.Core;

/// <summary>Upstream <c>globals</c> that stay process-wide; set them before creating a second engine (COMPOSITOR-PLAN §4.1).</summary>
internal static class Globals
{
    public static int Precision = 4;
    public static double TimeScale = 1.0;
    public static double TickThreshold = 200;
}
