using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Monaco.Presto.AvaloniaBridge.Compositor;
using Path = Avalonia.Controls.Shapes.Path;

namespace Monaco.Presto.AvaloniaBridge.Tests;

/// <summary>
/// Per-frame allocation budget of the bridge write paths, the counterpart of <see cref="Tests.AllocationTests"/>. What is
/// left in each budget is Avalonia's own property system: a bare <c>border.Opacity = x</c> costs 80 bytes on its own.
/// </summary>
public sealed class AllocationTests : CompositorTestBase
{
    [AvaloniaFact]
    public void StyledPropertyTweenTicksWithinBudget()
    {
        var border = new Border { Opacity = 0 };
        border.Animate(new() { ["Opacity"] = 1, Duration = 400, Loop = true, Alternate = true });

        var bytes = BytesPerTick();

        // measured 104: 80 for the property change Avalonia raises, 24 for the double boxed into the untyped binding.
        Assert.True(bytes <= 128, $"{bytes:0.##} bytes per tick");
    }

    [AvaloniaFact]
    public void TransformShorthandTweenTicksWithinBudget()
    {
        var border = new Border();
        border.Animate(new() { ["X"] = 100, ["Rotate"] = 90, Duration = 400, Loop = true, Alternate = true });

        var bytes = BytesPerTick();

        // measured 160: the composer reuses its transforms, so this is 80 per shorthand for the property change each raises.
        Assert.True(bytes <= 192, $"{bytes:0.##} bytes per tick");
    }

    [AvaloniaFact]
    public void BrushTweenTicksWithinBudget()
    {
        var border = new Border { Background = new SolidColorBrush(Colors.Red) };
        border.Animate(new() { ["Background"] = "#0000ff", Duration = 400, Loop = true, Alternate = true });

        var bytes = BytesPerTick();

        // measured 64: recoloring the brush the codec reuses; pushing that same brush again costs nothing.
        Assert.True(bytes <= 96, $"{bytes:0.##} bytes per tick");
    }

    [AvaloniaFact]
    public void DrawTweenTicksWithinBudget()
    {
        var path = new Path
        {
            Data = Geometry.Parse("M 0,0 L 100,0 L 100,100"),
            Stretch = Stretch.None,
            Stroke = Brushes.Black,
            StrokeThickness = 2,
        };
        PrestoAvalonia.CreateDrawable(path).Animate(new() { ["Draw"] = "0 1", Duration = 400, Loop = true, Alternate = true });

        var bytes = BytesPerTick();

        // measured 288, down from 944 for a fresh list per frame: 144 per dash written into the reused
        // AvaloniaList, which is the change args and the two boxed doubles its render subscription is handed.
        Assert.True(bytes <= 320, $"{bytes:0.##} bytes per tick");
    }

    [AvaloniaFact]
    public void CompositorSceneFramesWithinBudget()
    {
        var (_, canvas) = ShowScene(new DotScene(animated: true));
        var animated = BytesPerFrame();
        canvas.Scene = new DotScene(animated: false);
        Tick();

        var bytes = animated - BytesPerFrame();

        // measured 6 to 22: the twin differenced out carries the same tick, the same Invalidate and the same ellipses,
        // so what is left is the brush cache, which stops allocating once the tween has visited its 256 opacity steps.
        Assert.True(bytes <= 64, $"{bytes:0.##} bytes per frame");
    }

    /// <summary>Bytes the render loop allocates per compositor frame, measured once the scene has been drawing for a while.</summary>
    private static double BytesPerFrame()
    {
        const int measured = 1000;
        for (var i = 0; i < 500; i++) Tick();
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < measured; i++) Tick();
        return (GC.GetAllocatedBytesForCurrentThread() - before) / (double)measured;
    }

    private sealed class DotScene(bool animated) : CompositorScene
    {
        private readonly SceneTarget[] _dots = [.. Enumerable.Range(0, 36).Select(_ => new SceneTarget())];

        protected override void OnAttached() =>
            Presto.Animate(_dots, new()
            {
                ["Opacity"] = 0.15,
                Duration = 400,
                Delay = Presto.Stagger(8),
                Ease = Eases.InOutSine,
                Loop = true,
                Alternate = true,
            });

        protected override void OnRender(ImmediateDrawingContext context, Size size)
        {
            for (var i = 0; i < _dots.Length; i++)
            {
                var angle = i * Math.Tau / _dots.Length;
                var centre = new Point(size.Width / 2 + Math.Cos(angle) * 18, size.Height / 2 + Math.Sin(angle) * 18);
                context.DrawEllipse(Brush(Colors.SteelBlue, animated ? _dots[i].Opacity : 1), null, centre, 3, 3);
            }
        }
    }
}
