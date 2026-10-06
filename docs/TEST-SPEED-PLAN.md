# Test speed — virtual time for the headless suites

Phase 66 in [PLAN.md](PLAN.md). The three suites pass but take about four minutes end to end. This plan records where
that time goes (measured on 2026-10-04, Debug, `--report-xunit` per-test times) and sketches the fix. It begins with a
spike, because the main cost sits in an Avalonia clock that Presto does not own.

## 1. Where the time goes

| Suite | Tests | Wall, before (2026-10-04) | Wall, after (2026-10-05) | Target | Notes (before) |
|---|---|---|---|---|---|
| `Monaco.Presto.Tests` | 1 279 → 1 280 | 1.5 s | 1.2 s | none | Already on `ManualFrameSource`; not a problem. |
| `Monaco.Presto.AvaloniaBridge.Tests` | 1 366 → 1 371 | 36 s | 11.6 s | < 15 s, **met** | 1 297 tests average 2.6 ms; 69 tests take 31 s. |
| `PrestoSample.Tests` | 494 → 496 | 205 s | 94.9 s | < 60 s, **not met** | 148 tests average 13 ms; 146 tests at ≥ 0.25 s take 172 s. |

Before is the original estimate (the §3.1 baseline measured 38.1 s and 229.9 s). After is the second of two
back-to-back runs of each `tests/<Project>/bin/Debug/net10.0/<Project>.exe --report-xunit`, all tests passing.

Almost all of it is **real time spent waiting for the compositor**, not CPU work:

- **The compositor has its own clock.** Presto's UI-thread animations run on `ManualFrameSource`, so `TestFrames.Tick()`
  advances them 16 ms instantly. Compositor animations (`RunOnCompositor`, layout and shared-element transitions,
  connected flights, particle and Lottie scenes) are timed by Avalonia's `ServerCompositor`, which sets
  `ServerNow` from an internal `Stopwatch` (`ServerCompositor.Clock`, checked by reflection on 12.1.2). Forcing a render
  tick draws a frame but does not move that clock, so a test has to let wall-clock time pass.
- **The waits are fixed and coarse.** `SceneHarness.Pump`, `CompositorTestBase.Pump` and several hand-written loops do
  `Tick(); await Task.Delay(8)`. On Windows `Task.Delay(8)` takes 15.6 ms (timer resolution; measured, 60-sample average),
  so a frame costs twice what it should.
- **`SceneHarness.Settle()` never exits early.** It is `Pump(() => false, 120)`: always 120 rounds, so 1.9 s per call
  whatever the animation's length. Page tests call it from `Show` and again after each action; e.g.
  `ConnectedXamlPageTests.BackFliesThePhotoHomeIntoTheScrolledList` (5.9 s) is three `Settle` calls.
- **Nothing runs in parallel.** All three assemblies set `DisableTestParallelization`, because `Presto.Engine`,
  `Presto.Defaults` and the test frame source are process-wide statics. That is correct as things stand, and it means
  every real-time wait is paid in series.

Slowest classes (sample): `BoardPageTests` 26.5 s, `GalleryPageTests` 21.3 s, `GalleryFlipPageTests` 21.2 s,
`ActivityFeedPageTests` 17.2 s, `ConnectedXamlPageTests` 16.3 s, `ConnectedNavPageTests` 15.5 s. Slowest classes
(bridge): `CorrectedLayoutTransitionTests`, `ConnectedTests`, `LayoutTransitionTests`, `AllocationTests` (compositor and
particle scenes), 4–5 s each.

`GalleryPageTests` (every page in both motion modes) and `ActivityFeedPageTests` are mostly real work (page
construction, layout and Skia frames); they are not the target here beyond whatever virtual time saves them.

## 2. Design sketch

### 2.1 Spike: can a headless test drive the compositor clock?

Settle this first; everything else depends on it.

- `ServerCompositor` is internal and its `Clock` is a `Stopwatch` read in `UpdateServerTime()`. Look for a supported
  seam in 12.1.x and on Avalonia `master`: a pluggable clock on `Compositor`, a headless option, or a render-loop
  overload that takes a time. Check whether `IRenderTimer.Tick(TimeSpan)` (public, and swappable through
  `AvaloniaLocator` in the test app builder) reaches `ServerNow` anywhere.
- If no seam exists, measure the fallbacks in §2.3 and file an Avalonia issue asking for one (a `TimeProvider` on
  the compositor would be the natural shape). Reflection into `ServerCompositor` is a last resort for tests only, and
  only if it is a single, guarded point that fails loudly when Avalonia changes.
