---
id: CORE-ENGINE
title: Engine, clock and frame sources
package: Monaco.Presto
namespaces:
  - Monaco.Presto
  - Monaco.Presto.Core
sources:
  - Monaco.Presto/Engine.cs
  - Monaco.Presto/Core/Clock.cs
  - Monaco.Presto/Core/Globals.cs
  - Monaco.Presto/Core/Constants.cs
  - Monaco.Presto/Core/TickMode.cs
  - Monaco.Presto/IFrameSource.cs
  - Monaco.Presto/ManualFrameSource.cs
  - Monaco.Presto/StopwatchFrameSource.cs
  - Monaco.Presto/TimeUnit.cs
types:
  - Engine
  - Clock
  - IFrameSource
  - ManualFrameSource
  - StopwatchFrameSource
  - TimeUnit
tests:
  - EngineTests
  - EngineTimeTests
  - EngineHiddenTests
  - ClockTests
  - FrameSourceTests
  - ParametersTests
  - LeaksTests
upstream:
  - src/engine/engine.js
  - src/core/clock.js
  - tests/suites/engine.test.js
  - tests/suites/leaks.test.js
depends:
  - CORE-TIMER
  - CORE-THREADING
  - CORE-MOTIONVALUE
  - CORE-COMPOSITION
  - CORE-DIAGNOSTICS
  - CORE-SCOPE
---

# Engine, clock and frame sources

## Purpose

The engine is the root clock: it owns the linked list of running root timers and ticks them from one `IFrameSource`, the stand-in for upstream's `requestAnimationFrame`/`Date.now()`. `Clock` is the frame-rate and playback-rate base it shares with `Timer` (CORE-TIMER). This spec covers the frame pump, engine time (pause, freeze, dilation, step, hidden), the engine-wide rates and units, and the two frame sources the core ships; which engine a call reaches is CORE-THREADING. Also specifies `Presto.Engine` and `Presto.Defaults`.

## Requirements

### Construction and engine time

- **CORE-ENGINE-001** — When an engine is constructed, it shall start with `Defaults` set to an independent copy of `Presto.Defaults` as they are at that moment.
  - Tests: `EnginesTests.DefaultsStartAsACopyOfTheMainEnginesAndThenDiverge`
- **CORE-ENGINE-002** — The main engine, `Presto.Engine`, shall be driven by a `StopwatchFrameSource` and start from a fresh `PrestoDefaults`.
  - Tests: untested
- **CORE-ENGINE-003** — The engine shall report `Now` as the frame source's `Now` times `TimeDilation` (times 0 while frozen) plus an offset that keeps `Now` continuous across every change of rate.
  - Tests: `EngineTimeTests.AFrozenEngineIgnoresTheFrameSource`, `EngineTimeTests.HalfDilationHalvesTimerAndSpringProgress`, `EngineTimeTests.ThawingDoesNotJump`
- **CORE-ENGINE-004** — When `FrameSource` is replaced, the engine shall keep `Now` continuous and, if the pump was running, stop the old source and start the new one.
  - Tests: untested

### Frame pump

- **CORE-ENGINE-005** — When `Wake` is called (every timer `Resume` calls it), the engine shall start its frame source unless it is already running, `Frozen`, `Hidden` with `PauseOnHidden` on, or `UseDefaultMainLoop` is false.
  - Tests: `EngineTests.SetUseDefaultMainLoopToFalseShouldPreventTimersFromRunning`, `EngineHiddenTests.ATimerCreatedWhileHiddenWaitsForTheShow`, `EngineTimeTests.ATimerCreatedWhileFrozenStartsFromFrozenTime`
- **CORE-ENGINE-006** — When `Wake` starts the frame source, the engine shall first request a tick at `Now`, so a timer created before the first frame measures its offset from an up-to-date tick time.
  - Tests: `EngineTests.InitialOffsetPositionShouldBeProperlyCalculatedOnColdStart`
