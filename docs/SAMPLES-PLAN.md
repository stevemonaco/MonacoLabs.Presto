# Monaco.Presto — Advanced sample gallery plan

The gallery built through Phases 5–12 is a *reference*: one page per anime.js documentation area, each showing a
feature in isolation with its parameters spelled out next to it. That is worth keeping exactly as it is.

This plan adds a second section — **Showcase** — of nine demos that use many features at once, at a scale and a level
of polish the reference pages deliberately avoid, and restyles the shell so the whole gallery looks like a product
rather than a test harness. Its three jobs:

1. **Showpiece motion design** — set-pieces that stand next to the anime.js homepage demos without apology.
2. **Real-world UI patterns** — the shapes people actually ship: filtered grids, boards, dashboards, scroll stories.
3. **Proof of the hard parts** — the things this port can do that the original cannot: an engine on the render thread
   that keeps animating through a frozen UI thread, FLIP on the compositor, thousands of targets, additive blending.

A fourth job falls out of the work: these pages are the first real dogfooding of the public API. Every friction point
they turn up is recorded in §8 and lands in [PLAN.md](PLAN.md) §7 — the demos do **not** quietly grow the library to
paper over a gap.

## 1. Gallery restructure

`GalleryPage` gains a `Section`, and `MainViewModel` exposes `IReadOnlyList<GallerySection>` instead of a flat list.

| Section | Pages |
|---|---|
| **Reference** | the existing 17, unchanged in code and in order |
| **Showcase** | the nine below |

The nav becomes a `TreeView` with the two sections as expanded roots. A `TreeView` rather than two `ListBox`es because
two lists bound to one `SelectedItem` fight each other: the list that does not contain the selection writes `null` back
over it. Section nodes are not selectable; `SelectedPage` is set from the `TreeView`'s selection when it is a
`GalleryPage`.

Shell changes (`Views/MainView.axaml`):

- Nav rail restyled: section headers, a selected-item indicator, hover states.
- Page header keeps title + description, gains the section as an eyebrow.
- Page switches animate — a short fade/rise on the incoming page, driven by Presto. The gallery should animate itself.

## 2. Visual language

A bespoke dark look applied to the **whole** gallery, reference pages included, without editing a single reference page:
the existing class styles (`stage`, `demo`, `chip`, `dot`, `swatch`, `readout`, `code`, `caption`) keep their names and
their roles, and only their setter *values* change. Any page that already overrides one locally keeps winning.

- `Styles/Showcase.axaml`, merged from `App.axaml`: palette resources, the restyled shared classes, and the new
  showcase-only classes (`stage.showcase`, `tile`, `card`, `rail`, `pill`…).
- `RequestedThemeVariant="Dark"` on the application. The showcase demos are designed against a dark stage, and a
  sample app is allowed one opinion. (The library itself stays theme-agnostic — nothing in the bridge changes.)
- Palette: one accent ramp plus two supporting hues, defined once in `Showcase/Palette.cs` for code-built visuals and
  mirrored as XAML resources. Compositor scenes cannot read `StaticResource`, so `Palette` is the single source and the
  XAML resources reference the same literals.
- Type: Inter is already loaded by the heads; the showcase uses it with a tighter scale, and keeps the monospace
  `code`/`readout` classes for anything numeric.

**Acceptance for this section: all 17 reference pages are stepped through on Desktop afterwards and still read correctly.**

## 3. Shared showcase kit

New folder `samples/PrestoSample/Showcase/`:

| File | Purpose |
|---|---|
| `ShowcasePage.cs` | Base `UserControl` for every showcase page: owns a `ControlScope`, reverts it on detach, exposes `Scope`. Kills the repeated `OnDetachedFromVisualTree` cleanup in each page. |
| `Palette.cs` | Colours, brushes and ramps, as `Color`/`IImmutableBrush` so scenes and controls share them. |
| `TransportBar.axaml(.cs)` | Play / Pause / Restart / Reverse / Alternate / Loop, a progress scrubber, a speed slider and a duration (`Stretch`) slider, bound to any `Timer`. Used by the title sequence and the dashboard. |
| `FpsMeter.cs` | UI-thread frame rate from a `Presto.CreateTimer` with `FrameRate = 0`; a second readout fed by a scene for the render thread. |
| `SceneText.cs` | Glyph-run text for `CompositorScene.OnRender` — `ImmediateDrawingContext` has no `DrawText`. See the spike in §7. |
| `Datasets.cs` | The seeded sample data the gallery, board and dashboard share, via `Utils.CreateSeededRandom` so every run looks the same. |

