using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Platform;
using Avalonia.VisualTree;
using MonacoLabs.Presto;
using MonacoLabs.Presto.Compositor;
using MonacoLabs.Presto.Tests;
using PrestoGallery.Showcase;

namespace PrestoGallery.Tests;

/// <summary>
/// Shows headless content and reads back the frames it rendered, so a test can say where a control was drawn
/// rather than where layout put it. The bridge's own
/// <c>CompositorTestBase</c> has the same helpers; they are copied because that base class belongs to the
/// bridge's test project and drags its fixture along.
/// </summary>
internal static class SceneHarness
{
    /// <summary>
    /// Points the engine at the test frame source, shows <paramref name="content"/> in a window of the given size and
    /// ticks, which is when the first layout pass and the batched compositor attach reach the screen.
    /// </summary>
    public static Window Show(Control content, double width, double height, int frames = 1)
    {
        TestFrames.Install();
        var window = new Window { Content = Hosted(content, height), Width = width, Height = height };
        window.Show();
        TestFrames.Tick(frames);
        return window;
    }

    // A gallery page sits as MainView hosts it: in a vertical scroller and at least the viewport tall, unless it fills the viewport.
    private static Control Hosted(Control content, double height)
    {
        if (content is not UserControl || content.GetType().Namespace is not ("PrestoGallery.Pages" or "PrestoGallery.Builder" or "PrestoGallery.Showcase.Pages"))
            return content;
        if (content is ShowcasePage { FillsViewport: true }) return content;
        content.MinHeight = height;
        return new ScrollViewer { Content = content, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
    }

    /// <inheritdoc cref="Show(Control, double, double, int)"/>
    public static Window Show(CompositorCanvas canvas) => Show(canvas, canvas.Width, canvas.Height);

    public static void Close(Window window)
    {
        window.Content = null;
        window.Close();
    }

    /// <summary>Clicks the center of <paramref name="target"/> with real pointer input and ticks the frame it schedules.</summary>
    public static void Click(Window window, Visual target)
    {
        var center = Center(target, window);
        window.MouseDown(center, MouseButton.Left);
        window.MouseUp(center, MouseButton.Left);
        TestFrames.Tick();
    }

    /// <summary>Ticks until <paramref name="until"/> holds or <paramref name="rounds"/> run out; it never waits, as each tick moves both clocks.</summary>
    public static Task<bool> Pump(Func<bool> until, int rounds = 60)
    {
        for (var i = 0; i < rounds && !until(); i++) TestFrames.Tick();
        return Task.FromResult(until());
    }

    /// <summary>
    /// Ticks a frame per 16 ms of wall-clock time until <paramref name="until"/> holds or 5 s pass,
    /// for a flow that waits on <see cref="Task.Delay(int)"/>, which test frames do not advance.
    /// </summary>
    public static bool WaitFor(Func<bool> until)
    {
        var clock = Stopwatch.StartNew();
        while (!until() && clock.ElapsedMilliseconds < 5000)
        {
            Thread.Sleep(16);
            TestFrames.Tick();
        }
        return until();
    }

    /// <summary>
    /// Ticks at least once, then until no engine or compositor animation is running, which is the only way to wait for every
    /// card of a reflow at once; <paramref name="rounds"/> caps it, for a page that never comes to rest.
    /// </summary>
    public static Task Settle(int rounds = 120)
    {
        TestFrames.Tick();
        return Pump(Idle, rounds - 1);
    }

    private static bool Idle() => !Presto.Engine.IsRunning && !CompositorClock.IsAnimating();

    /// <summary>Ticks until <paramref name="card"/> is drawn in the slot layout has given it, which is both its entry and its reflow finishing.</summary>
    public static Task<bool> Settles(Visual page, Window window, Border card) =>
        Pump(() => Extent(page, CardColor(card)) is { } box && Near(box, Slot(card, window)), rounds: 140);

    /// <summary>The overlay canvases the particle recipes draw into over <paramref name="visual"/>'s window.</summary>
    public static CompositorCanvas[] ParticleOverlays(Visual visual) =>
        OverlayLayer.GetOverlayLayer(visual)!.Children.OfType<CompositorCanvas>().ToArray();

    /// <summary>The overlay hosts Presence draws exit ghosts into over <paramref name="visual"/>'s window.</summary>
    public static IEnumerable<Canvas> Ghosts(Visual visual) => OverlayLayer.GetOverlayLayer(visual)!.Children.OfType<Canvas>();

    /// <summary>Each exit ghost's delay, keyed by the item it stands in for.</summary>
    public static Dictionary<T, double> GhostDelays<T>(Visual page) where T : notnull =>
        Ghosts(page).SelectMany(host => host.GetVisualDescendants().OfType<Image>())
            .Where(ghost => ghost.DataContext is T).ToDictionary(ghost => (T)ghost.DataContext!, ghost => Assert.Single(Presto.GetAnimations(ghost)).Delay);

    /// <summary>The number a readout is showing.</summary>
    public static double Number(TextBlock text) => double.Parse(text.Text!, CultureInfo.InvariantCulture);

    /// <summary>Asserts <paramref name="block"/> shows <paramref name="text"/> unsplit, as a text recipe leaves it when it ends.</summary>
    public static void AssertPlain(TextBlock block, string text)
    {
        Assert.Equal(text, block.Text);
        Assert.True(block.Inlines is null or [], $"{block.Name} is still split");
    }

    public static IEnumerable<Border> Cards(Visual page) =>
        page.GetLogicalDescendants().OfType<Border>().Where(border => border.Classes.Contains("card"));

    /// <summary>The RGBA pixels of the last frame the window holding <paramref name="visual"/> rendered, with the row stride.</summary>
    public static (byte[] Pixels, int Stride) Capture(Visual visual)
    {
        var window = TopLevel.GetTopLevel(visual)!;
        if (HeadlessDrawing.Resume()) AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        using var frame = window.GetLastRenderedFrame() ?? throw new InvalidOperationException("The window has not rendered yet.");
        using var locked = frame.Lock();
        var pixels = new byte[locked.RowBytes * locked.Size.Height];
        Marshal.Copy(locked.Address, pixels, 0, pixels.Length);
        for (var i = 0; i < pixels.Length; i += 4) (pixels[i], pixels[i + 2]) = (pixels[i + 2], pixels[i]);
        return (pixels, locked.RowBytes);
    }

    /// <summary>The box <paramref name="color"/> fills in the last frame, in the window's coordinates, or null when nothing holds it.</summary>
    public static Rect? Extent(Visual visual, Color color)
    {
        var (pixels, stride) = Capture(visual);
        double left = int.MaxValue, top = int.MaxValue, right = -1, bottom = -1;
        for (var y = 0; y < pixels.Length / stride; y++)
        {
            for (var x = 0; (x * 4) + 3 < stride; x++)
            {
                var i = (y * stride) + (x * 4);
                if (pixels[i] != color.R || pixels[i + 1] != color.G || pixels[i + 2] != color.B) continue;
                left = Math.Min(left, x);
                top = Math.Min(top, y);
                right = Math.Max(right, x + 1);
                bottom = Math.Max(bottom, y + 1);
            }
        }
        return right < 0 ? null : new Rect(left, top, right - left, bottom - top);
    }

    /// <summary>
    /// Paints a whole card one color, text included: subpixel glyph antialiasing writes a channel at a time, so a
    /// neighbor's text over its own background can land on another card's exact tint and widen its extent.
    /// </summary>
    public static void Flood(Border card, Color color)
    {
        var brush = new ImmutableSolidColorBrush(color);
        card.Background = brush;
        foreach (var text in card.GetLogicalDescendants().OfType<TextBlock>()) text.Foreground = brush;
    }

    /// <summary>The color <see cref="Flood"/> painted <paramref name="card"/> with.</summary>
    public static Color CardColor(Border card) => ((ImmutableSolidColorBrush)card.Background!).Color;

    /// <summary>A color per card that no palette entry uses, so one card's pixels can be told from another's.</summary>
    public static Color Tint(int index) => Color.FromRgb(255, 0, (byte)(23 + (index * 53 % 197)));

    /// <summary>The box layout has given <paramref name="control"/>, in <paramref name="window"/>'s coordinates.</summary>
    public static Rect Slot(Control control, Visual window) =>
        new(control.TranslatePoint(default, window)!.Value, control.Bounds.Size);

    /// <summary>The center of <paramref name="control"/>, in <paramref name="window"/>'s coordinates.</summary>
    public static Point Center(Visual control, Visual window) =>
        control.TranslatePoint(new Point(control.Bounds.Width / 2, control.Bounds.Height / 2), window)!.Value;

    /// <summary>A card's background stops inside its one pixel border, so a slot it fills is a few pixels wider than its pixels.</summary>
    public static bool Near(Rect box, Rect slot) =>
        Math.Abs(box.X - slot.X) <= 3 && Math.Abs(box.Y - slot.Y) <= 3 &&
        Math.Abs(box.Width - slot.Width) <= 4 && Math.Abs(box.Height - slot.Height) <= 4;

    /// <summary>The pixels of the last frame inside <paramref name="box"/> that hold something other than <paramref name="background"/>.</summary>
    public static int CountPixels(Visual visual, Rect box, Color background)
    {
        var (pixels, stride) = Capture(visual);
        var clipped = box.Intersect(new Rect(visual.Bounds.Size));
        var count = 0;
        for (var y = (int)clipped.Y; y < (int)Math.Ceiling(clipped.Bottom); y++)
        {
            for (var x = (int)clipped.X; x < (int)Math.Ceiling(clipped.Right); x++)
            {
                var i = (y * stride) + (x * 4);
                if (pixels[i] != background.R || pixels[i + 1] != background.G || pixels[i + 2] != background.B) count++;
            }
        }
        return count;
    }
}
