# MonacoLabs.Presto — Architecture

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
| `core/clock.js`, `core/globals.js`, `core/consts.js` | `MonacoLabs.Presto.Core.Clock`, `PrestoDefaults`, `Constants` | Core | [engine](specs/core/engine.md) |
| `engine/engine.js` | `MonacoLabs.Presto.Engine` + `IFrameSource` | Core | [engine](specs/core/engine.md), [threading](specs/core/threading.md) |
| `core/render.js` (`tick`, `render`) | `MonacoLabs.Presto.Core.Renderer` (static tick/render) | Core | [timer](specs/core/timer.md), [callbacks](specs/core/callbacks.md) |
| `timer/timer.js` | `MonacoLabs.Presto.Timer` | Core | [timer](specs/core/timer.md), [callbacks](specs/core/callbacks.md) |
| `easings/*` | `Ease` delegate, `Eases`, `LinearStop`, `Spring`, `EaseParser` | Core | [eases](specs/core/eases.md), [ease-parser](specs/core/ease-parser.md), [spring](specs/core/spring.md) |
| `core/values.js`, `core/units.js`, `core/colors.js`, `core/targets.js`, `core/helpers.js`, `adapters/registry.js` | `MonacoLabs.Presto.Values` (`TweenValue`, `ValueKind`, `PrestoColor`, `FunctionValue`), `MonacoLabs.Presto.Targets` (`ITargetAdapter`, `IPropertyAccessor`, `ObjectTargetAdapter`), registered through `Presto.Adapters`/`Presto.Codecs` | Core | [values](specs/core/values.md), [colors](specs/core/colors.md), [targets](specs/core/targets.md) |
| `animation/animation.js`, `composition.js`, `additive.js` | `MonacoLabs.Presto.Animation`, `Tween`, `Composition`, `Core.Additive` | Core | [animation](specs/core/animation.md), [keyframes](specs/core/keyframes.md), [composition](specs/core/composition.md) |
| `core/transforms.js`, `core/styles.js` | Bridge only: `RenderTransformComposer`, property name resolution | Bridge | [transforms](specs/avalonia-bridge/transforms.md), [property-resolution](specs/avalonia-bridge/property-resolution.md) |
| `timeline/timeline.js`, `position.js` | `MonacoLabs.Presto.Timeline`, `TimelinePosition` | Core | [timeline](specs/core/timeline.md) |
| `utils/stagger.js` | `MonacoLabs.Presto.Stagger` (`Presto.Stagger(...)`) | Core | [stagger](specs/core/stagger.md) |
| `utils/number.js`, `random.js`, `time.js`, `chainable.js` | `MonacoLabs.Presto.Utils` | Core | [utils](specs/core/utils.md) |
| `utils/target.js` (`$`, `get`, `set`, `remove`, `cleanInlineStyles`) | `Presto.Get/Set/Remove` on objects; bridge overloads for controls. `$` has no equivalent (no selectors); `cleanInlineStyles` is unnecessary (animation-priority writes) | Core + Bridge | [presto](specs/core/presto.md), [control-helpers](specs/avalonia-bridge/control-helpers.md) |
| `animatable/animatable.js` | `MonacoLabs.Presto.Animatable` | Core | [animatable](specs/core/animatable.md) |
| `scope/scope.js` | `MonacoLabs.Presto.Scope`; bridge `ControlScope` (reverts when its control leaves the visual tree) | Core + Bridge | [scope-and-defaults](specs/core/scope-and-defaults.md), [control-helpers](specs/avalonia-bridge/control-helpers.md) |
| `draggable/draggable.js` | `MonacoLabs.Presto.Draggable` | Bridge | [draggable](specs/avalonia-bridge/draggable.md) |
| `events/scroll.js` | `MonacoLabs.Presto.ScrollObserver` (ScrollViewer) | Bridge | [scroll-observer](specs/avalonia-bridge/scroll-observer.md) |
| `svg/*` (`morphTo`, `createDrawable`, `createMotionPath`) | Path/Geometry based shape helpers | Bridge | [shapes](specs/avalonia-bridge/shapes.md) |
| `text/scramble.js` (`scrambleText`) | `Presto.ScrambleText` | Core | [scramble](specs/core/scramble.md) |
| `text/split.js` (`splitText`) | `MonacoLabs.Presto.Text` | Bridge | [text](specs/avalonia-bridge/text.md) |
| `layout/*` | Not ported as such: upstream's real width/height animation maps to `Animate.ContentSize`; compositor FLIP covers position and size moves | Bridge | [content-size](specs/avalonia-bridge/content-size.md), [layout-transitions](specs/avalonia-bridge/layout-transitions.md) |
| `waapi/*` | `CompositorAnimation`: server-side `KeyFrameAnimation`s compiled from Presto params, plus `CompositorCanvas`/`CompositorScene`, a whole engine on the render thread (no upstream counterpart) | Bridge | [animation-thread](specs/avalonia-bridge/animation-thread.md), [compositor-scenes](specs/avalonia-bridge/compositor-scenes.md) |
| `adapters/three.js` | not ported | — | — |

