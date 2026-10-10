using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Input.GestureRecognizers;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Rendering.Composition;

namespace MonacoLabs.Presto.Tests;

/// <summary>
/// What the Phase 62 spike (GAPS-2-PLAN §8.4) found in Avalonia 12.1.3, kept as pinning tests: how a custom visual is
/// attached to and clipped by its host, and what a touch drag that starts a scroll does to a pressed child.
/// </summary>
public sealed class RippleSpikeTests : CompositorTestBase
{
    private static readonly IImmutableBrush Red = new ImmutableSolidColorBrush(Colors.Red);

    /// <remarks>
    /// <c>ElementComposition.SetElementChildVisual</c> takes any visual, so a ripple needs no layer control of its own; the
    /// child visual is composed after the host's visual children.
    /// </remarks>
    [AvaloniaFact]
    public void AnElementChildVisualDrawsOverTheHostsChildren()
    {
        var host = new Border { Width = 60, Height = 40, Child = new Border { Background = Brushes.Lime }, HorizontalAlignment = HorizontalAlignment.Left };
        ShowWhite(host);
        var visual = ElementComposition.GetElementVisual(host)!.Compositor.CreateCustomVisual(new FillHandler());
        visual.Size = new Vector(60, 40);
        ElementComposition.SetElementChildVisual(host, visual);
        Run(2);

        AssertNear(Colors.Red, Row(host, 20)[30]);
    }

    /// <remarks>
    /// A custom visual starts with <c>ClipToBounds</c> on, and unclipped it is still drawn only within
    /// <c>GetRenderBounds</c>, which defaults to its size: an unbounded ripple needs both.
    /// </remarks>
    [AvaloniaTheory]
    [InlineData(true, false, false)]
    [InlineData(false, false, false)]
    [InlineData(false, true, true)]
    public void AnElementChildVisualDrawsOutsideItsSizeOnlyUnclippedAndWithinWidenedRenderBounds(bool clips, bool widened, bool spills)
    {
        var host = new Border { Width = 20, Height = 20, Margin = new Thickness(20), HorizontalAlignment = HorizontalAlignment.Left };
        ShowWhite(host);
        var visual = ElementComposition.GetElementVisual(host)!.Compositor.CreateCustomVisual(new FillHandler { Spill = 10, Widened = widened });
        Assert.True(visual.ClipToBounds);
        visual.Size = new Vector(20, 20);
        visual.ClipToBounds = clips;
        ElementComposition.SetElementChildVisual(host, visual);
        Run(2);

        AssertNear(Colors.Red, Row(host, 30)[30]);
        AssertNear(spills ? Colors.Red : Colors.White, Row(host, 30)[15]);
    }

    /// <remarks>
    /// <c>TouchDevice</c> captures a touch to the element it lands on, which the test does by hand. Once the recognizer holds
    /// the pointer, the device hands every later move and the release to it alone, so the child sees no
    /// <c>PointerReleased</c>; <see cref="Touch"/> raises them as routed events, so the test stops at the capture.
    /// </remarks>
    [AvaloniaFact]
    public void ATouchDragThatStartsAScrollTakesThePointerFromThePressedChild()
    {
        var row = new Border { Height = 40, Background = Brushes.Red };
        var scroller = new ScrollViewer { Height = 100, VerticalAlignment = VerticalAlignment.Top, Content = new StackPanel { Children = { row, new Border { Height = 400 } } } };
        var window = Show(scroller);
        var events = new List<string>();
        row.PointerPressed += (_, _) => events.Add("pressed");
        row.PointerMoved += (_, _) => events.Add("moved");
        row.PointerCaptureLost += (_, _) => events.Add("capture lost");
        row.PointerReleased += (_, _) => events.Add("released");
        var touch = new Touch(window);

        var pointer = touch.Down(1, new Point(50, 30)).Pointer;
        pointer.Capture(row);
        touch.Move(1, new Point(50, 28));
        Assert.Same(row, pointer.Captured);
        touch.Move(1, new Point(50, 24));

        Assert.Equal(["pressed", "moved", "moved", "capture lost"], events);
        Assert.Null(pointer.Captured);
        var recognizer = typeof(Avalonia.Input.Pointer).GetProperty("CapturedGestureRecognizer", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(pointer);
        Assert.IsType<ScrollGestureRecognizer>(recognizer);
    }

    private sealed class FillHandler : CompositionCustomVisualHandler
    {
        public double Spill { get; init; }

        public bool Widened { get; init; }

        public override void OnRender(ImmediateDrawingContext context) => context.FillRectangle(Red, Painted, 0);

        public override Rect GetRenderBounds() => Widened ? Painted : base.GetRenderBounds();

        private Rect Painted => new Rect(0, 0, EffectiveSize.X, EffectiveSize.Y).Inflate(Spill);
    }
}