- Output: a decision in §3 and a pinning test that advances a compositor animation by a known amount of virtual time.

### 2.2 `TimeProvider` in the library

The user-facing part, independent of the spike. Every place Presto reads the wall clock takes a `TimeProvider`
(default `TimeProvider.System`), so tests use `FakeTimeProvider` instead of sleeping:

- `StopwatchFrameSource.Now` and `AvaloniaFrameSource.Now`. The `StopwatchFrameSource` pump (its `Thread.Sleep(1)`
  loop) deliberately stays on real time: it is a background thread pacing a real engine, so `FrameSourceTests` keeps
  short real-time waits for it.
- `LayoutTransition.Prepare`/the prepared-rect lifetime (`Stopwatch.GetTimestamp`).
- `EngineDiagnostics` keeps `Stopwatch`: it measures real cost, not animation time.

This removes the sleeps in `FrameSourceTests` and `AvaloniaFrameSourceTests` and makes the prepared-rect expiry
testable, but on its own saves only a second or two; the compositor clock (§2.1) is where the minutes are.

### 2.3 Test harness

- **One clock for both worlds.** If §2.1 finds a seam, `TestFrames.Tick()` and the bridge's `Frames` advance the
  manual frame source and the compositor clock together by 16 ms, and `Pump`/`Settle` drop `Task.Delay` entirely. In the bridge suite
  `Tick` moves only the compositor clock, and tests advance `Frames` explicitly when they need the two in step (§3.3).
- **Fallbacks if it does not:**
  - `Settle` waits on a condition or on the known animation length (plus a frame), never a fixed 120 rounds.
  - Real-time pumps wait on elapsed `Stopwatch` time rather than counting `Task.Delay(8)` rounds, so a frame is not
    paid twice; consider `Thread.Sleep(1)`/spin waits with `timeBeginPeriod` only if measurements justify it.
  - Tests that need a compositor animation to finish run it at a short duration or through `Engine` speed where the
    behavior under test does not depend on the duration (confirm the compositor path honors speed, as
    `CompositorPlaybackTests.DoublingTheSpeedCoversTwiceTheDistance` suggests).
- **Parallelism stays off.** Per-test engines (an `AsyncLocal` or per-session `Presto.Engine`) would let the idle waits
  overlap, but they touch every static entry point; revisit only if virtual time is not enough.

### 2.4 Targets and tests

- Sample suite under 60 s and bridge suite under 15 s in Debug on the primary dev machine, with no test weakened:
  every assertion that checks a mid-flight position still checks it at the same animation progress.
- A guard against regressions: a test that fails if a harness pump waits on the wall clock when a virtual clock is
  installed (or, under the fallbacks, a per-class time budget reported in the test output).

## 3. Decisions

### 3.1 Baseline (2026-10-05)

Debug, `dotnet build Monaco.Presto.slnx`, then each test exe run directly with `--report-xunit`; wall time is the whole
exe run.

| Suite | Tests | Wall | Sum of per-test times |
|---|---|---|---|
| `Monaco.Presto.Tests` | 1 279 | 1.6 s | 1.1 s |
| `Monaco.Presto.AvaloniaBridge.Tests` | 1 366 | 38.1 s | 36.0 s |
| `PrestoSample.Tests` | 494 | 229.9 s | 226.9 s |

Slowest bridge classes: `CorrectedLayoutTransitionTests` 5.1 s (10 tests), `AllocationTests` 4.9 s (44),
`ConnectedTests` 4.3 s (19), `LayoutTransitionTests` 4.2 s (33), `CompositorPlaybackTests` 2.1 s (8),
`CompositorRegistryTests` 2.0 s (10), `CompositorAnimationTests` 2.0 s (16), `LottieRenderThreadTests` 1.6 s (5).
Slowest bridge tests, about 1.1–1.3 s each: `AllocationTests.AParticleSceneOf500DrawsWithoutAllocating` and
`CompositorSceneFramesWithinBudget` (both cases each), `ConnectedTests.DefaultMotionAppliesWhenNeitherElementSetsOne` and
`TheArrivingElementsMotionSetsTheFlight`, `CompositorRenderTests.AnAnimatingSceneRedrawsItselfEveryFrame`,
`CorrectedLayoutTransitionTests.AGrowInterruptedMidFlightKeepsTheCorrection`. The next nine are 0.8 s layout-transition,
alignment and particle tests.

