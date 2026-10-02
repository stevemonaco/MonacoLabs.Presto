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
- Palette: one accent ramp plus a supporting hue per demo that needs one, defined once in `Showcase/Palette.cs` for
  code-built visuals and mirrored as XAML resources. Compositor scenes cannot read `StaticResource`, so `Palette` is the
  single source and the XAML resources reference the same literals.
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
| `FpsMeter.cs` | UI-thread frame rate from an uncapped `Presto.CreateTimer`; a second readout fed by a scene for the render thread. |
| `SceneText.cs` | Glyph-run text for `CompositorScene.OnRender` — `ImmediateDrawingContext` has no `DrawText`. See the spike in §7. |
| `Datasets.cs` | The seeded sample data the gallery, board and dashboard share, via `Utils.CreateSeededRandom` so every run looks the same. |

Pages live in `samples/PrestoSample/Showcase/Pages/`, scenes beside the page that hosts them.

## 4. The ten demos

Each entry: what it proves, the API it exercises, how it is built, and what "done" means.

### 4.1 Title sequence — `TitleSequencePage`

*Showpiece. The one that goes in the README.*

A cinematic build: a monogram `Path` draws itself, morphs into the wordmark's mark, "PRESTO" arrives character by
character, a subtitle scrambles into place, three feature cards rise in, and a field of drifting dots sits behind it all.

- **Exercises** `Timeline` labels and `TimelinePosition` (`"+=100"`, `"<"`, `"<<"`), nested timelines added with
  `Sync`, `Call`, `SplitText` per-character targets, `CreateDrawable` + `["Draw"] = "0 1"`, `MorphTo` chained off the
  draw, `Stagger` with `From = Center`, and the full transport: `Seek` to a label, `Stretch`, `Speed`,
  `Reverse`, `Alternate`.
- **Build** one root `Timeline` assembled in `OnAttached`, labels `draw`/`mark`/`title`/`cards`/`out`; `TransportBar`
  bound to it; label buttons call `Seek(tl.Labels[name])`. The cards start hidden and come in `"+=200"` after the
  subtitle (`Y` `OutQuart`, a shorter `Opacity` `OutQuad`), and `out` holds 800 ms so a loop restart does not cut
  straight from the finished frame.
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
- **Measured at 13b** — 60 fps holds to ~7 500 on the dev machine (5 000 → ~120 fps, 7 500 → ~70, 10 000 → ~40,
  20 000 → ~25, Debug, 710×278 canvas); default 5 000, slider top end stays 20 000.

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
- **Settled at 13c** — the expand is played by the card's own `AnimateLayout`: the card is reparented into the detail
  pane and the transition it was armed with inverts from wherever it is drawn, so a reversal halfway carries on from
  there. `AnimateSharedElement` cannot meet the reversibility clause — it is one-shot and re-measures from the slot
  (§8 row 13) — so no showcase page calls it; the Reference section's `LayoutPage` is where it is demonstrated. The
  rail's `Position`/`Position+Size` toggle re-arms every card, which is where the two `AnimateLayout` modes are shown.

### 4.5 Board — `BoardPage`

*Real-world. Draggable and FLIP together, which is where both get hard.*

Three columns of cards. Lift a card and the others close the gap; drop it in another column and it snaps into place;
fling it at the bin and it spins out. While a card is lifted or thrown, the lane it is aimed at keeps a gap open at
the slot it would land in, so the drop fills that gap and nothing else moves.

- **Exercises** `CreateDraggable` across its parameter surface — `Container`/`ContainerPadding`/`ContainerFriction`,
  `Snap`, `ReleaseStiffness`/`ReleaseDamping`/`ReleaseMass`, `MaxVelocity`, `ScrollSpeed`/
  `ScrollThreshold` for edge auto-scroll, `OnGrab`/`OnDrag`/`OnUpdate`/`OnRelease`/`OnSettle` — composed with
  `AnimateLayout` on every other card.
- **Build** the drag target is reparented on drop; the gap-closing and gap-opening reflow is FLIP, driven by
  `Layout(lane, gapAt)` from `OnUpdate` so it follows the release throw; the release spring settles in ~290 ms; the
  dismiss is a one-shot animation plus removal in `OnComplete`.
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

One sentence at the top says what the page is for: *what happens when an animation is interrupted mid-flight.* Two
panels answer it visibly.

- **Retarget — three ways to change your mind.** Three side-by-side panes, `replace` / `none` / `blend`, each with its
  own puck. Clicking any pane sends all three pucks to the same relative point mirrored into their own pane, by one
  900 ms `OutQuad` animation each that differs only in its composition. A fading trail per pane, sampled from one
  timer's `OnUpdate` into a reused ring of dots, shows the paths: `replace` and `none` trace the same one (the newest
  animation renders last either way), `blend` rounds every corner. **Burst** plays a seeded script of five targets
  160 ms apart from a timeline of `Call`s; **Cancel newest** cancels each pane's most recent live animation, which stops
  `replace` dead and yanks `none` back toward the older targets still running underneath. **Reset** is `Presto.Remove`
  plus a zeroing `Presto.Set`. Under each pane a line reads the live animation count from the page's own pruned list
  (§8 row 19).
- **Hover — interrupt from where you are.** A row of tiles that lift on enter and settle on leave, with a toggle
  between *retarget* (`To`-only values, so an interrupted hover continues from where it is) and *restart* (`FromTo`
  from the resting or hovered values, as naive code does), so a quick sweep across the row jumps under restart and
  stays continuous under retarget.
- **Exercises** `Composition.Replace`/`None`/`Blend`, `Animation.Cancel`, `Presto.Remove`, timeline `Call`s,
  `Utils.CreateSeededRandom`.
