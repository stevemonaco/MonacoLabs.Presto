# Presto DevTools — an in-app animation inspector

Phase 48 in [PLAN.md](PLAN.md) §5. A toggleable overlay that lists every live tickable, lets you scrub, pause and reverse
any of them, slows or freezes all motion, steps frame by frame and outlines what an animation is moving. The reference
points are GSAP's GSDevTools (global timeline scrubber, speed, loop a section) and the Chrome Animations panel (live list,
slow-mo, scrub, highlight on hover). anime.js 4.5.0 has no equivalent, so everything here is an addition: no upstream
semantics change and no parity test moves.

The overlay is the small part. The core today can only see what is *running*, cannot hold time still, and hides most of
the state an inspector needs behind `internal`. §2 is therefore the bulk of the work and ships first.

## 1. What the core can and cannot observe today

| Need | Today | Gap |
|---|---|---|
| Enumerate tickables | `Engine._head` holds only running, unpaused roots; `Engine.Update` unlinks a timer the frame after it pauses or completes. `Presto.GetAnimations(target)` walks the same list. | A paused, not-yet-played (`Autoplay = false`), completed or cancelled timer is invisible. No registry. |
| Timeline structure | `Timer._head/_next` child list is internal. | No public `Children`. |
| Tween detail | `Tween` exposes `Id`, `Parent`, `Property`, `Target`, `Accessor`; from/to, timings, ease, composition and override state are internal fields. | No read-only view of a tween's span, values or whether a newer tween overrode it. |
| Timer timings | `Duration`, `IterationDuration`, `IterationCount`, `CurrentTime`, `Progress`, state flags are public. | `Delay`, `LoopDelay`, alternate flag, offset in parent, playback ease, `Priority`, the owning engine and whether it is in the engine list are internal. |
| Global speed | `Engine.Speed` rebases every timer. | `MotionValue` springs and velocity tracking step on raw engine time (`MotionValue.StepSpring` uses `now - _time`) and ignore `Engine.Speed`. Slow-mo would leave springs at full speed. |
| Freeze / step | `Engine.Pause()` stops the frame source; `Resume()` rebases timers. | Engine time is the frame source's wall clock (`Engine.Now => _frameSource.Now`), so there is no way to advance it by one frame. After `Pause`/`Resume`, an active spring sees the whole pause as one `dt` and snaps to rest (a latent bug, independent of DevTools). |
| Wall clock outside the engine | — | `Draggable` (lines 128, 459) and `Transformable` (272) read `_engine.FrameSource.Now` directly, so they would keep wall time while the engine is frozen or dilated. `LayoutTransition`'s `Stopwatch` lifetime check is a cache expiry and can stay on wall time. |
| Who created a timer | `Timer.Id` is a counter unless `TimerParams.Id` is set. | No notion of origin: the bridge's own timers (Draggable, ScrollObserver, Reorder, Presence, Motion, page transitions, the additive lookup animation) would flood the list indistinguishable from the app's. |
| Per-frame cost | — | No frame stats (tickables ticked, tweens rendered, values stepped, update time). |
| Ease identity | `Ease` is a bare delegate. | Cannot print `outQuad`; can still sample the curve. |

## 2. Core changes (`Monaco.Presto`, new `Monaco.Presto.Diagnostics` namespace)

All of it is opt-in and pay-for-play: with diagnostics off, the hot path gains one predictable branch in the `Timer`
constructor and one in `Engine.Update`, no allocation.

### 2.1 Engine time base — freeze, step, dilate

Give each engine its own clock instead of reading the frame source directly.

- `Engine.Now` becomes engine time: on each frame it advances by `(source.Now − lastSourceNow) × TimeDilation`, and not at
  all while frozen. Everything that already reads `engine.Now` (timer start/rebase, `Renderer`'s timeline tick time,
  `MotionValue` springs and samples) follows for free.
- `public double TimeDilation` (default 1, > 0). Deliberately separate from `Engine.Speed`: `Speed` is upstream API the app
  may own; dilation is the inspector's slow-mo and scales *everything* on the engine, springs and velocity tracking
  included. Velocity sampled in dilated time stays visually consistent: a fling measured at 0.25× plays back at 0.25×.
