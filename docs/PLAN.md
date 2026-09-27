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

- WAAPI-style compositor animation was a non-goal through Phase 8. It is now planned as Phases 9–12: the scope study, the verified Avalonia behaviour and the design live in [docs/COMPOSITOR-PLAN.md](COMPOSITOR-PLAN.md).
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
| `core/values.js`, `core/units.js`, `core/colors.js`, `core/targets.js`, `core/helpers.js`, `adapters/registry.js` | `Monaco.Presto.Values` (`TweenValue`, `ValueKind`, `PrestoColor`, `FunctionValue`), `Monaco.Presto.Targets` (`ITargetAdapter`, `IPropertyAccessor`, `ObjectTargetAdapter`), registered through `Presto.Adapters`/`Presto.Codecs` | Core | 3 |
| `animation/animation.js`, `composition.js`, `additive.js` | `Monaco.Presto.Animation`, `Tween`, `Composition`, `Core.Additive` | Core | 3 |
| `core/transforms.js`, `core/styles.js` | Bridge only: `RenderTransformComposer`, property name resolution | Bridge | 5 |
| `timeline/timeline.js`, `position.js` | `Monaco.Presto.Timeline`, `TimelinePosition` | Core | 4 |
| `utils/stagger.js` | `Monaco.Presto.Stagger` (`Presto.Stagger(...)`) | Core | 4 |
| `utils/number.js`, `random.js`, `time.js`, `chainable.js` | `Monaco.Presto.Utils` (`Lerp`, `Clamp`, `MapRange`, `Damp`, `Snap`, `Wrap`, `Round`, `RoundPad`, `Random`, `CreateSeededRandom`, `RandomPick`, `Shuffle`, `DegToRad`, `RadToDeg`, `KeepTime`, `Sync`) | Core | 4 |
| `utils/target.js` (`$`, `get`, `set`, `remove`, `cleanInlineStyles`) | Core `Presto.Get/Set/Remove` on objects; bridge overloads for controls. `$` has no equivalent (no selectors) and `cleanInlineStyles` none is needed (animation-priority writes) | Core + Bridge | 4, 6 |
| `animatable/animatable.js` | `Monaco.Presto.Animatable` | Core | 6 |
| `scope/scope.js` | `Monaco.Presto.Scope`; bridge `ControlScope` (auto-revert on unload) | Core + Bridge | 6 |
| `draggable/draggable.js` | `Monaco.Presto.AvaloniaBridge.Draggable` | Bridge | 7 |
| `events/scroll.js` | `Monaco.Presto.AvaloniaBridge.ScrollObserver` (ScrollViewer) | Bridge | 7 |
| `svg/*` (`morphTo`, `createDrawable`, `createMotionPath`) | `Monaco.Presto.AvaloniaBridge.Shapes` (Path/Geometry based) | Bridge | 7 |
| `text/*` (`splitText`, `scrambleText`) | `Monaco.Presto.AvaloniaBridge.Text` | Bridge | 7 |
| `layout/*` | not ported: FLIP on the compositor was built and dropped (see [COMPOSITOR-PLAN.md](COMPOSITOR-PLAN.md) §4.4) | — | — |
| `waapi/*` | `Monaco.Presto.AvaloniaBridge.Compositor`: `CompositorAnimation` for existing controls, server-side `KeyFrameAnimation`s compiled from Presto params ([COMPOSITOR-PLAN.md](COMPOSITOR-PLAN.md) §4.3), plus `CompositorCanvas`/`CompositorScene`, a whole engine on the render thread (§4.2), which has no upstream counterpart | Bridge | 10, 11 |
| `adapters/three.js` | not ported | — | — |

## 3. Architecture

### 3.1 Core (`Monaco.Presto`)

