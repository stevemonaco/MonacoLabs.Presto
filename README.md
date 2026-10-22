# MonacoLabs.Presto

A .NET animation library inspired by [anime.js](https://animejs.com/documentation) with a primary focus on Avalonia.

## Project Status

This is an AI-created library with many iterations via prompts, developing challenging samples, and manual testing.

Code quality and API maintenance are uncertain, so this library should be considered experimental and aspirational though it 
is feature-complete enough to demonstrate impressive capabilities in the gallery and sample app.

**[Try the gallery in your browser](https://stevemonaco.github.io/MonacoLabs.Presto/)**: a reference page per feature
and multi-feature showcases, each printing the code that runs it.

[Tempo](https://stevemonaco.github.io/MonacoLabs.Presto/tempo/), a focus timer, is a whole app built on Presto.

The GitHub Releases have NativeAOT win-x64 versions, too, but other platforms must be manually built.

## AI-drafted content starts here

| Package | What it is |
|---|---|
| `MonacoLabs.Presto` | The engine: clock, timers, animations, timelines, tweens, values, eases, stagger, utils, animatable, scope. No UI dependency. |
| `MonacoLabs.Presto.Avalonia` | Avalonia adapter: frame pump, `AvaloniaProperty` targets, transform composition, color/brush/struct codecs, draggable, scroll observer, shapes, text, render-thread scenes, controls and recipes. |
| `MonacoLabs.Presto.Avalonia.Lottie` | Lottie playback through SkiaSharp.Skottie: a `LottieView` whose frame Presto animates. See [Lottie](#lottie). |
| `MonacoLabs.Presto.Avalonia.DevTools` | An in-app animation inspector for MonacoLabs.Presto on Avalonia: freeze, step and slow the engine and watch what it runs. |

**The full manual is [GUIDE.md](GUIDE.md)**: every feature with its API and examples, in one file.

## Install

```powershell
dotnet add package MonacoLabs.Presto
dotnet add package MonacoLabs.Presto.Avalonia
dotnet add package MonacoLabs.Presto.Avalonia.Lottie
dotnet add package MonacoLabs.Presto.Avalonia.DevTools
```

The bridge is built against **Avalonia 12.1.4** and every package targets **net10.0**. The core has no package
dependencies at all; take it alone if you are driving a game loop, a view model or your own renderer.

## Avalonia setup

Install the bridge once at startup:

```csharp
public static AppBuilder BuildAvaloniaApp()
    => AppBuilder.Configure<App>()
        .UsePlatformDetect()
        .UsePresto()
        .WithInterFont();
```

`UsePresto()` is optional: everything in the bridge initializes it on first use. See
[Avalonia setup](GUIDE.md#avalonia-setup) in the guide for the frame pump.

## Features

| Feature | What it does |
|---|---|
| [Timers](GUIDE.md#timers) and [playback control](GUIDE.md#playback-control) | Play, pause, seek, reverse, stretch and await anything that runs |
| [Values](GUIDE.md#values) and [keyframes](GUIDE.md#keyframes) | Numbers, colors, brushes, gradients, units and relative values; per-property keyframes |
| [Timelines](GUIDE.md#timelines) | Sequence children with anime.js positions, labels and callbacks |
| [Stagger](GUIDE.md#stagger) | Offset delays and values across many targets, in a line or a grid |
| [Eases](GUIDE.md#eases) | The full anime.js set, springs, and a string parser for XAML |
| [Animatable](GUIDE.md#animatable) | Push values into paused animations, for followers and live input |
| [Scope](GUIDE.md#scope) | Revert everything built inside together |
| [Draggable](GUIDE.md#draggable) and [inertia](GUIDE.md#inertia) | Drag with bounds, snapping, release velocity and drag to reorder |
| [Transformable](GUIDE.md#transformable) | Pan, pinch-zoom and rotate a control |
| [onScroll](GUIDE.md#onscroll) | Drive or trigger animations from a `ScrollViewer` |
| [Shapes](GUIDE.md#shapes) | Stroke drawing, motion paths and path morphing |
| [Split and scramble text](GUIDE.md#split-and-scramble-text) and [Marquee](GUIDE.md#marquee) | Lines, words and characters as targets; scrambled and morphing text; a scrolling marquee |
| [Render thread](GUIDE.md#render-thread) | Composition animations, scenes and layout animations that keep running when the UI thread is busy |
| [Lifecycle animations](GUIDE.md#lifecycle-animations) | Entrances, scroll reveals and exits from XAML |
| [State motion](GUIDE.md#state-motion) | Hover, press, focus and view-model states, Framer Motion style |
| [Effects](GUIDE.md#effects) | Blur, shadows, 3D rotation, clips and masks, image filters and pixel transitions |
| [Recipes](GUIDE.md#recipes) | Motion tokens, page transitions, ripples, confetti, sprite sheets and Lottie |
| [Reduced motion](GUIDE.md#reduced-motion) | Each builder states what it becomes when the user asks for less motion |

## Lottie

Lottie files play through a separate package. `LottieView` shows one
frame of a composition drawn by SkiaSharp.Skottie, and Presto drives that frame like any other property:

```xml
<presto:LottieView Composition="avares://MyApp/Assets/Lottie/spinner.json" Width="120" Height="120"
                   presto:Animate.OnAttached="{presto:PlayLottie}" />
```

See [Lottie](GUIDE.md#lottie) in the guide.

## anime.js → Presto

Every anime.js entry point has a counterpart, and parameter objects keep their names in PascalCase (`duration` →
`Duration`, `onComplete` → `OnComplete`). The guide maps [each entry point](GUIDE.md#animejs--presto) and
[parameter name](GUIDE.md#parameter-names), and lists the [C# differences](GUIDE.md#c-differences).

## Trimming and NativeAOT

All four packages are trimmable and AOT-compatible with no warnings. A plain object animated by member name registers
its type once, before its first animation: `Presto.RegisterTarget<Ship>()`. Avalonia controls need nothing. See
[Trimming and NativeAOT](GUIDE.md#trimming-and-nativeaot) in the guide.

## Platforms and status

Version `0.1.0-preview.1`: the API is settled enough to build against but not frozen. Desktop on Windows is the
development target and fully exercised; the Browser (WebAssembly) and Android heads run with some pages unchecked; iOS
compiles but has not run. The guide's [Platforms and status](GUIDE.md#platforms-and-status) says what was verified on
each.

## More

- [GUIDE.md](GUIDE.md): the complete manual.
- [CHANGELOG.md](CHANGELOG.md): what changed in each release.
- [Feature specs](docs/specs/README.md): each feature's exact behavior and design decisions.
- [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md): how it is built, and the anime.js provenance map.
- [llms.txt](llms.txt): an index for AI assistants.

## Credits

Presto is inspired by, and was initially ported from, **[anime.js](https://github.com/juliangarnier/anime)** by Julian
Garnier, used under the MIT license. The timing model, the parameter vocabulary and many of the test suites began in
anime.js 4.5.0; the bugs are mine. If you write JavaScript, use the original — its
[documentation](https://animejs.com/documentation) explains many of the concepts the two libraries share.

## License

Presto is MIT licensed — see [LICENSE](LICENSE), which also carries anime.js's own MIT notice.
