using Avalonia;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Monaco.Presto.AvaloniaBridge.Compositor;
using PrestoSample.Showcase;

namespace PrestoSample.Tests;

/// <summary>
/// The SAMPLES-PLAN §7.1 spike: glyph runs cached on the UI thread and drawn by a scene through
/// <c>ImmediateDrawingContext.DrawGlyphRun</c> really rasterise, and follow the string the scene holds.
/// </summary>
public sealed class SceneTextTests
{
    private const double FontSize = 20;

    private static readonly Point Origin = new(8, 6);

    [AvaloniaFact]
    public async Task GlyphsLandInsideTheMeasuredBoxAndAnEmptyStringLeavesItUntouched()
    {
        TestFrames.Install();
        const string Text = "1024 fps";

        var (drawn, drawnScene) = ShowScene(Text);
        var (blank, blankScene) = ShowScene("");
        Assert.True(await SceneHarness.Pump(() => drawnScene.Renders > 0 && blankScene.Renders > 0), "the scenes never rendered");

        var metrics = new SceneText(FontSize);
        var box = new Rect(Origin, new Size(metrics.Measure(Text), metrics.Height));

        Assert.True(SceneHarness.CountPixels(drawn, box, Colors.Black) > 0, "SceneText drew no glyph pixels");
        Assert.Equal(
            SceneHarness.CountPixels(drawn, new Rect(drawn.Bounds.Size), Colors.Black),
            SceneHarness.CountPixels(drawn, box.Inflate(1), Colors.Black));
        Assert.Equal(0, SceneHarness.CountPixels(blank, box, Colors.Black));
    }

    [AvaloniaFact]
    public async Task TheReadoutFollowsTheStringTheSceneHolds()
    {
        TestFrames.Install();

        var (canvas, scene) = ShowScene("0 fps");
        Assert.True(await SceneHarness.Pump(() => scene.Renders > 0), "the scene never rendered");
        var first = SceneHarness.Capture(canvas).Pixels;

        canvas.Post("240 fps");

        Assert.True(
            await SceneHarness.Pump(() => !SceneHarness.Capture(canvas).Pixels.AsSpan().SequenceEqual(first)),
            "the readout never changed");
    }

    private static (CompositorCanvas Canvas, TextScene Scene) ShowScene(string text)
    {
        var scene = new TextScene(text);
        var canvas = new CompositorCanvas { Scene = scene, Width = 220, Height = 48 };
        SceneHarness.Show(canvas);
        return (canvas, scene);
    }

    private sealed class TextScene(string text) : CompositorScene
    {
        private readonly SceneText _text = new(FontSize);

        private string _value = text;

        public int Renders { get; private set; }

        protected override void OnMessage(object message)
        {
            _value = (string)message;
            Invalidate();
        }

        protected override void OnRender(ImmediateDrawingContext context, Size size)
        {
            context.FillRectangle(Brush(Colors.Black), new Rect(size), 0);
            _text.Draw(context, _value, Origin, Brush(Colors.Lime));
            Renders++;
        }
    }
}
