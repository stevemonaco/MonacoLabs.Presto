using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;

namespace Monaco.Presto.AvaloniaBridge.Tests;

/// <summary>Snapshots shown in place of live content draw at their real size on a scaled display.</summary>
public sealed class SnapshotScalingTests : BridgeTestBase
{
    private const double Scaling = 1.25;

    private static PixelRect Ink(Window window)
    {
        using var frame = window.CaptureRenderedFrame()!;
        using var locked = frame.Lock();
        var (width, height) = (frame.PixelSize.Width, frame.PixelSize.Height);
        var pixels = new byte[locked.RowBytes * height];
        Marshal.Copy(locked.Address, pixels, 0, pixels.Length);
        int left = width, top = height, right = -1, bottom = -1;
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var at = (y * locked.RowBytes) + (x * 4);
                if (pixels[at] + pixels[at + 1] + pixels[at + 2] > 300) continue;
                (left, top, right, bottom) = (Math.Min(left, x), Math.Min(top, y), Math.Max(right, x), Math.Max(bottom, y));
            }
        }
        return new PixelRect(left, top, right - left + 1, bottom - top + 1);
    }

    private static void AssertSameInk(PixelRect expected, PixelRect actual)
    {
        Assert.InRange(actual.X, expected.X - 1, expected.X + 1);
        Assert.InRange(actual.Y, expected.Y - 1, expected.Y + 1);
        Assert.InRange(actual.Right, expected.Right - 1, expected.Right + 1);
        Assert.InRange(actual.Bottom, expected.Bottom - 1, expected.Bottom + 1);
    }

    private static Window ShowScaled(Control content)
    {
        var window = new Window { Content = content, Width = 300, Height = 120, Background = Brushes.White };
        window.SetRenderScaling(Scaling);
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return window;
    }

    [AvaloniaTheory]
    [InlineData(TextPieces.None)]
    [InlineData(TextPieces.Chars)]
    public void AnExitGhostDrawsWhereAndAsLargeAsTheElementDid(TextPieces pieces)
    {
        var block = new TextBlock { Text = "layouts", FontSize = 40, Foreground = Brushes.Black, [TextReveal.PiecesProperty] = pieces };
        Presence.SetExit(block, Presto.Build().Opacity(to: 0).Duration(100000).Ease("linear"));
        var window = ShowScaled(new StackPanel { Children = { block } });
        var live = Ink(window);

        ((Panel)window.Content!).Children.Remove(block);
        Dispatcher.UIThread.RunJobs();

        AssertSameInk(live, Ink(window));
    }

    [AvaloniaFact]
    public void AThemeRevealStartsFromThePictureThatWasOnScreen()
    {
        var content = new Border { Background = Brushes.White, Child = new TextBlock { Text = "theme", FontSize = 40, Foreground = Brushes.Black } };
        var window = ShowScaled(content);
        var live = Ink(window);

        Recipes.ThemeReveal(window, () => ((TextBlock)content.Child!).Foreground = Brushes.White, duration: 100000);
        Dispatcher.UIThread.RunJobs();

        AssertSameInk(live, Ink(window));
    }
}
