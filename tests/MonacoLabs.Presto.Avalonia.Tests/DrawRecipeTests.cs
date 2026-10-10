using Avalonia.Headless.XUnit;
using Avalonia.Media;
using MonacoLabs.Presto.Prebuilt;
using Path = Avalonia.Controls.Shapes.Path;

namespace MonacoLabs.Presto.Tests;

/// <summary><see cref="Recipes.DrawIn"/>, <see cref="Recipes.DrawOut"/> and the marks they draw (GAPS-2-PLAN §10.2 65b).</summary>
public sealed class DrawRecipeTests : BridgeTestBase
{
    private static double[] Drawn(Path path) => (double[])PrestoAvalonia.Get(path, "Draw")!;

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void DrawInEndsDrawnAndDrawOutErased(bool reduced)
    {
        Presto.Defaults.ReducedMotion = reduced;
        var path = new Path { Data = Draws.Check, Stroke = Brushes.Black, StrokeThickness = 2 };
        Show(path);
        PrestoAvalonia.CreateDrawable(path);

        path.Animate(Recipes.DrawIn());
        Frames.Advance(16);
        if (reduced) Assert.Equal([0, 1], Drawn(path));
        else Assert.InRange(Drawn(path)[1], 0, .5);
        Frames.Advance(Recipes.Tokens.Medium, 16);
        Assert.Equal([0, 1], Drawn(path));

        path.Animate(Recipes.DrawOut());
        Frames.Advance(16);
        if (reduced) Assert.Equal([1, 1], Drawn(path));
        else Assert.InRange(Drawn(path)[0], 0, .5);
        Frames.Advance(Recipes.Tokens.Medium, 16);
        Assert.Equal([1, 1], Drawn(path));
    }

    [AvaloniaFact]
    public void TheMarksAreFreshGeometriesInATwentyUnitBox()
    {
        Assert.NotSame(Draws.Check, Draws.Check);
        Assert.All([Draws.Check.Bounds, Draws.Cross.Bounds], bounds => Assert.True(bounds.Right <= 20 && bounds.Bottom <= 20));
    }
}
