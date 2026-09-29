using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
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

        // measured 80, down from 104 when the double was boxed into an untyped binding: all of it is the property change Avalonia raises.
        Assert.True(bytes <= 96, $"{bytes:0.##} bytes per tick");
    }

    [AvaloniaTheory]
    [InlineData("Rotate")]
    [InlineData("RotateY")]
    public void TransformShorthandTweenTicksWithinBudget(string rotation)
    {
        var border = new Border();
        border.Animate(new() { ["X"] = 100, [rotation] = 90, Duration = 400, Loop = true, Alternate = true });

        var bytes = BytesPerTick();

        // measured 160: the composer reuses its transforms, so this is 80 per shorthand for the property change each raises.
        Assert.True(bytes <= 192, $"{bytes:0.##} bytes per tick");
    }

    [AvaloniaTheory]
    [InlineData("Blur")]
    [InlineData("ShadowBlur")]
    public void EffectShorthandTweenTicksWithinBudget(string shorthand)
    {
        var border = new Border();
        border.Animate(new() { [shorthand] = new[] { 2, 8 }, Duration = 400, Loop = true, Alternate = true });

        var bytes = BytesPerTick();

        // measured 80: the composer mutates its one effect, so all of it is the property change the effect raises.
        Assert.True(bytes <= 96, $"{bytes:0.##} bytes per tick");
    }

    [AvaloniaFact]
    public void MaskSweepTicksWithinBudget()
    {
        var border = new Border();
        border.Animate(new() { ["MaskSweep"] = new[] { 0, 0.9 }, Duration = 400, Loop = true, Alternate = true });

        var bytes = BytesPerTick();

        // measured 224: the composer mutates its one brush, so this is 112 each for the StartPoint and EndPoint changes, whose args carry two RelativePoints.
        Assert.True(bytes <= 256, $"{bytes:0.##} bytes per tick");
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
    public void AHoverAllocatesOnlyAtItsTransitions()
    {
        var box = Box();
        Motion.SetHover(box, Presto.Build().Scale(1.1).Duration(100));
        var window = Show(box);
        Tick();

        var idle = BytesPerTick();
        // BytesPerTick's warm-up frames play each 100 ms transition out, so only the settled frames are measured.
        window.MouseMove(new Point(25, 25));
        var hovered = BytesPerTick();
        window.MouseMove(new Point(150, 150));
        var rested = BytesPerTick();

        Assert.True(idle == 0 && hovered == 0 && rested == 0,
            $"{idle:0.##} bytes per tick idle, {hovered:0.##} hovered, {rested:0.##} at rest");
    }

    [AvaloniaFact]
    public void AnIdleTiltAllocatesNothingPerFrame()
    {
        var box = Box();
        Tilt.SetEnabled(box, true);
        var window = Show(box);
        Tick();

        var idle = BytesPerTick();
        window.MouseMove(new Point(40, 10));
        Frames.Advance(2000, 16);
        var tilted = BytesPerTick();

        Assert.True(idle == 0 && tilted == 0, $"{idle:0.##} bytes per tick idle, {tilted:0.##} tilted and still");
    }

    /// <remarks>
    /// The scene's cost is what drawing with a tweened opacity adds to drawing the same dots opaque, measured in alternating
    /// blocks with the median difference kept: every window earlier tests left open is rendered with each frame too, which in
    /// a full run drifts the baseline of about 4.6 KB per frame by ±100 bytes, more than the budget.
    /// </remarks>
    [AvaloniaFact]
    public void CompositorSceneFramesWithinBudget()
    {
        const int blocks = 11;
        var (_, canvas) = ShowScene(new DotScene());
        // Scene time is the compositor's own clock, so warming up takes wall time: two alternate cycles fill the brush cache.
        var warmUp = System.Diagnostics.Stopwatch.StartNew();
        while (warmUp.ElapsedMilliseconds < 1000) Tick();

        var differences = new double[blocks];
        for (var i = 0; i < blocks; i++)
        {
            var animated = BytesPerFrame(canvas, animated: true);
            differences[i] = animated - BytesPerFrame(canvas, animated: false);
        }
        Array.Sort(differences);
        var bytes = differences[blocks / 2];

        // measured a median of 0 to 6 over twelve full runs, with single blocks ±80: once the brush cache is full, tweening costs nothing.
        Assert.True(bytes <= 64, $"{bytes:0.##} bytes per frame, median of {string.Join(", ", differences.Select(d => d.ToString("0")))}");
    }

    /// <summary>Bytes the render loop allocates per compositor frame while the scene draws its dots tweened or opaque.</summary>
    private static double BytesPerFrame(CompositorCanvas canvas, bool animated)
    {
        const int measured = 100;
        canvas.Post(animated);
        for (var i = 0; i < 10; i++) Tick();
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < measured; i++) Tick();
        return (GC.GetAllocatedBytesForCurrentThread() - before) / (double)measured;
    }

    /// <summary>A ring of dots whose opacity tweens; <see cref="OnMessage"/> switches between drawing that opacity and drawing them opaque.</summary>
    private sealed class DotScene : CompositorScene
    {
        private readonly SceneTarget[] _dots = [.. Enumerable.Range(0, 36).Select(_ => new SceneTarget())];
        private bool _animated = true;

        protected override void OnMessage(object message) => _animated = (bool)message;

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
                context.DrawEllipse(Brush(Colors.SteelBlue, _animated ? _dots[i].Opacity : 1), null, centre, 3, 3);
            }
        }
    }
}