Pages live in `samples/PrestoSample/Showcase/Pages/`, scenes beside the page that hosts them.

## 4. The nine demos

Each entry: what it proves, the API it exercises, how it is built, and what "done" means.

### 4.1 Title sequence — `TitleSequencePage`

*Showpiece. The one that goes in the README.*

A cinematic build: a monogram `Path` draws itself, morphs into the wordmark's mark, "PRESTO" arrives character by
character, a subtitle scrambles into place, three feature cards rise in, and a field of drifting dots sits behind it all.

- **Exercises** `Timeline` labels and `TimelinePosition` (`"+=100"`, `"<"`, `"<<"`), nested timelines added with
  `Sync`, `Call`, `SplitText` per-character targets, `CreateDrawable` + `["Draw"] = "0 1"`, `MorphTo` chained off the
  draw, `Stagger` with `From = Center`, `PlaybackEase`, and the full transport: `Seek` to a label, `Stretch`, `Speed`,
  `Reverse`, `Alternate`.
- **Build** one root `Timeline` assembled in `OnAttached`, labels `draw`/`mark`/`title`/`cards`/`out`; `TransportBar`
  bound to it; label buttons call `Seek(tl.Labels[name])`.
- **Done when** scrubbing backwards through the whole sequence leaves nothing behind, reversing mid-flight is
  continuous, and `Stretch` retimes without a jump.

### 4.2 Morph lab — `MorphLabPage`

*Showpiece. Everything in `Shapes` at once.*

Three panels: a large `Path` looping through six geometries (circle → squircle → star → heart → bolt → wave) with an
ease picker and a precision slider; a signature that writes itself and erases; a track with a marker riding its contour
plus five followers strung out behind it.

- **Exercises** `MorphTo(geometry, precision)` chained across timeline children, the `"Draw"` accessor with keyframes,
  `CreateMotionPath` with the `Rotate` tangent and an offset, `Stagger` for the followers.
- **Build** geometries as `Geometry.Parse` constants; the morph loop is one `Timeline` with `Loop = true`; the followers
  are one animation over five markers with `CreateMotionPath(track, offset)` per index via a function value.
- **Done when** precision changes are visible and honest (low precision visibly polygonal), and the marker's rotation
  tracks the tangent through the sharpest corner of the track.

### 4.3 Particle field — `ParticleFieldPage` + `ParticleFieldScene`

*Showpiece and hard-parts proof in one.*

Thousands of dots on a `CompositorScene`, drifting on staggered loops, parting around the pointer, and bursting from
each click — all ticked and drawn on the render thread.

- **Exercises** `CompositorScene`/`CompositorCanvas`, a `Presto.Engine` per scene, `CompositorCanvas.Post` for pointer
  messages (input is a UI-thread affair; the scene only ever receives messages), `Composition.Blend` so a burst adds
  onto the drift instead of replacing it, `Spring` eases, `Stagger` over a grid, `Utils.Damp` for the pointer follow,
  `Utils.CreateSeededRandom`.
- **Build** `Particle` is a plain class (`X`, `Y`, `Size`, `Opacity`, `Tint`) — the core's reflection adapter drives it,
  which is also the point: no Avalonia objects are involved below the canvas. Count slider 500 → 20 000 (capped lower
  on Browser). A "freeze the UI thread for 2 s" button, and the scene's own FPS drawn inside the scene next to a
  UI-thread FPS `TextBlock`, so the freeze is visible as a divergence rather than a claim.
- **Done when** the field holds 60 fps at the default count on the dev machine, the burst blends visibly rather than
  snapping, and the in-scene readout keeps climbing through the freeze while the UI-thread one stalls.

### 4.4 Card gallery — `GalleryFlipPage`

*Real-world. Phase 12 in the shape people actually want it.*

Twenty-four cards; filter chips, a sort toggle, and a grid/list switch. Everything reflows with FLIP. Clicking a card
expands it into a detail pane as a shared element; closing returns it to its cell.