- **CORE-ENGINE-007** — When a frame arrives while the engine has a running child, a queued `MotionValue` sink or a moving `MotionValue`, the engine shall run `Update`; otherwise it shall stop the frame source and report `IsRunning` false.
  - Tests: `LeaksTests.EngineShouldNotContainAnyActiveTickable`, `MotionValueTests.SettingASourceWakesTheEngineUntilTheFlush`, `MotionValueDynamicsTests.ASpringSettlesExactlyOnItsTargetAndTheEngineSleeps`
- **CORE-ENGINE-008** — While `Frozen`, a frame from the frame source shall do nothing.
  - Tests: `EngineTimeTests.AFrozenEngineIgnoresTheFrameSource`
- **CORE-ENGINE-009** — While `UseDefaultMainLoop` is false, the engine shall never start its frame source, and each `Update()` call shall tick it once at the frame source's current time.
  - Tests: `EngineTests.SetUseDefaultMainLoopToFalseShouldPreventTimersFromRunning`, `EngineTests.ManuallyTickTheEngineWithAnExternalLoop`

### Update

- **CORE-ENGINE-010** — When less than the engine's frame duration (`1000 / Fps`) minus a tolerance of `min(frameDuration / 4, 4 ms)` has elapsed since its last tick, `Update` shall do nothing.
  - Tests: `ParametersTests.EngineFpsThrottlesUpdates`, `ClockTests.RequestTickReturnsNoneWhenFrameArrivesTooEarly`, `ClockTests.RequestTickReturnsAutoWithinTolerance`
- **CORE-ENGINE-011** — When a tick is accepted, the engine shall align its last tick time down to its frame grid and set `DeltaTime` to the time since its previous tick.
  - Tests: `ClockTests.RequestTickAlignsLastTickTimeToFrameGrid`, `ClockTests.ComputeDeltaTimeSetsDeltaTimeAndAdvancesLastTime`
- **CORE-ENGINE-012** — On each tick, the engine shall tick every unpaused child in list order at `(Now − child start time) × child speed × engine speed`.
  - Tests: `ParametersTests.PlaybackRateOnTimer`, `ParametersTests.EngineSpeedDoublesTimerPlayback`
- **CORE-ENGINE-013** — The engine's child list shall be ordered by ascending `Priority`, a timer of equal priority going after those already linked.
  - Tests: `HelpersTests.AddChildInsertsByPriorityWhenSorted`
- **CORE-ENGINE-014** — When a child's `Fps` is below the engine's, the engine shall tick that child only on its own frame grid.
  - Tests: `ParametersTests.FrameRateThrottlesUpdates`
- **CORE-ENGINE-015** — When `Update` reaches a paused child, it shall unlink it; when that child is also completed and not canceled, the engine shall detach its tweens and mark it canceled without firing `OnPause`.
  - Tests: `LeaksTests.EngineShouldNotContainAnyActiveTickable`, `CallbacksTests.OnPauseDoesNotFireWhenATimerCompletesOnItsOwn`
- **CORE-ENGINE-016** — When a callback run by `Update` unlinks another child, the children still linked shall all tick in that same frame.
  - Tests: `EngineTests.RemovingATargetFromACallbackKeepsTheOtherChildrenTicking`
- **CORE-ENGINE-017** — After the children, `Update` shall update the engine's additive animation, then step its moving `MotionValue`s, then flush the queued sinks.
  - Tests: untested
- **CORE-ENGINE-018** — When a sink is queued while the engine flushes sinks, it shall flush on the next frame.
  - Tests: `MotionValueTests.ASetFromAHandlerFlushesOnTheNextFrame`

### Pause and resume

- **CORE-ENGINE-019** — When `Pause` is called while the frame source is running, the engine shall stop it, set `Paused`, and record the engine time it stopped at, leaving every timer where it stands.
  - Tests: `EngineTests.PauseAndResumeTheEngine`
