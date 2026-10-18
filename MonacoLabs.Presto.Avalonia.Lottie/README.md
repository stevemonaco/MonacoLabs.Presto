# MonacoLabs.Presto.Avalonia.Lottie

Lottie playback for [MonacoLabs.Presto.Avalonia](https://www.nuget.org/packages/MonacoLabs.Presto.Avalonia), drawn
through SkiaSharp.Skottie. `LottieView` shows one frame of its `Composition`; Presto drives the frame and does not
interpret Lottie itself. `Frame` and `Progress` are ordinary styled properties, so a tween, a timeline, `Seek`, a
scroll-synced animation or a binding drives them like any other. It reads JSON and dotLottie (`.lottie`) files, plays
named marker segments, and can play on the render thread so a spinner keeps moving while the UI thread is blocked.

## Install

```powershell
dotnet add package MonacoLabs.Presto.Avalonia.Lottie
```

Targets **net10.0**; the bridge and core packages come with it.

## Quick start

```xml
<presto:LottieView Composition="avares://MyApp/Assets/Lottie/spinner.json" Width="120" Height="120"
                   presto:Animate.OnAttached="{presto:PlayLottie}" />
```

`xmlns:presto="https://github.com/stevemonaco/MonacoLabs.Presto"` covers this package too. From code:

```csharp
using MonacoLabs.Presto;
using MonacoLabs.Presto.Lottie;

toggle.Composition = LottieComposition.Load(new Uri("avares://MyApp/Assets/Lottie/toggle.json"));
toggle.Play();                                    // the whole composition once, at its frame rate
toggle.Play("on");                                // a marker's segment, ignoring case
toggle.PlaySegment(60, 0, loop: true, speed: 2);  // a frame range, backwards when to is the lower
timeline.Add(toggle, Recipes.PlayLottie("on", loop: false), 0);
```

`LottieComposition` is immutable and can be shared between views; input Skottie cannot play throws
`FormatException`.

## Trimming and NativeAOT

The package is marked `IsTrimmable` and `IsAotCompatible` and builds with no trim or AOT warnings; `LottieView` needs
no registration. See the [Trimming and NativeAOT](https://github.com/stevemonaco/MonacoLabs.Presto#trimming-and-nativeaot)
section.

## More

- [Lottie documentation](https://github.com/stevemonaco/MonacoLabs.Presto#lottie): segments, recipe, dotLottie,
  render thread and what is out of scope
- [Documentation and feature tour](https://github.com/stevemonaco/MonacoLabs.Presto#readme)
- [Changelog](https://github.com/stevemonaco/MonacoLabs.Presto/blob/main/CHANGELOG.md)

## License

MIT; see [LICENSE](https://github.com/stevemonaco/MonacoLabs.Presto/blob/main/LICENSE). Presto is inspired by, and
was initially ported from, [anime.js](https://github.com/juliangarnier/anime) by Julian Garnier, used under the MIT
license.