Features with no upstream counterpart are listed in the spec indexes: [core](specs/core/index.md), [Avalonia bridge](specs/avalonia-bridge/index.md), [Lottie](specs/lottie/index.md), [DevTools](specs/devtools/index.md).

## 3. Architecture

### 3.1 Core (`MonacoLabs.Presto`)

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

### 3.2 Avalonia bridge (`MonacoLabs.Presto.Avalonia`)

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
- **Bridge**: `tests/MonacoLabs.Presto.Avalonia.Tests` on `Avalonia.Headless` with `[AvaloniaFact]`/`[AvaloniaTheory]`. That package targets `xunit.v3.extensibility.core` 3.2.2, so `xunit.v3` is pinned to 3.2.x repo-wide (on 4.x, discovery throws `MissingMethodException`); it is also why `AssemblyInfo.cs` uses `CollectionBehavior(DisableTestParallelization = true)`. A `ManualFrameSource` replaces the frame pump.
- **Specs**: the feature specs are the acceptance reference. Each requirement cites the tests that pin it; `SpecTests` and `SpecCoverageTests` keep citations, paths and type ownership honest.
- **Visual**: the sample gallery is the manual acceptance surface; DevTools screenshots and property reads confirm end states.

Decisions behind the test setup:

- **Tests run on virtual time.** Headless tests freeze Avalonia's compositor clock and advance it through one test-only helper, `CompositorClock`, reached by reflection (each lookup throws naming a missing member; `CompositorClockTests` pins the seam). Tests step 16 ms frames and never sleep unless the code under test measures real time. The library itself keeps no reflection, and its own wall-clock reads go through a `TimeProvider`. Reason: Avalonia offers no clock seam, and real-time waits made the suites take minutes; the fragility of four private names is accepted because an upgrade that renames one fails loudly. Rejected: condition-based settles, elapsed-time pumps and shortened durations.
- **The bridge harness's `Tick(ms)` moves only the compositor clock.** It runs UI jobs, advances the compositor and renders, but does not advance the manual frame source; a test needing both engines in step advances both. The sample harness's `TestFrames.Tick()` moves both. Reason: advancing the frame source on every tick broke 35 tests whose UI-thread values assume `Tick` only renders.
- **The bridge test project uses the `MonacoLabs.Presto.Tests` namespace**, and its allocation tests are `BridgeAllocationTests`. Reason: a `MonacoLabs.Presto.Avalonia.Tests` namespace would reintroduce the `Avalonia.*` shadowing in the most Avalonia-heavy code, and the project already compiles the core tests' `EngineTestBase`, `TestHelpers` and `SpecCatalog` in that namespace; the rename keeps any test class name from existing in both projects.
- **Test assemblies run in series.** Reason: `Presto.Engine`, `Presto.Defaults` and the test frame source are process-wide statics. Rejected for now: per-test engines, which touch every static entry point.
- **`BridgeTestBase` tests must be `[AvaloniaFact]`**, and the base asserts it runs on the UI thread. Reason: a plain `[Fact]` reset the engine off the UI thread and caused order-dependent failures; the assert turns those into failures every time.
- **The sample suite's 60 s target is accepted as missed** (about 92 s). What remains is real rendering work in the headless framebuffer; tests were not weakened to reach it.

## 5. Project-wide decisions