- `public bool Frozen` / `Freeze()` / `Thaw()`: engine time stops; the frame source is stopped while frozen (no idle CPU)
  and restarted on thaw without any rebase, because engine time never moved. Distinct from upstream `Pause()`, whose
  semantics (`Paused`, rebase on `Resume`) stay untouched.
- `public void Step(double milliseconds = 1000.0 / 60)`: advances engine time and runs one `Update()` synchronously,
  sinks flushed. Works frozen or not; it is also handy in tests next to `ManualFrameSource`.
- Fix the spring snap on `Pause`/`Resume`: rebase `MotionValue._time` for active values on `Resume`, as timers are rebased
  (separate small item; surfaced by this audit, worth a test regardless of the overlay).
- Bridge: `Draggable` and `Transformable` read `engine.Now` (made `public double Now { get; }`) instead of
  `FrameSource.Now`.
- `TimeUnit.Seconds` keeps working: dilation multiplies raw time before `Globals.TimeScale` is applied, as frame time does
  today.

Tests (`EngineTimeTests`): frozen engine ignores `ManualFrameSource.Advance`; `Step(16)` moves a timer and a spring by
exactly one frame; dilation 0.5 halves timer progress *and* spring progress; thaw does not jump; `Freeze` does not fire
`OnPause`; a timer created while frozen starts from frozen time; spring survives `Pause`/`Resume` without snapping.

### 2.2 Registry — `Engine.Diagnostics`

`public EngineDiagnostics Diagnostics { get; }` on `Engine`, single-threaded like everything else, no locks.

- `bool Enabled` — off by default; turning it on starts registering from that point (no retroactive discovery except
  what is currently in the engine list, which is walked once on enable).
- `IReadOnlyList<Timer> Roots` — live root timers (any state) via `WeakReference<Timer>`, compacted lazily. Children are
  reached through `Timer.Children` (§2.3), so only roots are registered. Weak so the inspector never extends a lifetime:
  a completed, unreferenced animation disappears when collected.
- `RecentlyEnded` — a bounded ring (default 32) of strong references to roots that completed, were cancelled or reverted,
  so short animations remain inspectable after they finish. Cleared on disable.
- `IReadOnlyList<MotionValue> ActiveValues` and a weak list of `Animatable`s, for the springs section.
- `event Action<Timer>? TimerCreated` — raised from the `Timer` constructor (after `Init` for factory timers).
- `ref readonly FrameStats LastFrame` — a struct filled by `Update` when enabled: engine time, delta, real update
  duration (`Stopwatch` ticks), roots ticked, tweens rendered, values stepped, sinks flushed. No event per frame: the
  overlay polls once per its own render, which keeps the tick allocation-free and avoids callbacks inside `Update`.

State changes (began, looped, paused, completed) are read by polling the public flags each overlay frame rather than
hooked into `Renderer.Tick`: the overlay redraws every frame anyway, and polling keeps the parity-critical tick code
untouched. If a later feature (break-on-complete) needs edges, add one internal notification in `Renderer` behind the
`Enabled` branch.

### 2.3 Read-only introspection surface

Public getters only; no new setters except where a control already exists.

- `Timer`: `Delay`, `LoopDelay`, `IsAlternating` (the name `Alternate` is the method), `Offset` (start in parent, or engine
  time for a root), `PlaybackEase`, `Priority`, `Engine`, `IsScheduled` (in the engine list), `Children`
  (`TimerChildren`, a struct enumerator over the linked list: no allocation), `Tweens` (same pattern over
  `_headTween`), `Origin` and `CreationTrace` (§2.4).
- `Tween` gains: `Delay`, `StartTime`, `Duration` (relative to the parent iteration), `AbsoluteStart/End`, `Ease`,
  `Composition`, `IsOverridden`, `IsOverlapped`, `ValueKind`, `Unit`, and `From`/`To`/`Current` as `TweenSnapshot`
  (number or channel array copy, plus the composed display string). Snapshot methods allocate and are for tools only.
