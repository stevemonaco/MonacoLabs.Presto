# MonacoLabs.Presto

A framework-agnostic .NET animation engine inspired by, and initially ported from,
[anime.js](https://animejs.com/documentation) v4.5.0: timers, animations, timelines, tweens, keyframes, eases,
staggering, springs and full playback control, for any property of any object. It has no UI dependency and no package
dependencies, so it suits a game loop, a view model or your own renderer.

For UI controls, add [MonacoLabs.Presto.Avalonia](https://www.nuget.org/packages/MonacoLabs.Presto.Avalonia), which
builds on this package.

## Install

```powershell
dotnet add package MonacoLabs.Presto
```

Targets **net10.0**.

## Quick start

```csharp
using MonacoLabs.Presto;

Presto.RegisterTarget<Ship>();                          // needed when trimmed or NativeAOT; see below
var ship = new Ship();

var animation = Presto.Build()
    .Property(nameof(Ship.X), 320)                      // 0 → 320
    .Property(nameof(Ship.Opacity), from: 1, to: 0.2)   // explicit from → to
    .Duration(1250)
    .Ease(Eases.InOutQuint)
    .Loop(1)
    .Alternate()
    .OnUpdate(_ => Console.WriteLine($"{ship.X:0} {ship.Opacity:0.00}"))
    .Animate(ship);

await animation;                                        // anime.js .then()

sealed class Ship
{
    public double X { get; set; }
    public double Opacity { get; set; } = 1;
}
```

Frames come from the built-in `StopwatchFrameSource`, which posts to the `SynchronizationContext` of the thread that
started it; tests swap in `ManualFrameSource` and call `Advance(16)`. Time is always `double` milliseconds.

## Trimming and NativeAOT

The package is marked `IsTrimmable` and `IsAotCompatible` and builds with no trim or AOT warnings. A plain object
animated by member name has its members found by reflection, and a trimmed or NativeAOT app may have removed them, so
register the type once before its first animation with `Presto.RegisterTarget<T>()`. The call does nothing at run time;
its annotation tells the trimmer what to keep, and an unregistered type that lost its members throws
`ArgumentException` naming `RegisterTarget<T>()`. Under NativeAOT a registered type's accessors run as interpreted
expression trees; a type that needs allocation-free frames there registers an `ITargetAdapter` in `Presto.Adapters`
instead. Details are in the [Trimming and NativeAOT](https://github.com/stevemonaco/MonacoLabs.Presto#trimming-and-nativeaot)
section.

## More

- [Documentation and feature tour](https://github.com/stevemonaco/MonacoLabs.Presto#readme)
- [anime.js → Presto mapping](https://github.com/stevemonaco/MonacoLabs.Presto#animejs--presto)
- [Changelog](https://github.com/stevemonaco/MonacoLabs.Presto/blob/main/CHANGELOG.md)
- [Issues](https://github.com/stevemonaco/MonacoLabs.Presto/issues)

## License

MIT; see [LICENSE](https://github.com/stevemonaco/MonacoLabs.Presto/blob/main/LICENSE). Presto is inspired by, and
was initially ported from, [anime.js](https://github.com/juliangarnier/anime) by Julian Garnier, used under the MIT
license.
