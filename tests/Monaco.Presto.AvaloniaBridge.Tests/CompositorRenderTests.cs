using Avalonia;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Monaco.Presto.AvaloniaBridge.Compositor;

namespace Monaco.Presto.AvaloniaBridge.Tests;

/// <summary>Covers what a scene draws: the cached immutable brushes and the pixels the render loop puts on screen.</summary>
public sealed class CompositorRenderTests : CompositorTestBase
{
    [AvaloniaFact]
    public void TheBrushCacheReusesInstancesAndQuantizesOpacity()
    {
        var scene = new PixelScene(animate: false);

        Assert.Same(scene.BrushFor(Colors.Lime, 0.5), scene.BrushFor(Colors.Lime, 0.5));
        Assert.Same(scene.BrushFor(Colors.Lime, 0.5), scene.BrushFor(Colors.Lime, 0.5001));
        Assert.NotSame(scene.BrushFor(Colors.Lime, 0.5), scene.BrushFor(Colors.Lime, 0.6));
    }

    [AvaloniaFact]
    public void AnAnimatingSceneRedrawsItselfEveryFrame()
    {
        var scene = new PixelScene(animate: true);
        var (_, canvas) = ShowScene(scene);

        Assert.True(Pump(() => scene.X > 4), "the scene animation never advanced");
        var first = Capture(canvas);
        Assert.True(Pump(() => scene.X > 24), "the scene animation stopped advancing");

        Assert.NotEqual(first, Capture(canvas));
    }

    [AvaloniaFact]
    public void ASceneThatNeverInvalidatesLeavesThePixelsAlone()
    {
        var (_, canvas) = ShowScene(new PixelScene(animate: false));
        Tick();

        var first = Capture(canvas);
        Tick();
        Tick();

        Assert.True(Contains(first, Colors.Lime), "the scene never drew its rectangle");
        Assert.Equal(first, Capture(canvas));
    }

    [AvaloniaFact]
    public void InvalidateRedrawsAnIdleSceneAndNothingElseDoes()
    {
        var (_, canvas) = ShowScene(new FieldScene());
        Tick();
        var first = Capture(canvas);

        canvas.Post(new FieldScene.MoveTo(24, Redraw: false));
        Tick();
        Tick();
        var unredrawn = Capture(canvas);

        canvas.Post(new FieldScene.MoveTo(24, Redraw: true));
        Tick();
        Tick();

        Assert.True(Contains(first, Colors.Lime), "the scene never drew its rectangle");
        Assert.Equal(first, unredrawn);
        Assert.NotEqual(first, Capture(canvas));
    }

    private static bool Contains(byte[] pixels, Color color)
    {
        for (var i = 0; i + 3 < pixels.Length; i += 4)
        {
            if (pixels[i] == color.R && pixels[i + 1] == color.G && pixels[i + 2] == color.B) return true;
        }
        return false;
    }

    private sealed class PixelScene(bool animate) : CompositorScene
    {
        private readonly Mover _mover = new();

        public double X => _mover.X;

        public IImmutableBrush BrushFor(Color color, double opacity) => Brush(color, opacity);

        protected override void OnAttached()
        {
            if (animate) Presto.Animate(_mover, new() { ["X"] = 40, Duration = 2000, Ease = "linear" });
        }

        protected override void OnRender(ImmediateDrawingContext context, Size size) =>
            context.DrawRectangle(Brush(Colors.Lime), null, new Rect(_mover.X, 8, 20, 20));

        private sealed class Mover
        {
            public double X { get; set; }
        }
    }

    /// <summary>Draws from a field nothing animates, so the only thing that can repaint it is <c>Invalidate</c>.</summary>
    private sealed class FieldScene : CompositorScene
    {
        private double _x;

        protected override void OnMessage(object message)
        {
            if (message is not MoveTo move) return;
            _x = move.X;
            if (move.Redraw) Invalidate();
        }

        protected override void OnRender(ImmediateDrawingContext context, Size size) =>
            context.DrawRectangle(Brush(Colors.Lime), null, new Rect(_x, 8, 20, 20));

        internal sealed record MoveTo(double X, bool Redraw);
    }
}
