namespace Monaco.Presto;

public static partial class Eases
{
    /// <summary>
    /// Cubic bezier ease through the control points (<paramref name="x1"/>, <paramref name="y1"/>) and
    /// (<paramref name="x2"/>, <paramref name="y2"/>), solved by bisection as in gre/bezier-easing.
    /// </summary>
    public static Ease CubicBezier(double x1 = .5, double y1 = 0, double x2 = .5, double y2 = 1) =>
        Named(x1 == y1 && x2 == y2 ? None : t => t == 0 || t == 1 ? t : CalcBezier(BinarySubdivide(t, x1, x2), y1, y2), "cubicBezier", x1, y1, x2, y2);

    private static double CalcBezier(double t, double a1, double a2) =>
        (((1 - 3 * a2 + 3 * a1) * t + (3 * a2 - 6 * a1)) * t + 3 * a1) * t;

    private static double BinarySubdivide(double x, double x1, double x2)
    {
        double a = 0, b = 1, currentX, currentT;
        var i = 0;
        do
        {
            currentT = a + (b - a) / 2;
            currentX = CalcBezier(currentT, x1, x2) - x;
            if (currentX > 0) b = currentT; else a = currentT;
        } while (Math.Abs(currentX) > .0000001 && ++i < 100);
        return currentT;
    }
}