- **CORE-ENGINE-020** — When `Pause` is called while the frame source is idle, the engine shall change nothing.
  - Tests: untested
- **CORE-ENGINE-021** — `Paused` shall read true from construction until the first `Resume`.
  - Tests: untested
- **CORE-ENGINE-022** — When `Resume` is called while `Paused`, the engine shall clear `Paused`, rebase every child's start time so the paused interval does not count as elapsed, shift each moving `MotionValue` by the paused interval, and then `Wake`.
  - Tests: `EngineTests.PauseAndResumeTheEngine`, `EngineTimeTests.ASpringSurvivesPauseAndResumeWithoutSnappingToRest`
- **CORE-ENGINE-023** — When `Resume` is called while not `Paused`, the engine shall do nothing.
  - Tests: untested
- **CORE-ENGINE-024** — When a timer resumes after `Pause`, the engine's `Wake` shall restart the frame source while `Paused` stays true and no child is rebased.
  - Tests: untested

### Speed, time unit and precision

- **CORE-ENGINE-025** — The engine's `Speed` shall multiply the playback rate of every child, and setting it shall rebase the running children so they continue from their current time.
  - Tests: `ParametersTests.EngineSpeedDoublesTimerPlayback`, `ParametersTests.ChangingEngineSpeedContinuesRunningTimersFromTheirCurrentTime`
- **CORE-ENGINE-026** — When `TimeUnit` is set to `Seconds`, every duration, delay and time value in the API shall be read in seconds: the numeric default duration and the engine's internal speed are multiplied by 0.001 and the forced-render threshold becomes 0.2, while `Speed` still reads 1; setting `Milliseconds` reverses it.
  - Tests: `EngineTests.ChangingTheTimeUnitShouldAffectDurationValues`, `EngineTests.EngineSpeedReads1InSecondsMode`, `SecondsTests.ATimerPlaysInSeconds`
- **CORE-ENGINE-027** — When `TimeUnit` is set to the unit already in force, the engine shall change nothing.
  - Tests: untested
- **CORE-ENGINE-028** (inherited) — When the default duration is function-based, switching `TimeUnit` shall leave it unscaled.
  - Tests: untested
- **CORE-ENGINE-029** — `Precision` (default 4) shall set the decimals that time getters report and that the in-between frames of string values are rounded to.
  - Tests: `EngineTests.DefaultPrecisionShouldBe4`, `EngineTests.ChangingPrecisionAffectsReportedTime`, `EngineTests.PrecisionRoundsOnlyTheInBetweenFrames`

### Freeze, dilation and step

- **CORE-ENGINE-030** — When `Freeze` is called, the engine shall hold `Now` still and stop its frame source.
  - Tests: `EngineTimeTests.AFrozenEngineIgnoresTheFrameSource`
- **CORE-ENGINE-031** — `Freeze` shall not set `Paused`, pause any timer or fire `OnPause`.
  - Tests: `EngineTimeTests.FreezingNeitherPausesNorFiresOnPause`
- **CORE-ENGINE-032** — When `Thaw` is called while frozen, engine time shall run again from where it was held, and the engine shall `Wake` unless `Pause` stopped it.
  - Tests: `EngineTimeTests.ThawingDoesNotJump`, `EngineHiddenTests.ThawingWhileHiddenDoesNotWake`, `EngineHiddenTests.ThawingAfterAFrozenShowWakes`
- **CORE-ENGINE-033** — `TimeDilation` shall scale how fast engine time advances for everything on the engine, timers and springs alike, in either time unit.
  - Tests: `EngineTimeTests.HalfDilationHalvesTimerAndSpringProgress`, `EngineTimeTests.DilationAppliesInSecondsMode`
- **CORE-ENGINE-034** — When `TimeDilation` is set to zero or a negative value, the engine shall throw `ArgumentOutOfRangeException`.
  - Tests: `EngineTimeTests.TimeDilationMustBePositive`