Slowest sample classes: `BoardPageTests` 26.9 s (9), `ActivityFeedPageTests` 25.3 s (26), `GalleryPageTests` 22.5 s (140),
`GalleryFlipPageTests` 21.8 s (6), `ConnectedXamlPageTests` 16.5 s (6), `ConnectedNavPageTests` 16.2 s (4),
`ReferencePagesTests` 15.4 s (42), `ConnectedPageTests` 10.1 s (3). Slowest sample tests:
`ConnectedNavPageTests.RapidTogglesLeaveThePhotoDrawnInItsCard` 6.3 s,
`ConnectedXamlPageTests.BackFliesThePhotoHomeIntoTheScrolledList` 6.0 s,
`GalleryFlipPageTests.TheLayoutSwitchEasesACardsWidthAndTheSortEasesItsPosition` 5.7 s,
`AnExpandReversedMidFlightPutsEveryCardBackInItsSlot` 5.2 s, `ACardDroppedMidReflowLeavesEveryOtherOneInItsSlot` 4.8 s,
`BoardPageTests.ACardBinnedWhileTheBoardIsStillReflowingLeavesNoHole` 4.7 s, then nine more at 3.9–4.2 s (board flings,
activity-feed reveal, connected toggles and flights).

### 3.2 The compositor clock (decision 79)

**There is no supported seam; tests freeze and advance the compositor's `Stopwatch` through one guarded reflection
helper.** Evidence, from Avalonia.Base 12.1.2 decompiled with `ilspycmd` and from `master` on GitHub (unchanged there):

- Every compositor time read comes from one object. `ServerCompositor.Clock` is a public get-only `Stopwatch`
  (`Stopwatch.StartNew()` in the initializer). `UpdateServerTime()` sets `ServerNow = Clock.Elapsed` at the top of each
  `RenderCore`. `Compositor.CommitCore` stamps each batch's `CommittedAt` from `Server.Clock.Elapsed`, and
  `KeyFrameAnimationInstance` evaluates `ServerNow - _startedAt`. `CompositionCustomVisualHandler.CompositionNow`, which
  `CompositorSceneHandler.Now` reads, returns `ServerNow`.
- `IRenderTimer.Tick(TimeSpan)` does not reach it: `DefaultRenderLoop.TimerTick(TimeSpan time)` ignores `time` and
  calls each `IRenderLoopTask.Render()`, which takes no time. No `TimeProvider`, clock option or time-taking render
  overload exists on `Compositor`, `ServerCompositor`, `CompositionOptions` or `AvaloniaHeadlessPlatformOptions`, and
  `Compositor.TryGetDefaultCompositor()` returns null under headless because the compositor is not in the locator.
- Headless isolation is `PerTest` (the default; no `AvaloniaTestIsolation` attribute here), so each test starts with a
  fresh platform, `Compositor` and `Stopwatch`. The clock has to be installed in every test, and nothing needs undoing
  afterwards.

