# Monaco.Presto

A .NET port of the [anime.js](https://animejs.com/documentation) v4.5.0 animation model: a framework-agnostic core that
animates any property of any object, plus an Avalonia adapter that makes controls first-class targets.

The core is a faithful port of anime.js's timing model — the same clock, the same `tick()` maths for
delay/loop/alternate/reverse/seek/stretch, the same composition rules — so the upstream test suites port across
one-to-one. The API is C#-idiomatic: `init`-only parameter records, a string indexer for the properties you animate, and
the anime.js names in PascalCase.

| Package | What it is |
|---|---|
| `Monaco.Presto` | The engine: clock, timers, animations, timelines, tweens, values, eases, stagger, utils, animatable, scope. No UI dependency. |
| `Monaco.Presto.AvaloniaBridge` | Avalonia adapter: frame pump, `AvaloniaProperty` targets, transform composition, colour/brush/struct codecs, draggable, scroll observer, shapes, text, render-thread scenes. |

## Install

```powershell
dotnet add package Monaco.Presto
dotnet add package Monaco.Presto.AvaloniaBridge
```

The bridge is built against **Avalonia 12.1.2** and both packages target **net10.0**. The core has no package
dependencies at all; take it alone if you are driving a game loop, a view model or your own renderer.

## Quick start

### A plain object

```csharp
using Monaco.Presto;

var ship = new Ship();

var animation = Presto.Animate(ship, new()
{
    ["X"] = 320,                                // 0 → 320
    ["Opacity"] = new double[] { 1, 0.2 },      // explicit from → to
    Duration = 1250,
    Ease = Eases.InOutQuint,
    Loop = 1,
    Alternate = true,
    OnUpdate = _ => Console.WriteLine($"{ship.X:0} {ship.Opacity:0.00}"),
});

await animation;                                // anime.js .then()

sealed class Ship
{
    public double X { get; set; }
    public double Opacity { get; set; } = 1;
}
```

Properties are found by reflection and cached as delegates. Frames come from the built-in `StopwatchFrameSource`, which
posts to the `SynchronizationContext` of the thread that started it; tests swap in `ManualFrameSource` and call
`Advance(16)`.

### An Avalonia control

```csharp
using Avalonia;
using Monaco.Presto;
using Monaco.Presto.AvaloniaBridge;

Square.Animate(new()
{
    ["X"] = 320,                                // transform shorthand → RenderTransform
    ["Y"] = -40,
    ["Rotate"] = new TweenParams { From = -180 },
    ["Scale"] = 1.4,
    [Visual.OpacityProperty] = 0.4,             // typed AvaloniaProperty
    ["Background"] = "#ff6b6b",                 // brush, from a CSS colour string
    Duration = 1250,
    Delay = 250,
    Ease = Eases.InOutQuint,
    Loop = true,
    Alternate = true,
});
```

Named properties resolve through `AvaloniaPropertyRegistry`, so `"Opacity"`, `"Width"`, `"Margin"` and attached
properties like `"Canvas.Left"` all work. Values are written at `BindingPriority.Animation`, so `Revert()` puts the
control's own values back exactly.

## Avalonia setup

Install the bridge once at startup:

```csharp
public static AppBuilder BuildAvaloniaApp()
    => AppBuilder.Configure<App>()
        .UsePlatformDetect()
        .UsePresto()
        .WithInterFont();
```

`UsePresto()` is optional. Everything in the bridge calls `PrestoAvalonia.EnsureInitialized()` first, which points the
engine at the Avalonia frame source and registers the adapter and codecs. Call it yourself if you animate a control
through the core `Presto.Animate` overload rather than the `Animate` extension.

Frames come from `TopLevel.RequestAnimationFrame`, re-requested every frame while the engine has children, with a
render-priority `DispatcherTimer` fallback until a `TopLevel` exists. The engine stops the pump when nothing is running.

## Feature tour

### Timers

```csharp
var timer = Presto.CreateTimer(new()
{
    Duration = 1000,
    Loop = true,
    FrameRate = 30,
    OnUpdate = t => CurrentTimeText.Text = t.CurrentTime.ToString("0"),
    OnLoop = _ => loops++,
});
```

### Playback control

Every timer, animation and timeline has the same controls:

```csharp
animation.Pause().Resume();
animation.Restart();
animation.Reverse();
animation.Alternate();
animation.Seek(250);
animation.Progress = 0.5;
animation.Stretch(2000);
animation.Complete();
animation.Cancel();
animation.Revert();                     // undo the writes, restore the target
await animation.Then(a => Log(a.Id));
```

### Values

```csharp
["X"] = 220,                                        // number
["Rotate"] = "1turn",                               // deg / rad / turn
["X"] = "+=140", ["Scale"] = "*=1.5",               // relative
["Value"] = "75%",                                  // percentage of the current value
["Opacity"] = new double[] { 0.2, 1 },              // from → to
["Background"] = "hsl(280, 70%, 60%)",              // #rgb, #rrggbbaa, rgb(), rgba(), hsl(), hsla()
["BorderThickness"] = TweenValue.Of(new Thickness(10)),   // typed structs, via codecs
["Y"] = new FunctionValue((target, i, total) => -10 - i * 6),
```

### Keyframes

Per-property, each frame with its own duration and ease:

```csharp
Presto.Animate(box, new()
{
    ["X"] = TweenValue.Keyframes(
        new TweenParams { To = 200, Duration = 500, Ease = Eases.OutQuad },
        new TweenParams { To = 80, Duration = 300, Delay = 200 },
        new TweenParams { To = 0, Duration = 600, Ease = Eases.InOutQuad }),
});
```

Or a shared timeline of frames, indexed by order or by percentage:

```csharp
Presto.Animate(box, new()
{
    Keyframes = new Keyframe[]
    {
        new() { ["Y"] = -40, ["Rotate"] = -20 },
        new() { ["X"] = 220, Ease = Eases.OutQuad },
        new() { ["X"] = 0, ["Rotate"] = 0, Duration = 600 },
    },
    Duration = 2000,
});

Presto.Animate(box, new()
{
    Keyframes = new Dictionary<string, Keyframe>
    {
        ["0%"] = new() { ["X"] = 0, ["Scale"] = 1 },
        ["60%"] = new() { ["Scale"] = 1.6 },
        ["100%"] = new() { ["X"] = 0, ["Scale"] = 1 },
    },
    Duration = 2500,
    Ease = "linear",
});
```

### Timelines

```csharp
var tl = Presto.CreateTimeline(new() { Defaults = new() { Duration = 600, Ease = Eases.OutQuad } })
    .Add(a, new() { ["X"] = 300 }, 0)               // absolute position
    .Add(b, new() { ["X"] = 300 })                  // appended
    .Add(c, new() { ["X"] = 300 }, "-=400")         // relative to the end
    .Add(d, new() { ["X"] = 300 }, "<")             // the previous child's end ("<<" for its start)
    .Label("flash", "+=200")
    .Add(e, new() { ["Rotate"] = 360 }, "flash")    // at a label
    .Add(new TimerParams { Duration = 400, OnUpdate = t => Show(t) }, 20)
    .Call(_ => Log("half way"), "flash")
    .Sync(existingAnimation, 0);

tl.Seek(tl.Labels["flash"]);
```

### Stagger

```csharp
Presto.Animate(strip.Children, new()
{
    ["Y"] = -14,
    Delay = Presto.Stagger(60),                                          // 60 ms apart
    Duration = 450,
});

Presto.Stagger(new double[] { 0, 700 }, new() { Ease = "inOutQuad" });   // a range, eased
Presto.Stagger(50, new() { From = StaggerFrom.Center, Total = 24 });
Presto.Stagger(40, new() { Grid = [7, 4], Axis = StaggerAxis.X, Reversed = true });
PrestoAvalonia.Stagger(40, new() { From = StaggerFrom.Center });         // grid measured from the layout
```

`Presto.Stagger` also produces the value of a property (`["X"] = Presto.Stagger(6)`) and the position of a timeline
child (`.Add(targets, parameters, Presto.Stagger(60))`).

### Eases

```csharp
Eases.OutQuad;  Eases.InOutQuint;  Eases.OutInBounce;  Eases.None;
Eases.Out(3);   Eases.InOutBack(1.7);   Eases.OutElastic(1, .3);
Eases.CubicBezier(.17, .67, .83, .67);
Eases.Steps(5);
Eases.Linear(0, "0.25 75%", 1);
Eases.Irregular(10, 1);
Eases.Spring(mass: 1, stiffness: 100, damping: 10, velocity: 0);
EaseParser.Parse("outElastic(1.2, .4)");
```

An `Ease` is just `double → double`, and anywhere one is accepted a string works too (`Ease = "inOutQuint"`). Avalonia's
own `Easing` types convert both ways through `PrestoEasing`.

### Animatable

A set of paused per-property animations you push values into — for cursor followers and other continuously changing
targets, where a new animation per frame would be wasteful:

```csharp
var follower = Cursor.CreateAnimatable(new() { ["X"] = 500, ["Y"] = 500, Ease = Eases.Out(3) });

follower.Set("X", point.X).Set("Y", point.Y);    // eases toward the value over 500 ms
follower.Set("X", -140, 1200, "inOutQuad");      // one-off duration and ease
var x = follower.Get("X");
follower.Revert();
```

### Scope

A lifetime: everything built inside reverts together.

```csharp
var scope = Presto.CreateScope(new ScopeParams { Defaults = d => d.Ease = Eases.OutQuad });
scope.Add(_ => Presto.Animate(model, new() { ["Progress"] = 100 }));
scope.Revert();
```

The bridge roots a scope on a control, reverting when it leaves the visual tree:

```csharp
PrestoAvalonia.CreateScope(this).Execute(_ =>
{
    PrestoAvalonia.CreateDraggable(Card, new() { Container = Stage });
});
```

### Draggable

```csharp
PrestoAvalonia.CreateDraggable(SpringCard, new()
{
    Container = Stage,
    Snap = Utils.Snap(40),
    Y = new DraggableAxisParams { Enabled = false },
    ReleaseStiffness = 40,
    ReleaseDamping = 8,
    OnGrab = d => Log("grab"),
    OnUpdate = d => Show(d.X, d.Y, d.ProgressX, d.ProgressY),
});
```

### onScroll

Watches a control cross a `ScrollViewer`'s thresholds, and drives whatever is linked to it:

```csharp
PrestoAvalonia.OnScroll(new() { Target = RevealCard }).Link(reveal);        // play on enter

PrestoAvalonia.OnScroll(new()
{
    Target = ThresholdCard,
    Enter = "bottom-=120 top",
    Leave = "top center",
    OnEnter = _ => Highlight(true),
    OnLeave = _ => Highlight(false),
});

PrestoAvalonia.OnScroll(new() { Target = SyncCard, Sync = true }).Link(timeline);   // scrub with the scroll
```

### Shapes

```csharp
PrestoAvalonia.CreateDrawable(SignaturePath).Animate(new()
{
    ["Draw"] = TweenValue.FromTo("0 0", "0 1"),     // stroke dash reveal
    Duration = 2200,
});

var motion = PrestoAvalonia.CreateMotionPath(TrackPath);
Rider.Animate(new() { ["X"] = motion.X, ["Y"] = motion.Y, ["Rotate"] = motion.Rotate, Duration = 3000 });

MorphPath.Animate(new() { ["Data"] = PrestoAvalonia.MorphTo(starGeometry), Duration = 900 });
```

### Split and scramble text

```csharp
PrestoAvalonia.SplitText(Headline, new SplitTextParams
{
    Lines = new SplitPieceParams { Wrap = true },
    Chars = new SplitPieceParams { Class = "split-char" },
})
.AddEffect(split => Presto.Animate(split.Chars, new()
{
    ["Y"] = TweenValue.FromTo(44, 0),
    ["Opacity"] = TweenValue.FromTo(0, 1),
    Delay = Presto.Stagger(18),
}));

Transmission.Animate(new()
{
    ["Text"] = PrestoAvalonia.ScrambleText(new ScrambleTextParams { Text = "Signal acquired", From = ScrambleFrom.Center }),
});
```

### Render thread

Everything above runs on the UI thread, which means everything above stops when the UI thread does. A `CompositorScene`
does not: it is a world of its own — its own `Engine`, its own `Scope`, its own drawing — animated and drawn by
Avalonia's compositor. That makes it the one place in Presto where a busy indicator keeps turning through a synchronous
load.

```csharp
using Avalonia;
using Avalonia.Media;
using Monaco.Presto;
using Monaco.Presto.AvaloniaBridge.Compositor;

public sealed class SpinnerScene : CompositorScene
{
    private readonly Dot[] _dots = [.. Enumerable.Range(0, 12).Select(_ => new Dot())];

    protected override void OnAttached() =>
        Presto.Animate(_dots, new()                             // registers with this scene's engine
        {
            ["Opacity"] = new double[] { 1, 0.15 },
            Duration = 900,
            Delay = Presto.Stagger(75, new() { From = StaggerFrom.Center }),
            Ease = Eases.InOutSine,
            Loop = true,
        });

    protected override void OnRender(ImmediateDrawingContext context, Size size)
    {
        for (var i = 0; i < _dots.Length; i++)
        {
            var angle = i * Math.Tau / _dots.Length - Math.PI / 2;
            var center = new Point(
                size.Width / 2 + Math.Cos(angle) * 22,
                size.Height / 2 + Math.Sin(angle) * 22);
            context.DrawEllipse(Brush(Colors.SteelBlue, _dots[i].Opacity), null, center, 4, 4);
        }
    }

    private sealed class Dot { public double Opacity { get; set; } = 1; }
}
```

A `CompositorCanvas` hosts it. The scene goes to the render thread when the canvas enters the visual tree, and its scope
is reverted when the canvas leaves:

```csharp
var spinner = new CompositorCanvas { Width = 64, Height = 64, Scene = new SpinnerScene() };

spinner.Post("pulse");                                  // → OnMessage("pulse"), on the render thread
spinner.Invoke<SpinnerScene>(s => s.Engine.Speed = 2);  // ditto, against the live scene
```

The rules that come with the thread:

- **From attach on, the scene belongs to the render thread.** `OnAttached`, `OnMessage`, `OnRender` and the callbacks of
  everything they create run there. In Debug the engine's thread pin catches UI-thread code that reaches into a live
  scene.
- **The UI thread talks to it through `Post` and `Invoke<TScene>` only.** Both go through the compositor's batch queue
  and arrive in order; messages posted before the canvas is shown are queued behind the attach.
- **Scene callbacks must not touch controls.** `OnUpdate`, `OnComplete` and the rest fire off the UI thread;
  `PostToUI(Action)` is the way back. `Then()` and `await` on a scene timer complete on the thread pool, because the
  render thread has no synchronization context.
- **Drawing is `ImmediateDrawingContext`**: bitmaps, lines, rectangles, ellipses and glyph runs, with immutable brushes
  and pens. `Brush(color, opacity)` caches them per scene, so drawing from a tween does not allocate a brush per frame. There is no path or geometry drawing — reach for the Skia lease feature if you
  need one.
- **The scene's `Engine` is its own.** It starts from a copy of `Presto.Defaults`, and after that `Presto.Engine`'s
  speed, fps and pause state do not apply to it.

Only what the scene draws itself runs on the render thread. Animating an *existing* control there — its transform or its
opacity through the composition visual Avalonia already gives it — is designed
([COMPOSITOR-PLAN.md](docs/COMPOSITOR-PLAN.md) §4.3) but not in this release: animation of controls is UI-thread work,
and stops with the UI thread. WebAssembly has a caveat of its own, in [Platforms and status](#platforms-and-status).

## anime.js → Presto

Every entry point of anime.js 4.5.0's `src/index.js`:

| anime.js | Presto |
|---|---|
| `animate(targets, params)` | `Presto.Animate(targets, new AnimationParams { … })`, or `control.Animate(new() { … })` |
| `createTimer(params)` | `Presto.CreateTimer(new TimerParams { … })` |
| `createTimeline(params)` | `Presto.CreateTimeline(new TimelineParams { … })`, then `.Add` / `.Set` / `.Sync` / `.Label` / `.Call` / `.Remove` / `.Refresh` |
| `createAnimatable(targets, params)` | `Presto.CreateAnimatable(targets, new AnimatableParams { … })`, or `control.CreateAnimatable(new() { … })` |
| `createScope(params)` | `Presto.CreateScope(new ScopeParams { … })`; `PrestoAvalonia.CreateScope(control)` reverts when the control detaches |
| `createDraggable(target, params)` | `PrestoAvalonia.CreateDraggable(control, new DraggableParams { … })` |
| `onScroll(params)` | `PrestoAvalonia.OnScroll(new ScrollObserverParams { … })`, over a `ScrollViewer` instead of the window |
| `svg.createDrawable(shape)` | `PrestoAvalonia.CreateDrawable(shape, start, end)`, animated through `["Draw"] = "0 1"` |
| `svg.createMotionPath(path)` | `PrestoAvalonia.CreateMotionPath(path, offset)` → the `X` / `Y` / `Rotate` tweens |
| `svg.morphTo(path, precision)` | `PrestoAvalonia.MorphTo(geometry, precision)`, assigned to a `Path`'s `["Data"]` |
| `text.split(target, params)` / `splitText` | `PrestoAvalonia.SplitText(textBlock, new SplitTextParams { … })` → `TextSplitter` with `Lines` / `Words` / `Chars` |
| `text.scramble(params)` / `scrambleText` | `PrestoAvalonia.ScrambleText(new ScrambleTextParams { … })`, assigned to a `["Text"]` property |
| `stagger(value, params)` | `Presto.Stagger(value, new StaggerParams { … })`; `PrestoAvalonia.Stagger(…)` measures the auto grid from the layout |
| `utils.*` | `Utils.*` in PascalCase; the value-less overloads return a chainable `ChainedUtil` (`Utils.MapRange(0, 100, 0, 1000).Clamp(0, 100)`), usable wherever a `Func<double, double>` modifier is; `utils.get` / `set` / `remove` are `Presto.Get` / `Set` / `Remove`, with `PrestoAvalonia` overloads that take an `AvaloniaProperty`; `padStart` / `padEnd` are not ported (`string.PadLeft` / `PadRight` already do this) |
| `eases` / `easings` | `Eases.*`, the `Ease` delegate (`double → double`), `Spring`, `EaseParser.Parse("inOutQuint")` |
| `engine` | `Presto.Engine` (`Speed`, `Fps`, `Precision`, `TimeUnit`, `Pause()`, `Resume()`, `FrameSource`) |
| `globals` / per-call defaults | `Presto.Defaults`, or `ScopeParams.Defaults` / `TimelineParams.Defaults` for a subtree |
| `registerAdapter` (`adapters/`) | `Presto.Adapters` (an `ITargetAdapter` list) and `Presto.Codecs` (per property type). `adapters/three.js` itself is **not ported** — the interfaces are there if you want to write one. |
| `waapi.*` | Partly, and not as an API port — there is no Web Animations API here. `CompositorCanvas` / `CompositorScene` run a whole engine on Avalonia's render thread, which upstream has no counterpart for; `waapi.animate` itself — an existing control driven by the compositor — is **not ported**, so control animation is still UI-thread property animation. |
| `createLayout` / `AutoLayout` (`layout/`) | **Not ported.** FLIP layout animation needs a port over measured bounds rather than the DOM box model; deferred past 0.1. |
| `utils.$` (`registerTargets`) | **Not ported.** There are no selector strings: pass an object, an `IEnumerable`, or an Avalonia `Controls` collection straight to `Animate`. |
| `utils.cleanInlineStyles` | **Not ported.** The bridge writes at `BindingPriority.Animation`, so `Revert()` clears the animated values and the control's own values reappear; there is nothing inline to clean. |
| `core/units.js`, `core/styles.js` | Partly. Numbers, `deg` / `rad` / `turn`, and `%` against the current value are ported; `px` / `em` / `rem`, CSS variables and DOM attribute animation are **not** — Avalonia has no CSS box model. |
| `core/*`, `types/*` | Internal (`Clock`, `Renderer`, `Composer`, value parsing) or TypeScript declarations with no C# counterpart. |

There is no XAML surface for animations yet — no attached properties, no markup extension. It is deliberately deferred
until the C# API has settled, so it is not built against something about to change. Animate from code-behind or a view
model for now.

### Parameter names

anime.js parameter objects become `init`-only records with the same names in PascalCase.

| anime.js | Presto |
|---|---|
| `duration` | `Duration` |
| `delay` | `Delay` |
| `loop` | `Loop` (`LoopCount`: `true` or a count) |
| `loopDelay` | `LoopDelay` |
| `alternate` | `Alternate` |
| `reversed` | `Reversed` |
| `autoplay` | `Autoplay` |
| `frameRate` | `FrameRate` |
| `playbackRate` | `PlaybackRate` |
| `ease` | `Ease` |
| `playbackEase` | `PlaybackEase` |
| `composition` | `Composition` (`Replace` / `None` / `Blend`) |
| `modifier` | `Modifier` |
| `keyframes` | `Keyframes` |
| `id` | `Id` |
| `from`, `to` | `TweenParams.From`, `TweenParams.To` |
| `onBegin`, `onBeforeUpdate`, `onUpdate`, `onLoop`, `onPause`, `onComplete`, `onRender` | `OnBegin`, `OnBeforeUpdate`, `OnUpdate`, `OnLoop`, `OnPause`, `OnComplete`, `OnRender` |
| stagger `from`, `reversed`, `ease`, `grid`, `axis`, `start`, `total`, `use`, `modifier`, `jitter` | `StaggerParams.From`, `.Reversed`, `.Ease`, `.Grid`, `.Axis`, `.Start`, `.Total`, `.Use`, `.Modifier`, `.Jitter` |
| spring `mass`, `stiffness`, `damping`, `velocity`, `bounce`, `duration` | `Eases.Spring(mass:, stiffness:, damping:, velocity:, bounce:, duration:)` |
| `persist` | Not ported — it only exists on anime.js's WAAPI path. |

## C# differences

A handful of places where the JS shape does not survive the type system:

- **Properties go through an indexer**, which is the only C# syntax that mixes named settings and arbitrary keys in one
  initializer: `new() { ["X"] = 320, Duration = 1250 }`. The bridge adds a typed `AvaloniaProperty` indexer, so
  `[Visual.OpacityProperty] = 0.4` sits alongside `["Opacity"] = 0.4`.
- **A lambda literal needs its delegate type named.** C# does not apply user-defined implicit conversions to lambdas, so
  write `Ease = (Ease)(t => t)` and `["X"] = new FunctionValue((target, i, total) => i * 10)`. Values that already carry
  their type (`Eases.OutQuad`, `Presto.Stagger(…)`, `Utils.Snap(40)`) convert on their own.
- **A positioned timer child names its type.** `Add(targets, parameters, position)` and `Add(parameters, position)` are
  ambiguous when the first argument is a target-typed `new() { … }`, so write
  `tl.Add(new TimerParams { Duration = 400 }, 20)`.
- **`Loop` is a `LoopCount`**, with implicit conversions from `bool` and `int`: `Loop = true` is infinite, `Loop = 2` runs
  three iterations, and `LoopCount.Infinite` is the explicit form.
- **`.then()` is `Then()` or `await`.** `await animation` works directly and yields the animation itself; `Then(callback)`
  returns the `Task` for parity with upstream.

## Platforms and status

Version 0.1.0, pre-release: the API is settled enough to build against but not frozen, and a pass over it is planned
before 1.0. Behavioural parity with anime.js 4.5.0 is covered by the upstream suites ported into
`tests/Monaco.Presto.Tests` and `tests/Monaco.Presto.AvaloniaBridge.Tests`.

The core is pure .NET and runs anywhere net10.0 does, and the bridge uses no desktop-only API. All four sample heads
build; what has actually been *run* differs, and the table says so rather than implying more.

| Head | Verified |
|------|----------|
| **Desktop** — `net10.0`, Windows/macOS/Linux | Runs. The development target: every gallery page is driven here, and it is what the bridge is written against. |
| **Browser** — `net10.0-browser`, WebAssembly | Runs, with one caveat (below): the 15 gallery pages that predate the compositor one were stepped through in Chrome — the timer tracks wall-clock time, the draggable cards take pointer capture, the scroll observer follows the `ScrollViewer` offsets, the animatable grid follows the cursor, and split/scramble text, motion paths and stroke drawing all render. The `Compositor` page has not been driven there. |
| **Android** — `net10.0-android` | Builds, installs and launches on a device (arm64, Android 16): the activity comes up with no startup exception. The gallery has not been driven on a device, so touch input and per-page rendering are unverified. |
| **iOS** — `net10.0-ios` | Compiles for `iossimulator-x64` only. Signing, packaging and running need a macOS build host, so nothing has been run. |

Three platform notes:

- **On WebAssembly the render loop is not a thread.** `BrowserRenderTimer` drives the compositor from the single WASM
  thread that also runs the UI, so a `CompositorScene` runs and draws there but does **not** survive a blocked UI
  thread — the busy-indicator guarantee is a desktop and mobile one. The same goes for any platform that opts into
  `ShouldRenderOnUIThread` (a Win32 and X11 option). Everywhere else the render loop has a thread of its own: Windows,
  macOS, Linux X11, Android (`ChoreographerTimer`) and iOS (`DisplayLinkTimer`).
- **Hot reload does not work on WebAssembly.** The file watcher cannot start there, and the browser head logs that at
  startup; `.axaml` edits need a rebuild.
- **On Android the frame pump starts on a dispatcher timer.** Avalonia's Android lifetime exposes only a factory for its
  main view, so the bridge cannot ask it for a `TopLevel`; it uses the first visual it animates instead and moves onto
  that top level's frame callback. Until then a 60 Hz render-priority timer pumps, so animations run either way.

## Running the sample and the tests

The sample gallery has 16 pages — one per anime.js documentation area, plus the render-thread one — and doubles as the
manual acceptance surface.

```powershell
dotnet build Monaco.Presto.slnx
dotnet run --project samples/PrestoSample.Desktop
dotnet watch --project samples/PrestoSample.Desktop        # hot reload

dotnet test --project tests/Monaco.Presto.Tests            # note --project, not a bare path
dotnet test --project tests/Monaco.Presto.AvaloniaBridge.Tests
dotnet test --solution Monaco.Presto.slnx
```

Tests use xunit v3 on Microsoft.Testing.Platform, which the .NET 10 SDK requires — hence `--project`.

The implementation plan, architecture notes and the phase checklist live in [docs/PLAN.md](docs/PLAN.md).

## Credits

Presto is a port of **[anime.js](https://github.com/juliangarnier/anime)** by Julian Garnier, used under the MIT
licence. The timing model, the parameter vocabulary and most of the test suites come from anime.js 4.5.0; the bugs are
mine. If you write JavaScript, use the original — its
[documentation](https://animejs.com/documentation) is also the best reference for this library.

## Licence

Presto is MIT licensed — see [LICENSE](LICENSE), which also carries anime.js's own MIT notice.