- **CORE-ENGINE-035** (inherited) — When `TimeDilation` is set while frozen, the new rate shall take effect at `Thaw`.
  - Tests: untested
- **CORE-ENGINE-036** — When `Step(ms)` is called, the engine shall advance engine time by `ms`, undilated, and run one `Update`, whether frozen or not.
  - Tests: `EngineTimeTests.StepMovesATimerAndASpringByExactlyOneFrame`, `EngineTimeTests.ATimerCreatedWhileFrozenStartsFromFrozenTime`
- **CORE-ENGINE-037** — When a timer is created while the engine is frozen, it shall start from the frozen engine time.
  - Tests: `EngineTimeTests.ATimerCreatedWhileFrozenStartsFromFrozenTime`

### Hidden

- **CORE-ENGINE-038** — When `Hidden` becomes true while `PauseOnHidden` is on, the engine shall `Pause` and set `Paused` even if the pump was idle.
  - Tests: `EngineHiddenTests.HidingPausesAndShowingResumesWithoutAJump`, `EngineHiddenTests.ATimerCreatedWhileHiddenWaitsForTheShow`
- **CORE-ENGINE-039** — When `Hidden` becomes false while `PauseOnHidden` is on, the engine shall `Resume`.
  - Tests: `EngineHiddenTests.HidingPausesAndShowingResumesWithoutAJump`, `EngineHiddenTests.AnAnimationStartedBeforeHidingCompletesAfterShowing`
- **CORE-ENGINE-040** — When the engine was paused by hand before it was hidden, showing it shall still `Resume` it.
  - Tests: untested
- **CORE-ENGINE-041** — While `Hidden` with `PauseOnHidden` on, `Wake` shall not start the frame source, so a timer created or resumed then waits for the show.
  - Tests: `EngineHiddenTests.ATimerCreatedWhileHiddenWaitsForTheShow`
- **CORE-ENGINE-042** — While `PauseOnHidden` is off, setting `Hidden` shall not affect playback.
  - Tests: `EngineHiddenTests.HiddenIsANoOpWithoutPauseOnHidden`
- **CORE-ENGINE-043** (inherited) — When `PauseOnHidden` is turned off while hidden, the engine shall resume.
  - Tests: `EngineHiddenTests.TurningPauseOnHiddenOffWhileHiddenResumes`
- **CORE-ENGINE-044** (inherited) — When `PauseOnHidden` is turned on while hidden, the engine shall pause.
  - Tests: untested
- **CORE-ENGINE-045** — When the engine is shown while frozen, it shall stay stopped until `Thaw`.
  - Tests: `EngineHiddenTests.ThawingAfterAFrozenShowWakes`
- **CORE-ENGINE-046** — When the engine is shown, timers and moving values shall carry on without counting the hidden interval, in either time unit.
  - Tests: `EngineHiddenTests.HidingPausesAndShowingResumesWithoutAJump`, `EngineHiddenTests.AMovingValueResumesWithoutAJump`, `EngineHiddenTests.HidingDoesNotJumpInSecondsMode`
- **CORE-ENGINE-047** (inherited) — When `Hidden` or `PauseOnHidden` is set to its current value, the engine shall change nothing.
  - Tests: untested

### Clock

- **CORE-ENGINE-048** — When `Fps` is set to zero or less, the clock shall run uncapped at 240 fps.
  - Tests: `ClockTests.FpsZeroOrNegativeIsUncapped`, `ParametersTests.FrameRateZeroOrNegativeRendersEveryFrame`
- **CORE-ENGINE-049** — When `Fps` is set above its engine's `Defaults.FrameRate`, the clock shall raise that default to the new rate.
  - Tests: `ClockTests.FpsHigherThanDefaultFrameRateRaisesDefault`
