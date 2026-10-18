# MonacoLabs.Presto.Avalonia

The Avalonia adapter for [MonacoLabs.Presto](https://www.nuget.org/packages/MonacoLabs.Presto), an animation library
inspired by anime.js. It makes controls first-class targets: a frame pump on `TopLevel.RequestAnimationFrame`,
`AvaloniaProperty` targets written at animation priority, render-transform composition, color, brush and struct
interpolation, a fluent builder with a typed method per property, XAML attached properties and markup extensions,
lifecycle and state motion, draggable, scroll observer, shapes, text effects, render-thread scenes and ready-made
controls such as `Sheet`, `Modal` and `ToastHost`.

## Install

```powershell
dotnet add package MonacoLabs.Presto.Avalonia
```

It is built against **Avalonia 12.1.4** and targets **net10.0**. The core package comes with it.

## Quick start

Install the bridge at startup (optional: everything in the bridge initializes it on first use):

```csharp
public static AppBuilder BuildAvaloniaApp()
    => AppBuilder.Configure<App>()
        .UsePlatformDetect()
        .UsePresto()
        .WithInterFont();
```

Then animate any control:

```csharp
using Avalonia.Controls;
using MonacoLabs.Presto;

Square.Animate(b => b
    .X(320)                                             // transform shorthand → RenderTransform
    .Rotate(from: -180)
    .Scale(1.4)
    .Property(Border.BorderBrushProperty, "#2563eb")    // any AvaloniaProperty
    .Background("#ff6b6b")                              // brush, from a CSS color string
    .Duration(1250)
    .Ease(Eases.InOutQuint)
    .Loop()
    .Alternate());
```

Values are written at `BindingPriority.Animation`, so `Revert()` puts the control's own values back exactly. From XAML,
with `xmlns:presto="https://github.com/stevemonaco/MonacoLabs.Presto"`:

```xml
<TextBlock presto:Presence.Arrive="{presto:Fade Direction=Up, Distance=48, Delay=100}" />
```

## Trimming and NativeAOT

The package is marked `IsTrimmable` and `IsAotCompatible` and builds with no trim or AOT warnings. Avalonia controls,
`AvaloniaProperty` keys and the library's own objects never go through reflection, so they need nothing. A plain object
animated by member name, such as a dot in a `CompositorScene`, needs `Presto.RegisterTarget<T>()`; see the
[Trimming and NativeAOT](https://github.com/stevemonaco/MonacoLabs.Presto#trimming-and-nativeaot) section.

## More

- [Documentation and feature tour](https://github.com/stevemonaco/MonacoLabs.Presto#readme)
- [Avalonia setup](https://github.com/stevemonaco/MonacoLabs.Presto#avalonia-setup)
- [Platforms and status](https://github.com/stevemonaco/MonacoLabs.Presto#platforms-and-status)
- [Changelog](https://github.com/stevemonaco/MonacoLabs.Presto/blob/main/CHANGELOG.md)

## License

MIT; see [LICENSE](https://github.com/stevemonaco/MonacoLabs.Presto/blob/main/LICENSE). Presto is inspired by, and
was initially ported from, [anime.js](https://github.com/juliangarnier/anime) by Julian Garnier, used under the MIT
license.
