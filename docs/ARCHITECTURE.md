# Monaco.Presto — Architecture

A port of the anime.js v4.5.0 animation model to .NET, with a framework-agnostic core and an Avalonia adapter. This document is the map: goals, how anime.js maps onto Presto, the architecture, and how it is tested. What each feature does, and why, is in the [feature specs](specs/README.md); work not done yet is in the [backlog](BACKLOG.md); new work starts as a [change proposal](changes/README.md).

Upstream reference: https://github.com/juliangarnier/anime (tag `v4.5.0`, `src/` and `tests/suites/`). Docs: https://animejs.com/documentation.

## 1. Goals and non-goals

**Goals**

- anime.js-level ergonomics from C#: one call to animate any property of any object or control, with staggering, keyframes, timelines and full playback control.
- Behavioral parity with anime.js for the timing model (delay, loop, loopDelay, alternate, reversed, seek, stretch, playbackRate, frameRate, composition), so the upstream tests are the acceptance suite.
- A core with zero UI dependencies that can drive plain objects (view models, custom renderers, game-style loops) and is fully testable with a manual clock.
- An Avalonia bridge that makes controls first-class targets: `AvaloniaProperty` by name or typed reference, transform shorthands (`X`, `Y`, `Rotate`, `Scale`…), colors and brushes, thickness, corner radius and points, with correct thread affinity and lifetime handling.

**Non-goals**

- CSS-specific value handling: unit conversion between px/em/rem, CSS variables, DOM attribute animation. Only what maps to Avalonia survives: numbers, degrees/turns/radians for rotation, percentages where Avalonia uses `RelativePoint`/`RelativeRect`.
- Three.js-style adapters. The adapter interface allows one; none ships.

## 2. Module map: anime.js → Presto

| anime.js (`src/`) | Presto namespace / type | Project | Spec |
|---|---|---|---|
| `core/clock.js`, `core/globals.js`, `core/consts.js` | `Monaco.Presto.Core.Clock`, `PrestoDefaults`, `Constants` | Core | [engine](specs/core/engine.md) |
| `engine/engine.js` | `Monaco.Presto.Engine` + `IFrameSource` | Core | [engine](specs/core/engine.md), [threading](specs/core/threading.md) |
| `core/render.js` (`tick`, `render`) | `Monaco.Presto.Core.Renderer` (static tick/render) | Core | [timer](specs/core/timer.md), [callbacks](specs/core/callbacks.md) |
| `timer/timer.js` | `Monaco.Presto.Timer` | Core | [timer](specs/core/timer.md), [callbacks](specs/core/callbacks.md) |
| `easings/*` | `Ease` delegate, `Eases`, `LinearStop`, `Spring`, `EaseParser` | Core | [eases](specs/core/eases.md), [ease-parser](specs/core/ease-parser.md), [spring](specs/core/spring.md) |
| `core/values.js`, `core/units.js`, `core/colors.js`, `core/targets.js`, `core/helpers.js`, `adapters/registry.js` | `Monaco.Presto.Values` (`TweenValue`, `ValueKind`, `PrestoColor`, `FunctionValue`), `Monaco.Presto.Targets` (`ITargetAdapter`, `IPropertyAccessor`, `ObjectTargetAdapter`), registered through `Presto.Adapters`/`Presto.Codecs` | Core | [values](specs/core/values.md), [colors](specs/core/colors.md), [targets](specs/core/targets.md) |
| `animation/animation.js`, `composition.js`, `additive.js` | `Monaco.Presto.Animation`, `Tween`, `Composition`, `Core.Additive` | Core | [animation](specs/core/animation.md), [keyframes](specs/core/keyframes.md), [composition](specs/core/composition.md) |
| `core/transforms.js`, `core/styles.js` | Bridge only: `RenderTransformComposer`, property name resolution | Bridge | [transforms](specs/avalonia-bridge/transforms.md), [property-resolution](specs/avalonia-bridge/property-resolution.md) |
| `timeline/timeline.js`, `position.js` | `Monaco.Presto.Timeline`, `TimelinePosition` | Core | [timeline](specs/core/timeline.md) |
| `utils/stagger.js` | `Monaco.Presto.Stagger` (`Presto.Stagger(...)`) | Core | [stagger](specs/core/stagger.md) |
| `utils/number.js`, `random.js`, `time.js`, `chainable.js` | `Monaco.Presto.Utils` | Core | [utils](specs/core/utils.md) |
| `utils/target.js` (`$`, `get`, `set`, `remove`, `cleanInlineStyles`) | `Presto.Get/Set/Remove` on objects; bridge overloads for controls. `$` has no equivalent (no selectors); `cleanInlineStyles` is unnecessary (animation-priority writes) | Core + Bridge | [presto](specs/core/presto.md), [control-helpers](specs/avalonia-bridge/control-helpers.md) |
| `animatable/animatable.js` | `Monaco.Presto.Animatable` | Core | [animatable](specs/core/animatable.md) |
| `scope/scope.js` | `Monaco.Presto.Scope`; bridge `ControlScope` (reverts when its control leaves the visual tree) | Core + Bridge | [scope-and-defaults](specs/core/scope-and-defaults.md), [control-helpers](specs/avalonia-bridge/control-helpers.md) |
| `draggable/draggable.js` | `Monaco.Presto.AvaloniaBridge.Draggable` | Bridge | [draggable](specs/avalonia-bridge/draggable.md) |
| `events/scroll.js` | `Monaco.Presto.AvaloniaBridge.ScrollObserver` (ScrollViewer) | Bridge | [scroll-observer](specs/avalonia-bridge/scroll-observer.md) |
| `svg/*` (`morphTo`, `createDrawable`, `createMotionPath`) | Path/Geometry based shape helpers | Bridge | [shapes](specs/avalonia-bridge/shapes.md) |
| `text/scramble.js` (`scrambleText`) | `Presto.ScrambleText` | Core | [scramble](specs/core/scramble.md) |
| `text/split.js` (`splitText`) | `Monaco.Presto.AvaloniaBridge.Text` | Bridge | [text](specs/avalonia-bridge/text.md) |
| `layout/*` | Not ported as such: upstream's real width/height animation maps to `Animate.ContentSize`; compositor FLIP covers position and size moves | Bridge | [content-size](specs/avalonia-bridge/content-size.md), [layout-transitions](specs/avalonia-bridge/layout-transitions.md) |
| `waapi/*` | `CompositorAnimation`: server-side `KeyFrameAnimation`s compiled from Presto params, plus `CompositorCanvas`/`CompositorScene`, a whole engine on the render thread (no upstream counterpart) | Bridge | [animation-thread](specs/avalonia-bridge/animation-thread.md), [compositor-scenes](specs/avalonia-bridge/compositor-scenes.md) |
| `adapters/three.js` | not ported | — | — |