- **CORE-ENGINE-050** — When `Clock.Speed` is set below 1e-11, the clock shall store 1e-11, so it never stops or runs backwards.
  - Tests: `ClockTests.SpeedClampsToMinValue`
- **CORE-ENGINE-051** — The engine's `Speed` setter shall store any value without clamping.
  - Tests: untested

### Frame sources

- **CORE-ENGINE-052** — `ManualFrameSource.Advance(ms)` shall add `ms` to `Now` and, while started, invoke the frame callback once.
  - Tests: `FrameSourceTests.ManualAdvanceBumpsNowWithoutRunningBeforeStart`, `FrameSourceTests.ManualAdvanceInvokesCallbackOncePerAdvanceAfterStart`
- **CORE-ENGINE-053** (inherited) — `ManualFrameSource.Advance(ms, step)` shall advance in `step`-sized frames, the last one partial, landing exactly on `Now + ms`.
  - Tests: `FrameSourceTests.ManualSteppedAdvanceInvokesPerFrameAndLandsOnTarget`
- **CORE-ENGINE-054** (inherited) — When `Advance(ms, step)` is given a `step` of zero or less, it shall throw `ArgumentOutOfRangeException`.
  - Tests: untested
- **CORE-ENGINE-055** (inherited) — When `Start` is called on a started frame source, it shall keep the first callback.
  - Tests: `FrameSourceTests.ManualStartTwiceKeepsFirstCallback`
- **CORE-ENGINE-056** (inherited) — When `Stop` is called from inside the frame callback, the frame source shall deliver no further frames, and a stepped `Advance` shall still land on its target time.
  - Tests: `FrameSourceTests.ManualStopFromInsideCallbackPreventsFurtherInvocations`
- **CORE-ENGINE-057** — `StopwatchFrameSource.Now` shall be the milliseconds its `TimeProvider` reports as elapsed since the source was constructed.
  - Tests: `FrameSourceTests.StopwatchNowFollowsTheTimeProvider`
- **CORE-ENGINE-058** — When the thread calling `StopwatchFrameSource.Start` has no `SynchronizationContext`, the source shall invoke the callback about every millisecond on one background thread until `Stop`, which waits for that thread unless called from it.
  - Tests: `FrameSourceTests.StopwatchWithoutASynchronizationContextPumpsOnOneBackgroundThreadUntilStopped`
- **CORE-ENGINE-059** (inherited) — When the starting thread has a `SynchronizationContext`, the source shall post each frame through it with at most one frame in flight, and a frame posted before `Stop` shall not run after it.
  - Tests: `FrameSourceTests.StopwatchPostsFramesThroughTheStartingThreadsSynchronizationContext`
- **CORE-ENGINE-060** (inherited) — When `Stop` and then `Start` are called from inside a `StopwatchFrameSource` callback, the old pump loop shall retire instead of running beside the new one.
  - Tests: untested

### Defaults

- **CORE-ENGINE-061** — When `UseDefaultMainLoop` is not set, the engine shall run its own frame pump (true), as anime.js does.
  - Tests: untested
- **CORE-ENGINE-062** — When `PauseOnHidden` is not set, the engine shall pause while `Hidden` (true), as anime.js's `pauseOnDocumentHidden` does.
  - Tests: `EngineHiddenTests.HidingPausesAndShowingResumesWithoutAJump`
- **CORE-ENGINE-063** (inherited) — When `Hidden` has not been set by the host, the engine shall treat itself as shown (false).
  - Tests: untested
- **CORE-ENGINE-064** — When `TimeDilation` is not set, engine time shall advance at the frame source's rate (1).
  - Tests: untested
- **CORE-ENGINE-065** — When `Step` is called without an argument, the engine shall advance by one 60 fps frame (`1000 / 60` ms).
  - Tests: untested
- **CORE-ENGINE-066** — When `Speed` is not set, the engine and every `Clock` shall run at a playback rate of 1, as anime.js does.
  - Tests: `EnginesTests.AnEngineCreatedInSecondsModeRunsAtTheSameSpeedAsTheMainOne`
