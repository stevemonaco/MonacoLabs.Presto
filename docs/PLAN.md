# Monaco.Presto — Implementation Plan

Port of the anime.js v4.5.0 animation model to .NET, with a framework-agnostic core and an Avalonia adapter. This document is the working plan: architecture, API shape, phase checklist and open decisions. Update the checklist as phases land.

Upstream reference: https://github.com/juliangarnier/anime (tag `v4.5.0`, `src/` and `tests/suites/`). Docs: https://animejs.com/documentation.

## 1. Goals and non-goals

**Goals**

- anime.js-level ergonomics from C#: one call to animate any property of any object or control, with staggering, keyframes, timelines and full playback control.
- Behavioural parity with anime.js for the timing model (delay, loop, loopDelay, alternate, reversed, seek, stretch, playbackRate, frameRate, composition) so upstream tests can be ported as the acceptance suite.
- A core with zero UI dependencies that can drive plain objects (view models, custom renderers, game-style loops) and is fully testable with a manual clock.
- An Avalonia bridge that makes controls first-class targets: `AvaloniaProperty` by name or typed reference, transform shorthands (`X`, `Y`, `Rotate`, `Scale`…), colors/brushes, thickness/corner radius/points, with correct thread affinity and lifetime handling.

**Non-goals (for now)**

- WAAPI / composition-thread animations. Everything is UI-thread property animation; GPU-side animations can be explored later via Avalonia's composition API.
- CSS-specific value handling (unit conversion between px/em/rem, CSS variables, DOM attribute animation). Only what maps to Avalonia survives: numbers, degrees/turns/radians for rotation, percentages where Avalonia uses `RelativePoint`/`RelativeRect`.
- Three.js-style adapters. The adapter interface is designed so that one could be written, but none ships.

## 2. Module map: anime.js → Presto

| anime.js (`src/`) | Presto namespace / type | Project | Phase |
|---|---|---|---|
| `core/clock.js`, `core/globals.js`, `core/consts.js` | `Monaco.Presto.Core.Clock`, `PrestoDefaults`, `Constants` | Core | 1 |
| `engine/engine.js` | `Monaco.Presto.Engine` + `IFrameSource` | Core | 1 |
| `core/render.js` (`tick`, `render`) | `Monaco.Presto.Core.Renderer` (static tick/render) | Core | 1, 3 |
| `timer/timer.js` | `Monaco.Presto.Timer` | Core | 1 |
| `easings/*` | `Monaco.Presto` (`Ease` delegate, `Eases`, `LinearStop`, `Spring`, `EaseParser`) | Core | 2 |
| `core/values.js`, `core/units.js`, `core/colors.js`, `core/targets.js`, `core/helpers.js` | `Monaco.Presto.Values` (`TweenValue`, `ValueKind`, `PrestoColor`, `RelativeValue`, `FunctionValue`), `Monaco.Presto.Targets` (`ITargetAdapter`, `IPropertyAccessor`, `ObjectTargetAdapter`) | Core | 3 |
| `animation/animation.js`, `composition.js`, `additive.js` | `Monaco.Presto.Animation`, `Tween`, `Composition`, `AdditiveAnimation` | Core | 3 |
| `core/transforms.js`, `core/styles.js` | Bridge only: `RenderTransformComposer`, property name resolution | Bridge | 5 |
| `timeline/timeline.js`, `position.js` | `Monaco.Presto.Timeline`, `TimelinePosition` | Core | 4 |
| `utils/stagger.js` | `Monaco.Presto.Stagger` (`Presto.Stagger(...)`) | Core | 4 |
| `utils/number.js`, `random.js`, `time.js`, `chainable.js` | `Monaco.Presto.Utils` (`Lerp`, `Clamp`, `MapRange`, `Damp`, `Snap`, `Wrap`, `Round`, `RoundPad`, `Random`, `SeededRandom`, `RandomPick`, `Shuffle`, `DegToRad`, `RadToDeg`, `KeepTime`, `Sync`) | Core | 4 |
| `utils/target.js` (`$`, `get`, `set`, `remove`, `cleanInlineStyles`) | Core `Presto.Get/Set/Remove` on objects; bridge overloads for controls | Core + Bridge | 4, 6 |
| `animatable/animatable.js` | `Monaco.Presto.Animatable` | Core | 6 |
| `scope/scope.js` | `Monaco.Presto.Scope`; bridge `ControlScope` (auto-revert on unload) | Core + Bridge | 6 |
| `draggable/draggable.js` | `Monaco.Presto.AvaloniaBridge.Draggable` | Bridge | 7 |
| `events/scroll.js` | `Monaco.Presto.AvaloniaBridge.ScrollObserver` (ScrollViewer) | Bridge | 7 |
| `svg/*` (`morphTo`, `createDrawable`, `createMotionPath`) | `Monaco.Presto.AvaloniaBridge.Shapes` (Path/Geometry based) | Bridge | 7 |
| `text/*` (`splitText`, `scrambleText`) | `Monaco.Presto.AvaloniaBridge.Text` | Bridge | 7 |
| `layout/*` | maybe later (FLIP via `TransformedBounds`) | Bridge | — |
| `waapi/*`, `adapters/three.js` | not ported | — | — |

