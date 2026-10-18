using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media;
using Avalonia.Themes.Fluent;
using Avalonia.Threading;

namespace MonacoLabs.Presto.AotProbe;

/// <summary>
/// Drives each path that trimming or NativeAOT can break, in a NativeAOT publish, and exits with the number of cases that
/// failed (docs/ARCHITECTURE.md §3, Trimming and NativeAOT).
/// </summary>
internal static partial class Program
{
    private static readonly ManualFrameSource s_frames = new();
    private static int s_failures;

    private static int Main()
    {
        AppBuilder.Configure<ProbeApp>()
            .UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
            .SetupWithoutStarting();
        PrestoAvalonia.EnsureInitialized();
        Presto.Engine.FrameSource = s_frames;

        Case("animatable-set", AnimatableSet);
        Case("timeline-sync-timer", SyncTimer);
        Case("timeline-sync-animation", SyncAnimation);
        Case("compositor-animation", CompositorAnimation);
        Case("layout-transition", LayoutTransition);
        Case("pager-travel", PagerTravel);
        Case("draggable", Draggable);
        Case("draggable-auto-scroll", DraggableAutoScroll);
        Case("content-size", ContentSize);
        Case("selection-glide", SelectionGlide);
        Case("follow-channels", FollowChannels);
        Case("distort-redraw", DistortRedraw);
        Case("glyph-shards", GlyphShardsOf);
        Case("reorder-move", ReorderMove);
        Case("reorder-filtered", ReorderFiltered);
        Case("dot-lottie", DotLottie);
        Case("devtools-row", DevToolsRow);
        Case("registered-target", RegisteredTarget);
        return s_failures;
    }

    private static void Case(string name, Action body)
    {
        try
        {
            body();
            Console.WriteLine($"PASS {name}");
        }
        catch (Exception e)
        {
            s_failures++;
            Console.WriteLine($"FAIL {name}: {e}");
        }
    }

    private static void Check(bool ok, string message)
    {
        if (!ok) throw new InvalidOperationException(message);
    }

    private static void Render()
    {
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
    }

    private static void Advance(double ms)
    {
        for (var elapsed = 0d; elapsed < ms; elapsed += 16)
        {
            s_frames.Advance(16);
            Render();
        }
    }

    private static Window Show(Control content, double width = 200, double height = 200)
    {
        var window = new Window { Content = content, Background = Brushes.White, Width = width, Height = height };
        window.Show();
        Render();
        return window;
    }

    private static int Count(TopLevel top, Color color)
    {
        var (pixels, _) = Pixels(top);
        var count = 0;
        for (var i = 0; i + 3 < pixels.Length; i += 4)
        {
            if (pixels[i] == color.R && pixels[i + 1] == color.G && pixels[i + 2] == color.B) count++;
        }
        return count;
    }

    private static Color At(TopLevel top, int x, int y)
    {
        var (pixels, stride) = Pixels(top);
        var i = (y * stride) + (x * 4);
        return Color.FromRgb(pixels[i], pixels[i + 1], pixels[i + 2]);
    }

    private static (byte[] Pixels, int Stride) Pixels(TopLevel top)
    {
        using var frame = top.GetLastRenderedFrame() ?? throw new InvalidOperationException("The window has not rendered.");
        using var locked = frame.Lock();
        var pixels = new byte[locked.RowBytes * locked.Size.Height];
        Marshal.Copy(locked.Address, pixels, 0, pixels.Length);
        return (pixels, locked.RowBytes);
    }

    private sealed class ProbeApp : Application
    {
        public override void Initialize() => Styles.Add(new FluentTheme());
    }
}