- **CORE-ENGINE-067** — When `TimeUnit` has not been set, time values shall be read in milliseconds, as anime.js does.
  - Tests: untested
- **CORE-ENGINE-068** — When `Fps` is not set, a clock shall render at up to 240 fps, as anime.js does.
  - Tests: untested
- **CORE-ENGINE-069** (inherited) — A new `ManualFrameSource` shall start with `Now` at 0.
  - Tests: `FrameSourceTests.ManualAdvanceBumpsNowWithoutRunningBeforeStart`
- **CORE-ENGINE-070** — When `StopwatchFrameSource` is constructed without a `TimeProvider`, it shall read time from `TimeProvider.System`.
  - Tests: untested

## Invariants

- `Now` never jumps when dilation, freeze, thaw or the frame source changes; only `Step` moves it discontinuously, by exactly its argument.
- The child list holds only timers whose `IsScheduled` is true, ordered by ascending priority.
- `IsRunning` is true exactly while the engine has started its frame source and not stopped it.
- `Frozen` is equivalent to an engine time rate of 0.

## Edge cases

- `Step` by less than the frame duration minus its tolerance (about 3.2 ms at 240 fps) advances engine time but `Update` skips the tick; the next accepted tick catches up.
- `Engine.Speed` of 0 or a negative value is stored as given and makes rebasing divide by zero (upstream parity).
- `Pause` on an idle engine does not stop the next `Wake`, so a timer created afterwards plays.
- A function-based default duration is not rescaled by `TimeUnit`.
- `Update()` may be called while the pump runs; it ticks only if the frame grid allows, so it does not double-advance timers.

## Threading and lifetime

- An engine and its timers are single-threaded; see CORE-THREADING for the thread pin, `Engine.Current` and the process-wide statics (`TimeUnit`, `Precision`, the tick threshold).
- `StopwatchFrameSource` with no `SynchronizationContext` runs the callback on its pump thread, so the engine may then only be touched from inside frame callbacks.
- The engine holds its running children strongly; a paused child leaves the list on the next tick and is held only by its owner.

## Reduced motion

- Not affected: the engine plays whatever timers it is given; reduced forms are chosen when animations are built (CORE-REDUCED).

## Allocation

- `Update`, `OnFrame`, value stepping and sink flushing allocate nothing per frame: linked lists, swapped sink lists and static lambdas only.
- `StopwatchFrameSource` allocates a thread and a pump object per `Start`, not per frame.
- Pinned by `AllocationTests.LoopingTimerTicksWithoutAllocating` and `AllocationTests.MotionValueGraphFlushesWithoutAllocating`.

## Deviations from anime.js

- Engines are instances with a public constructor, one per ticking thread, instead of a module singleton, because a thread that ticks its own timers needs its own clock without locks (CORE-THREADING).
- The pump is an `IFrameSource` instead of `requestAnimationFrame`/`setImmediate`, because the core has no host loop of its own; it keeps running while `MotionValue` sinks or moving values remain, because those are not tickables in the child list (CORE-MOTIONVALUE).
- `Now`, `TimeDilation`, `Freeze`, `Thaw` and `Step` have no upstream counterpart; see "Each engine owns its time base" and "Freeze, Thaw and Step are separate from Pause and Resume". `Resume` also shifts moving values by the paused interval, so a spring does not take the whole pause as one step and snap to rest.
- `PauseOnHidden` is upstream's `pauseOnDocumentHidden`, driven by a host-set `Hidden`; `Wake` does not start the pump while hidden, and hiding an idle engine marks it paused; see "A hidden engine does not pump".
- Child loops read the next child from a cursor that unlinking advances, so a callback removing a sibling does not empty the list; see "Child loops survive a callback unlinking a sibling".
- `Fps` of zero or less is uncapped, where upstream clamps it to a rate that never renders, because "never render" is useless and left no way to ask for every frame other than leaving the rate unset.
- `Pause` returns the engine even when idle, where upstream returns `undefined`, so the method has one return type.