- `MotionValue`: `IsAnimating` (in the engine's active list), `Spring` (the follower's spring, if any).
- Ease names: `Eases.TryGetName(Ease, out string)` backed by a `ConditionalWeakTable<Ease, string>`. The fixed eases are
  registered once at static init. Parameterised factories (`Eases.Out(3)`, `Eases.Spring(...)`) and `EaseParser` register
  only while a process-wide `Diagnostics.Active` flag is set (any engine's diagnostics enabled): those factories mint a new
  delegate per call, often in loops, and must not pay a locked table insert in a release app. Eases created before
  enabling show as `custom`; the overlay always draws a sampled curve thumbnail anyway.

### 2.4 Origin and creation trace

- `Timer.Origin` (`string?`): who made it. Set through an ambient tag, in the same shape as `Engine.MakeCurrent()`:
  `using (Diagnostics.Origin("Draggable")) { … }`. The bridge wraps its own timer creation (Draggable, ScrollObserver,
  Reorder, Presence, Motion states, page transitions, Connected, Follow, recipes) and the core tags the additive lookup
  animation. Untagged means app code. With diagnostics inactive, `Origin()` returns an empty struct whose `Dispose` does
  nothing, and the constructor reads the `[ThreadStatic]` tag only behind the `Enabled` branch.
- `Diagnostics.CaptureCreationTrace` (off by default, only honoured while `Enabled`): stores `new StackTrace(skip, true)`
  so the overlay can show the app frame that created an animation. Expensive by design; the overlay toggles it.
- Grouping by `Scope`/`ControlScope`: `Scope.Register` already sees every timer created inside a scope; record the scope so
  the list can group "everything this control's scope owns".
- `Origin`, `CreationTrace` and `Scope` live in one lazily created `TimerDiagnostics` side object behind a single
  `Timer` field, allocated only when diagnostics are enabled: a release app pays 8 bytes per timer and no allocation.

### 2.5 Explicitly not in the core change

- Seeking or scrubbing needs nothing new: `Seek`, `Progress`, `Pause`, `Resume`, `Reverse`, `Speed` already exist.
- Compositor scene engines run on the render thread and cannot be read from the UI thread. Out of scope for 48 (§5).

### 2.6 Cost budget with diagnostics off (acceptance)

- Per frame: no added work per timer or tween; one `Frozen` branch in `OnFrame`, one `Enabled` branch in `Update`, and a
  multiply-add for dilation in `Engine.Now`. `Renderer.Tick` and tween rendering are unchanged.
- Per timer created: one `Enabled` branch, no `[ThreadStatic]` read, no allocation; +8 bytes per `Timer`.
- No allocation from ease factories, `EaseParser` or bridge `Origin()` scopes while inactive.
- Verified by: the existing `AllocationTests` (core and bridge) run with diagnostics off *and* on (the tick must stay
  allocation-free either way), new allocation tests for `Eases.Out(3)`/`Origin()` with diagnostics inactive, and a
  BenchmarkDotNet comparison against the pre-48 commit (create 10k animations; tick 1k tweens) whose creation and tick
  numbers must sit within noise. The benchmark is a throwaway under the scratch area, not a checked-in project.

## 3. The overlay (Avalonia)

### 3.1 Hosting

- `PrestoDevTools.Attach(TopLevel, KeyGesture?)` and `AppBuilder.UsePrestoDevTools()`; the sample calls it under
  `#if DEBUG` next to `AttachDeveloperTools()`. Turning the overlay on enables `Engine.Diagnostics`; closing it disables
  diagnostics and thaws/undilates the engine so nothing is left slowed.
- Rendered in the window's `OverlayLayer` (same mechanism as `ParticleOverlay` and `Presence` ghosts), docked bottom,
  resizable, collapsible to a slim transport bar. Highlights go in the `AdornerLayer`.
- The overlay must not observe or freeze itself: it uses no Presto animations, or runs them on a private `Engine` with its
  own `AvaloniaFrameSource` that is excluded from the registry. Freezing the main engine must leave the overlay responsive.

### 3.2 Feature set

**v1 (Phase 48)**

1. **Transport bar** — freeze/thaw, step one frame (and hold to repeat), dilation presets 0.1 / 0.25 / 0.5 / 1 / 2, a
   frame-time sparkline and counters from `FrameStats` (roots, tweens, active springs, update ms), engine `Speed` shown
   read-only so an app-set rate is not mistaken for the inspector's.
2. **Tickable list** — tree of roots → timeline children → tweens. Row: kind glyph (timer/animation/timeline), `Id`,
   origin chip, target summary (`Name` or type + index, "3 targets"), properties, state (pending/delay/running/paused/
   completed/cancelled), live progress bar with iteration count. Filters: state, hide bridge-internal origins (on by
   default), text search over id/target/property. Recently ended roots shown dimmed.
3. **Inspector for the selected root** — a scrub track showing delay, iterations and loop delays; for timelines, labels
   and a Gantt of child spans; per-timer play/pause/reverse/restart/seek/speed. Scrubbing pauses the root and seeks it;
   letting go restores the previous play state. Controls always act on the root: a timeline child is selectable and
   highlighted on its parent's track but is not seeked on its own, which upstream semantics do not support cleanly.
4. **Tween table** — property, from → current → to, ease name and curve thumbnail with the current point, span on the
   track, composition and an "overridden by #id" marker.
5. **Target highlighting** — hover a row to outline its targets, selection keeps the outline, and a pick mode: click a
   control in the app to filter the list to everything that tweens it (via the registry, so paused and pending
   animations are included, unlike `Presto.GetAnimations`).
6. **Springs** — active `MotionValue`s and `Animatable` properties: value, target, velocity sparkline.

**Later**

- Break on event: freeze when a chosen timer begins, loops or completes (needs the §2.2 internal notification).
- Loop a range on a selected root (GSDevTools in/out markers).
- "Copy as code": print a timer's resolved parameters through the fluent builder (the gallery's `Builder/` pages already
  render builder code).