- **Done when** the trails make the three modes visibly different, Cancel newest makes `none` legible, and a hover
  sweep shows the jump that retargeting avoids.

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
- **Built at 13g.** The targets live in a fixed 720×300 field (scaled down to fit, never up), so a count means the same
  pixels at every window size: `Border`s on a `Canvas` for the two per-visual modes, `BenchScene` drawing plain `Cell`s
  for the scene mode. Every set loops the same 1.2 s `InOutSine` over a `[0, 1200]` stagger. Compositor tracks have no
  colour, so the per-visual mode runs the colour as a UI-thread `Background` tween beside them. Each count/mode/set is
  built in a nested `Scope`'s constructor and swapped with `Refresh()`, which reverts the previous set before building.
  `FrameGraphScene` graphs the last 240 UI frame times (posted once per UI frame) with their mean and nearest-rank p95,
  and draws the render fps; a UI frame that has not arrived grows as a coral bar. The worker toggle is **included**: a
  second `Engine` on a `StopwatchFrameSource` is filled under `MakeCurrent()` and woken from the thread pool, so the
  pump thread ticks it, drives the same count of `Cell`s, posts its fps and a cell to the UI twice a second, and pauses
  itself from its own tick when switched off. Browser caps the count at 4 000, keeps the mode on UI-thread animations and disables the worker.
