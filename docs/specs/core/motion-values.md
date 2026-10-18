---
id: CORE-MOTIONVALUE
title: Motion values
package: MonacoLabs.Presto
namespaces:
  - MonacoLabs.Presto
sources:
  - MonacoLabs.Presto/MotionValue.cs
  - MonacoLabs.Presto/MotionObjectValue.cs
  - MonacoLabs.Presto/Engine.cs
  - MonacoLabs.Presto/Timer.cs
  - MonacoLabs.Presto/Presto.cs
  - MonacoLabs.Presto/SettleParams.cs
  - MonacoLabs.Presto/Core/TimerSettle.cs
types:
  - MotionValue
  - MotionObjectValue
  - SettleParams
tests:
  - MotionValueTests
  - MotionValueDynamicsTests
  - SettleTests
  - AllocationTests
  - EngineHiddenTests
  - EngineTimeTests
  - EngineDiagnosticsTests
upstream: []
depends:
  - CORE-ENGINE
  - CORE-TIMER
  - CORE-SPRING
  - CORE-INERTIA
  - CORE-EASES
  - CORE-VALUES
  - CORE-TARGETS
  - CORE-UTILS
---

# Motion values

## Purpose

A `MotionValue` is a `double` cell that derived values follow through `Map`, `Snap`, `Resist`, `Combine`, `Spring`, `Lag`, `Trail` and `VelocityOf`, so one live source (a scroll offset, a drag, a timer's progress) drives several properties without hand-written handlers (after Motion's `motionValue`/`useTransform`/`useSpring`/`useVelocity`); `Lag` replays a source a fixed time late, `Trail` chains several springs or lags for a follow-the-leader effect (after react-spring's `useTrail`), and `Resist` rubber-bands a value past its bounds. Evaluation is lazy and versioned, and `Changed` is raised from the engine frame at most once per frame. `MotionObjectValue` maps a value onto colors, structs or strings. The bridge supplies the sources and sinks (see BRIDGE-FOLLOW); `Animatable.ValueOf` (CORE-ANIMATABLE) and `Timer.ProgressValue` are core sources. Also specifies `Presto.CreateMotionValue`, `Timer.ProgressValue` and `Timer.Scrub`, in its absolute form and in a relative form that keeps the current progress and applies the source's deltas, and `Timer.Settle`, which springs a released timer's playhead to a resting progress chosen from the release velocity.

## Requirements

### Sources and derived values

- **CORE-MOTIONVALUE-001** — `Presto.CreateMotionValue` shall create a settable source value holding `initial`, owned by `Engine.Current`.
  - Tests: `MotionValueTests.MapClampsToTheOutputRange`
- **CORE-MOTIONVALUE-002** — When `Set` is called on a derived value or a value only Presto assigns (`Map`, `Snap`, `Resist`, `Combine`, `Spring`, `Lag`, `Trail`, `VelocityOf`, `ProgressValue`, `ValueOf`), it shall throw `InvalidOperationException`.
  - Tests: `MotionValueTests.SettingADerivedValueThrows`, `MotionValueDynamicsTests.SettingALagThrows`
- **CORE-MOTIONVALUE-074** — In a Debug build, once the value's engine is pinned, `Set` shall fail the engine's debug thread assertion when called from another thread.
  - Tests: `MotionValueTests.SettingFromAnotherThreadFailsTheDebugAssertionOnceTheEngineIsPinned`
- **CORE-MOTIONVALUE-003** — `Map(transform)`, `Snap(increment)`, `Snap(points)` and both `Combine` forms shall compute their value from their sources' current values.
  - Tests: `MotionValueTests.MapFuncCombineAndSnapDerive`
- **CORE-MOTIONVALUE-004** — The array form of `Combine` shall copy the source array and pass `combine` one reused buffer of the sources' values.
  - Tests: `MotionValueTests.MapFuncCombineAndSnapDerive`
- **CORE-MOTIONVALUE-005** — Reading a derived value's `Value` shall return a value consistent with its sources' current values, observed or not, between frames as well as during them.
  - Tests: `MotionValueTests.ReadingADerivedValueBetweenFramesIsFresh`
- **CORE-MOTIONVALUE-006** — A derived value shall recompute only when one of its sources' versions moved since it last computed, and shall bump its own version only when the result changed, so a diamond computes once per change.
  - Tests: `MotionValueTests.ADiamondEvaluatesOncePerChange`

### Range maps

- **CORE-MOTIONVALUE-007** — `Map(input, output)` shall map the value piecewise linearly from the input breakpoints onto the outputs.
  - Tests: `MotionValueTests.MapInterpolatesAcrossSegments`
- **CORE-MOTIONVALUE-008** — When `ease` is given, a range map shall apply it to the progress within each segment.
  - Tests: `MotionValueTests.MapEasesEachSegment`, `MotionValueTests.ObjectMapEasesEachSegment`
- **CORE-MOTIONVALUE-009** — While `clamp` is true (the default), a range map shall hold the end outputs outside the input range.
  - Tests: `MotionValueTests.MapClampsToTheOutputRange`
- **CORE-MOTIONVALUE-010** — While `clamp` is false, a range map shall extrapolate the end segments.
  - Tests: `MotionValueTests.UnclampedMapExtrapolatesTheEndSegments`
- **CORE-MOTIONVALUE-011** — When the ranges differ in length, have fewer than two points, or the input does not strictly ascend, a range map shall throw `ArgumentException`.
  - Tests: `MotionValueTests.InvalidMapRangesThrow`
- **CORE-MOTIONVALUE-071** — `Resist(min, max, friction)` shall return the value unchanged inside [`min`, `max`] and, past a bound, the bound plus the excess × (1 − `friction` clamped to [0, 1]), as `Draggable`'s container friction does (BRIDGE-DRAGGABLE).
  - Tests: `MotionValueTests.ResistScalesTheExcessPastEitherBound`
- **CORE-MOTIONVALUE-072** — When `min` is greater than `max`, `Resist` shall throw `ArgumentException`.
  - Tests: `MotionValueTests.ResistWithMinAboveMaxThrows`

### Object maps

- **CORE-MOTIONVALUE-012** — `Map(input, outputs)` with non-number outputs shall return a `MotionObjectValue` that interpolates each output's channels separately within the eased segment and encodes the result.
  - Tests: `MotionValueTests.MapInterpolatesColorStringsAcrossStopsAndClamps`, `MotionValueTests.ObjectMapEasesEachSegment`
- **CORE-MOTIONVALUE-013** — The outputs shall be decoded once, through the `Presto.Codecs` entry found by exact type, then base type, then the first registered interface type the output implements, or through the core value parser for strings.
  - Tests: `MotionValueTests.MapInterpolatesColorStringsAcrossStopsAndClamps`
- **CORE-MOTIONVALUE-014** — When the outputs resolve to different codecs, or neighboring outputs differ in kind, unit, channel count or string parts after reconciliation, the object map shall throw `ArgumentException`.
  - Tests: `MotionValueTests.ObjectMapOutputsOfDifferentKindsThrow`
- **CORE-MOTIONVALUE-015** (inherited) — `MotionObjectValue.Value` shall encode again only when the segment or eased progress moved since the last read.
  - Tests: `MotionValueTests.AnObjectMapEncodesAgainOnlyWhenItsOutputMoves`
- **CORE-MOTIONVALUE-016** — `MotionObjectValue.Changed` shall be raised from the source's flush, and only when the segment or eased progress moved since the last raise.
  - Tests: `MotionValueTests.ObjectMapRaisesOnlyWhenTheOutputMoves`
- **CORE-MOTIONVALUE-017** (inherited) — The object map shall subscribe to its source only while it has a `Changed` handler.
  - Tests: `MotionValueTests.AnObjectMapSubscribesToItsSourceOnlyWhileItHasAHandler`

### Changed and the frame

- **CORE-MOTIONVALUE-018** — `Changed` shall be raised from the engine frame after every timer has ticked and additive groups have rendered, then after the moving values have stepped.
  - Tests: `MotionValueTests.ASinkRaisesOnceWhenEverySourceChangesInOneFrame`
- **CORE-MOTIONVALUE-019** — `Changed` shall be raised at most once per frame, however many sources moved.
  - Tests: `MotionValueTests.ASinkRaisesOnceWhenEverySourceChangesInOneFrame`
- **CORE-MOTIONVALUE-020** — `Changed` shall be raised only when `Value` differs from its value at the last raise (or at subscription).
  - Tests: `MotionValueTests.ASinkDoesNotRaiseWhenTheFrameEndsUnchanged`
- **CORE-MOTIONVALUE-021** — When a value is set from a `Changed` handler, its own change shall be raised on the next frame, not within the current one.
  - Tests: `MotionValueTests.ASetFromAHandlerFlushesOnTheNextFrame`
- **CORE-MOTIONVALUE-022** — When a set queues a sink, the engine shall wake, and it shall sleep again once nothing is queued or moving.
  - Tests: `MotionValueTests.SettingASourceWakesTheEngineUntilTheFlush`
- **CORE-MOTIONVALUE-023** — When the last handler is removed before the flush, the value shall be dequeued and not raised.
  - Tests: `MotionValueTests.UnsubscribingDequeuesTheSink`
- **CORE-MOTIONVALUE-024** — Only observed values (with a `Changed` handler, an observed dependent, a `Velocity` read, a spring or a lag) shall link into their sources; an unobserved chain is never queued.
  - Tests: `MotionValueTests.ReadingADerivedValueBetweenFramesIsFresh`

### Spring

- **CORE-MOTIONVALUE-025** — `Spring(spring)` shall return a value that starts at the source's value and follows it every frame as a damped oscillator with the spring's mass, stiffness and damping.
  - Tests: `MotionValueDynamicsTests.ASpringSettlesExactlyOnItsTargetAndTheEngineSleeps`, `MotionValueDynamicsTests.ASpringOverADerivedValueFollowsItsSource`
- **CORE-MOTIONVALUE-026** — The follower shall be stepped by elapsed engine time, so its trajectory does not depend on the frame rate.
  - Tests: `MotionValueDynamicsTests.ASpringIsFrameRateIndependent`
- **CORE-MOTIONVALUE-027** — When the source moves mid-flight, the follower shall keep its position and velocity.
  - Tests: `MotionValueDynamicsTests.ASourceJumpMidFlightKeepsTheSpringsVelocity`
- **CORE-MOTIONVALUE-028** — When the displacement is below the spring rest threshold (0.0005) and the velocity below that figure per millisecond, the follower shall land exactly on the target and stop stepping, so the engine can sleep.
  - Tests: `MotionValueDynamicsTests.ASpringSettlesExactlyOnItsTargetAndTheEngineSleeps`
- **CORE-MOTIONVALUE-029** — While a follower or a lag is stepping, `IsAnimating` shall be true; `FollowSpring` shall be a spring follower's spring and `SpringTarget` its source's value, both null for any other value, a lag included.
  - Tests: `MotionValueDynamicsTests.ALagAnimatesUntilCaughtUpThenSleepsAndTheEngineIdles`
- **CORE-MOTIONVALUE-030** — A follower or a lag shall stay linked to its source whether or not it is observed.
  - Tests: `MotionValueDynamicsTests.ASpringSettlesExactlyOnItsTargetAndTheEngineSleeps`, `MotionValueDynamicsTests.ALagHoldsItsInitialValueForTheFirstDelay`

### Lag and trail

- **CORE-MOTIONVALUE-061** — `Lag(delay)` shall return a value that starts at the source's value and, at engine time `t`, holds the source's value at `t − delay`, interpolated linearly between the samples recorded either side, holding the oldest sample before the first and the newest after the last.
  - Tests: `MotionValueDynamicsTests.ALagReplaysItsSourceDelayLateAtAnyFrameRate`, `MotionValueDynamicsTests.ALagHoldsItsInitialValueForTheFirstDelay`, `MotionValueDynamicsTests.ALagInterpolatesBetweenUnevenFrames`
- **CORE-MOTIONVALUE-062** — `delay` shall be in the engine's time unit, so seconds mode gives the same trajectory scaled in time.
  - Tests: `MotionValueDynamicsTests.SecondsModeGivesALagTheSameTrajectoryScaledInTime`
- **CORE-MOTIONVALUE-063** — When `delay` is not finite or not greater than 0, `Lag` shall throw `ArgumentOutOfRangeException`.
  - Tests: `MotionValueDynamicsTests.AnInvalidLagDelayThrows`
- **CORE-MOTIONVALUE-064** — A lag shall record its source's value, with the engine time, on each engine step in which the source's version moved since the last sample.
  - Tests: `MotionValueDynamicsTests.ALagInterpolatesBetweenUnevenFrames`
- **CORE-MOTIONVALUE-065** — While a lag holds more than one sample or its value differs from its newest sample, the engine shall step it every frame; once it holds one sample and equals it, it shall stop stepping, so the engine can sleep.
  - Tests: `MotionValueDynamicsTests.ALagAnimatesUntilCaughtUpThenSleepsAndTheEngineIdles`
- **CORE-MOTIONVALUE-066** — When a sleeping lag's source moves, the lag shall restart its history from its held value at the current engine time, so time spent asleep does not count.
  - Tests: `MotionValueDynamicsTests.AChangeAfterALongSleepReplaysFromNow`
- **CORE-MOTIONVALUE-067** — Stepping a lag shall allocate nothing once its buffer has grown to hold `delay` worth of samples at the current frame rate.
  - Tests: `AllocationTests.LagOfAMovingSourceStepsWithoutAllocating`
- **CORE-MOTIONVALUE-068** — `Trail(count, spring)` shall return `count` values where the first is `Spring(spring)` of the source and each next is `Spring(spring)` of the previous.
  - Tests: `MotionValueDynamicsTests.ASpringTrailMatchesHandChainedSpringsFrameForFrame`
- **CORE-MOTIONVALUE-069** — `Trail(count, delay)` shall return `count` values where the first is `Lag(delay)` of the source and each next is `Lag(delay)` of the previous, so element `i` replays the source `(i + 1) × delay` late.
  - Tests: `MotionValueDynamicsTests.ALagTrailElementReplaysTheSourceOneDelayLaterThanThePrevious`
- **CORE-MOTIONVALUE-070** — When `count` is less than 1, both `Trail` forms shall throw `ArgumentOutOfRangeException`.
  - Tests: `MotionValueDynamicsTests.ATrailOfFewerThanOneThrows`

### Velocity

- **CORE-MOTIONVALUE-031** — `Velocity` shall start measuring on its first read, which returns 0, and then sample the value once per frame while it moves, as a default `VelocityTracker` does, in units per engine-time unit.
  - Tests: `MotionValueDynamicsTests.VelocityAndVelocityOfMatchAVelocityTrackerFedTheSameSamples`
- **CORE-MOTIONVALUE-032** — `VelocityOf()` shall return one cached value per source, assigned the source's `Velocity` on every sample.
  - Tests: `MotionValueDynamicsTests.VelocityAndVelocityOfMatchAVelocityTrackerFedTheSameSamples`
- **CORE-MOTIONVALUE-033** (inherited) — When the value stops moving, sampling shall continue until the velocity reaches 0, and then stop.
  - Tests: `MotionValueDynamicsTests.VelocityAndVelocityOfMatchAVelocityTrackerFedTheSameSamples`
- **CORE-MOTIONVALUE-034** — When a measured value starts moving again, sampling shall rebase to now, so time spent asleep does not count.
  - Tests: `MotionValueDynamicsTests.VelocityRebasesToNowWhenAMeasuredValueStartsMovingAgain`

### Engine time

- **CORE-MOTIONVALUE-035** — When the engine resumes after a pause or after being hidden, moving values, a lag's recorded samples included, shall be rebased by the paused interval and continue without a jump.
  - Tests: `EngineHiddenTests.AMovingValueResumesWithoutAJump`, `SettleTests.ResumesWithoutAJumpAfterAPause`, `MotionValueDynamicsTests.ALagContinuesWithoutAJumpAfterAPauseOrHide`
- **CORE-MOTIONVALUE-036** — While the engine is frozen, `Engine.Step` shall move a follower or a lag by exactly one frame.
  - Tests: `EngineTimeTests.StepMovesATimerAndASpringByExactlyOneFrame`, `SettleTests.StepMovesTheSettleByExactlyOneFrame`, `MotionValueDynamicsTests.FrozenStepMovesALagByExactlyOneFrame`
- **CORE-MOTIONVALUE-037** — While engine diagnostics are enabled, the frame stats shall count the values stepped and the sinks flushed.
  - Tests: `EngineDiagnosticsTests.LastFrameCountsRootsTweensValuesAndSinks`

### Timer sources

- **CORE-MOTIONVALUE-038** — `Timer.ProgressValue` shall return one cached value per timer, assigned the timer's `Progress` whenever it renders.
  - Tests: `MotionValueDynamicsTests.ProgressValueTracksAPlayingTimer`
- **CORE-MOTIONVALUE-039** — `Timer.Scrub(progress)` shall pause the timer and seek it to `progress.Value × Duration`, then again on every flushed change of the value.
  - Tests: `MotionValueDynamicsTests.AScrubbedAnimationSeeksToValueTimesDuration`, `MotionValueDynamicsTests.AScrubbedTimelineSeeksToValueTimesDuration`
- **CORE-MOTIONVALUE-040** — `Scrub(null)`, `Cancel`, `Revert` and `Settle` shall end the scrub; the engine's cancel of a completed timer shall not.
  - Tests: `MotionValueDynamicsTests.ScrubNullAndRevertEndTheScrub`, `SettleTests.UsesTheScrubSourceVelocityWhenNoneIsGiven`
- **CORE-MOTIONVALUE-041** (inherited) — A new `Scrub` shall replace the previous one.
  - Tests: `MotionValueDynamicsTests.ANewScrubReplacesThePreviousOne`
- **CORE-MOTIONVALUE-044** — `Timer.Scrub(progress, relative: true)` shall pause the timer without seeking, then on every flushed change move its progress by the value's change since the previous flush (or the call), clamped to [0, 1], and seek to that progress × `Duration`.
  - Tests: `SettleTests.RelativeScrubKeepsTheCurrentProgressAndAppliesDeltas`, `SettleTests.RelativeScrubClampsToTheUnitRange`
- **CORE-MOTIONVALUE-045** — When `Scrub` is called with a non-null value or `Settle` is called on a timer with a `Parent`, it shall throw `InvalidOperationException` whose message says to scrub or settle the parent timeline.
  - Tests: `SettleTests.ChildTimerThrows`

### Settle

- **CORE-MOTIONVALUE-051** — `Timer.Settle` shall end any running scrub and settle, pause the timer, and move its playhead from `Progress` to the chosen target by a spring, seeking to the spring's value × `Duration` on every frame the spring moves.
  - Tests: `SettleTests.LandsExactlyOnTheTargetAndCompletesTheTask`, `SettleTests.ASecondSettleReplacesTheFirst`
- **CORE-MOTIONVALUE-052** — When `Velocity` is null, the settle shall use the `Velocity` of the value the timer was scrubbed by when `Settle` was called, or 0 when it was not scrubbed; `Scrub` shall start measuring that velocity when it attaches.
  - Tests: `SettleTests.UsesTheScrubSourceVelocityWhenNoneIsGiven`, `SettleTests.UnscrubbedTimerSettlesWithZeroVelocity`
- **CORE-MOTIONVALUE-053** — The spring shall start at `Progress` with the velocity converted to progress per second, step by elapsed engine time as a `Spring` follower does, and land exactly on the target at rest.
  - Tests: `SettleTests.ReleaseVelocityCarriesIntoTheSpring`, `SettleTests.TrajectoryIsFrameRateIndependent`, `SettleTests.LandsExactlyOnTheTargetAndCompletesTheTask`, `SettleTests.SecondsModeGivesTheSameTrajectoryScaledInTime`
- **CORE-MOTIONVALUE-054** — The progress a settle seeks to shall be clamped to [0, 1].
  - Tests: `SettleTests.ProgressIsClampedWhenTheSpringOvershoots`
- **CORE-MOTIONVALUE-055** — Each seek of a settle shall fire callbacks as `Seek` does, so landing on 1 sets `Completed`, fires `OnComplete` and completes `Then()`, and landing on 0 does none of these.
  - Tests: `SettleTests.LandingOnOneCompletesAndResolvesThen`, `SettleTests.LandingOnZeroFiresNoCompletion`
- **CORE-MOTIONVALUE-056** — When the spring rests on the target, the task `Settle` returned shall complete with the target; when a playhead control ends it first (CORE-MOTIONVALUE-059), with `Progress` at that call. The task shall never fault.
  - Tests: `SettleTests.LandsExactlyOnTheTargetAndCompletesTheTask`, `SettleTests.ASecondSettleReplacesTheFirst`
- **CORE-MOTIONVALUE-057** — While a settle runs, the engine shall stay awake; once it rests or is ended, the spring shall stop stepping so the engine can sleep.
  - Tests: `SettleTests.EngineSleepsOnceTheSettleRests`
- **CORE-MOTIONVALUE-058** — When the timer's `Duration` is infinite (an infinite loop), `Settle` shall throw `InvalidOperationException`.
  - Tests: `SettleTests.InfiniteTimerThrows`
- **CORE-MOTIONVALUE-059** — When `Scrub`, `Seek`, `Play`, `Resume`, `Reverse`, `Restart`, `Pause`, `Cancel`, `Revert`, `Complete` or another `Settle` is called during a settle, the settle shall stop and its task shall complete with `Progress` at that moment; the engine's cancel of a completed timer shall not stop it.
  - Tests: `SettleTests.APlayheadControlEndsTheSettleWithTheCurrentProgress`, `SettleTests.ASecondSettleReplacesTheFirst`, `SettleTests.RegrabMidSettleWithARelativeScrubDoesNotJump`
- **CORE-MOTIONVALUE-060** — When `ReducedMotion` is set on the timer's engine when `Settle` is called, the settle shall choose its target as it otherwise would, seek to it once as `Seek` does, and return a task already completed with the target, without starting a spring.
  - Tests: `SettleTests.ReducedMotionLandsOnTheChosenTargetAtOnce`
- **CORE-MOTIONVALUE-046** — The settle shall choose the target as the resting point nearest `Progress + Velocity × Projection`.
  - Tests: `SettleTests.ProjectionPicksTheNearestPoint`
- **CORE-MOTIONVALUE-047** — When `FlickVelocity` is set and `|Velocity|` is at least `FlickVelocity`, the settle shall choose the nearest resting point strictly beyond `Progress` in the velocity's direction, or the last point in that direction when none lies beyond.
  - Tests: `SettleTests.FlickAdvancesToTheNextPointInTheVelocityDirection`
- **CORE-MOTIONVALUE-048** — The resting points shall be `Points`, or 0 and 1 when null, plus, when `Labels` is set on a `Timeline`, each label's time divided by `Duration`, each clamped to [0, 1].
  - Tests: `SettleTests.LabelsBecomeRestingPointsOnATimeline`, `SettleTests.PointsAreClampedToTheUnitRange`
- **CORE-MOTIONVALUE-049** — When `Points` is empty or contains a non-finite value, or `Projection` or `FlickVelocity` is negative or non-finite, `Settle` shall throw `ArgumentException`.
  - Tests: `SettleTests.InvalidParamsThrow`

### Defaults

- **CORE-MOTIONVALUE-042** — When `initial` is not given, `Presto.CreateMotionValue` shall create a value holding 0.
  - Tests: `MotionValueTests.CreateMotionValueDefaultsToZero`
- **CORE-MOTIONVALUE-043** — When `ease` is not given, a range map or object map shall interpolate linearly within each segment, as Motion's `useTransform` does.
  - Tests: `MotionValueTests.MapInterpolatesAcrossSegments`, `MotionValueTests.MapInterpolatesColorStringsAcrossStopsAndClamps`
- **CORE-MOTIONVALUE-050** — When not given, a settle's `Points` shall be 0 and 1, `Projection` 350 in the engine time unit, `FlickVelocity` off, and `Spring` a new `Springs.Gentle`.
  - Tests: `SettleTests.ProjectionPicksTheNearestPoint`
- **CORE-MOTIONVALUE-073** — When `friction` is not given, `Resist` shall use 0.8, as `Draggable`'s `ContainerFriction` does.
  - Tests: `MotionValueTests.ResistDefaultsToAFrictionOfPointEight`

## Invariants

- A derived value's `Value` always equals its compute function over its sources' current values.
- Each queued value is flushed once per frame; a flush never re-enters itself within the same frame.
- A value is in the engine's active list only while it is a stepping spring, a lag with history to play, or a moving velocity sample.

## Edge cases

- `Changed += null` is ignored; removing a handler that was never added is a no-op.
- `Set` to the current value does nothing: no version bump, no queue.
- A set and a set back within one frame raises nothing.
- An unclamped eased map applies the ease to progress outside [0, 1] when extrapolating.
- A spring follower over an unobserved derived value still links the chain, keeping it alive as long as the follower.
- A settle with zero velocity is never a flick, even with `FlickVelocity` 0, since it has no direction; it takes the point nearest `Progress`.
- Equidistant resting points resolve to the lower one.
- A timeline with zero `Duration` adds no label points.
- A settle that starts on its target with zero velocity seeks there and completes its task before `Settle` returns, since a spring at rest would never move.

## Threading and lifetime

- Engine thread only: a value belongs to the engine current when it was created, and `Set` from another thread fails the engine's Debug thread pin and is undefined in Release.
- Unobserved derived values hold no reference from their sources and are collectable. An observed value is referenced by its sources' dependent lists until its last handler is removed; a spring follower, a lag and a value whose `Velocity` was read stay linked for the source's lifetime.
- `Engine` retirement drops queued sinks and moving values.

## Reduced motion

- Not affected: a motion value is not an animation, and `ReducedMotion`/`ReduceAll` do not change mapping, springs, lags or velocity. A sink decides its own reduced form (see BRIDGE-FOLLOW).
- `Timer.Settle` is the exception: under `ReducedMotion` it seeks to the chosen target at once instead of springing there (CORE-MOTIONVALUE-060).

## Allocation

- Setting a source, evaluating and flushing a `Map` → `Combine` graph, stepping a settling spring with `VelocityOf()`, and stepping a lag allocate nothing per frame (`AllocationTests.MotionValueGraphFlushesWithoutAllocating`, `AllocationTests.SettlingSpringAndVelocityOfStepWithoutAllocating`, `AllocationTests.LagOfAMovingSourceStepsWithoutAllocating`).
- A lag's buffer grows (doubling) while it must hold more samples than ever before.
- A `MotionObjectValue` boxes one struct per changed frame unless its codec returns a reused instance.
- Building a graph, subscribing and the first `Velocity` read allocate.
- Starting a settle allocates the spring follower, its target source and the task; stepping it allocates nothing per frame.

## Provenance

- No upstream counterpart; the API follows Motion.

## Decisions

- **Derived values are a feature of their own, not part of `Connected`.** Reason: `Connected` measures two rectangles once and plays one flight, and nothing in it reads a changing value to feed other properties; derived values are a reactive graph.
- **The type is named `MotionValue`, as in Motion.** Reason: the bridge's `Motion` class (state triggers) is a different thing, and the two never appear in the same call.
- **The core owns the cell, the bridge owns the sources.** `MotionValue` has no UI-framework dependency; bridge sources assign it through an internal `Assign`, and `Set` on a derived or Presto-assigned value throws. Reason: the core knows nothing about the UI framework, and a source is owned by whoever feeds it.
- **An interface codec is found by scanning the registered interface keys.** IsAssignableFrom over the keys of Presto.Codecs, in registration order. Reason: Type.GetInterfaces() needs the trimmer to keep every output type's interfaces, and the registry holds a handful of entries. Rejected: annotating the output type, which cannot be expressed on object.GetType().
- **Evaluation is lazy, versioned and linked only when observed.** `Set` bumps the source's version; a read recomputes only if a source's version moved and bumps its own only if the result changed; only observed values link into their sources. Reason: a diamond computes once per change, an unchanged result stops propagating, a value read between frames is fresh, and an unobserved chain costs nothing and can be collected.
- **Sinks flush from an engine after-update list, not as tickables.** They flush after the children, `Additive.Update()` and the moving values, each raising `Changed` at most once per frame and only on a real change; the queue is double-buffered, and the engine stays awake only while sinks are queued or values move. Reason: as tickables, their place in the child list would decide whether they saw this frame's or last frame's values, and they would inherit timer state (play, pause, completion) a cell does not have; the frame source stops once springs and velocities settle. Rejected: sinks as tickables in the engine's child list.
- **`Spring` is a closed-form oscillator stepped by engine time.** It keeps position and velocity on retarget and lands exactly inside `Spring.RestThreshold`. Reason: frame-rate independence.
- **`Lag` is a time-delay buffer, not a low-pass filter.** It reproduces the source's path exactly, overshoots included, which is what a trail or echo needs. Reason: smoothing already exists as `Spring`; a delay cannot be built from the existing operators. Rejected: an exponential "lerp toward" follower (frame-rate dependent unless solved in closed form, and `Spring` with high damping covers it).
- **A lag records samples on engine steps and reads them with linear interpolation.** Reason: the output is frame-rate independent and smooth when the reading and recording frame rates differ. Rejected: holding the last sample (stair-steps at uneven frame times).
- **A lag's buffer is a growable ring of two parallel `double` arrays.** It starts at 16 samples, doubles when full, never shrinks, and drops samples older than the one bracketing `now − delay` on each step. Reason: the steady-state step allocates nothing; growth happens only while a source moves faster than the buffer has ever held, and at 60 fps a one-second lag needs 64 samples. Rejected: a fixed capacity (silently drops history at high frame rates or long delays).
- **A lag sleeps once caught up.** A later source change wakes it through the same path that wakes spring followers, and it restarts its history at the current time. Reason: the engine must sleep when nothing moves (CORE-MOTIONVALUE-022), and time spent asleep must not stretch the first segment.
- **Pause and hide shift a lag's samples, freeze does not.** `Engine.Resume` rebases each moving value, and a lag shifts every sample time by the paused interval; `Freeze` stops engine time itself, so `Engine.Step` advances a lag by exactly one frame. Reason: the replay continues where it was, as springs do (CORE-MOTIONVALUE-035, 036).
- **A lag's `delay` is in the engine's time unit and must be greater than 0.** `Lag(0)` throws rather than returning the source. Reason: every duration is in the engine's time unit, and the result is then always a Presto-assigned value with the same `Set` contract.
- **`Trail` always chains, with one `Spring` or delay for the whole chain.** Each element follows the previous one, as react-spring's `useTrail` does, so the follow-the-leader spacing comes from the chain itself. Rejected: a `Func<int, Spring>` per element (fans out from the source instead, and a non-uniform trail is a short loop over `Spring` or `Lag` the caller can already write).
- **`Resist` is a `Map` with the library's linear curve.** It applies `Draggable`'s container-friction formula, friction clamped to [0, 1] and defaulting to 0.8, so a resisted motion value and a dragged control stretch the same way. Rejected: `PullToRefresh`'s asymptotic curve (a second rubber band in the library with different numbers).
- **`Resist` is linear only.** No curve or easing parameter. Reason: nothing needs another curve, and `Map(Func)` covers a custom one.
- **No bridge `Follow.Trail` helper.** A trail is an array of motion values, which reach controls through the existing sinks (`Follow.Bind`, `Follow.X`/`Follow.Y`). Reason: a helper would only loop over those sinks.
- **No XAML extension for building graphs.** Graphs are built in code and view models and reach markup through the bridge's `Follow` extension. Reason: not recorded.
- **Each engine owns its time base, and dilation is separate from `Speed`.** See CORE-ENGINE.
- **Re-grab continuity comes from a relative scrub.** `Scrub(source, relative: true)` keeps the current progress and applies the source's later deltas, so re-grabbing a settling timer never jumps; the default stays absolute so existing scrubs are unchanged. Rejected: documenting a "Set the source to `Progress` first" pattern (easy to forget), and making every scrub relative (changes CORE-MOTIONVALUE-039).
- **Child timers cannot be scrubbed or settled.** A child's time is recomputed from its parent on every parent render, so an independent scrub or settle works only while the parent is idle and is silently overwritten once it moves. `Scrub` and `Settle` throw for a timer with a `Parent`; scrub or settle the timeline instead, using labels as resting points. Rejected: allowing it and accepting the overwrite.
- **Target by projection plus an optional flick threshold.** Projection with `Inertia`'s default 350 ms time constant matches how `Sheet` and `Pager` pick a rest today; the flick threshold covers "a quick swipe always advances" without making the projection huge. Rejected: a fixed 50 % threshold only, which ignores velocity.
- **`Settle` lives on `Timer`, beside `Scrub`.** It works for an `Animation` and a `Timeline` alike, since both seek by progress. Rejected: a `MotionValue` operator (`drag.SettleTo(...)`), because the resting points (labels, `Duration`) belong to the timer, and the caller would still need `Scrub` plumbing.
- **Velocity defaults to the scrub source's `Velocity`.** `MotionValue.Velocity` is already in value units per engine time unit, so a scrub source in 0..1 gives progress velocity directly, and the common case needs no arguments. `Scrub` reads it once when it attaches, because the first read only starts measuring. Rejected: requiring a velocity in DIPs, which would need the caller's DIP-to-progress mapping inside the core.
- **The spring is the core `MotionValue` spring stepper, reused internally.** The settle is an internal source holding the target and a spring follower seeded with the current progress and the release velocity, whose changes seek the timer. Reason: it is already frame-rate independent, engine-time based, sleeps at rest and rebases on pause/hide (CORE-MOTIONVALUE-025…036). Rejected: a tween with a spring ease, which cannot start with a velocity without the numeric derivative CORE-SPRING declines to expose.
- **Progress is clamped to [0, 1]; the default spring does not bounce.** A timer cannot render outside its duration, so a bouncy spring would flatten against an end. `Springs.Gentle` (bounce 0) is the default; a bouncy spring is allowed and clamps.
- **The task never faults and never hangs.** Ended settles complete with the progress where they stopped, so `await` always continues. Rejected: cancelling the task (callers would need try/catch for a routine re-grab), and returning `Timer` for chaining (its `Then()` only completes at the end, never at 0).
- **Any playhead control ends the settle.** `Scrub`, `Seek`, `Play`, `Resume`, `Reverse`, `Restart`, `Pause`, `Cancel`, `Revert`, `Complete` and a new `Settle` end it first; the engine's own cancel of a completed timer goes through an internal pause that does not, so a settle passing 1 keeps running. Reason: the last caller wins, as with a new `Scrub` (CORE-MOTIONVALUE-041).
- **Reduced motion lands at once.** When `ReducedMotion` is set on the timer's engine at the call, the target is still chosen from the velocity, and the timer is seeked to it in one call. Reason: the destination is meaningful (open or closed), the travel is not; this matches `Inertia` under `ReduceAll` (CORE-INERTIA-030).
- **Labels are opt-in and live in time, not eased progress.** Labels are raw timeline times (CORE-TIMELINE), and the settle seeks in time, so a label resolves to `time ÷ Duration` and lands exactly on it whatever `PlaybackEase` does. Rejected: always including labels, which would surprise a timeline using labels only for layout.

## Non-goals

- No XAML extension for building graphs; derived values are built in code.
- No thread marshalling.
- No sink scheduling as tickables: sinks flush from an engine after-update list.

## Open items

- No core test for `FollowSpring`/`SpringTarget` or `Set` throwing on `ProgressValue`/`ValueOf`.
