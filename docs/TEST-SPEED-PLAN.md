# Test speed — virtual time for the headless suites

Phase 66 in [PLAN.md](PLAN.md). The three suites pass but take about four minutes end to end. This plan records where
that time goes (measured on 2026-10-04, Debug, `--report-xunit` per-test times) and sketches the fix. It begins with a
spike, because the main cost sits in an Avalonia clock that Presto does not own.

## 1. Where the time goes

| Suite | Tests | Wall | Notes |
|---|---|---|---|
| `Monaco.Presto.Tests` | 1 279 | 1.5 s | Already on `ManualFrameSource`; not a problem. |
| `Monaco.Presto.AvaloniaBridge.Tests` | 1 366 | 36 s | 1 297 tests average 2.6 ms; 69 tests take 31 s. |
| `PrestoSample.Tests` | 494 | 205 s | 148 tests average 13 ms; 146 tests at ≥ 0.25 s take 172 s. |

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

- `StopwatchFrameSource.Now` and its pump (`Thread.Sleep(1)` loop), and `AvaloniaFrameSource.Now`.
- `LayoutTransition.Prepare`/the prepared-rect lifetime (`Stopwatch.GetTimestamp`).
- `EngineDiagnostics` keeps `Stopwatch`: it measures real cost, not animation time.

This removes the sleeps in `FrameSourceTests` and `AvaloniaFrameSourceTests` and makes the prepared-rect expiry
testable, but on its own saves only a second or two; the compositor clock (§2.1) is where the minutes are.

### 2.3 Test harness

- **One clock for both worlds.** If §2.1 finds a seam, `TestFrames.Tick()` and the bridge's `Frames` advance the
  manual frame source and the compositor clock together by 16 ms, and `Pump`/`Settle` drop `Task.Delay` entirely.
- **Fallbacks if it does not:**
  - `Settle` waits on a condition or on the known animation length (plus a frame), never a fixed 120 rounds.
  - Real-time pumps wait on elapsed `Stopwatch` time rather than counting `Task.Delay(8)` rounds, so a frame is not
    paid twice; consider `Thread.Sleep(1)`/spin waits with `timeBeginPeriod` only if measurements justify it.
  - Tests that need a compositor animation to finish run it at a short duration or through `Engine` speed where the
    behaviour under test does not depend on the duration (confirm the compositor path honours speed, as
    `CompositorPlaybackTests.DoublingTheSpeedCoversTwiceTheDistance` suggests).
- **Parallelism stays off.** Per-test engines (an `AsyncLocal` or per-session `Presto.Engine`) would let the idle waits
  overlap, but they touch every static entry point; revisit only if virtual time is not enough.

### 2.4 Targets and tests

- Sample suite under 60 s and bridge suite under 15 s in Debug on the primary dev machine, with no test weakened:
  every assertion that checks a mid-flight position still checks it at the same animation progress.
- A guard against regressions: a test that fails if a harness pump waits on the wall clock when a virtual clock is
  installed (or, under the fallbacks, a per-class time budget reported in the test output).

## 3. Decisions

None yet; the spike settles §2.1 and records the outcome here and in PLAN §7.
