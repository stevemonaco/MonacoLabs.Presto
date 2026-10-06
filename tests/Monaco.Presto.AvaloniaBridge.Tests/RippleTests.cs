using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Rendering.Composition;
using Avalonia.Styling;

namespace Monaco.Presto.AvaloniaBridge.Tests;

/// <summary>The press ripple (Phase 62, decisions 100 and 101): where the ink starts, what clips it, how it grows and fades, and what removes it.</summary>
public sealed class RippleTests : CompositorTestBase
{
    [AvaloniaFact]
    public void TheInkGrowsFromThePressPoint()
    {
        var host = ShowHost(new Size(160, 100));

        WindowOf(host).MouseDown(new Point(60, 40), MouseButton.Left);
        TickFor(32);

        var ink = Box(host);
        Assert.Equal(60, ink.Center.X, 1.0);
        Assert.Equal(40, ink.Center.Y, 1.0);
        Assert.InRange(ink.Width, 20, 60);
    }

    [AvaloniaTheory]
    [InlineData(Key.Enter)]
    [InlineData(Key.Space)]
    public void EnterAndSpaceRippleFromTheCenter(Key key)
    {
        var host = ShowHost(new Size(160, 100));

        host.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = key, Source = host });
        TickFor(32);

        var ink = Box(host);
        Assert.Equal(80, ink.Center.X, 1.0);
        Assert.Equal(50, ink.Center.Y, 1.0);
        Assert.InRange(ink.Width, 10, 60);
    }

    [AvaloniaFact]
    public void AGrownRippleCoversTheHostButNotItsRoundedCorners()
    {
        var host = ShowHost(new Size(60, 40), new CornerRadius(16));

        WindowOf(host).MouseDown(new Point(30, 20), MouseButton.Left);
        TickFor(640);

        AssertNear(Colors.White, Row(host, 1)[1]);
        AssertNear(Colors.Red, Row(host, 20)[1]);
        AssertNear(Colors.Red, Row(host, 1)[30]);
    }

    [AvaloniaTheory]
    [InlineData(true)]
    [InlineData(false)]
    public void TheInkIsClippedToTheHostOnlyWhenBounded(bool bounded)
    {
        var host = ShowHost(new Size(20, 20));
        host.Margin = new Thickness(20);
        Ripple.SetBounded(host, bounded);
        Tick();

        WindowOf(host).MouseDown(new Point(22, 22), MouseButton.Left);
        TickFor(640);

        AssertNear(Colors.Red, Row(host, 30)[30]);
        AssertNear(bounded ? Colors.White : Colors.Red, Row(host, 30)[17]);
        AssertNear(Colors.White, Row(host, 30)[13]);
    }

    [AvaloniaFact]
    public void AHeldRippleCoversTheHostAndStaysThenReleaseFadesItAndTheHandlerIdles()
    {
        var host = ShowHost(new Size(60, 40));
        var window = WindowOf(host);

        window.MouseDown(new Point(10, 10), MouseButton.Left);
        TickFor(640);
        Assert.Equal(new Rect(0, 0, 60, 40), Box(host));
        TickFor(1000);
        Assert.Equal(new Rect(0, 0, 60, 40), Box(host));

        window.MouseUp(new Point(10, 10), MouseButton.Left);
        TickFor(100);
        Assert.Null(Extent(host, Colors.Red));
        Assert.Equal(1, Ripple.HandlerOf(host)!.Live);
        TickFor(150);

        AssertNear(Colors.White, Row(host, 20)[30]);
        Assert.Equal(0, Ripple.HandlerOf(host)!.Live);
    }

    [AvaloniaFact]
    public void SeveralRipplesRunAtOnce()
    {
        var host = ShowHost(new Size(100, 60));
        var window = WindowOf(host);

        window.MouseDown(new Point(10, 10), MouseButton.Left);
        window.MouseUp(new Point(10, 10), MouseButton.Left);
        window.MouseDown(new Point(90, 50), MouseButton.Left);
        Tick(Frame);

        Assert.Equal(2, Ripple.HandlerOf(host)!.Live);
    }

    [AvaloniaTheory]
    [InlineData(false, "SystemAccentColor")]
    [InlineData(true, "SystemAccentColorLight2")]
    public void TheDefaultColorIsTheThemeAccentAtLowAlpha(bool dark, string resource)
    {
        var host = ShowHost(new Size(60, 40));
        Ripple.SetColor(host, null);
        if (dark)
        {
            WindowOf(host).RequestedThemeVariant = ThemeVariant.Dark;
            Tick();
        }
        Assert.True(host.TryFindResource(resource, host.ActualThemeVariant, out var value));
        var accent = (Color)value!;

        WindowOf(host).MouseDown(new Point(30, 20), MouseButton.Left);
        TickFor(640);

        const double alpha = 61 / 255d;
        byte Over(byte channel) => (byte)Math.Round((channel * alpha) + (255 * (1 - alpha)));
        AssertNear(Color.FromRgb(Over(accent.R), Over(accent.G), Over(accent.B)), Row(host, 20)[30], 3);
    }

    [AvaloniaTheory]
    [InlineData("disable")]
    [InlineData("detach")]
    public void DisablingOrDetachingRemovesTheInk(string how)
    {
        var host = ShowHost(new Size(60, 40));
        WindowOf(host).MouseDown(new Point(30, 20), MouseButton.Left);
        TickFor(640);
        var panel = (Panel)host.Parent!;

        if (how == "disable")
        {
            Ripple.SetEnabled(host, false);
            Assert.Null(ElementComposition.GetElementChildVisual(host));
        }
        else
        {
            panel.Children.Remove(host);
            Tick();
            Assert.Null(ElementComposition.GetElementChildVisual(host));
            panel.Children.Add(host);
        }
        Tick();

        Assert.Null(Extent(host, Colors.Red));
        Assert.Equal(0, Ripple.HandlerOf(host)?.Live ?? 0);
    }

    /// <summary>Shows a white host with red ink at the top left of a white window, ticked once.</summary>
    internal static Border ShowHost(Size size, CornerRadius corners = default)
    {
        var host = new Border
        {
            Width = size.Width, Height = size.Height, CornerRadius = corners, Background = Brushes.White,
            HorizontalAlignment = HorizontalAlignment.Left,
        };
        Ripple.SetEnabled(host, true);
        Ripple.SetColor(host, Colors.Red);
        ShowWhite(host);
        return host;
    }
}