- **Measured at 13g** (Debug, dev machine, transform set unless noted; ui / render fps, UI frame mean / p95, gen-0 over
  10 s steady state):

  | Targets | UI-thread animations | Compositor, per visual | Compositor scene |
  |---|---|---|---|
  | 5 000 | 88 / 129, 8.8 / 16.9 ms, +98 | 51 / 51, 16.4 / 22.2 ms, +1 | 106 / 101, 9.9 / 11.9 ms, +1 |
  | 10 000 | 24 / 43, 25.8 / 50.8 ms, +83 | 27 / 27, 30.4 / 39.0 ms | 86 / 89, 11.9 / 16.7 ms |
  | 20 000 | 21 / 65, 38.5 / 85.3 ms | — | 45 / 45, 22.4 / 27.8 ms, +0 |

  At 5 000: opacity only on the UI thread 62 / 87 fps (+92 gen-0), transform + colour 101 fps (+82), opacity only in the
  scene 102 / 107 fps (+1); the worker ran at 64 fps (its pump's `Thread.Sleep(1)` at the Windows timer resolution)
  with the scene still at +0 gen-0. Default 5 000 in the UI-thread mode, which holds 60 fps there; the slider top stays
  20 000. The ~zero gen-0 target holds for the scene and the per-visual mode but not for the UI-thread mode (§8 row 27),
  and the per-visual mode is slower than the UI-thread one (§8 row 28).

### 4.10 Typography — `TypographyPage`

*Showpiece. The text module composed.* Four panels on one scrolling page, each with a `Replay` button.

- **Masked line reveal + reflow.** A paragraph split into `Lines` (`Wrap = true`) and `Words`; each word rises from a
  full line height under its line's mask, 120 ms per line plus 30 ms per word in the line, `OutExpo`. A width slider
  re-wraps the paragraph, the split re-runs, and `AddEffect`'s `Utils.KeepTime` hands the playhead to the rebuilt
  reveal, so it carries on instead of restarting (§8 row 4 shown working).
- **Word rotator.** "Animate _____" cycles `timelines`/`text`/`shapes`/`layouts`/`anything` from a looping `Scope`
  timer: a timeline per change drops the outgoing chars with a staggered `InBack`, swaps the text and re-splits it, the
  incoming chars rise with `OutBack` from `StaggerFrom.First`, and the slot's `Width` eases to the next word's measured
  width. The rotations are built outside `Scope` (§8 row 18) and reverted on detach.
- **Pointer-reactive wordmark.** "MOTION" split into chars, each with its own `CreateAnimatable` over `Y`, `Scale` and
  `Foreground`, built in the split effect so a re-split rebuilds them. `PointerMoved` pulls each char by its distance from
  the pointer (an `InOutQuad` falloff over 160 px): it lifts up to 30 px, grows up to 1.35× and tints along
  `Palette.AccentRamp` from the dim step to the palest, retargeting over 320 ms `OutQuint`. `PointerExited` springs `Y` and
  `Scale` back with `Eases.Spring` and eases the tint back. Replay runs the same lift as a 60 ms-staggered wave, each
  letter springing back as the next one rises.
- **Decode terminal.** One infinite timeline with a 2.4 s `LoopDelay`: four monospace lines type in one after another with
  `ScrambleText` (`OverrideChars = ""` so each starts blank, `Cursor = "_"`, `RevealRate = 48`), a status line scrambles
  to the next message with `Chars = "blocks"` as each line starts, and a bare percentage counts 0 → 100 across the whole
  iteration through `TextModifier` + `Utils.RoundPad`, the `%` in its own `TextBlock` (§8 row 16). Replay restarts it.
- **Exercises** `SplitText` lines/words/chars, `SplitPieceParams.Wrap`, `IncludeSpaces`, `AddEffect` + `KeepTime`,
  `Refresh` on resize, `Revert`, function-value delays, `Stagger` with `From`, `CreateAnimatable` with a brush,
  `ScrambleText` (`Cursor`, `OverrideChars`, `blocks`), `TextModifier`, `LoopDelay`.
- **Done when** dragging the slider mid-reveal re-wraps without restarting, the rotator cycles without the headline
  jumping, the wordmark follows the pointer and springs back, and the terminal loops.

### 4.11 Activity feed — `ActivityFeedPage`

*Real-world. The long, live list.* Built with PLAN Phase 27 ([LIFECYCLE-PLAN.md](LIFECYCLE-PLAN.md) §10).

A notifications and activity feed: about 2,000 seeded events (comments, orders, deploys, mentions) in a virtualized
`ListBox`, with day headers as items of their own type, since `ListBox` has no grouping. A live source adds events
at the top, and the bottom loads more as you scroll.

| What the user sees | Trigger |
|---|---|
| The first screen of rows rises in with a stagger as the page opens | `Presence.Initial` |
| Each row scrolled into view fades and rises 12px, rows from one scroll step rippling | `Presence.Reveal` |
| New events slide in at the top; scrolled down, a "3 new" pill appears and scrolls to the top on click | `Presence.Enter`, pill on `Presence.Initial`/`Enter` |
| A dismissed event collapses out | `Presence.Exit` |
| Skeleton rows shimmer at the bottom while the next page loads (600 ms, 50 events) | `Animate.OnAttached` |
| The unread badge pulses when its count changes | `Animate.OnChanged` |

An overlay has an "animations played" counter per trigger (fed from each builder's `OnBegin`), a Reveal switch
(Off / Once / Every time, over `Presence.RevealOnce`) and a "Use OnAttached" switch that moves the rows' reveal to
`Animate.OnAttached`, so the counter shows it replaying on every recycle.

- **Exercises** every `Presence` property and both `Animate` triggers in one list, `Stagger` on Initial and Reveal
  batches, a `Timer` for the live source, and the item-keyed Reveal memory under inserts, removals and paging.
- **Risks** Inserting at index 0 while scrolled down may move the viewport if `VirtualizingStackPanel` does not keep its
  anchor. If it moves, record it in §8 and hold new events behind the pill until the user returns to the top. Swipe
  to dismiss with `Draggable` on a recycled row is a stretch goal; a dismiss button is the baseline.
- **Done when** a slow scroll reveals each row once and scrolling back up plays nothing, a scrollbar drag to the end lands
  at rest, "Every time" and "Use OnAttached" visibly replay, a live insert while scrolled down does not move what you
  are reading, and ten minutes of live events and paging leave the counters and the Reveal memory bounded.

## 5. Phases

- [x] **13a — Shell and kit.** Sections + `TreeView` nav, `Styles/Showcase.axaml` and the palette, dark variant,
      page-switch animation, `ShowcasePage`, `TransportBar`, `FpsMeter`, `Palette`, `Datasets`. Verify: all 17
      reference pages stepped through on Desktop.
- [x] **13b — Showpieces.** Title sequence, Morph lab, Particle field (+ `SceneText` spike first, §7.1).
- [x] **13c — Real-world.** Card gallery, Board, Dashboard, Scroll story.
- [x] **13d — Hard parts.** Interruption lab, Stress bench (+ `FrameGraphScene`).
- [ ] **13f — Polish round.** Title sequence beat, board insertion gap, interruption lab rebuild, Typography page.
      Implemented; pending a full `slnx` build and a Desktop
      hand-check of the board drag and gap, the hover tiles and the wordmark.
- [ ] **13g — Round 2.** Typography clip and rotator blink, card-gallery load, real controls on the reference pages,
      compositor page rebuild, four new pages (Micro-interactions, Loaders, Carousel, Stress bench).
      Implemented; pending a full `slnx` build and
      `dotnet test --solution` (blocked by a locked Desktop bin), and a Desktop hand-check of the Card gallery
      first-load wave and a pointer drag on the Carousel.
- [ ] **13h — Round 3.** Six new pages (Connected animation, Colour bloom, Gestures, Liquid navigation, Swipe deck,
      Chart morph). Implemented, §8 rows 29–30; the
      three test projects pass, pending a full `slnx` build (blocked by a locked Desktop bin) and a Desktop pass over
      each page against its "done when" clause.
- [ ] **13i — Activity feed.** §4.11, built with PLAN Phase 27 rather than on its own. Implemented, with the overlay,
      `ActivityFeedPageTests` and §8 row 33 (no scroll anchor, so live events wait behind the pill while scrolled
      down). The overlay's counters are plain counts and the Reveal memory drops items as they leave `Items`; pending a
      Desktop pass over the "done when" clause, including the ten-minute run of live events and paging that checks
      both stay bounded, and a look at upward reveals for [LIFECYCLE-PLAN.md](LIFECYCLE-PLAN.md) §10.5.
- [ ] **13e — Docs.** README showcase section with captures, `CLAUDE.md` layout table updated with the
      `Showcase/` folder, this checklist ticked, findings from §8 folded into [PLAN.md](PLAN.md) §7.

Each demo is independently shippable; the order within a phase does not matter.

## 6. Verification

Samples are verified by running them, per `CLAUDE.md`: `dotnet run --project samples/PrestoSample.Desktop`, with
DevTools screenshots for the record. Per page, the "done when" clause above is the acceptance test.

Two additions worth making:

- **A headless smoke test** over the showcase pages — construct each one, attach it, tick 60 frames, detach, assert no
  exception and that the scope reverted. Ten pages of animation built in `OnAttached` is enough surface that a
  constructor-level regression should not wait for a human to click through. It needs a test project referencing
  `PrestoSample`; `tests/PrestoSample.Tests` was added in 13a, and pointer-driven demos (4.3, 4.8) need it either way,
  since the DevTools MCP clicks a `Button` through its automation peer but has no pointer-move or drag.
- **Browser/Android**: the compositor pages have never been driven on either. The showcase pages that need a scene
  (4.3, 4.9) cap their counts and degrade to the UI-thread path when `OperatingSystem.IsBrowser()`. Desktop stays the
  acceptance target; the other heads are checked once at 13e and the README Platforms table is updated with whatever
  is actually true.

## 7. Risks and spikes

1. **Text inside a compositor scene.** `ImmediateDrawingContext` offers `DrawGlyphRun`, not `DrawText`. `SceneText`
   builds a `GlyphRun` from `Typeface.Default.GlyphTypeface` — **spike this before 13b**, because it also has to be
   safe to construct on the render thread. Fallbacks, in order: pre-render digit bitmaps on the UI thread and
   `DrawBitmap` them; or draw digits from rectangles. The in-scene readout matters: it is what proves the freeze.

   **Settled in 13b — glyph runs work, no fallback needed.** `Showcase/SceneText.cs` caches immutable glyph runs built
   on the UI thread, so a per-frame readout allocates nothing. Seen live drawing every frame from Avalonia's real
   render thread; pinned headlessly by `tests/PrestoSample.Tests/SceneTextTests.cs`, which cannot answer the thread
   question because the headless compositor ticks on the UI thread. Promoting it to the bridge is §8 row 3.
2. **20 000 targets may not hold 60 fps.** The count is a slider precisely so the ceiling is discovered rather than
   promised. Measured for 4.3 at 13b (see §4.3); the stress bench was measured at 13g (see §4.9).
3. **Restyling the whole gallery can break reference pages.** Mitigated by changing setter values only — never adding,
   removing or renaming a class — and by the 13a acceptance pass over all 17 pages.
4. **`AnimateLayout` under churn** (4.4, 4.5): removing a control mid-transition, or reparenting during a drag, is
   exactly where FLIP implementations strand transforms. These two pages are the real test of Phase 12; expect to find
   something.
5. **Sample project growth.** Nine pages at 200–350 lines plus scenes roughly doubles the sample. Shared code goes in
   `Showcase/`, and anything that turns out to be generally useful is a §8 candidate for the bridge, not a quiet copy.

## 8. Library findings

Filled in during implementation; each entry is a gap the demos hit, with a recommendation. Nothing here gets fixed as a
side effect of a demo — it graduates to [PLAN.md](PLAN.md) §7 and is decided on its own. The Outcome column records
what the library-findings pass (commit `9bafac0`) decided and what landed.

| # | Found in | Gap | Recommendation | Outcome |
|---|---|---|---|---|
| 1 | 13a `TransportBar` | A `Timer` handed to a control cannot be reconfigured, because its live surface is internal: the callback properties (`OnUpdate`, `OnComplete` and the rest) have internal setters, and `IterationCount` is internal with `TimerParams.Loop` init-only. So the bar can neither observe nor re-loop a timer it did not create — hence its own shadow timer to read the playhead from, and a Loop button that restarts the timer once it reports `Completed`. Upstream reassigns `timer.onUpdate` freely. | Open the post-creation surface of `Timer` — callbacks publicly settable, loop count retimable — or accept looping as a creation-time decision and drop the button. | Fixed: `Timer` callbacks publicly settable, settable `Timer.Loop`; `TransportBar` chains `OnUpdate` and toggles `Loop`. |
| 2 | 13a `FpsMeter` | `FrameRate = 0` means *never render*, not *uncapped*: `Clock.Fps` clamps 0 to `MinValue`, giving a frame duration of 1e14 ms. There is no spelling for "every frame" other than leaving `FrameRate` unset. | Treat 0 as uncapped in `Clock.Fps`, or document that the default is already uncapped. | Fixed: `Clock.Fps` treats 0 or less as uncapped. |
| 3 | 13b `SceneText` | A scene gets Avalonia's `ImmediateDrawingContext`, which has `DrawGlyphRun` and no `DrawText`, and `CompositorScene` caches brushes but offers nothing for text. Every scene wanting a readout has to build and cache its own glyph runs; the sample now carries `Showcase/SceneText.cs` for it. | Promote a `SceneText` to the bridge beside `CompositorScene.Brush`, once 13b has settled the shape it wants. | Fixed: public `SceneText` in `AvaloniaBridge.Compositor`; sample copy deleted. |
| 4 | 13b `TitleSequencePage` | A `SplitText` has no pieces until its block has been measured, and a re-split replaces them, so a timeline child over `split.Chars` can neither be built in `OnAttached` nor survive a re-split: a child's targets are fixed when it is added and `Timeline` cannot re-point one. The page builds its whole root timeline inside the split effect, so `Utils.KeepTime` rebuilds it over the new pieces and hands over the playhead. | Either let a timeline child resolve its targets lazily, or give `TextSplitter` an effect overload that re-points a child of an existing timeline. | Deferred to PLAN.md §7; the `AddEffect` + `Utils.KeepTime` rebuild is documented on `TextSplitter.AddEffect`. |
| 5 | 13b `TitleSequencePage` | `Timeline.Labels` holds raw times, but `PlaybackEase` re-maps the playhead, so `Seek(tl.Labels[name])` renders `ease(t / duration) * duration` instead of the beat the label names — about 360 ms out on this page. Nothing public inverts the ease or reads the eased position back (`_iterationTime` is internal). `Reversed` mirrors `CurrentTime` as well, so the same seek lands on the label's mirror. Since 13f the page no longer sets `PlaybackEase`, so its SEEK buttons land on the labels. | Expose the eased playhead (a public `IterationTime`), or a seek that takes a label and inverts the playback ease and the direction. | Fixed: public `Timer.IterationTime`; remark on `Timeline.Labels`. |
| 6 | 13b `TransportBar`, found by the title sequence | `Timer.Stretch` scales the durations, offsets and labels but not `CurrentTime`, so a duration slider leaves the playhead on the same millisecond of a longer sequence and the picture jumps back to an earlier beat. Upstream behaves the same way. The bar now reads `Progress` across the call and writes it back. | Faithful as it stands; worth a remark on `Stretch` saying progress is not preserved, or a `Stretch` overload that keeps it. | Documented on `Timer.Stretch`. |
| 7 | 13b `MorphLabPage` | Nothing public says how many points a `MorphTo` precision actually samples: `GeometryCodec.PointCount` and `Sample` are internal, so a precision slider can only show the raw points-per-unit it was given. Re-deriving `max(2, ceil(max(contourLength) * precision))` in the sample would be the quiet copy §7.5 warns against, so the page shows the precision and lets the visible polygon at the low end be the proof. | Expose the sampled point count for a source/destination/precision triple beside `MorphTo`, so a caller can report — and test — what the morph is really doing. | Fixed: `PrestoAvalonia.MorphPointCount`. Morph lab shows the sampled point count. |
| 8 | 13b `ParticleFieldScene` | `Composition.Blend` is all-or-nothing per property: the additive pass runs after every engine child, so once one animation blends a property, a later `Replace` animation on it is written and then overwritten the same frame and never appears. The field's burst therefore *has* to blend — which is what §4.3 wanted, but it is discovered rather than documented, and `ParticleFieldSceneTests` pins it. | Say so on `Composition.Blend`: while a blended tween is live on a property, that property belongs to the additive pass. | Documented on `Composition.Blend`. |
| 9 | 13b `ParticleFieldScene` | `CompositorScene.Brush` keys on the exact colour and quantizes only the opacity, which suits a fixed palette. A scene that tweens a per-particle `Tint` asks for a colour no one asked for before on nearly every frame, so the cache grows without bound; the scene coarsens each channel to 16 levels before asking. | Quantize the colour in `Brush` as it already quantizes opacity, or say in its doc that it assumes a small palette. | Fixed: `CompositorScene.Brush` clears its cache at 1 024 brushes; small palette documented. |
| 10 | 13b `ParticleFieldScene` | `TargetParser.Parse` de-duplicates with a linear scan per target, so building one animation over N targets costs N²/2 reference comparisons — 2×10⁸ at §4.3's 20 000 ceiling, and the field rebuilds one on every count change. The burst dodges it by animating only the particles inside its radius. | De-duplicate through a reference-keyed set above a size threshold, or skip the scan when the targets are an array the caller owns. | Fixed: `TargetParser.Parse` switches to a reference-keyed set above 16 targets. |
| 11 | 13c `GalleryFlipPage`, `BoardPage` | A `LayoutTransition` measures the control in its window, so scrolling it is indistinguishable from layout moving it: put an armed control in a `ScrollViewer` and the content trails the scrollbar by the whole transition. Measured — a 40px scroll left the control rendered at its old window position while `Bounds` was already 40 up. The gallery therefore does not scroll, and sizes its field to the stage instead. The board is the second instance and keeps its `ScrollViewer`, because §4.5 asks for the edge auto-scroll: its twelve-row lanes are taller than the stage, so a wheel scroll or a drag into the edge band moves every armed card’s measured position at once and starts a 420 ms transition — the un-lifted cards trail the scrollbar for that long, while the dragged one, whose transition `OnGrab` reverted, tracks the pointer. | Measure against the nearest scrolling ancestor rather than the window, or give `AnimateLayout` a way to skip a pass (a `Suspend`/`Resume`, or an opt-out while a scroll is in flight). | Fixed: `LayoutTransition.Measure` adds back `ScrollContentPresenter` offsets. The card gallery still does not scroll: a card expanded from a scrolled field into the unscrolled detail pane would start from the wrong place, now that the scroll offset is taken out. |
| 12 | 13c `GalleryFlipPage`, `BoardPage` | A `LayoutTransition` and the render-transform shorthands both own the control, and collide at each end of the frame. At measure time `LayoutTransition.Measure` reads the shorthands back through `TranslatePoint` and inverts a drag or an entry transform straight back: every layout pass a drag outlives — the gap closing behind the lifted card, a lane growing, a scroll — measures the card where the pointer has taken it and pulls it off the pointer. At render time the composition `Translation` the transition drives multiplies with `RenderTransform`, and `RenderTransformComposer` leaves the shorthand marked as animating after the tween ends, so every card's first FLIP move logs the collision warning. The gallery runs its entry and exit through `AnimateOnCompositor` instead, where scale and opacity are composition tracks of their own; the board reverts the dragged card's transition in `OnGrab` and arms a fresh one in `OnSettle`, one line before the reparent, which is also what plays the drop. | Let a transition compose with the render-transform shorthands rather than beside them, measuring the layout slot rather than the drawn box. | Fixed: `LayoutTransition.Measure` reads the layout slot; `RenderTransformComposer.Animates` clears when the tween ends. A card's transition now stays live through a drag. Because `Measure` ignores the drag offset, re-arming with `AnimateLayout` on drop played the settle from the slot the card was lifted out of, so the board now arms the drop with `AnimateSharedElement(card, card, …, once: false)`, which measures the drawn box. |
| 13 | 13c `GalleryFlipPage` | `AnimateSharedElement` cannot play §4.4's expand, because it cannot be reversed while it is running: it is a one-shot (`once: true`), and building any transition over a control reverts the live one and measures `first` from the slot rather than from where the control is drawn. Measured on a 600 ms linear move interrupted halfway — reversing it through the control's own transition carried on from the 72 px it was drawn at, while re-arming with `AnimateSharedElement` jumped to the 210 px slot first and then eased back, and left the control with no transition at all afterwards. The page therefore makes the card itself the shared element: it is reparented into the detail pane and the `AnimateLayout` it was built with plays the expand, the collapse and every reversal between them. | Let a shared-element arm hand its `first` to the transition already holding the control instead of replacing it, or give `AnimateSharedElement` the `mode` and `once` arguments `AnimateLayout` has, so a shared element can be armed to stay. | Fixed: `AnimateSharedElement` takes `mode`/`once` and measures `first` with `LayoutTransition.MeasureDrawn`, which also adds the render transform's translation (a drag offset, the X/Y shorthands), so a dropped drag starts from where it was let go. The card gallery keeps the card as its own shared element; the board arms each drop this way. |
| 14 | 13c `BoardPage` | A container that resizes while a drag is in flight cancels the throw. `SizeChanged` restarts the draggable's 150 ms resize debounce, and the `Refresh()` it ends with rewrites both axes with duration 0, which replaces the release tweens; because that write is muted, `OnSettle` never fires either, so the card stops in mid-air and is never dropped. Measured on a release aimed at x=340: frozen at x=212, no settle, ever. Lifting a card out of a lane resized the board, so every flick shorter than the debounce hit it; the page now gives each lane a fixed twelve rows so a lift cannot resize the board. | Re-target a release in flight from `Refresh` instead of replacing it with a zero-duration write, and report the settle when a refresh ends one. | Fixed: `Draggable.Refresh` re-aims a release in flight and it settles. The board's lanes size to their cards. |
| 15 | 13c `BoardPage` | `Draggable.Reset()` — the obvious way to put the axes back to 0 after a drop — stops a Presto animation running on a **different** control: the card spinning out over the bin froze at 30% and never fired the `OnComplete` that removes it. Bisected to `Stop()`; neither `Presto.Remove(draggable)` nor pausing the animatable's own animations does it alone, so it is one of the ticker pauses or the removal of the private `_overshoot`/`_scroll` scratch targets, which an out-of-bounds release leaves two `Composition.Blend` animations on. Writing `drag.X = 0; drag.Y = 0;` instead leaves the other animation alone, which is what the page does. | Have `Stop` retire only what it owns — the blended release tweens can be cancelled through the animations that hold them — or say on `Stop`/`Reset` that it is not safe to call while anything else is animating. | Fixed: `Draggable.Stop`/`Reset` cancel only the draggable's own overshoot and scroll animations. The board drops with `Reset()`. |
| 16 | 13c `DashboardPage` | A retarget reads its from-value back off the target, so a counter's from is whatever its `Text` parses to. A `TextModifier` that writes anything but a bare number poisons the next tween: measured on a counter formatted with `ToString("N0")` and retargeted from 4,428 to 2 000, the first frame after the retarget wrote `NaN` and every frame after it did too, because "4,428" decomposes as the two-number complex value `[4, 428]` against a plain number. The tiles therefore keep `Utils.RoundPad`'s bare output and put the unit in a label of its own. | Keep the overridden tween's own number as the from when a text modifier wrote the value, or say on `TextModifier` that the string it writes has to parse back to the number it was given. | Documented on `TweenParams.TextModifier`. |
| 17 | 13c `DashboardPage` | A `TextModifier` belongs to the tween, not the target: it is handed the number and returns the whole string, with no target or index, so four counters that want their own precision cannot share one animation through a function value. The page runs an animation per tile, which is also why a round of the live feed is seven animations rather than four. Upstream's `modifier` has the same shape. | Give `TextModifier` and `Modifier` the `(target, index, total)` signature every other function-based value has, or resolve them per target. | Deferred to PLAN.md §7. |
| 18 | 13c `DashboardPage` | Everything created while a scope is current registers with it (`Scope.Register` appends; only `Revert` empties the list), so a live feed retargeting seven animations every 800 ms would grow the page scope's revertible list without bound — thousands of dead animations a minute. The page therefore builds its retargets outside `Scope`, which in exchange means nothing reverts a retarget still in flight when the page is detached. | Let a scope drop the revertibles that have finished, or give it an unregister, so repeatedly animating inside a scope is safe. | Fixed: `Scope.Register` prunes superseded animations and hands their original values on. Dashboard and Interruption lab retarget inside `Scope`; the Typography rotator stays outside, as timelines are not pruned. |
| 19 | 13c `DashboardPageTests`, 13d `InterruptionPage` | Nothing public reports how many animations or tweens are alive on a target, so the test that proves the live feed leaks nothing walks the engine's internal `_head`/`_next` chain by reflection. §4.8's readout cannot do that — `PrestoSample` has no `InternalsVisibleTo` — so the page keeps its own list of every animation it made per puck and prunes it on the public `Animation.Cancelled` and `Animation.Completed`. That answers §4.8 for animations and not at all for tweens: `Tween`, `Timer._headTween` and `TweenSiblings` are internal, so "tweens alive" cannot be shown from a page by any means short of reflection, and the page's caption claims only what it can count. | Expose the live animations of a target — a count, or the animations themselves — beside `Presto.Remove`, and a tween count per target property with it. | Fixed: `Presto.GetAnimations(target, property)` and `PrestoAvalonia.GetAnimations`; tween counts stay internal. Interruption lab counts with it; `DashboardPageTests` no longer reflects. |
| 20 | 13c `ScrollStoryPage` | Nothing holds a control still while its track passes, so §4.7's pinned scrub cannot live in the scrolled content at all: upstream's `onscroll-sticky` example leans on `position: sticky`, which Avalonia has no equivalent of. The page draws the pinned stage as a sibling of the `ScrollViewer` inside a `Panel`, keeps it `IsHitTestVisible="False"` so the wheel still reaches the scroller under it, and shows and hides it from the observer's `OnEnter`/`OnLeave`; the fade at each end is two children of the scrubbed timeline rather than the visibility toggle, which would cut. | Say on `OnScroll` that a pinned section is the caller's overlay, or give `ScrollObserver` a way to pin a control to the container's viewport between its thresholds. | Superseded by Phase 45: `ScrollObserverParams.Pin` holds the stage in place inside the scroller, and the page no longer uses an overlay. |
| 21 | 13c `ScrollStoryPage` | Re-measuring a scroller's observers after a resize means keeping your own list of them: `ScrollContainer.Containers` and `Register` are internal, so nothing public maps a `ScrollViewer` to the container that already holds its observers, and each `Refresh()` has to be guarded by `Ready` the way the bridge's own `RefreshObservers` guards it, because a page's `SizeChanged` fires on the first layout pass — before the deferred resolve has measured anything. The container does refresh itself, but 250 ms behind the resize; the page's own `Refresh()` is what makes the re-measure immediate, which `ScrollStoryPageTests` pins by ticking two frames after it. Measured live: shrinking the window 130px mid-pin took the scrub from 43% to the re-measured 39%, and back to 43% when it was restored. | Make `Refresh()` a no-op until the observer has resolved, and expose the container of a `ScrollViewer` (or a refresh across an observer's siblings), so a page does not have to track its own observers to re-measure them. | Fixed: `ScrollObserver.Refresh` waits for the resolve; `PrestoAvalonia.RefreshScroll(ScrollViewer)`. Scroll story calls `RefreshScroll`. |
| 22 | 13d `InterruptionPage` | §4.8 asks for a travelling card whose hover wobble *rides* the travel when it blends and *kills* it when it replaces, and only one of those is reachable, because row 8 cuts both ways. Blended, a property belongs to the additive pass, so the travel has to blend too for the wobble to add to it — and then a replaced wobble is written and overwritten in the same frame and never reaches the card at all, rather than killing the travel. Making the travel replace instead buys the kill and loses the ride: the blended wobble takes the property over and the card stops travelling. The page blends the travel, so the toggle shows the ride and row 8 from the other side. A blended tween's `To` also becomes the additive group's base rather than an offset from where the target is, so the wobble has to aim at the travel's destination; aimed around zero it drags the travel back to the start with it. | Give a blended tween a spelling for "add this much to whatever else is on the property" — a delta or relative `To` that does not move the group's base — so a layered effect can be written without knowing the animation underneath it, and say on `Composition` that a replaced tween cannot reach a property the additive pass owns. Since 13f the page no longer has the travelling card, so this is no longer demonstrated. | Documented on `Composition.Blend`; relative `To` deferred to PLAN.md §7. |
| 23 | 13f `TypographyPage` | A split effect runs before its pieces have been laid out, so nothing it can read says how tall a line or how wide a word is: every piece's `Bounds` is empty, and the text is measured only inside `TextSplitter` on a private copy. The page measures a probe `TextBlock` with the host's font for the line height the reveal rises from, and again for each rotator word's width. Words are not addressable per line either — `Lines` and `Words` are flat lists — so the per-line stagger splits with `IncludeSpaces`, which makes every child of a line row a word piece, and counts them. And a split host whose width follows its text re-splits 150 ms after every text swap, rebuilding the incoming animation mid-flight; the rotator pins the word block's width so it never resizes. | Give a piece its line (a `LineIndex`, or `Words` grouped per line) and expose the split's measured line height and piece widths, so an effect can size and stagger its tweens without a probe. | Fixed: `TextSplitter.LineIndex` attached property and `TextSplitter.LineHeight`; piece widths not exposed. Typography staggers by `LineIndex` without `IncludeSpaces`; the rotator still measures word widths with a probe. |
| 24 | 13g `TypographyPage` | An `Opacity` tween that overshoots 1 makes the control vanish instead of saturating: the bridge writes the eased value straight to `Visual.Opacity`, and Avalonia draws an opacity above 1 nearly transparent. Measured on the word rotator, whose `InBack` exit took each char to 1.10 before fading it: the rendered word lost ~40% of its ink while the chars were still in place and came back as they fell below 1, so every char blinked out, back and out again. CSS clamps opacity, so upstream never shows this. The rotator now eases its `Opacity` with `InQuad` and keeps `InBack` on `Y`. The composition track does the same: `GalleryFlipPage`'s entry ran `Opacity` through `OutBack(2)` on the compositor, so every card faded up to ~0.95, all but vanished while the ease overshot and snapped back at the end; its `OutBack` is now on `Scale` alone. | Clamp `Opacity` to [0, 1] in the Avalonia property adapter (and on the composition track), as the browser does. | Fixed: `Opacity` clamped in `AvaloniaObjectTargetAdapter` and in compiled compositor tracks. Card gallery, Connected and the Typography rotator overshoot `Opacity` again. |
| 25 | 13g `TypographyPage` | A split's pieces are drawn inside the host `TextBlock`, which clips to its bounds by default, so any piece transformed past the text's line box is cut off — upstream's spans overflow freely. The wordmark's lifted, swollen letters lost their tops and the outer halves of M and N; the page sets `ClipToBounds="False"` on the host. | Have `TextSplitter` turn off the host's `ClipToBounds` while split and restore it on `Revert`, or say on `SplitText` that the host clips. | Fixed: `TextSplitter` unclips the host while split; sample `ClipToBounds="False"` removed. |
| 26 | 13g `MicroInteractionsPage` | `MorphTo` reads the geometry it starts from off a `Path` only (`(target as Path)?.Data`), so the `Data` of a `PathIcon` — the icon control a button actually holds — throws "Can't morph a PathIcon" unless a previous tween chains into it. The submit button's spinner is therefore a stroked `Path` rather than the `PathIcon` the plan asked for. | Read the start geometry through the target adapter (any `Geometry`-typed `Data` property), so `PathIcon` and other geometry-bearing controls morph too. | Fixed: `MorphTo` reads `Data` through the target adapter. Micro-interactions keeps a stroked `Path`: `PathIcon` only fills, which suits neither the arc nor the check. |
| 27 | 13g `StressBenchPage` | Animating `AvaloniaObject` targets is not allocation-free: at 5 000 targets in steady state the UI-thread mode runs ~9 gen-0 collections a second (+98 over 10 s for transforms, +92 for opacity only, +82 with colour), while the same animation over plain `Cell`s in the scene mode, or 5 000 compositor tracks, shows +0 to +1. So the core's tick path is allocation-free, and the allocation is in writing values through the bridge or in Avalonia's property system. Not bisected further; the likeliest suspects are the boxed `object` every `IValueCodec.Encode`/adapter write goes through and Avalonia's per-change `AvaloniaPropertyChangedEventArgs`. | Profile the bridge's write path at 5 000 targets and remove whatever allocation is the bridge's own (a typed `double` write for the shorthands and `Opacity`, no boxing), and say on the adapter what is left to Avalonia. | Fixed: typed `AnimatedValue<T>` binding for `double` properties (`Opacity` 104 → 80 bytes/frame); PLAN.md §7 decision 9. |
| 28 | 13g `StressBenchPage` | `AnimateOnCompositor` over many visuals is slower than animating them on the UI thread, and does not free the UI thread: 5 000 transformed `Border`s run at 51 ui / 51 render fps (UI-thread animations: 88 / 129), 10 000 at 27 / 27. Each target gets its own paused mirror `Animation`, which the UI engine ticks as the playback model, and its own composition key frame animations, so the cost lands on both threads at once. Reverting also leaves each mirror at minus its delay rather than 0, so `CurrentTime` of a reverted per-visual animation reads -1 200 on this page. | Share one playback model across the targets of one `CompositorAnimation` (the mirrors differ only by their resolved delay and values), and say on `AnimateOnCompositor` that it is meant for a handful of visuals rather than thousands. | Fixed: revert leaves each mirror at 0; `AnimateOnCompositor` documented for a handful of visuals. Shared playback model deferred to PLAN.md §7. |
| 29 | 13h `ConnectedPage` | `CompositorAnimation.Reverse()` does not hand `Visual.Opacity` back. The bridge holds the control's opacity and pushes the final key frame through a one-shot `mirror.Then(...)` registered when the track starts, which resolves on the first completion only. Reversing a finished fade therefore leaves the composition visual drawing 1 while `Visual.Opacity` still reads 0. The next compositor animation seeds its from-value from that 0 (`CompositorScratch.Seed`) and pops the control invisible. Measured: after open, settle, Back and settle, all eleven scattered cards read `Opacity` 0 while drawn at 1. The completion callback does not help either. `OnComplete` belongs to the mirror with the longest delay, and in a staggered reverse from mid-flight that mirror finishes *first*, so no callback says when the last card is home. The page reverts the scatter from a timer `ScatterDuration` after Back, which is the longest any mirror can take to reverse. | Push the opacity end value on every completion in either direction. Report `OnComplete`/`Then` from the mirror that finishes last in the current direction. | Fixed: `CompositorAnimation` pushes the opacity end value on every completion and completes after the last mirror. Connected retires the scatter from `Then()` instead of a timer. |
| 30 | 13h `GesturesPage` | A draggable axis `Modifier` has to be idempotent: from the release on, it is applied to its own output. The release reads the from-value back through `Animatable.Set`, which takes `modifier(number)`, and the render applies the modifier again, both to that from and to the snapped destination. A rubber band such as `max × (1 − e^(−y / max))` therefore jumps at release and settles short of its snap point, and `Draggable.Y` never reads the snapped value. Measured: held at 67.5 past a 64 threshold, the release jumped to 44.5 and settled at 43.0, the modifier applied to 64, so an `OnSettle` checking for the threshold never fired. Upstream behaves the same way (`animatable.js` sets `_fromNumber = _modifier(_number)`). The pull-to-refresh list applies its modifier only between `OnGrab` and `OnRelease`, so the release starts from the drawn value and lands on the snap exactly. | Keep the unmodified number as the release's from and destination, or say on `DraggableAxisParams.Modifier` that it must be idempotent (rounding, wrapping) and is re-applied to its own output. | Documented on `DraggableAxisParams.Modifier`. |
| 31 | Reference pages (Controls, Callbacks, Keyframes, Value types, Animation basics, Playback settings) | Pressing a button whose X/Y/Rotate/Scale shorthands are animating drew it at its un-animated position while the press lasted, and again on release. Fluent's `Button:pressed` sets `RenderTransform` to `scale(0.98)` through a `TransformOperationsTransition`, and the transition binds at `BindingPriority.Animation` like `RenderTransformComposer`'s group, so the newer binding covered the group until the transition finished. The group also kept the render transform it captured at construction, so the pressed scale was dropped. Present at 83b942c as well, so this is not a regression from rows 12 or 27. | Keep the shorthand group on top of any transition on `RenderTransform`, and compose with the style value underneath it. | Fixed: the composer takes the group back when something displaces it, and composes the base value the style now holds (read one dispatcher job later, because the transition starts before the style value is committed). A base value that changes with no transition raises no change event, so the group only picks it up the next time it is displaced or released. |
| 32 | Loaders (`RenderThread`) | `AnimateOnCompositor` always scales and rotates about the middle of the control: `CompositorAnimation` drives `CompositionVisual.CenterPoint` with a fixed `Size * 0.5` expression and ignores `RenderTransformOrigin`, which the UI-thread shorthands honour and upstream WAAPI gets from CSS `transform-origin`. The bouncing ball (`RenderTransformOrigin="50%,100%"`) therefore squashes about its centre on the render thread and lifts off the floor, instead of flattening onto it. | Build the `CenterPoint` expression from the control's `RenderTransformOrigin` (relative or absolute), as Avalonia does for `RenderTransform`. | Fixed in Phase 34 (FIXES-PLAN item 2): CompositorAnimation builds its CenterPoint expression from RenderTransformOrigin (relative as a Size expression, absolute as a constant) and restarts it when the origin changes, so the Loaders ball flattens onto the floor. |
| 33 | 13i `ActivityFeedPage` | `VirtualizingStackPanel` keeps no scroll anchor, so inserting an item above a scrolled-down list moves what it shows. Measured headless with the feed scrolled to 3 000: one event inserted at index 1 recycled the row being read at y=19 and drew the row above it at y=28, about 77 px of shift for a 68 px row, while the extent grew 447 as the panel re-estimated. The page first held live events behind an "N new" pill while scrolled down. This is Avalonia's panel, not the bridge; §4.11 names the risk. | Keep the hold-behind-a-pill pattern for live lists, and say so in the `Presence.Enter` docs for lists; revisit if Avalonia adds scroll anchoring to `VirtualizingStackPanel`. | Fixed in Phase 34 (FIXES-PLAN item 7): `Presence.ScrollAnchor` scrolls the list back after a change above the first row in view, so that row stays put. The feed sets it and inserts live events at once, even while scrolled down; a pill counts them as "N new above" and scrolls to the top on click, and at the top events still enter through `Presence.Enter`. |