- **Exercises** `AnimateLayout` (`Position` and `PositionAndSize`), `AnimateSharedElement`, entry/exit animations for
  cards that appear and disappear (FLIP moves things, it does not birth them), `Stagger` on the entry, `Scope` revert.
- **Build** an `ObservableCollection` filtered/sorted into an `ItemsControl` over a `WrapPanel`; every card arms
  `AnimateLayout` on creation inside the page scope.
- **Done when** filter, sort and layout switch all reflow continuously, a card removed mid-flight does not strand a
  transform, and the shared-element expand/collapse is reversible at any point.

### 4.5 Board — `BoardPage`

*Real-world. Draggable and FLIP together, which is where both get hard.*

Three columns of cards. Lift a card and the others close the gap; drop it in another column and it snaps into place;
fling it at the bin and it spins out.

- **Exercises** `CreateDraggable` across its parameter surface — `Container`/`ContainerPadding`/`ContainerFriction`,
  `Snap`, `ReleaseStiffness`/`ReleaseDamping`/`ReleaseMass`, `MinVelocity`/`MaxVelocity`, `ScrollSpeed`/
  `ScrollThreshold` for edge auto-scroll, `OnGrab`/`OnDrag`/`OnRelease`/`OnSnap`/`OnSettle` — composed with
  `AnimateLayout` on every other card.
- **Build** the drag target is reparented on drop; the gap-closing reflow is FLIP; the dismiss is a one-shot animation
  plus removal in `OnComplete`.
- **Done when** a fast fling across two columns lands correctly, the reflow never fights the drag, and dismissing during
  a reflow does not leave a hole.

### 4.6 Dashboard — `DashboardPage`

*Real-world. Numbers that move.*

KPI tiles counting up, a staggered bar chart that re-randomises, a donut revealed arc by arc, and a sparkline that
morphs between datasets. A "live" switch pushes new data every 800 ms so retargeting is visible mid-flight.

- **Exercises** `TweenParams.TextModifier` with `Utils.RoundPad` for the counters, `CreateDrawable` for the donut arcs
  and the sparkline stroke, `MorphTo` between dataset geometries, `Stagger` on the bars, a `Timer` driving the live
  feed, and `Composition` on retarget (the default replace, seen properly for the first time).
- **Done when** a retarget mid-animation is continuous in every widget, and the live mode runs for minutes without
  drift or leak.

### 4.7 Scroll story — `ScrollStoryPage`

*Real-world. The long-page pattern.*

A scrolling article: a parallax hero, a pinned section whose timeline is scrubbed by the scroll position, reveals on
threshold crossings, a progress rail, and an element that reacts to scroll velocity.

- **Exercises** `OnScroll` with named `Enter`/`Leave` thresholds, `ScrollSync` with `SyncEase` and `SyncSmooth`,
  `Link(timeline)`, `Velocity`, `Backward`, `Progress`, `Refresh` on resize.
- **Done when** the pinned scrub is frame-accurate in both directions, and a resize mid-scroll re-measures without a jump.

### 4.8 Interruption lab — `InterruptionPage`

*Hard parts. The one thing the reference gallery cannot show at all.*

Three identical pucks, one per `Composition` mode, retargeted to wherever you click. Spam the stage and the difference
is obvious: `replace` snaps authority to the newest, `none` lets them pile up, `blend` sums them additively. Below it, a
card travelling across the stage with a hover wobble layered on top — blended, the wobble rides the travel; replaced, it
kills it.

- **Exercises** `Composition.Replace`/`None`/`Blend`, `Core.Additive`, `Presto.Remove`, `Animatable` retargeting, with a
  live readout of how many animations and tweens are actually alive per puck.
- **Done when** the three modes are visibly, explainably different, and the alive-count readout proves `replace` is
  actually retiring tweens.

### 4.9 Stress bench — `StressBenchPage` + `FrameGraphScene`

*Hard parts. Honest numbers, not adjectives.*

A count slider from 100 to 20 000 targets, a mode switch (UI-thread animations / compositor animations per visual /
compositor scene), a property-set switch (opacity only / transform / transform + colour), and readouts: UI-thread fps,
render-thread fps, mean and 95th-percentile frame time, gen-0 collections and managed-heap delta over the last ten
seconds. The frame-time graph is itself a `CompositorScene`, so it keeps drawing while the UI thread is frozen.