## 3. Architecture

### 3.1 Core (`Monaco.Presto`)

```
Clock                      framerate + playback-rate bookkeeping (base of Engine, Timer)
└─ Engine                  singleton root clock; owns the linked list of running Tickables; Update(now)
└─ Timer                   delay/duration/loop/alternate/reverse state machine, callbacks, controls
   ├─ Animation            Timer + linked list of Tweens over resolved targets
   └─ Timeline             Timer + linked list of child Timers/Animations, labels, positions
Tween                      one (target, property) interpolation: from/to channels, ease, composition
Renderer                   static Tick(tickable, time, muteCallbacks, internalRender, tickMode) and Render()
IFrameSource               Start(Action tick) / Stop(); core ships ManualFrameSource (tests) and StopwatchFrameSource (headless)
ITargetAdapter             CanHandle(object) → IPropertyAccessor Resolve(object target, string property)
IPropertyAccessor          GetValue/SetValue on a target + IValueCodec describing how to tween the value
IValueCodec                Decode(object) → channels (double[] or single double), Encode(channels) → object
```

Key ports, kept structurally identical to upstream for testability:

- **Linked lists** (`_head/_tail/_prev/_next`) for engine children and timeline children. Tweens hang off `Timer` itself (`_headTween`/`_tailTween`), mirroring upstream where `JSAnimation` reuses the renderable's list, so `Renderer` and the additive animation work on any timer. Zero-allocation ticking.
- **`tick()` in `render.js`** — the single function that turns absolute time into iteration time, handles loop/alternate/reverse/delay/loopDelay, fires callbacks and decides whether to render. This is the highest-value port; do it first and port `timings`, `controls`, `callbacks`, `directions` tests against it before any tweens exist.
- **Composition** (`replace` | `none` | `blend`): per (target, property) sibling tween registry (`ConditionalWeakTable<object, …>`), so a new animation on the same property cancels/blends the old one as in anime.js. `blend` uses additive animations (`additive.js`).
- **Function-based values** everywhere a number is accepted: `FunctionValue` is `(object target, int index, int total) => TweenValue`. Upstream's fourth argument is the timeline a value is resolved in, which only stagger positions use; here it lives on `Stagger` instead, which `Timeline.Add` invokes with `this` and `Animation` with `null`, leaving `FunctionValue` at three arguments. Its `prevTween` argument belongs to the Phase 7 WAAPI/draggable work.
- **Values**: `ValueKind.Number | Unit | Color | Complex`. Colors are parsed by the core (`PrestoColor` from `#rgb`, `#rrggbb(aa)`, `rgb()`, `rgba()`, `hsl()`, `hsla()`), so `colors.test.js` is portable. `Complex` covers typed structs that decompose into several numeric channels (Thickness, Point…) via the target's `IValueCodec`.
- **Time** is `double` milliseconds; `Engine.TimeUnit = Seconds` scales defaults and speed exactly as upstream.
- **Thread model**: single-threaded, no locks. The engine records the thread it was first ticked on and asserts in Debug if touched from another thread.