Features with no upstream counterpart are listed in the spec indexes: [core](specs/core/index.md), [Avalonia bridge](specs/avalonia-bridge/index.md), [Lottie](specs/lottie/index.md), [DevTools](specs/devtools/index.md).

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
IValueCodec                Decode(object) → channels, Encode(channels) → object
```

Kept structurally identical to upstream, so the upstream tests port one to one:

- **Linked lists** (`_head/_tail/_prev/_next`) for engine children, timeline children and tweens, for zero-allocation ticking.
- **`tick()`** turns absolute time into iteration time, handles loop/alternate/reverse/delay/loopDelay, fires callbacks and decides whether to render.
- **Composition** (`replace` | `none` | `blend`) through a per (target, property) sibling registry; `blend` uses additive animations.
- **Function-based values** wherever a number is accepted: `(target, index, total) => TweenValue`.
- **Time** is `double` milliseconds; seconds mode is an engine scale factor.
- **Threads**: one engine per thread that ticks it, no locks. `Presto.Engine` is the main (UI) engine and the ambient default; `Engine.Current`/`MakeCurrent()` install another for a block, and every `Timer` captures its engine at construction ([threading](specs/core/threading.md)).

### 3.2 Avalonia bridge (`Monaco.Presto.AvaloniaBridge`)

- **Frame pump**: `AvaloniaFrameSource` re-requests `TopLevel.RequestAnimationFrame` each frame while the engine has children, falling back to a `DispatcherTimer` before any `TopLevel` exists; installed on first use. Minimized windows and backgrounded apps set `Engine.Hidden` ([initialization](specs/avalonia-bridge/initialization.md)).
- **Targets**: `AvaloniaObjectTargetAdapter` resolves typed `AvaloniaProperty` keys, names (including attached `"Canvas.Left"`) and the bridge's shorthands; writes go in at `BindingPriority.Animation`, so `Revert` clears them cleanly ([property-resolution](specs/avalonia-bridge/property-resolution.md)).
- **Shorthands**: transform (`X`, `Y`, `Rotate`, `Scale`…) composed into a bridge-owned `TransformGroup`; effects, 3D rotation, clips and masks over bridge-owned visuals ([transforms](specs/avalonia-bridge/transforms.md), [effects](specs/avalonia-bridge/effects.md)).
- **Codecs** for colors, brushes and gradients, box shadows, thickness, corner radius, points, sizes, rects, vectors, relative points, geometry and `GridLength` ([value-codecs](specs/avalonia-bridge/value-codecs.md)).
- **Render thread**: compositor animations for existing controls, and whole engines on the render thread in `CompositorScene` ([animation-thread](specs/avalonia-bridge/animation-thread.md), [compositor-scenes](specs/avalonia-bridge/compositor-scenes.md)).
- **Declarative layer**: `Presence`, `Motion`, `Follow`, `Animate.*`, recipes and markup extensions, and the controls built on them; see the [bridge spec index](specs/avalonia-bridge/index.md).

## 4. Testing strategy

- **Core**: xunit v3, no Avalonia. Tests drive `Engine.Update(now)` through `ManualFrameSource.Advance(ms)`, giving deterministic timing identical to how upstream tests use `engine.update()` and fake timers.
- **Upstream parity**: suites ported from `tests/suites/*.test.js`, with test names close to the Mocha `it()` descriptions so gaps are easy to audit. DOM-only suites (`waapi`, `threejs`, `node`, `build`) are skipped.
- **Generated fixtures**: `tools/anime-fixtures/` (Node, `npm i animejs@4.5.0`) dumps JSON for easing curves (101 samples each), spring durations, stagger outputs and timeline positions; the C# tests assert within `1e-6`. Regenerate only when bumping the upstream reference version.
- **Bridge**: `tests/Monaco.Presto.AvaloniaBridge.Tests` on `Avalonia.Headless` with `[AvaloniaFact]`/`[AvaloniaTheory]`. That package targets `xunit.v3.extensibility.core` 3.2.2, so `xunit.v3` is pinned to 3.2.x repo-wide (on 4.x, discovery throws `MissingMethodException`); it is also why `AssemblyInfo.cs` uses `CollectionBehavior(DisableTestParallelization = true)`. A `ManualFrameSource` replaces the frame pump.
- **Specs**: the feature specs are the acceptance reference. Each requirement cites the tests that pin it; `SpecTests` and `SpecCoverageTests` keep citations, paths and type ownership honest.
- **Visual**: the sample gallery is the manual acceptance surface; DevTools screenshots and property reads confirm end states.

Decisions behind the test setup:

- **Tests run on virtual time.** Headless tests freeze Avalonia's compositor clock and advance it through one test-only helper, `CompositorClock`, reached by reflection (each lookup throws naming a missing member; `CompositorClockTests` pins the seam). Tests step 16 ms frames and never sleep unless the code under test measures real time. The library itself keeps no reflection, and its own wall-clock reads go through a `TimeProvider`. Reason: Avalonia offers no clock seam, and real-time waits made the suites take minutes; the fragility of four private names is accepted because an upgrade that renames one fails loudly. Rejected: condition-based settles, elapsed-time pumps and shortened durations.
- **The bridge harness's `Tick(ms)` moves only the compositor clock.** It runs UI jobs, advances the compositor and renders, but does not advance the manual frame source; a test needing both engines in step advances both. The sample harness's `TestFrames.Tick()` moves both. Reason: advancing the frame source on every tick broke 35 tests whose UI-thread values assume `Tick` only renders.
- **Test assemblies run in series.** Reason: `Presto.Engine`, `Presto.Defaults` and the test frame source are process-wide statics. Rejected for now: per-test engines, which touch every static entry point.
- **`BridgeTestBase` tests must be `[AvaloniaFact]`**, and the base asserts it runs on the UI thread. Reason: a plain `[Fact]` reset the engine off the UI thread and caused order-dependent failures; the assert turns those into failures every time.
- **The sample suite's 60 s target is accepted as missed** (about 92 s). What remains is real rendering work in the headless framebuffer; tests were not weakened to reach it.

## 5. Project-wide decisions

- **Time is `double` milliseconds**; seconds mode is an engine scale factor, not a second code path. Reason: parity with anime.js (`timeUnit: 'ms'`).
- **The bridge's root namespace is `Monaco.Presto.AvaloniaBridge`.** Reason: `...Avalonia` would shadow the `Avalonia.*` namespaces.
- **Both packages target `net10.0` only.** Reason: the one blocker for older frameworks is `OrderedDictionary<TKey, TValue>` (.NET 9+); multi-targeting would mean carrying a hand-written replacement on a framework no test runs.
- **Code that needs nothing from Avalonia lives in the core.** A bridge type whose implementation uses only core types moves to the core, unless a bridge feature defines its meaning (as Recipes defines `MotionTokens`, or compositor layout transitions define `LayoutMotion`). Reason: core users get it, its tests run without a headless platform, and `using Monaco.Presto;` reaches it.
- **Every trigger, recipe and control states its reduced-motion form** in its spec, and its tests run under both settings. Reason: four places once ignored `ReducedMotion` and samples worked around them; stating the reduced form up front stops new holes.
- The architecture rules (the core knows nothing about Avalonia, single-threaded engines, faithful port first) are in [CLAUDE.md](../CLAUDE.md).

## 6. Sample gallery

The gallery (`samples/PrestoSample*`) is the dogfooding surface and the manual acceptance target. Decisions that shape it:

- **Demos never quietly grow the library.** Every friction point a sample hits is recorded as a finding and decided on its own; generally useful sample code is a candidate for the bridge rather than a quiet copy in the sample.
- **Reference pages stay isolated; multi-feature demos are separate.** The Reference section (`Pages/`) keeps one page per anime.js documentation area, in the dictionary/indexer form with string keys, so it reads side by side with the anime.js docs; those pages ignore the reduced-motion switch, as anime.js has none, and link their Builder twin. Showcase (`Showcase/Pages/`) holds the multi-feature demos; Builder pages use only the builder, never the params indexer, and existing Reference and Showcase pages were not converted to it.
- **Printed code is the code that ran**, captured at compile time (`Snippet.Of`, and the `PrestoSample.SnippetGenerator` regions), never a hand-copied string. Reason: copies drift silently.
- **The nav is one `TreeView` of sections.** Reason: two `ListBox`es bound to one `SelectedItem` fight, the one not containing the selection writing `null` back.
- **One dark look, by setter values only.** Shared classes keep their names and roles and only setter values change, so no reference page is edited; colors come once from `Showcase/Palette.cs`, because compositor scenes cannot read `StaticResource`. The library stays theme-agnostic.
- **The reduced-motion switch sets `ReducedMotion` only**, not `ReduceAll`.
- **Showcase pages share a kit**: `ShowcasePage` owns a `ControlScope` reverted on detach, and data comes from seeded `Datasets`, so every run looks the same.
- **Page patterns stay page code where no library primitive fits.** Liquid's squashing pill and the Swipe deck's card throw stay in the pages; Dashboard's donut and sparkline, Loaders' ring and counter, MorphLab's signature and TitleSequence's monogram stay off the draw recipes. Reason: Liquid's edges lead and trail on different durations and squash vertically, which `Stretch` cannot express; the deck is a card throw, not a row action; the draw cases are not direct swaps (scrubbable timelines, keyframe loops, a `ProgressBar`, a timeline the reduced form would shorten).
- **The Animatable reference page stays hand-built**, and the `Magnetic` demo lives on the Micro-interactions page. Reason: `Pages/` mirrors the anime.js documentation, and that page demonstrates `Animatable.Set` itself; its follower also maps the pointer to stage coordinates and holds on leave, which is neither proximity nor the pointer sources' absent-reads-0 rule.
- **GalleryFlip stays imperative**, the `AnimateLayout` counterpart to Storefront. Reason: converting it to `Presence` would make it a near-copy of Storefront's grid.
- **The Photo viewer is its own Showcase page** (pinch, bounds, 90° snap, double-tap, Ctrl+wheel, a filmstrip `Carousel`), linked from the Draggable page, not a fifth Gestures tile. Reason: the Gestures page's first row of four tiles is full at its width; on desktop the `Carousel` swipes on touch and pen only, so photos change through the filmstrip.
- **The Gestures drag-to-open card is a second, full-width row**, not a narrower fifth column. Reason: its staggered rows need a wider card than a quarter column gives, and a fifth column would squeeze the first four tiles.
- **The Gestures trails and resist demo is a third full-width row**, below the drag-to-open card. Reason: a pointer trail needs a wide stage to read as a trail, and the first row's four tiles are full at the page's width.
- **Image effects demos.** The duotone card flips a `ThemeVariantScope` (the app is fixed to Dark); Storefront's product art is a `DrawingImage` in a `FilteredImage`, grayed when sold out by a style setter, exercising rasterization; the hero dissolve is the Photo viewer's dimmed `FilteredImage` backdrop; the dissolve/pixelate card animates the live image, then removes it. Reason: the Connected and Gallery flip heroes hold no image, and exit ghosts cannot carry pixel effects.
- **The carousel snaps through the draggable's `Snap`** (as `ModifyTarget`) and marks the active dot in `OnSettle`. Reason: `OnSnap` fires at release, before the inertia lands.
- **Storefront skips its cart-badge burst under reduced motion**, since the badge's own `Pop` covers it; the Micro-interactions like passes `Recipes.Fade()` as its reduced form and bursts when the heart turns on, not on press-in.
- **The Activity feed inserts live events at once**, even scrolled down, counting unseen ones in an "N new above" pill that scrolls to the top. Reason: scroll anchoring makes holding events behind the pill unnecessary. Its one-off 39 px offset was closed as not reproduced (selection, focus and the FLIP ruled out; anchoring cannot engage at offset 0), with `ActivityFeedPageTests` kept as regressions. Those tests seed the feed from 18:00 because seeding from `DateTime.Now` made the live timer's phase vary with the time of day.
- **Desktop is the acceptance target.** Scene-heavy pages cap their counts and degrade on Browser; other heads are checked once and the README's Platforms table states only what is true. `PrestoSample.Tests` constructs, attaches, ticks and detaches every page headlessly in both motion modes, because DevTools input cannot send pointer moves or drags.
