using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using PrestoSample.Pages;
using PrestoSample.Showcase;

namespace PrestoSample.Tests;

/// <summary>The Ripple page: buttons, focusable cards and list rows that ripple when pressed.</summary>
public sealed class RipplePageTests
{
    /// <remarks>Both points sit near the top edge, one beside the press and one at the far end the ink has not reached yet.</remarks>
    [AvaloniaTheory]
    [InlineData("Filled", false)]
    [InlineData("Filled", true)]
    [InlineData("AquaCard", false)]
    [InlineData("AquaCard", true)]
    [InlineData("Row", false)]
    [InlineData("Row", true)]
    public void PressingGrowsInkFromThePressPointOrOnlyTintsUnderReducedMotion(string target, bool reduced)
    {
        var page = new RipplePage();
        var window = SceneHarness.Show(page, 1280, 2000);
        using var motion = reduced ? ReducedMotion.On() : null;
        var host = target == "Row" ? page.GetControl<ListBox>("Rows").ContainerFromIndex(0)! : page.GetControl<Control>(target);
        var near = host.TranslatePoint(new Point(12, 6), window)!.Value;
        var far = host.TranslatePoint(new Point(host.Bounds.Width - 12, 6), window)!.Value;
        var idle = Pixel(SceneHarness.Capture(window), far);

        window.MouseDown(near, MouseButton.Left);
        TestFrames.Tick(3);
        var frame = SceneHarness.Capture(window);

        if (reduced)
        {
            Assert.NotEqual(idle, Pixel(frame, far));
            Assert.Equal(Pixel(frame, far), Pixel(frame, near));
        }
        else
        {
            Assert.NotEqual(Pixel(frame, far), Pixel(frame, near));
        }
        SceneHarness.Close(window);
    }

    [AvaloniaFact]
    public void TheRipplePagePrintsEverySnippet()
    {
        var page = new RipplePage();

        Assert.Contains(Snippets.RippleButtons, page.GetControl<CodePanel>("SurfacesCode").Code);
        Assert.Contains(Snippets.RippleCards, page.GetControl<CodePanel>("SurfacesCode").Code);
        Assert.Equal(Snippets.RippleList, page.GetControl<CodePanel>("ListCode").Code);
    }

    private static Color Pixel((byte[] Pixels, int Stride) frame, Point at)
    {
        var i = ((int)at.Y * frame.Stride) + ((int)at.X * 4);
        return Color.FromRgb(frame.Pixels[i], frame.Pixels[i + 1], frame.Pixels[i + 2]);
    }
}