### 3.2 Avalonia bridge (`Monaco.Presto.AvaloniaBridge`)

- **`AvaloniaFrameSource`** — `TopLevel.RequestAnimationFrame(Action<TimeSpan>)`, re-requested each frame while the engine has children (exactly `requestAnimationFrame`). It is a one-shot on the global `MediaContext`, so any live `TopLevel` works; the bridge picks the first one from the application lifetime or from the first animated control, and falls back to a `DispatcherTimer` at `DispatcherPriority.Render` when no `TopLevel` exists yet. Installed automatically on first use (`PrestoAvalonia.EnsureInitialized()`), optionally explicit via `AppBuilder.UsePresto()`.
- **`AvaloniaObjectTargetAdapter`** — resolves properties on any `AvaloniaObject`:
  - typed: `AvaloniaProperty` instances (`Canvas.LeftProperty`, `Visual.OpacityProperty`);
  - by name: `"Opacity"`, `"Width"`, `"Canvas.Left"`, `"Margin"` through `AvaloniaPropertyRegistry` (owner type + attached);
  - transform shorthands `X`, `Y` (`TranslateX/Y`), `Rotate`, `Scale`, `ScaleX`, `ScaleY`, `SkewX`, `SkewY` — composed into a `TransformGroup` that the bridge owns on `Visual.RenderTransform` (mirrors `transforms.js`, which manages the CSS transform string). Values in DIPs / degrees; `"1turn"`, `"0.5rad"` accepted like upstream.
  - Value codecs: `double/float/int`, `Color`, `SolidColorBrush` (animates the brush's color, creating a local brush if the current one is frozen/shared), `Thickness`, `CornerRadius`, `Point`, `Size`, `Rect`, `Vector`, `RelativePoint`, and an `Easing` ↔ `Ease` adapter (`PrestoEasing : Avalonia.Animation.Easings.Easing`) so Presto eases can be used in Avalonia `Transitions` and vice versa. A decomposed `Matrix` codec is deferred until a property needs one — the transform shorthands go through the bridge's own `TransformGroup`.
  - Writes use `SetValue(..., BindingPriority.Animation)` so animated values sit above local values and are cleanly removed by `Revert()` (`ClearValue` at animation priority).
- **Control helpers**: `control.Animate(...)`, `Presto.Get(control, "X")`, `Presto.Set(control, …)`, `Presto.Remove(control)`, `ControlScope` that reverts everything created inside it when the control is unloaded.
- Later: `Draggable`, `ScrollObserver`, shape helpers (`Geometry.ContourLength`, `TryGetPointAndTangentAtDistance` for drawable strokes and motion paths), text splitting, XAML attached properties/behaviors.

## 4. Public API sketch

The shape below is the target; details get refined while porting, but new APIs should stay in this style.

```csharp
using Monaco.Presto;
using Monaco.Presto.AvaloniaBridge;

// Timer (anime.js createTimer)
var timer = Presto.CreateTimer(new()
{
    Duration = 1000,
    Loop = true,
    FrameRate = 30,
    OnUpdate = t => label.Text = t.CurrentTime.ToString("0"),
});

// Animation on a control (anime.js animate)
var anim = square.Animate(new()
{
    ["X"] = 320,                                  // transform shorthand → RenderTransform
    ["Rotate"] = new TweenParams { From = -180 },  // per-property tween params
    [Visual.OpacityProperty] = 0.5,               // typed AvaloniaProperty
    ["Background"] = "#ff6b6b",                   // brush/color by name, string parsed by core
    Duration = 1250,
    Delay = Presto.Stagger(65, new() { From = StaggerFrom.Center }),
    Ease = Eases.InOutQuint,
    Loop = true,
    Alternate = true,
    OnComplete = a => { /* ... */ },
});

// Plain objects work with the same call (anime.js JS object targets)
Presto.Animate(viewModel, new() { ["Progress"] = 100, Duration = 500, Ease = Eases.Out(3) });

// Keyframes
Presto.Animate(box, new()
{
    ["X"] = TweenValue.Keyframes(new TweenParams { To = 100, Duration = 400 }, new TweenParams { To = 0, Ease = Eases.InQuad }),
});

// Timeline (anime.js createTimeline)
var tl = Presto.CreateTimeline(new() { Defaults = new() { Duration = 750, Ease = Eases.OutExpo } })
    .Add(a, new() { ["X"] = 200 })
    .Add(b, new() { ["Rotate"] = 90 }, "-=250")
    .Label("half")
    .Call(() => Console.WriteLine("half way"), "half")
    .Add(c, new() { ["Scale"] = 1.5 }, "<");

await tl.Then();              // anime.js .then()
tl.Seek(500); tl.Reverse(); tl.Stretch(3000);

// Animatable (anime.js createAnimatable)
var follower = cursor.CreateAnimatable(new() { ["X"] = 500, ["Y"] = 500, Ease = Eases.Out(3) });
follower.Set("X", pointer.X);   // eases toward the value over 500 ms

// Eases
Eases.OutQuad; Eases.Out(2); Eases.InOutBack(1.7); Eases.OutElastic(1, .3);
Eases.CubicBezier(.17, .67, .83, .67); Eases.Steps(5); Eases.Linear(0, "0.25 75%", 1);
Eases.Spring(mass: 1, stiffness: 100, damping: 10, velocity: 0); EaseParser.Parse("inOutQuint");

// Global defaults and engine (anime.js engine / defaults)
Presto.Defaults.Ease = Eases.Out(2);
Presto.Engine.Speed = 0.5;
Presto.Engine.Fps = 60;
Presto.Engine.Precision = 4;
```

Design notes behind the sketch:

- Parameter objects are `init`-only: `TimerParams`, `AnimationParams` and `TimelineParams` are records so `Timeline.Set` can override a few members with `with`; `TweenParams` is a plain class. `AnimationParams` exposes a `string` indexer (and, in the bridge, an `AvaloniaProperty` indexer via a derived params type) so property tweens and settings can mix in a single initializer. Indexer initializers are the only C# syntax that allows this mix, which is why a collection-initializer/`Add` design was rejected.
- `TweenValue` is a small struct with implicit conversions from `double`, `int`, `string` (`"+=50"`, `"#ff0000"`, `"1turn"`, `"50%"`), `TweenParams`, `FunctionValue` and typed values (`object`). It carries the raw value; adapters decode it against the property's current value, mirroring `values.js`.
- `LoopCount` has implicit conversions from `int` and `bool` (`Loop = true` ⇒ infinite).
- Every `Timer` is awaitable (`GetAwaiter`) as well as exposing `Then()` for parity; `Animation` narrows both to itself.
- `Timeline.Add(TimerParams, position)` and `Add(targets, AnimationParams, position)` are ambiguous when the first argument is a target-typed `new() { … }`, so a positioned timer child must name its type: `Add(new TimerParams { … }, 20)`.
- C# does not apply user-defined implicit conversions to lambda literals, so a delegate written inline needs its type named: `Ease = (Ease)(t => t)`, `["X"] = new FunctionValue((t, i, n) => i * 10)`. Values that already carry their own type (`Eases.*`, `Presto.Stagger(...)`) convert implicitly as the sketch shows.
- `utils.set()` is `Presto.Set(targets, parameters)`. It landed in Phase 3 rather than with the other target utils because it is the animation constructor's `fastSet` path; `Presto.Get`/`Presto.Remove` follow in Phase 4.
- `persist` exists only on anime.js 4.5.0's WAAPI path, so the core does not port it.

## 5. Phases

Phase order follows dependencies; each phase ends green on its ported test suites and, from phase 5 on, with a sample page.

- [x] **Phase 0 — Solution setup.** Root `Directory.Build.props`/`Directory.Packages.props`, `.editorconfig`, project references, core test project, DevTools + Hot Reload wiring, `CLAUDE.md`, this plan.
- [x] **Phase 1 — Timing engine.** `Constants`, `PrestoDefaults`, `Clock`, `Engine` (+ `IFrameSource`, `ManualFrameSource`, `StopwatchFrameSource`), `Timer`, `Renderer.Tick` (with a no-tween render path), callbacks, controls (`Play/Pause/Resume/Restart/Reverse/Alternate/Seek/Stretch/Complete/Cancel/Revert/Reset`), `Then()`/awaitable. Tests: `engine`, `timings`, `controls`, `callbacks` (timer subset), `directions`, `promises`, `seconds`.
- [x] **Phase 2 — Easings.** All built-in eases, `in/out/inOut/outIn` composition with parameters (`Back`, `Elastic`, power), `Linear` (with stops), `Steps`, `Irregular`, `CubicBezier`, `Spring` (with computed duration), `EaseParser`. Tests: `eases` plus generated parity fixtures (see §6).
- [x] **Phase 3 — Values, targets and animations.** `TweenValue`, value parsing (numbers, relative `+=/-=/*=`, units, colors), `PrestoColor`, `FunctionValue`, `ITargetAdapter`/`IPropertyAccessor`/`IValueCodec`, `ObjectTargetAdapter` (reflection with cached delegates), `Tween`, `Animation` (from/to, per-property params, keyframes duration- and percentage-based, `playbackEase`, `persist`, `modifier`), composition (`replace/none/blend`) and additive. Tests: `tweens`, `values`, `colors`, `units` (core subset), `keyframes`, `function-based-values`, `parameters`, `targets`, `animations`, `callbacks` (animation subset), `leaks`.
- [x] **Phase 4 — Timeline, stagger, utils.** `TimelinePosition` parsing (`"+=100"`, `"<"`, `"<<"`, labels), `Timeline.Add/Set/Sync/Label/Call/Remove/Init/Refresh`, timeline defaults, `Stagger` (time/value/range, `from`, `reversed`, `ease`, `grid`, `axis`, `modifier`, `use`, `total`, `jitter`), `Utils` (numeric, random, time, chainable). Tests: `timelines`, `stagger`, `utils`, plus the two composition cases Phase 3 could not reach without a timeline (`tweens`: "Properly override timeline tweens", "Do not pause timeline with partially overriden tweens").
- [x] **Phase 5 — Avalonia bridge MVP + sample gallery.** `AvaloniaFrameSource`, `AvaloniaObjectTargetAdapter` (typed + named properties, transform shorthands via `TransformGroup`, codecs for double/Color/Brush/Thickness/CornerRadius/Point/Size/Rect/Vector/RelativePoint), `BindingPriority.Animation` writes and revert, `control.Animate(...)` extensions, `AvaloniaAnimationParams`' typed `AvaloniaProperty` indexer, `PrestoEasing` adapter. Sample: gallery shell (nav + page host) and pages for Timer, Animation basics, Value types, Keyframes, Playback settings, Callbacks, Controls, Timeline, Stagger, Eases visualizer. Bridge tests project on `Avalonia.Headless`. Also closes the two Phase 4 items that need real layout: `Stagger` auto-grid from measured control positions (`stagger` "Computes auto grid from DOM element positions"), which the core opens up through a `StaggerParams.Coordinates` provider, and `PrestoAvalonia.Stagger(...)`, the bridge overload that fills it by measuring each `Visual`. Tests: `transforms` (adapted), bridge codec tests.
- [x] **Phase 6 — Animatable, Scope, target utils.** `Animatable` (getter/setter API with per-property duration/ease), `Scope` + `ControlScope` (revert on unload), `Presto.Get/Set/Remove` for controls. Tests: `animatables`, `scope`. Sample: follow-cursor page.
- [ ] **Phase 7 — Extras (each independently shippable).** `Draggable` (pointer capture, axes, snap, container, release physics), `ScrollObserver` (ScrollViewer thresholds/sync modes), shape helpers (`CreateDrawable`, `CreateMotionPath`, `MorphTo`), `SplitText`/`ScrambleText`, XAML attached properties for declarative use. Tests: `draggables`, `scroll`, `svg`, `text` (adapted). Sample pages per feature.
- [ ] **Phase 8 — Polish and packaging.** XML docs, README with anime.js → Presto cheat sheet, NuGet metadata for both packages, consider `net8.0` multi-targeting for the core, Browser/Android/iOS smoke runs, perf pass on the tick loop (allocation-free under `dotnet-counters`; `BrushCodec.Encode` allocates a `SolidColorBrush` per frame because the source brush is often immutable or shared).

## 6. Testing strategy

- **Core**: xunit v3, no Avalonia. Tests drive `Engine.Update(now)` directly through `ManualFrameSource.Advance(ms)`, giving deterministic timing identical to how upstream tests use `engine.update()`/fake timers.
- **Upstream parity**: port suites from `tests/suites/*.test.js` one per phase; keep test names close to the Mocha `it()` descriptions so gaps are easy to audit. Suites that are DOM-only (`waapi`, `threejs`, `node`, `build`) are skipped.
- **Generated fixtures**: `tools/anime-fixtures/` (Node, `npm i animejs@4.5.0`) dumps JSON for easing curves (101 samples each), spring durations, stagger outputs and timeline positions; the C# tests assert within `1e-6`. Regenerate only when bumping the upstream reference version.
- **Bridge**: `tests/Monaco.Presto.AvaloniaBridge.Tests` on `Avalonia.Headless` with `[AvaloniaFact]`/`[AvaloniaTheory]` from `Avalonia.Headless.XUnit`. That package targets `xunit.v3.extensibility.core` 3.2.2, so `xunit.v3` is pinned to 3.2.x repo-wide (on 4.x, discovery throws `MissingMethodException`). It is also why `AssemblyInfo.cs` uses `CollectionBehavior(DisableTestParallelization = true)` rather than the 4.x `Parallelization` attribute. Tests run with a `ManualFrameSource` substituted for the RAF pump: codec round-trips, transform composition, animation-priority revert, scope lifetime.
- **Visual**: the sample gallery is the manual acceptance surface; DevTools screenshots/property reads via MCP can be used to confirm end states.

## 7. Decisions and open questions

Decided (change here if reconsidered):

1. Time unit is milliseconds as `double`; seconds mode is an engine scale factor (parity).
2. `Defaults.Ease` is `Eases.Out(2)` like upstream, not linear.
3. Property tweens use the string indexer on params objects; typed `AvaloniaProperty` indexer available in the bridge. No fluent builder in v1 (can be layered on later without changing the model).
4. Transform shorthands write a bridge-owned `TransformGroup` to `RenderTransform`; if the user already set a non-Presto `RenderTransform`, it is wrapped as the first child rather than replaced.
5. Animated values are written at `BindingPriority.Animation` so `Revert()` restores user values precisely.
6. Bridge root namespace is `Monaco.Presto.AvaloniaBridge` to avoid shadowing `Avalonia.*`.
7. Scoping to a control lifetime is explicit: `PrestoAvalonia.CreateScope(control)` reverts when the root detaches from the visual tree (not on `Unloaded`, which never fires for a root detached before its `Loaded` job runs), and `Animate` never auto-scopes; no `AutoRevert` parameter in v1.

Open (confirm before the relevant phase):

- `Timeline.Set` takes only a `TimelinePosition`, so a stagger cannot position a setter group as it can upstream; add the overload if a sample needs it.
- Function-based `composition` (upstream runs `composition` through `getFunctionValue`): `TweenParams.Composition`/`AnimationParams.Composition` are plain enums, so a per-target delegate is not expressible. No ported test needs it; decide whether parity is worth making the property function-capable.
- Rotation unit for the bare `Rotate` shorthand: degrees (Avalonia `RotateTransform.Angle`) is the natural default; upstream defaults to `deg` too, so parity holds. Confirm we do not want radians anywhere by default.
- Core target frameworks for NuGet: `net10.0` only for now; decide on `net8.0` at phase 8.
- XAML surface: attached property triggers (`presto:Animate.OnLoaded`) vs. behaviors package. Decide at phase 7 once the C# API is stable.
