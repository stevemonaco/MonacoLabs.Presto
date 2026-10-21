using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Headless;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Platform;
using Avalonia.Styling;
using MonacoLabs.Presto;
using Semi.Avalonia;

namespace Tempo.Tests;

public static class TestAppBuilder
{
    /// <summary>
    /// Skia replaces the headless drawing stub, whose geometries report a <c>ContourLength</c> of 0. <c>Fps = 1</c> all but stops the
    /// headless render timer, which otherwise renders again inside <c>RunJobs</c> whenever a frame outlasts 16 ms of wall time.
    /// </summary>
    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<TestApp>()
        .UseSkia()
        .WithInterFont()
        .UseHeadless(new AvaloniaHeadlessPlatformOptions { Fps = 1, UseHeadlessDrawing = false, FrameBufferFormat = PixelFormat.Bgra8888 });
}

/// <summary>
/// Tempo's styles without its <see cref="App"/>: the headless session builds a fresh application per test, and
/// <c>App.Initialize</c> attaches the developer tools, which refuses a second attachment in the same process.
/// </summary>
internal sealed class TestApp : Application
{
    /// <summary>The OS reduce-motion setting as the test app reads it, in place of the build machine's.</summary>
    public static bool SystemReducedMotion { get; set; }

    public override void Initialize()
    {
        PrestoAvalonia.SystemReducedMotionProbe = static () => SystemReducedMotion;
        RequestedThemeVariant = ThemeVariant.Dark;
        Styles.Add(new SemiTheme());
        // Compiled XAML is found only in loaded assemblies, and a test that has not touched a Tempo type yet has not loaded it.
        RuntimeHelpers.RunClassConstructor(typeof(App).TypeHandle);
        Styles.Add(new StyleInclude(new Uri("avares://Tempo/"))
        {
            Source = new Uri("avares://Tempo/Styles/Tempo.axaml"),
        });
    }
}