- **Time is `double` milliseconds**; seconds mode is an engine scale factor, not a second code path. Reason: parity with anime.js (`timeUnit: 'ms'`).
- **Avalonia packages share the core's namespace.** The bridge's root namespace is `MonacoLabs.Presto`, and its package and assembly are `MonacoLabs.Presto.Avalonia`; its `Targets` and `Values` merge with the core's. Reason: an `Avalonia` namespace segment would shadow `Avalonia.*` for every qualified name and generated source inside it, and one `using` matches the API convention; separate assemblies contributing to one namespace is ordinary .NET practice (every `Microsoft.Extensions.*` package adds to `Microsoft.Extensions.DependencyInjection`). No core type shares a name with a bridge type in the merged namespaces, and no type is named like a sibling namespace. Rejected: a `MonacoLabs.Presto.Avalonia` namespace with `global::` at each qualified reference (the hazard stays for every future file and generator); `.Ava`/`.Xaml`/`.UI` (they read as workarounds, and `.UI` would not fit a second UI-framework adapter); merging the assemblies (the core would gain an Avalonia dependency).
- **Package names carry `Avalonia`; namespaces never do.** Lottie and DevTools are packaged as `MonacoLabs.Presto.Avalonia.Lottie`/`.DevTools`, with namespaces `MonacoLabs.Presto.Lottie`/`.DevTools`. Reason: on NuGet the package name is how people find the adapter for their UI framework; an adapter for another framework would be `MonacoLabs.Presto.<Framework>`, again in the `MonacoLabs.Presto` namespace.
- **Folders match the root namespace, not the project name.** In `MonacoLabs.Presto.Avalonia/`, `Controls/` holds `MonacoLabs.Presto.Controls`. Reason: the "folder structure equals namespace" convention is read relative to each project's `RootNamespace`.
- **Both packages target `net10.0` only.** Reason: the one blocker for older frameworks is `OrderedDictionary<TKey, TValue>` (.NET 9+); multi-targeting would mean carrying a hand-written replacement on a framework no test runs.
- **Code that needs nothing from Avalonia lives in the core.** A bridge type whose implementation uses only core types moves to the core, unless a bridge feature defines its meaning (as Recipes defines `MotionTokens`, or compositor layout transitions define `LayoutMotion`). Reason: core users get it, its tests run without a headless platform, and `using MonacoLabs.Presto;` reaches it.
- **Every trigger, recipe and control states its reduced-motion form** in its spec, and its tests run under both settings. Reason: four places once ignored `ReducedMotion` and samples worked around them; stating the reduced form up front stops new holes.
- The architecture rules (the core knows nothing about Avalonia, single-threaded engines, faithful port first) are in [CLAUDE.md](../CLAUDE.md).

## 6. Samples

Two samples dogfood the library: the gallery, one page per feature, and Tempo, one app built with it. No spec owns sample code, so their decisions live here. One rule covers both:

- **Demos never quietly grow the library.** Every friction point a sample hits is recorded as a finding and decided on its own; generally useful sample code is a candidate for the bridge rather than a quiet copy in the sample.

### Gallery