- **Exercises** engine throughput, `Engine.FrameRate`/`Speed`, allocation behaviour of the tick path, and — behind a
  toggle — a **second `Engine` on a worker thread** with its own `StopwatchFrameSource` and `MakeCurrent()`, driving
  plain objects and posting results to the UI. That is Phase 9 made visible.
- **Done when** the numbers are reproducible, a steady state at 5 000 targets shows ~zero gen-0 growth, and any figure
  that disappoints is written down in §8 rather than hidden behind a lower default.

## 5. Phases

- [ ] **13a — Shell and kit.** Sections + `TreeView` nav, `Styles/Showcase.axaml` and the palette, dark variant,
      page-switch animation, `ShowcasePage`, `TransportBar`, `FpsMeter`, `Palette`, `Datasets`. Verify: all 17
      reference pages stepped through on Desktop.
- [ ] **13b — Showpieces.** Title sequence, Morph lab, Particle field (+ `SceneText` spike first, §7.1).
- [ ] **13c — Real-world.** Card gallery, Board, Dashboard, Scroll story.
- [ ] **13d — Hard parts.** Interruption lab, Stress bench (+ `FrameGraphScene`).
- [ ] **13e — Docs.** README showcase section with captures, `CLAUDE.md` layout table updated with the
      `Showcase/` folder, this checklist ticked, findings from §8 folded into [PLAN.md](PLAN.md) §7.

Each demo is independently shippable; the order within a phase does not matter.

## 6. Verification

Samples are verified by running them, per `CLAUDE.md`: `dotnet run --project samples/PrestoSample.Desktop`, with
DevTools screenshots for the record. Per page, the "done when" clause above is the acceptance test.

Two additions worth making:

- **A headless smoke test** over the showcase pages — construct each one, attach it, tick 60 frames, detach, assert no
  exception and that the scope reverted. Nine pages of animation built in `OnAttached` is enough surface that a
  constructor-level regression should not wait for a human to click through. It needs a test project referencing
  `PrestoSample`; propose it in 13a and drop it if the reference cost is not worth it.
- **Browser/Android**: the compositor pages have never been driven on either. The showcase pages that need a scene
  (4.3, 4.9) cap their counts and degrade to the UI-thread path when `OperatingSystem.IsBrowser()`. Desktop stays the
  acceptance target; the other heads are checked once at 13e and the README Platforms table is updated with whatever
  is actually true.

## 7. Risks and spikes

1. **Text inside a compositor scene.** `ImmediateDrawingContext` offers `DrawGlyphRun`, not `DrawText`. `SceneText`
   builds a `GlyphRun` from `Typeface.Default.GlyphTypeface` — **spike this before 13b**, because it also has to be
   safe to construct on the render thread. Fallbacks, in order: pre-render digit bitmaps on the UI thread and
   `DrawBitmap` them; or draw digits from rectangles. The in-scene readout matters: it is what proves the freeze.
2. **20 000 targets may not hold 60 fps.** The count is a slider precisely so the ceiling is discovered rather than
   promised. Measure at 13d and set the defaults from the measurement.
3. **Restyling the whole gallery can break reference pages.** Mitigated by changing setter values only — never adding,
   removing or renaming a class — and by the 13a acceptance pass over all 17 pages.
4. **`AnimateLayout` under churn** (4.4, 4.5): removing a control mid-transition, or reparenting during a drag, is
   exactly where FLIP implementations strand transforms. These two pages are the real test of Phase 12; expect to find
   something.
5. **Sample project growth.** Nine pages at 200–350 lines plus scenes roughly doubles the sample. Shared code goes in
   `Showcase/`, and anything that turns out to be generally useful is a §8 candidate for the bridge, not a quiet copy.

## 8. Library findings

Filled in during implementation; each entry is a gap the demos hit, with a recommendation. Nothing here gets fixed as a
side effect of a demo — it graduates to [PLAN.md](PLAN.md) §7 and is decided on its own.

| # | Found in | Gap | Recommendation |
|---|---|---|---|
| | | | |
