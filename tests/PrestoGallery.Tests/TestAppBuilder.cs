using Avalonia;
using Avalonia.Headless;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Platform;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;

namespace PrestoGallery.Tests;

public static class TestAppBuilder
{
    /// <summary>
    /// Skia replaces the headless drawing stub, whose geometries report a <c>ContourLength</c> of 0. The framebuffer
    /// takes Skia's own pixel order: converting every frame from it took a quarter of the suite's time.
    /// <c>Fps = 1</c> all but stops the headless render timer, which otherwise renders again inside <c>RunJobs</c> whenever
    /// a frame outlasts 16 ms of wall time.
    /// </summary>
    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<TestApp>()
        .UseSkia()
        .UseHeadless(new AvaloniaHeadlessPlatformOptions { Fps = 1, UseHeadlessDrawing = false, FrameBufferFormat = PixelFormat.Bgra8888 });
}

/// <summary>
/// The gallery's styles without its <see cref="App"/>: the headless session builds a fresh application per test, and
/// <c>App.Initialize</c> attaches the developer tools, which refuses a second attachment in the same process.
/// </summary>
internal sealed class TestApp : Application
{
    public override void Initialize()
    {
        RequestedThemeVariant = ThemeVariant.Dark;
        Styles.Add(new FluentTheme());
        Styles.Add(new StyleInclude(new Uri("avares://PrestoGallery/"))
        {
            Source = new Uri("avares://PrestoGallery/Styles/Showcase.axaml"),
        });
    }
}