- Frame history: record `FrameStats` and per-root progress for N seconds and scrub *history* rather than the live engine.
- Compositor scenes (§5).

### 3.3 Mapping targets to visuals

Most tween targets are the `Visual` itself (transform, effect, clip and mask shorthands all resolve on the visual).
The rest need a bridge resolver, `TargetOwner.Find(object) → Visual?`:

- brushes the color codec created locally on a property: record the owning visual when the brush is created;
- `Transform` objects the `RenderTransformComposer` owns: map back to their visual;
- text pieces (`PieceTargets`, `SplitText`) → the host `TextBlock`, with the piece's rect if available (as built: pieces
  are `Visual`s inside their block's visual tree, so they resolve to themselves, outline their own rect, and a pick on the
  block matches them as descendants);
- anything else (plain objects, view models) → no outline; the row shows the type name.

### 3.4 Hazards to design around

- **Scrubbing revives.** `Seek` on a completed or cancelled timer calls `ReviveTimer`, which re-composes its tweens and can
  override a newer animation on the same property. The inspector allows scrubbing a recently ended root only with an
  explicit "revive" affordance, and shows when doing so would override a live tween.
- **Reverting from the overlay** is destructive to app state; offer it, but not one click from scrub.
- **Allocation.** The list rebuilds from the registry at most every frame and diffs by reference; snapshots allocate, so
  only expanded rows take them.
- **Thread pin.** All reads happen on the UI thread in the overlay's render; a non-UI engine is never touched.

## 4. Breakdown

- **48a — Engine time base (core).** §2.1 incl. the spring rebase fix and the Draggable/Transformable switch. Tests first.
- **48b — Registry and introspection (core).** §2.2, §2.3, ease names. Tests: weak registry drops a collected timer;
  ring buffer bounds; a paused and an unplayed timer are listed; `Children`/`Tweens` enumerate without allocating; snapshot
  values match `Presto.Get`; zero-allocation tick test still passes with diagnostics off and on.
- **48c — Origin tagging (core + bridge).** §2.4; every bridge feature tags its timers; test that a Draggable release and a
  page transition carry their origin.