`CompositorClock` (bridge tests, self-contained so the sample tests link the same file) reaches the stopwatch through
`AvaloniaHeadlessPlatform.Compositor` (internal static), `Compositor.Server` (internal) and `ServerCompositor.Clock`.
`Install()` calls the public `Stopwatch.Stop()`. `Advance(ms)` adds to the stopped stopwatch's private `_elapsed`, the
only member outside Avalonia it touches. Each lookup throws an `InvalidOperationException` naming the missing member,
and `Advance` throws when the current test's clock is still running. Pinned by `CompositorClockTests`: a linear 1000 ms
`AnimateOnCompositor` X move renders at exactly 30, 45, 60 and 90 px after 0, 250, 500 and 1500 ms of virtual time, with
no `Task.Delay`, in about 0.2 s (mostly the first window's JIT and Skia start-up).

Fragility: four private names, three in Avalonia and one in the BCL (`Stopwatch._elapsed`, stable across .NET Core but
not a contract). An Avalonia or .NET upgrade that renames any of them fails `CompositorClockTests` and every test that
installs the clock, with a message naming the member. The §2.3 fallbacks were not measured, since this seam exists.

Draft Avalonia issue (not filed):

> **Allow headless tests to drive the compositor clock.** `ServerCompositor` reads `ServerNow` (and stamps
> `CompositionBatch.CommittedAt`) from a private `Stopwatch`, and `DefaultRenderLoop.TimerTick` discards the `TimeSpan`
> its `IRenderTimer` passes. `AvaloniaHeadlessPlatform.ForceRenderTimerTick()` therefore renders a frame without moving
> composition animations, so tests of `KeyFrameAnimation`s, implicit animations or `CompositionCustomVisualHandler`
> scenes have to sleep in real time. Proposal: a `TimeProvider` on `CompositionOptions` (default `TimeProvider.System`),
> read by `ServerCompositor` in place of `Stopwatch`, and an `AvaloniaHeadlessPlatformOptions.TimeProvider` that passes
> it through, so `FakeTimeProvider.Advance` plus `ForceRenderTimerTick` steps animations deterministically.

### 3.3 Bridge harness (2026-10-05)

`CompositorTestBase` installs `CompositorClock` in its constructor and forgets it in `Dispose`. `Tick(ms = 0)` runs the
UI jobs, moves the compositor clock on by `ms` and renders; `Pump` and `TickFor` tick a 16 ms `Frame` at a time, with no
`Task.Delay`. Four things the sample harness will meet too:

- **`Tick` does not move the engine.** Advancing `Frames` with every tick broke 35 tests whose UI-thread values assume
  `Tick` only renders, so the manual frame source still moves only on `Frames.Advance`; a test that needs the mirror and
  the render thread in step advances both (`CompositorPlaybackTests.Run`).
- **No frame lands on its commit.** `KeyFrameAnimationInstance` evaluates exactly zero elapsed as the last key frame, so a
  frame rendered at the instant of its commit shows a just-started move as finished. `Tick` adds 1 µs to every advance.
- **The clock is read without reflection per frame.** `Install` caches the stopwatch and `Advance` moves it through an
  `UnsafeAccessor`; three `PropertyInfo.GetValue` calls per frame made `AllocationTests`' particle median fail 6 of 6
  isolated runs (0 of 6 cached).
- **Avalonia's own animations are still real time.** `MediaContext` pulses `Animation`s (`CrossFade`, transitions) from
  a private `Stopwatch` of its own, so `PresencePageTransitionTests.APageRootsExitLeavesNoGhostAfterACrossFade` keeps a
  real-time wait (about 0.1 s).

Bridge suite, Debug, `--report-xunit`: 36.0 s wall (34.6 s of tests) before, 9.3–10.6 s wall (7.6–8.4 s) after.
`CorrectedLayoutTransitionTests` 5.1 → 0.04 s, `ConnectedTests` 4.4 → 0.08 s, `LayoutTransitionTests` 4.2 → 0.08 s,
`AllocationTests` 4.4 → 2.5 s (the rest is rendering 500 particles for 5 000 frames). The bridge `Pump` is a plain loop
with no awaits, so the §2.4 wall-clock guard lives in `SceneHarnessTests` only.

### 3.4 Sample harness (2026-10-05)

`TestFrames.Install()` installs `CompositorClock` (the bridge file, linked into the sample test project), and `Tick()` moves the manual frame source and the
compositor clock 16 ms together. `SceneHarness.Pump` no longer waits, and `Settle` ticks once, then until
`Presto.Engine` stops running and the compositor's `ServerCompositorAnimations.NeedNextTick` is false, capped at its
`rounds` (120). Because `Presto.Engine` is process-wide, loops an earlier test's page left running kept it busy for
good, so `Install` drops the engine's children (internal `DropChildren`, through an `UnsafeAccessor`) once per headless
session. Guards in `SceneHarnessTests`: a 120-round `Pump` stays within 500 ms of real time, and `Settle` returns once
a 300 ms composition fade ends.

Sample suite, Debug, `--report-xunit`: 219.7 s wall (216.6 s of tests) before, 127.7 s (125.5 s) after.
`BoardPageTests` 26.7 → 1.2 s, `GalleryFlipPageTests` 21.2 → 3.3 s, `ConnectedXamlPageTests` 16.6 → 1.2 s,
`ConnectedNavPageTests` 15.8 → 1.9 s, `ConnectedPageTests` 9.7 → 2.0 s. Now slowest: `GalleryPageTests` 20.2 s,
`ActivityFeedPageTests` 18.4 s, `ReferencePagesTests` 13.0 s, `BuilderRecipePagesTests` 8.4 s, `StorefrontPageTests`
7.3 s. Two page tests that relied on real time passing now fail, left for the page items:
`CompositorPageTests.AStaleHeartbeatShowsTheUIThreadFrozenAndAFreshOneClearsIt` (`OrbitScene` times the heartbeat with
the wall-clock `Stopwatch`) and `ReferencePagesTests.EveryEffectsStageRunsAndClosingLeavesNoBridgeOwnedEffectClipOrMask`
(`Items.Count` is 4, not 3, after its 20 frames).

### 3.5 Sample pages and the 60 s target (2026-10-05)

What is left is real work, not waiting. A sampled profile of the test thread (`dotnet-trace`, thread time) put about
70% of it in the headless window's framebuffer: `HeadlessWindowImpl.Lock` allocates, clears and frees a new
window-sized `WriteableBitmap` for every rendered frame, and the compositor redraws into a layer and blits all of it,
because the framebuffer keeps nothing between frames. With the default `Rgba8888` framebuffer, Skia also converted
every frame from its native `Bgra8888`. The suite renders about 50 000 frames, 17 000 of them in `ActivityFeedPageTests`
(each live event is 4 s, or 250 frames), and 8 300 in `GalleryPageTests` (61 per page and mode). JIT, GC and tiered
compilation settings made no measurable difference.

- `TestAppBuilder` sets `FrameBufferFormat = Bgra8888`, and `SceneHarness.Capture` swaps the bytes back to RGBA, so
  every pixel assertion reads the same values. This took the suite from 125 s to 93 s.
- Three tests depended on real time passing and now wait for it explicitly: the `OrbitScene` freeze detector compares
  wall-clock heartbeats (a 300 ms delay), the stress bench's worker engine runs on a `StopwatchFrameSource` (it ticks
  until the worker reports, up to 5 s), and the effects page's remove button is clicked once hit testing finds it in
  the slot the stages above pushed it to.
