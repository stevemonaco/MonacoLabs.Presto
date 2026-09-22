namespace Monaco.Presto.Core;

internal static class Globals
{
    public static PrestoDefaults Defaults = new();
    public static Scope? CurrentScope;
    public static int Precision = 4;
    public static double TimeScale = 1.0;
    public static double TickThreshold = 200;
}
