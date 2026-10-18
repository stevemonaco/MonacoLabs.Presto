# MonacoLabs.Presto.Avalonia.DevTools

An in-app inspector for [MonacoLabs.Presto.Avalonia](https://www.nuget.org/packages/MonacoLabs.Presto.Avalonia). The
overlay docks to the bottom of a window and shows what `Presto.Engine` runs: every live timer, animation and timeline
with its progress, the tweens of the selected one, springs, and per-frame costs. It can freeze the engine, step it a
frame at a time, slow it with dilation presets, scrub a selection and outline the controls it animates.

## Install

```powershell
dotnet add package MonacoLabs.Presto.Avalonia.DevTools
```

Targets **net10.0**; the bridge and core packages come with it. It is meant for development, so a common setup
references it only in Debug.

## Quick start

Attach it to every window at startup:

```csharp
using MonacoLabs.Presto.DevTools;

public static AppBuilder BuildAvaloniaApp()
    => AppBuilder.Configure<App>()
        .UsePlatformDetect()
        .UsePresto()
        .UsePrestoDevTools();
```

Press **Ctrl+Shift+F12** to toggle the overlay (F12 belongs to Avalonia DevTools); pass a `KeyGesture` to
`UsePrestoDevTools` to choose another. `PrestoDevTools.Attach(topLevel)`, `Open`, `Close` and `Toggle` do the same for
a single top level, for example from a button.

## Trimming and NativeAOT

The package is marked `IsTrimmable` and `IsAotCompatible` and builds with no trim or AOT warnings. See the
[Trimming and NativeAOT](https://github.com/stevemonaco/MonacoLabs.Presto#trimming-and-nativeaot) section.

## More

- [Documentation and feature tour](https://github.com/stevemonaco/MonacoLabs.Presto#readme)
- [Changelog](https://github.com/stevemonaco/MonacoLabs.Presto/blob/main/CHANGELOG.md)

## License

MIT; see [LICENSE](https://github.com/stevemonaco/MonacoLabs.Presto/blob/main/LICENSE). Presto is inspired by, and
was initially ported from, [anime.js](https://github.com/juliangarnier/anime) by Julian Garnier, used under the MIT
license.