## Decisions

- **Hidden pauses the engine, as upstream's `pauseOnDocumentHidden` does.** `PauseOnHidden` defaults to true; the host sets `Hidden`, hiding pauses the engine and showing calls `Resume`, even after a manual pause, as upstream's `visibilitychange` handler does. Reason: without it a minimized window keeps the engine ticking at full rate; the core cannot detect visibility, so the host reports it (BRIDGE-INIT).
- **A hidden engine does not pump.** While hidden, `Wake` does not start the frame source, and hiding an idle engine marks it paused so the show's `Resume` rebases timers created while hidden. Reason: upstream's `wake()` restarts the loop while hidden, which costs nothing in a browser that stops frames in hidden tabs, but a minimized desktop window keeps delivering frames at display rate.
- **Each engine owns its time base, and dilation is separate from `Speed`.** `Now` advances by the frame source's delta times `TimeDilation`, so timers, timeline tick time and `MotionValue` springs and samples all follow it; dilation applies to raw time before the seconds scale, and it is set per engine. Reason: `Speed` is upstream API the app may own and did not reach springs or velocity tracking, so slow motion would have left springs at full speed; velocity sampled in dilated time stays visually consistent. Rejected: a process-wide dilation switch, which would cross threads to reach other engines.
- **Freeze, Thaw and Step are separate from Pause and Resume.** `Freeze` stops engine time and the frame source, `Thaw` restarts them with no rebase because engine time never moved, and `Step` advances engine time and runs one `Update`, frozen or not; `Freeze` fires no `OnPause`. Reason: engine time was the frame source's wall clock and could not be advanced by one frame, and keeping freeze separate leaves upstream's `Pause` (the `Paused` flag, the rebase on `Resume`) untouched.
- **Child loops survive a callback unlinking a sibling.** Unlinking a child that is not linked does nothing, and the engine and timeline loops read their next child from a cursor that unlinking advances; a callback that re-renders its own timeline reuses the cursor, so the outer loop ends the frame where the inner one did. Reason: removing a target from a callback could unlink the cached next child and empty the engine's list for good (upstream has the same bug, untested); the loop keeps upstream's shape so ported suites are untouched, and nothing is deferred or allocated. Rejected: a deferred unlink.
- **Wall-clock reads take a `TimeProvider`.** `StopwatchFrameSource.Now` reads a `TimeProvider`, `TimeProvider.System` by default, while its pump thread stays on real time. Reason: tests can use a fake time provider instead of sleeping; the pump is a background thread pacing a real engine.
- **Defaults, scope and additive state are per engine; a few statics are process-wide and documented, not locked.** See CORE-THREADING.
- **Sinks flush from an engine after-update list, not as tickables.** See CORE-MOTIONVALUE.
- **Time is `double` milliseconds; seconds mode is a scale factor.** A project-wide decision, recorded in [docs/ARCHITECTURE.md §5](../../ARCHITECTURE.md#5-project-wide-decisions).

## Non-goals

- No locks: an engine is never shared between threads.
- No automatic visibility detection; the host sets `Hidden`.
- No per-engine time unit or precision.

## Open items

- `TimeUnit` can be set on any engine, but it rescales only that engine's default duration and speed while changing process-wide state; the per-engine state decision (CORE-THREADING) says it is set on the main engine only, which nothing enforces.
- `Engine.Speed` is not clamped, unlike `Clock.Speed`; a 0 or negative engine speed produces infinite or NaN start times (upstream parity, untested).
- Untested: replacing `FrameSource` while running, `Pause` on an idle engine, `Wake` after `Pause`, `PauseOnHidden` turned on while hidden, and `Advance(ms, step)` with a non-positive step.