The gallery (`samples/PrestoSample*`) is the dogfooding surface and the manual acceptance target. The gallery's decisions:
- **Reference pages stay isolated; multi-feature demos are separate.** The Reference section (`Pages/`) keeps one page per anime.js documentation area, in the dictionary/indexer form with string keys, so it reads side by side with the anime.js docs; those pages ignore the reduced-motion switch, as anime.js has none, and link their Builder twin. Showcase (`Showcase/Pages/`) holds the multi-feature demos; Builder pages use only the builder, never the params indexer, and existing Reference and Showcase pages were not converted to it.
- **Printed code is the code that ran**, captured at compile time (`Snippet.Of`, and the `PrestoSample.SnippetGenerator` regions), never a hand-copied string. Reason: copies drift silently.
- **The nav is one `TreeView` of sections, in one `GalleryNav`.** Reason: two `ListBox`es bound to one `SelectedItem` fight, the one not containing the selection writing `null` back.
- **The shell is adaptive at a 720 DIP width breakpoint.** At 720 and wider it keeps the sidebar master-detail; narrower it is a two-level stack, Home (the nav at full width) and Page (top bar with back button and an About toggle). Width decides, not platform. Reason: desktop is the acceptance target and jumping between pages from an always-visible list is fastest there, while the Browser head runs on phones and desktops, tablets rotate, and a width rule is testable on Desktop (`--size 400x850`) and headlessly; 720 is the 240 px sidebar plus the narrowest content column (~480) that still reads as a page, so portrait tablets keep the sidebar. Rejected: page-based navigation at every width; `OperatingSystem.IsAndroid()`-style checks; container-query styles alone, which can restyle but not move the nav.
- **One `GalleryNav` is moved between the sidebar and Home**, only when the layout changes. Reason: a second nav would fight over the selection, and one instance keeps search and expansion state across a layout switch.
- **`MainViewModel` owns navigation** (the back stack, `AtHome`, `IsCompact`); the shell drives the transitions from it. Reason: one source of truth for both layouts and the headless tests. Rejected: Avalonia's `NavigationPage`, whose own stack and back button would have to be kept in step and which cannot host the wide layout.
- **Home and Page stay attached, and `PrestoPageTransition.Start` (`PagePush`) is called on them directly.** Reason: a `TransitioningContentControl` swapping Home out would detach the nav, losing its scroll position and replaying its entrance on every back.
- **Overlays get the back request first.** The shell re-adds its `TopLevel.BackRequested` handler each time a page loads, so it runs after the handlers of the overlays that page attached; at Home the request is left unhandled so the OS can close or background the app. Reason: `Dismissal` subscribes in registration order and the shell subscribes first, so back would otherwise leave a page with its sheet still open (BRIDGE-SHEET in the backlog).
- **Back is Alt+Left and the mouse back button, not Escape**, and the wide layout has no back button. Reason: Escape already clears the search box and closes overlays; the desktop look stays unchanged and the sidebar is always one click away. Rejected: a header back button shown while history exists. In compact, Avalonia turns Alt+Left and the mouse back button into `BackRequested` before the page sees the key, so each press pops one level but the Board's Alt+Left reorder is lost there; accepted, since compact is touch-first and the list still reorders by drag.
- **Twin links push; nav choices replace in compact**, so back from a page chosen at Home goes Home, while a twin link is a detour you return from. The wide history is capped at 50 pages.
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

### Tempo

Tempo (`samples/Tempo/`, usage in its [README](../samples/Tempo/README.md)) is a Pomodoro focus timer with a task list, built the way an outside consumer would build an app with Presto. Its friction points are the "Tempo findings" in [BACKLOG.md](BACKLOG.md). Its decisions:

- **Standalone, not a gallery page.** It references only the library packages, never `PrestoSample` or its kit, and has its own palette and styles. Reason: it should read as an app someone built *with* Presto, copyable as a whole. Rejected: a Showcase page, which would inherit the kit and the shell's constraints.
- **Desktop only**, one WinExe project with no shared/heads split. Reason: desktop is the acceptance target and a focus timer is a desktop app; the split can come later if a Browser head is wanted.
- **Semi.Avalonia (Dark), not Fluent.** Semi supplies the base controls, flyout and inputs; Tempo's styles restyle the hero surfaces over Semi's color tokens and clear theme transitions on properties Presto animates. Reason: every gallery page runs on Fluent, so a feature quietly coupled to Fluent's templates would go unnoticed. Rejected: Fluent (tests nothing new) and the Simple theme with hand-styled controls (no third-party-theme coverage).
- **The intro is built with Presto, not Lottie.** Reason: it shows the library doing what the app does (draw, particles, a motion path, OKLCH color, connected flights) and needs no licensed asset; a canned clip could not hand its elements off.
- **The intro becomes the app.** Its ring and wordmark carry the `Connected.Key`s of the Focus ring and the title-bar wordmark, so swapping `IntroView` for `ShellView` flies them into place, while the shell's other parts fade in alongside through a `handoff` class (`Connected.Anchor` plus `TempoMotion.HandoffFade`). Reason: a splash that fades out before a UI fades in reads as two things. Rejected: a splash window (no flight across windows) and one `Presence.Enter` fade on the shell, which would fade the flights with it. The flight on the first swap, onto a never-measured shell, lands on its target, so the planned fallback of attaching the shell hidden first was not needed.
- **The intro is a code-built timeline.** `TempoIntro` lays `TempoMotion` builders out on one `Timeline` by position (about 2.3 s, then the flight), and `IntroView` starts it on attach. Reason: overlaps, a skip (`Complete()`) and an awaited end are what timelines are for, and XAML has no timeline spelling by design. `Recipes.Assemble` starts from a timeline callback, and a skip cancels it and sets the wordmark's landed state, since a particle effect cannot join the timeline.
- **The intro only adds a minimum duration.** The real load runs underneath and the handoff waits for both; a click, Space, Enter or Esc skips it, a setting and `--skip-intro` turn it off, and reduced it is the ring and wordmark fading in over about 400 ms. Reason: a fake wait that cannot be skipped is the opposite of premium.
- **XAML owns the tree; `TempoMotion` owns the motion.** Every animation is a `public static readonly AnimationBuilder` in `Motion/TempoMotion.cs`, each with a `WhenReduced` form, assigned from XAML with `{x:Static}`, or a `{presto:…}` recipe extension where one says it exactly. Code-behind only triggers a builder where no XAML trigger exists, never builds one inline. Reason: one place to read and tune all the motion, and a whole-app test of builders as the value lifecycle surfaces carry (CORE-BUILDER). The tick dot's path builder is a method, since a motion path is read from a laid-out shape.
- **The code-only exceptions are explicit**: particle effects (`Recipes.Burst` on session complete, `Recipes.Assemble` in the intro), showing a toast (`ToastHost.Show`), the reduced-motion flag (`App.ApplyMotionSetting`) and the intro timeline. Each is a call in code-behind reacting to a view-model event or an attach, and each is a backlog finding. Reason: the rule is to prefer XAML, not to bend the app around it. Rejected: app-side attached properties wrapping them, which would be the library growing quietly inside a sample.
- **Wall-clock time is the truth; Presto only presents it.** `FocusClock` derives elapsed, remaining and progress from a `TimeProvider` on every read, a 4 Hz `DispatcherTimer` refreshes the view model, and the ring smooths between updates with `Follow`. Reason: the bridge pauses the engine while the window is minimized (BRIDGE-INIT), and a focus timer must keep counting; a `TimeProvider` also makes the clock testable. Rejected: a Presto `Timer` as the session clock.
- **The mode is the root's `Motion.State`.** The shell binds `FocusViewModel.MotionState` (the mode, plus `Running` while the clock runs, since a state takes one value), and the accents (the backdrop gradient's among them, an `accent-bg` border under a fading `OpacityMask`, since a gradient stop is not a control a state can target) are `MotionStates` in shared styles that descendants follow, staggered by `StaggerChildren`, recoloring through OKLCH. Reason: one binding drives the whole window's mode change, and OKLCH keeps coral to aqua from passing through gray.
- **Reduced motion is a first-class setting.** System (the default) follows the OS through `FollowSystemReducedMotion`; Full and Reduced set the flag, applied at startup before the window exists and live on every change. Reduced, motion becomes fades, loops stop and the burst is skipped, but the colors still change. Reason: the project-wide rule (§5) applies to apps too.
- **MVVM with CommunityToolkit.Mvvm**, compiled bindings and `ObservableCollection` task lists, so `Reorder` needs no command. Reason: the idiomatic Avalonia stack, already centrally managed.
- **No assets beyond fonts.** Icons are `PathGeometry` resources. Reason: no licensing questions, and morphable icons need paths anyway.
- **Focus and Stats swap in a `TransitioningContentControl`, not a `Carousel`.** Reason: a carousel only hides the view it is not showing, so the stats bars' `Presence.Arrive` stagger would never replay; a fresh view per swap does. Because each view is rebuilt, `FocusView` assigns the play icon's `IconToPause` states from `OnLoaded` (attaching while a `MorphTo` state holds throws).
- **A task moving to Done waits `MainViewModel.DoneMoveDelay` (450 ms).** Reason: the move is a removal and an add across two lists, and the new row's `Follow` snaps, so the strike line must draw in the old row first.
- **Plain-key shortcuts (Space, N, Del) are off while a text box has focus.** Reason: Avalonia runs window key bindings before the focused control sees the key, so typing would start the timer or delete a task.
- **`tests/Tempo.Tests` runs headlessly on Semi's Dark theme and Tempo's styles**, without Tempo's `App`, with each window test in both motion modes; `MainWindowTests` runs the intro, handoff, tasks and a mode change end to end. Reason: as for the gallery, DevTools cannot send drags or key shortcuts, and `App` attaches the developer tools, which refuse a second attachment in one process.