- `ParticleFieldLoadTests` still measures real construction time.
- A framebuffer the window keeps between frames would remove the allocation and the full blit. It cannot be built from
  the test project: `IWindowingPlatform` and `IPlatformRenderSurface` are `[PrivateApi]` and cannot be implemented
  outside Avalonia.

Sample suite, Debug, `--report-xunit`, three runs in a row, all 496 passing: 101.8, 92.4 and 91.8 s wall (98.3, 90.6
and 90.1 s of tests), down from 128.3 s (125.3 s). **The 60 s target is not met.** Slowest classes (third run):
`GalleryPageTests` 15.5 s (140 tests), `ActivityFeedPageTests` 11.7 s (26), `ReferencePagesTests` 10.9 s (42),
`BuilderRecipePagesTests` 5.7 s (32), `StorefrontPageTests` 4.7 s (9), `GalleryFlipPageTests` 3.3 s (6),
`TypographyPageTests` 3.0 s (7), `MicroInteractionsPageTests` 2.7 s (10), `NavMotionTests` 2.3 s (10),
`GalleryCodeTests` 2.0 s (3). Reaching 60 s would take fewer frames or smaller windows, which changes what the tests
exercise, or a retained headless framebuffer in Avalonia itself.

### 3.6 Outcome (2026-10-05)

The §2.1 seam, not the §2.3 fallbacks (§3.3 bridge, §3.4 sample); Presto's own wall-clock reads take a `TimeProvider`
(§2.2), except the `StopwatchFrameSource` pump, which stays on real time. Before and after timings are in §1.
The sample suite's 60 s target is not met (§3.5) and was accepted as missed when the phase closed.

The faster bridge runs surfaced an order-dependent failure: a plain `[Fact]` in a `BridgeTestBase` subclass
(`DotLottieTests`, `FollowerTests`, `LottieSegmentTests`) reset the engine off the UI thread and canceled an earlier
test's leftover exit ghost from the wrong thread. Those tests are now `[AvaloniaFact]`, and `BridgeTestBase` asserts it
runs on the UI thread, so a new plain `[Fact]` there fails every time instead of now and then.

Guard tests: `CompositorClockTests` pins the seam (`AdvancingTheClockMovesACompositorAnimationByExactlyThatProgress`,
`AdvancingWithoutInstallingFails`, `AMissingSeamFailsLoudly`); `SceneHarnessTests` holds the §2.4 wall-clock guard,
checking that the sample `Pump` does not wait (`PumpingNeverWaitsOnTheWallClock`), and checks that `Settle` exits early (`SettleReturnsOnceTheLastAnimationEnds`).

Still costly, for a future phase:

- **Rendering in the sample suite:** a framebuffer kept between frames needs a change in Avalonia (§3.5).
- **`AllocationTests`** (3.6 s of the bridge's 9.1 s) renders 500 particles for thousands of frames by design.
- **Parallelism.** Every suite still runs in series because `Presto.Engine`, `Presto.Defaults` and the test frame source
  are process-wide. Per-test engines (an `AsyncLocal` or per-session `Presto.Engine`) would let the sample suite's
  rendering use more than one core; it touches every static entry point.