```
Clock                      framerate + playback-rate bookkeeping (base of Engine, Timer)
└─ Engine                  root clock, one per ticking thread; owns the linked list of running Tickables; Update(now)
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
- **Thread model** (Phase 9, [COMPOSITOR-PLAN.md](COMPOSITOR-PLAN.md) §4.1): one engine per thread that ticks it, no locks; the remaining shared statics (scratch values, ids, parser/accessor caches) are thread-safe — §4.1 has the inventory. `Presto.Engine` is the main (UI) engine and the ambient default; `Engine.Current` is a `[ThreadStatic]` that falls back to it, and `Engine.MakeCurrent()` installs another for a block — whoever owns a thread sets the engine, exactly as `Scope.Execute` installs defaults, so the factories keep the anime.js call shape instead of taking an `engine` parameter. Every `Timer` captures its engine at construction — its parent's, otherwise the ambient one — into `Timer.Engine` and resolves through it afterwards, so a UI-thread call on another engine's timer reaches that engine's clock — and trips *its* thread pin, which each engine arms in Debug on its first `Update()`.
  - Per engine: `Defaults`, `CurrentScope`, the children list, and the additive lookup table with its lookup animation (`Core.Additive`, driven from `Engine.Update()`).
  - Process-wide, so set before a second engine exists: `Globals.TimeScale` (written through `Engine.TimeUnit`, which only the main engine sets), `Globals.Precision` (exposed as `Engine.Precision`), `Globals.TickThreshold`, `Composer`'s replace lookups (keyed by target) and `Presto.Adapters`/`Presto.Codecs` — written once during initialisation, read from every engine's thread, documented rather than locked.
  - `Then()` completes with `RunContinuationsAsynchronously`, so awaiting a non-UI engine's timer resumes off the tick, on the thread pool.

### 3.2 Avalonia bridge (`Monaco.Presto.AvaloniaBridge`)

- **`AvaloniaFrameSource`** — `TopLevel.RequestAnimationFrame(Action<TimeSpan>)`, re-requested each frame while the engine has children (exactly `requestAnimationFrame`). It is a one-shot on the global `MediaContext`, so any live `TopLevel` works; the bridge picks the first one from the application lifetime or from the first animated control, and falls back to a `DispatcherTimer` at `DispatcherPriority.Render` when no `TopLevel` exists yet. Installed automatically on first use (`PrestoAvalonia.EnsureInitialized()`), optionally explicit via `AppBuilder.UsePresto()`.
- **`AvaloniaObjectTargetAdapter`** — resolves properties on any `AvaloniaObject`:
  - typed: `AvaloniaProperty` instances (`Canvas.LeftProperty`, `Visual.OpacityProperty`);
  - by name: `"Opacity"`, `"Width"`, `"Canvas.Left"`, `"Margin"` through `AvaloniaPropertyRegistry` (owner type + attached);
  - transform shorthands `X`, `Y` (`TranslateX/Y`), `Rotate`, `Scale`, `ScaleX`, `ScaleY`, `SkewX`, `SkewY` — composed into a `TransformGroup` that the bridge owns on `Visual.RenderTransform` (mirrors `transforms.js`, which manages the CSS transform string). Values in DIPs / degrees; `"1turn"`, `"0.5rad"` accepted like upstream.
  - Value codecs: `double/float/int`, `Color`, `SolidColorBrush` (animates the brush's color, creating a local brush if the current one is frozen/shared), `Thickness`, `CornerRadius`, `Point`, `Size`, `Rect`, `Vector`, `RelativePoint`, and an `Easing` ↔ `Ease` adapter (`PrestoEasing : Avalonia.Animation.Easings.Easing`) so Presto eases can be used in Avalonia `Transitions` and vice versa. A decomposed `Matrix` codec is deferred until a property needs one — the transform shorthands go through the bridge's own `TransformGroup`.
  - Writes use `SetValue(..., BindingPriority.Animation)` so animated values sit above local values and are cleanly removed by `Revert()` (`ClearValue` at animation priority).
- **Control helpers**: `control.Animate(...)`, `Presto.Get(control, "X")`, `Presto.Set(control, …)`, `Presto.Remove(control)`, `ControlScope` that reverts everything created inside it when the control is unloaded.
- **Feature modules** (Phase 7): `Draggable`, `ScrollObserver`, shape helpers (`Geometry.ContourLength`, `TryGetPointAndTangentAtDistance` for drawable strokes and motion paths), text splitting and scrambling.
- **`Compositor` namespace** (Phase 10, [COMPOSITOR-PLAN.md](COMPOSITOR-PLAN.md) §4.2) — a Presto engine on the render thread, so a scene keeps animating while the UI thread is blocked. `CompositorCanvas` is the UI-thread `Control` that hosts a scene and marshals `Post`/`Invoke` to it; `CompositorScene` is what a user derives from, with an `Engine` and `Scope` of its own, `OnAttached`, `OnMessage`, `OnRender(ImmediateDrawingContext, Size)` and `PostToUI`. From attach on the scene belongs to the render thread, and `PostToUI` is the only way back to a control.
- Later: XAML attached properties/behaviors.

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
- [x] **Phase 7 — Extras (each independently shippable).** `Draggable` (pointer capture, axes, snap, container, release physics), `ScrollObserver` (ScrollViewer thresholds/sync modes), shape helpers (`CreateDrawable`, `CreateMotionPath`, `MorphTo`), `SplitText`/`ScrambleText`. No XAML surface: the declarative layer is deferred until after the post-Phase-8 API pass (string properties, possible fluent builder), so it is not built against an API that is about to change. Tests: `draggables`, `scroll`, `svg`, `text` (adapted). Sample pages per feature.
- [x] **Phase 8 — Polish and packaging.** XML docs, README with anime.js → Presto cheat sheet, NuGet metadata for both packages, Browser/Android/iOS smoke runs to the level each head allows (Browser: all 15 gallery pages driven in Chrome; Android: builds, installs and launches on a device, but the gallery was not driven on it; iOS: compiles for `iossimulator-x64` only, as running it needs a macOS build host — the README's Platforms table is the shipped record of this), perf pass on the tick loop (the core tick path is allocation-free and both test projects hold it there with an `AllocationTests` budget; `BrushCodec.Encode` already reuses one `SolidColorBrush` per tween, and what the bridge still allocates per frame is decision 9 in §7).

Phases 9–12 are the compositor work; [docs/COMPOSITOR-PLAN.md](COMPOSITOR-PLAN.md) §4 and §6 hold the design and the acceptance criteria for each.

- [x] **Phase 9 — Engine instancing (core).** `Engine` constructor, ambient `Engine.Current`/`MakeCurrent`, `Timer.Engine`, per-engine `Defaults`/`CurrentScope`/additive state, thread-safe statics and thread-static scratch, `Interlocked` ids; bridge helpers switch to `Engine.Current`. Tests: all existing suites unchanged, new `EnginesTests`.
- [x] **Phase 10 — Compositor scenes (bridge + sample).** `CompositorCanvas`, `CompositorScene`, `CompositorSceneHandler` (a `CompositionCustomVisualHandler` that is the scene engine's `IFrameSource`), sample page with a freeze-the-UI-thread button, headless pixel and allocation tests, README section.
- [x] **Phase 11a — Compositor animations, fire-and-forget (bridge).** `CompositorAnimation`/`CompositorAnimationParams`: mirror animation sampled into `KeyFrameAnimation`s on `Translation`/`Scale`/`RotationAngle`/`Opacity`, `CenterPoint` expression, `Opacity` ownership, compositor-only registry, `Cancel`/`Revert`/`Then`. Pixel tests including the invalidate-mid-fade guard. Sample: a `PathIcon` spun on the compositor beside the scene; README: the `waapi.*` row.
- [x] **Phase 11b — Compositor animations, playback control (bridge).** `Pause`/`Resume`/`Play`/`Reverse`/`Alternate`/`Seek`/`Restart`/`Complete`/`Progress`/`Speed` by recompiling from the mirror's position; phase rotation for infinite loops. Sample: the spun `PathIcon` gets Pause/Resume/Reverse buttons and a speed slider.
- [x] **Phase 12 — FLIP layout transitions (bridge).** `PrestoAvalonia.AnimateLayout` and `AnimateSharedElement`: First/Last measured on the UI thread, inverted onto the element visual's `Translation` (plus `Scale` in `.PositionAndSize`) and played to identity on the compositor, with a mirror for interruption and `Then()`. Sample (cards, growing panel, shared-element move) and tests that assert interpolation, watched on the Desktop head. The specs are live in `tests/Monaco.Presto.AvaloniaBridge.Tests/LayoutTransitionTests.cs` and the layout cases of `CompositorRegistryTests`, held behind a `LAYOUT_TRANSITION` compile symbol until the implementation lands again.
- [ ] **Phase 13 — Advanced sample gallery.** A second "Showcase" section of nine demos that use many features at once — motion-design set-pieces, real-world UI patterns and the render-thread/scale/blend proofs — plus a restyle of the shell and the shared classes the reference pages already use. The plan, per-demo specs and its own phase checklist live in [docs/SAMPLES-PLAN.md](SAMPLES-PLAN.md); the API gaps it turns up land in §7 below.
- [x] **Phase 14 — Fluent builder.** `Presto.Build()` → `AnimationBuilder`, a reusable recipe that snapshots into `AnimationParams` and ends in a live `Animation`; typed shorthand/property extensions in the bridge, timeline and compositor overloads. Beside the indexer, not replacing it. Design, API shape and tests in [docs/FLUENT-BUILDER-PLAN.md](FLUENT-BUILDER-PLAN.md).
- [x] **Phase 15 — Builder samples and XAML attach.** A "Builder" gallery section whose pages print the builder code they run (captured with `[CallerArgumentExpression]`), and a minimal bridge `Animate.OnAttached` attached property that takes a builder from `x:Static`, a markup extension or a binding. Plan in [docs/BUILDER-SAMPLES-PLAN.md](BUILDER-SAMPLES-PLAN.md).
- [x] **Phase 16 — Connected animations.** Shared elements measured on screen so scrolled sources and targets land right, keyed `PrepareSharedElement`/`AnimateSharedElement(key, …)` for navigation where both views never coexist, and builder overloads for layout transitions with per-axis eases (arc flights) and delay. Plan in [docs/CONNECTED-ANIMATIONS-PLAN.md](CONNECTED-ANIMATIONS-PLAN.md).
- [x] **Phase 17 — Connected animations from XAML.** A bridge `Connected` class with three separate attached properties — `Key` (which element is the same thing in both views), `Motion` (how this element arrives; the arriving side's wins) and `Anchor` (arrive with a keyed flight without flying) — so MVVM navigation gets connected animations with no code-behind: keyed elements record their on-screen slot while attached, prepare on detach, and claim on attach from a coexisting element or a prepared entry. Sample: a Connected XAML showcase page. Design, lifetime rules and tests in [docs/CONNECTED-ANIMATIONS-PLAN.md](CONNECTED-ANIMATIONS-PLAN.md) §6 only; §1–5 are Phase 16 and already done.

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
3. Property tweens use the string indexer on params objects plus an optional fluent builder over it (`Presto.Build()`, Phase 14), which only builds params and so leaves the model unchanged; typed `AvaloniaProperty` indexer available in the bridge.
4. Transform shorthands write a bridge-owned `TransformGroup` to `RenderTransform`; if the user already set a non-Presto `RenderTransform`, it is wrapped as the first child rather than replaced.
5. Animated values are written at `BindingPriority.Animation` so `Revert()` restores user values precisely.
6. Bridge root namespace is `Monaco.Presto.AvaloniaBridge` to avoid shadowing `Avalonia.*`.
7. `ScrollContainer` pauses its 30 fps data timer with the wake window instead of leaving it running as upstream does, so the engine has no children and can stop the frame source while an idle observer is alive.
8. Scoping to a control lifetime is explicit: `PrestoAvalonia.CreateScope(control)` reverts when the root detaches from the visual tree (not on `Unloaded`, which never fires for a root detached before its `Loaded` job runs), and `Animate` never auto-scopes; no `AutoRevert` parameter in v1.
9. An `AvaloniaProperty` is animated through one `BindingPriority.Animation` binding per target that every frame pushes a value into, rather than the value entry per frame a `SetValue` at that priority creates (`Opacity`: 288 → 104 bytes/frame). A `double` property is bound through a typed `IObservable<double>`, so its number is pushed without boxing (`Opacity`: 104 → 80 bytes/frame); every other type keeps the untyped binding and boxes its value. These residual per-frame allocations are accepted for 0.1.0, all of them measured in the two `AllocationTests` suites: 80 bytes for the property-changed args Avalonia raises on each write, which is left to Avalonia and is now the whole cost of a `double` property and of a transform shorthand (which writes a reused `TranslateTransform`/`RotateTransform`), and 24 bytes per child a timeline re-renders internally at a loop boundary, where the core composes `Tween._value` as upstream does.
10. Both packages target `net10.0` only for 0.1.0. The sole blocking API is `System.Collections.Generic.OrderedDictionary<TKey, TValue>` (.NET 9+), which `AnimationParams`, `AnimatableParams`, `Keyframe` and `KeyframeExpander` use as an insertion-ordered map *and* as the `IReadOnlyList<KeyValuePair<string, TweenValue>>` the `Animation` constructor consumes; a trial `net8.0` compile with a stand-in for it produced no other error, and the bridge builds clean on `net8.0` against Avalonia 12.1.2. Multi-targeting therefore means carrying a hand-written replacement on a framework neither test project runs, which is not worth it before the API pass.

11. On Android the frame pump resolves its `TopLevel` from the first visual the bridge animates rather than from the application lifetime, because no lifetime there can answer. `IActivityApplicationLifetime` publishes only a `MainViewFactory`, which `AvaloniaMainActivity` invokes and assigns to the activity's content, leaving `ISingleViewApplicationLifetime.MainView` null; `ISingleTopLevelApplicationLifetime`, which the Browser and iOS lifetimes do implement, is `[PrivateApi]` and its `TopLevel` getter is inaccessible to consumers. Until a visual is animated, the 60 Hz render-priority `DispatcherTimer` pumps, so animations run either way. For the same reason the handover is tested through a seeded visual instead of a fake lifetime: Avalonia's lifetime interfaces are `[NotClientImplementable]` and cannot be implemented in a test.

12. Render-thread work is an explicit, separate API named after Avalonia's own term: `CompositorCanvas`, `CompositorScene`, `CompositorAnimation`, `AnimateOnCompositor`. `RenderThread*` was the alternative and `Composition*` was out, because the core already uses `Composition` for replace/none/blend. A scene owns its `Engine`: the UI thread never ticks a scene and the render thread never writes an `AvaloniaProperty`. The engine is ambient (`Engine.Current`) rather than a parameter on every factory, so the anime.js call shape survives.
13. Compositor animations on controls are a reduced, separate type, as upstream's `waapi` is, sharing no registry with UI-thread animations; a property with no composition counterpart throws instead of falling back to the UI thread, which would defeat the point. They compile by sampling a mirror Presto `Animation` (upstream's `linear()` approach), with a single-segment tween compiled exactly through `PrestoEasing` instead; `RotationAngle` is radians on the wire and Presto keeps degrees at the API. `CompositionSolidColorVisual.Color` would add background colour to that set but only as a child visual drawn above the control's content, so backgrounds stay UI-thread work.
14. `Opacity` is owned through an `Animation`-priority binding for the life of a compositor animation, because the server animation dies the moment `Visual.Opacity` changes. Playback control is emulated by recompiling from the mirror's position; the mirror moves now and the server animation it compiles starts at the next commit, so the two run at most a frame apart until the next call, which samples the mirror again and puts them back in step. Measured compensation through `CompositionBatch.Processed` is not worth it until the skew is visible somewhere.
15. Layout transitions are FLIP, inverted onto the composition `Translation`, not an implicit animation on `Offset`. The `Offset` design was built first and measured: it does not animate on the Windows desktop backend, and a bare Avalonia implicit animation snaps identically, so it is not Presto misusing the API (COMPOSITOR-PLAN §4.4 has the evidence). Measuring First/Last on the UI thread and inverting onto `Translation` — never written by Avalonia, §2.2 — uses the explicit `StartAnimation` path that demonstrably survives a blocked UI thread, and additionally buys size changes and moves between parents. Enter/leave states (`enterFrom` / `leaveTo` / `swapAt`) stay out: they need a presenter hook, not a layout hook.
16. Browser runs compositor scenes and animations but does not survive a blocked UI thread, since its render loop is on the same thread; documented, nothing built for it. The Avalonia internals the whole compositor story rests on (the client setter's equality guard, `SetAnimatedValue(out)` removing the running animation, `Offset + Translation` composition) are pinned by `CompositorAnimationTests.InvalidateVisualDoesNotStopACompositorFade`, which has to be re-run when `AvaloniaVersion` moves.
17. The XAML surface is one attached property, `presto:Animate.OnAttached` (Phase 15), typed `AnimationBuilder?` so `x:Static`, a markup extension or a binding can supply it; it plays on visual-tree attach and reverts on detach. It is an attachment trigger only: other triggers and a behaviors layer stay open until a sample needs them.
18. Connected animations from XAML (Phase 17) are three separate attached properties on the bridge's static `Connected` class (`Key`, `Motion`, `Anchor`), so each keeps its own bindings and style setters. The arriving element's `Motion` wins, with no fallback to the leaving side's; otherwise `Connected.DefaultMotion`. See [CONNECTED-ANIMATIONS-PLAN.md §6](CONNECTED-ANIMATIONS-PLAN.md#6-xaml-keys-phase-17).
19. `Stretch` deviates from upstream for top-level timers: it leaves `_offset` alone and rebuilds the tweens' absolute times from their scaled local times, instead of multiplying both by the stretch factor. A top-level offset is the engine time the timer was created at, and stretching from a setter's `1e-11` duration (an `Animatable` property with duration 0, which `Draggable` uses on every axis) multiplied it by ~10¹⁴, past the precision a frame needs: the release tween then started up to seconds late or never, depending on how long the engine had run. Upstream has the same arithmetic; timeline children keep it unchanged. Pinned by `AnimatablesTests.RetargetingAZeroDurationPropertyLongAfterTheEngineStartedStillEases`.

Open (confirm before the relevant phase):

- `Timeline.Set` takes only a `TimelinePosition`, so a stagger cannot position a setter group as it can upstream; add the overload if a sample needs it.
- Function-based `composition` (upstream runs `composition` through `getFunctionValue`): `TweenParams.Composition`/`AnimationParams.Composition` are plain enums, so a per-target delegate is not expressible. No ported test needs it; decide whether parity is worth making the property function-capable.
- `autoplay: onScroll(...)`: `TimerParams.Autoplay` is a `bool?`, so a scroll observer is attached with `observer.Link(timer)` after creating the timer with `Autoplay = false` instead. Widening the parameter to accept an observer is an API-pass decision; reverting the linked timer already reverts the observer, as upstream does.
- Rotation unit for the bare `Rotate` shorthand: degrees (Avalonia `RotateTransform.Angle`) is the natural default; upstream defaults to `deg` too, so parity holds. Confirm we do not want radians anywhere by default.
- Lazily resolved timeline-child targets: a child's targets are fixed when it is added, so a timeline over a `SplitText`'s pieces can neither be built before the split has measured nor survive a re-split (SAMPLES-PLAN §8 row 4). The workaround is documented on `TextSplitter.AddEffect`; decide whether a child should resolve its targets at first render, or `Timeline` should re-point one.
- Per-target `TextModifier`/`Modifier`: both take the number alone, with no `(target, index, total)`, so counters that want their own precision need an animation each (SAMPLES-PLAN §8 row 17). Upstream's `modifier` has the same shape, so parity holds; decide whether to resolve them per target like every other function-based value.
- Relative or delta `To` for blended tweens: a blended tween's `To` becomes the additive group's base, so a layered effect has to aim at the destination of the animation beneath it, and aimed around zero drags it back (SAMPLES-PLAN §8 row 22). Decide on a spelling for "add this much" that does not move the group's base.
- One playback model per `CompositorAnimation`: each target gets its own paused mirror `Animation`, ticked on the UI thread, plus its own composition animations, so 5 000 visuals run at 51 UI / 51 render fps against 88 / 129 for UI-thread animations (SAMPLES-PLAN §8 row 28). The mirrors differ only by resolved delay and values; decide whether sharing one model across targets is worth the rework of per-mirror completion.
- `AnimateOnCompositor` ignores `RenderTransformOrigin`: `CompositorAnimation` drives `CenterPoint` with a fixed `Size * 0.5` expression, so a ball with a bottom origin squashes about its centre on the render thread (SAMPLES-PLAN §8 row 32). Build the expression from the control's `RenderTransformOrigin`, as Avalonia does for `RenderTransform`.
- `Presto.Remove` called mid-frame, for example from `Draggable.Stop`/`Reset` inside a callback: `Composer.RemoveTargetsFromRenderable` unlinks children at once, so it can drop the engine's cached next child and skip the rest of its list for that frame (SAMPLES-PLAN §8 row 15). Decide whether `Helpers.RemoveChild`/`Engine.Update` should defer the unlink.