- **48d — Overlay shell (bridge).** Hosting, transport bar, list, filters. Headless tests: list populates, freeze/step moves
  a timer by one frame, closing restores time.
- **48e — Inspector, tween table, highlighting, springs (bridge).** Headless tests: scrub seeks the root and restores play
  state; outline bounds match the target; pick mode filters.
- **48f — Sample and verify.** Attach in the gallery under `DEBUG`; a short "DevTools" page; walk several gallery pages on
  Desktop with the overlay at 0.25× and frozen/stepped. *As built:* `App.Initialize` registers the same `TopLevel`
  `Loaded` class handler `UsePrestoDevTools` does, so every head is covered without touching their `AppBuilder`s. The
  DevTools page loops on purpose, so it joins the pages that play in full under reduced motion.

### 4.1 What 48f measured and found

- **Cost budget (§2.6).** BenchmarkDotNet, in-process, against `f7331ce`, two alternating rounds: tick of 1k looping
  tweens 30.0/30.5 µs before, 29.7/28.7 µs after (within noise, 0 B both); create + cancel 10k animations 17.6/17.5 ms
  before, 9.4/9.3 ms after, with exactly +8 B per timer. Core and bridge `AllocationTests` pass with diagnostics off and
  forced on, except the two tests whose premise is diagnostics off.
- **Discovery.** Enabling only picks up roots in the engine list, so a paused or unplayed timer, or an `Animatable`
  created before the overlay opened, never appears. The DevTools page's toggle button rebuilds the page after toggling.
- **Re-ended roots.** A revived root, such as an `Animatable`'s property animation on every `Set`, ended twice and
  appeared twice in `RecentlyEnded`, which crashed the list's `ObservableCollection.Move`. `Ended` now moves it to the end.
- **Compositor-played transitions** (the gallery's `PageSlide`) appear as `CompositorScratch` rows that go from pending
  to cancelled, and they ignore freeze and dilation (§5 item 4). The gallery's own crossfade holds while frozen.
- **Follow-ups seen in the walk, all fixed:** the overlay now shows tween values with at most three decimals; the default
  ease (`Eases.Default`, `out(2)`) is named at static init and strings `EaseParser` cached before diagnostics turned on
  are named from their cache key when the first engine enables them; the springs panel's colour `NaN` and the untagged
  `Animatable` `CallbacksTarget` root were fixed in the final review. The gallery's Settings area has a "Presto DevTools"
  toggle button.
- **Open: manual check.** DevTools input cannot send modifier chords, hover or drag, so the `Ctrl+Shift+F12` gesture,
  hover outlines, drag-scrubbing the track, and freeze/dilation during a Draggable or Transformable drag are covered only
  headlessly. Someone has to do them by hand in `samples/PrestoSample.Desktop` before Phase 48 is ticked.

## 5. Open questions

1. **Packaging.** *Decided:* a separate `Monaco.Presto.AvaloniaBridge.DevTools` project (root namespace
   `Monaco.Presto.AvaloniaBridge.DevTools`, added to the solution, referenced by the sample), so a release app does not
   carry the overlay. Its headless tests live in the existing bridge test project. The core parts (§2) stay in the core.
2. **Toggle gesture.** *Decided:* F12 belongs to Avalonia DevTools, so the default is `Ctrl+Shift+F12`, configurable.
3. **Where dilation lives.** Proposed per engine (`Engine.TimeDilation`). A process-wide switch would also reach compositor
   engines but crosses threads; revisit with item 4.
4. **Compositor scenes.** A read-only listing could be marshalled with `CompositorCanvas.Invoke` once per overlay frame;
   time control would need dilation posted to the render thread. Deferred.
5. **Non-Presto motion.** Avalonia `Transitions`/`Animation`s do not obey freeze or dilation; leave them out. *Audited in
   48a:* `FramePlayer` and `LottieView.Play`/`PlaySegment` tween on the main engine, so they obey both;
   `LottieView.PlayOnRenderThread` runs on its render-thread engine and does not (same gap as item 4).
