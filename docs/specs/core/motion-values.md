---
id: CORE-MOTIONVALUE
title: Motion values
package: Monaco.Presto
namespaces:
  - Monaco.Presto
sources:
  - Monaco.Presto/MotionValue.cs
  - Monaco.Presto/MotionObjectValue.cs
  - Monaco.Presto/Engine.cs
  - Monaco.Presto/Timer.cs
  - Monaco.Presto/Presto.cs
types:
  - MotionValue
  - MotionObjectValue
tests:
  - MotionValueTests
  - MotionValueDynamicsTests
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

A `MotionValue` is a `double` cell that derived values follow through `Map`, `Snap`, `Combine`, `Spring` and `VelocityOf`, so one live source (a scroll offset, a drag, a timer's progress) drives several properties without hand-written handlers (after Motion's `motionValue`/`useTransform`/`useSpring`/`useVelocity`). Evaluation is lazy and versioned, and `Changed` is raised from the engine frame at most once per frame. `MotionObjectValue` maps a value onto colors, structs or strings. The bridge supplies the sources and sinks (see BRIDGE-FOLLOW); `Animatable.ValueOf` (CORE-ANIMATABLE) and `Timer.ProgressValue` are core sources. Also specifies `Presto.CreateMotionValue`, `Timer.ProgressValue` and `Timer.Scrub`.

## Requirements

### Sources and derived values

- **CORE-MOTIONVALUE-001** — `Presto.CreateMotionValue` shall create a settable source value holding `initial`, owned by `Engine.Current`.
  - Tests: `MotionValueTests.MapClampsToTheOutputRange`
- **CORE-MOTIONVALUE-002** — When `Set` is called on a derived value or a value only Presto assigns (`Map`, `Snap`, `Combine`, `Spring`, `VelocityOf`, `ProgressValue`, `ValueOf`), it shall throw `InvalidOperationException`.
  - Tests: `MotionValueTests.SettingADerivedValueThrows`
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

### Object maps

- **CORE-MOTIONVALUE-012** — `Map(input, outputs)` with non-number outputs shall return a `MotionObjectValue` that interpolates each output's channels separately within the eased segment and encodes the result.
  - Tests: `MotionValueTests.MapInterpolatesColorStringsAcrossStopsAndClamps`, `MotionValueTests.ObjectMapEasesEachSegment`
- **CORE-MOTIONVALUE-013** — The outputs shall be decoded once, through the `Presto.Codecs` entry found by exact type, then base type, then interface, or through the core value parser for strings.
  - Tests: `MotionValueTests.MapInterpolatesColorStringsAcrossStopsAndClamps`
- **CORE-MOTIONVALUE-014** — When the outputs resolve to different codecs, or neighboring outputs differ in kind, unit, channel count or string parts after reconciliation, the object map shall throw `ArgumentException`.
  - Tests: `MotionValueTests.ObjectMapOutputsOfDifferentKindsThrow`
- **CORE-MOTIONVALUE-015** (inherited) — `MotionObjectValue.Value` shall encode again only when the segment or eased progress moved since the last read.
  - Tests: untested
- **CORE-MOTIONVALUE-016** — `MotionObjectValue.Changed` shall be raised from the source's flush, and only when the segment or eased progress moved since the last raise.
  - Tests: `MotionValueTests.ObjectMapRaisesOnlyWhenTheOutputMoves`
- **CORE-MOTIONVALUE-017** (inherited) — The object map shall subscribe to its source only while it has a `Changed` handler.
  - Tests: untested

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
- **CORE-MOTIONVALUE-024** — Only observed values (with a `Changed` handler, an observed dependent, a `Velocity` read or a spring) shall link into their sources; an unobserved chain is never queued.
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
- **CORE-MOTIONVALUE-029** — While a follower is stepping, `IsAnimating` shall be true; `FollowSpring` shall be its spring and `SpringTarget` its source's value, both null for any other value.
  - Tests: untested
- **CORE-MOTIONVALUE-030** — A follower shall stay linked to its source whether or not it is observed.
  - Tests: `MotionValueDynamicsTests.ASpringSettlesExactlyOnItsTargetAndTheEngineSleeps`

### Velocity

- **CORE-MOTIONVALUE-031** — `Velocity` shall start measuring on its first read, which returns 0, and then sample the value once per frame while it moves, as a default `VelocityTracker` does, in units per engine-time unit.
  - Tests: `MotionValueDynamicsTests.VelocityAndVelocityOfMatchAVelocityTrackerFedTheSameSamples`
- **CORE-MOTIONVALUE-032** — `VelocityOf()` shall return one cached value per source, assigned the source's `Velocity` on every sample.
  - Tests: `MotionValueDynamicsTests.VelocityAndVelocityOfMatchAVelocityTrackerFedTheSameSamples`
- **CORE-MOTIONVALUE-033** (inherited) — When the value stops moving, sampling shall continue until the velocity reaches 0, and then stop.
  - Tests: `MotionValueDynamicsTests.VelocityAndVelocityOfMatchAVelocityTrackerFedTheSameSamples`
- **CORE-MOTIONVALUE-034** — When a measured value starts moving again, sampling shall rebase to now, so time spent asleep does not count.
  - Tests: untested

### Engine time

- **CORE-MOTIONVALUE-035** — When the engine resumes after a pause or after being hidden, moving values shall be rebased by the paused interval and continue without a jump.
  - Tests: `EngineHiddenTests.AMovingValueResumesWithoutAJump`
- **CORE-MOTIONVALUE-036** — While the engine is frozen, `Engine.Step` shall move a follower by exactly one frame.
  - Tests: `EngineTimeTests.StepMovesATimerAndASpringByExactlyOneFrame`
- **CORE-MOTIONVALUE-037** — While engine diagnostics are enabled, the frame stats shall count the values stepped and the sinks flushed.
  - Tests: `EngineDiagnosticsTests.LastFrameCountsRootsTweensValuesAndSinks`

### Timer sources

- **CORE-MOTIONVALUE-038** — `Timer.ProgressValue` shall return one cached value per timer, assigned the timer's `Progress` whenever it renders.
  - Tests: `MotionValueDynamicsTests.ProgressValueTracksAPlayingTimer`
- **CORE-MOTIONVALUE-039** — `Timer.Scrub(progress)` shall pause the timer and seek it to `progress.Value × Duration`, then again on every flushed change of the value.
  - Tests: `MotionValueDynamicsTests.AScrubbedAnimationSeeksToValueTimesDuration`, `MotionValueDynamicsTests.AScrubbedTimelineSeeksToValueTimesDuration`
- **CORE-MOTIONVALUE-040** — `Scrub(null)`, `Cancel` and `Revert` shall end the scrub; the engine's cancel of a completed timer shall not.
  - Tests: `MotionValueDynamicsTests.ScrubNullAndRevertEndTheScrub`
- **CORE-MOTIONVALUE-041** (inherited) — A new `Scrub` shall replace the previous one.
  - Tests: untested

### Defaults

- **CORE-MOTIONVALUE-042** — When `initial` is not given, `Presto.CreateMotionValue` shall create a value holding 0.
  - Tests: untested
- **CORE-MOTIONVALUE-043** — When `ease` is not given, a range map or object map shall interpolate linearly within each segment, as Motion's `useTransform` does.
  - Tests: `MotionValueTests.MapInterpolatesAcrossSegments`, `MotionValueTests.MapInterpolatesColorStringsAcrossStopsAndClamps`

## Invariants

- A derived value's `Value` always equals its compute function over its sources' current values.
- Each queued value is flushed once per frame; a flush never re-enters itself within the same frame.
- A value is in the engine's active list only while it is a stepping spring or a moving velocity sample.

## Edge cases

- `Changed += null` is ignored; removing a handler that was never added is a no-op.
- `Set` to the current value does nothing: no version bump, no queue.
- A set and a set back within one frame raises nothing.
- An unclamped eased map applies the ease to progress outside [0, 1] when extrapolating.
- A spring follower over an unobserved derived value still links the chain, keeping it alive as long as the follower.

## Threading and lifetime

- Engine thread only, with no thread check: a value belongs to the engine current when it was created, and `Set` from another thread is undefined.
- Unobserved derived values hold no reference from their sources and are collectable. An observed value is referenced by its sources' dependent lists until its last handler is removed; a spring follower and a value whose `Velocity` was read stay linked for the source's lifetime.
- `Engine` retirement drops queued sinks and moving values.

## Reduced motion

- Not affected: a motion value is not an animation, and `ReducedMotion`/`ReduceAll` do not change mapping, springs or velocity. A sink decides its own reduced form (see BRIDGE-FOLLOW).

## Allocation

- Setting a source, evaluating and flushing a `Map` → `Combine` graph, and stepping a settling spring with `VelocityOf()` allocate nothing per frame (`AllocationTests.MotionValueGraphFlushesWithoutAllocating`, `AllocationTests.SettlingSpringAndVelocityOfStepWithoutAllocating`).
- A `MotionObjectValue` boxes one struct per changed frame unless its codec returns a reused instance.
- Building a graph, subscribing and the first `Velocity` read allocate.

## Deviations from anime.js

- No upstream counterpart; the API follows Motion.

## Decisions

- **Derived values are a feature of their own, not part of `Connected`.** Reason: `Connected` measures two rectangles once and plays one flight, and nothing in it reads a changing value to feed other properties; derived values are a reactive graph.
- **The type is named `MotionValue`, as in Motion.** Reason: the bridge's `Motion` class (state triggers) is a different thing, and the two never appear in the same call.
- **The core owns the cell, the bridge owns the sources.** `MotionValue` has no UI-framework dependency; bridge sources assign it through an internal `Assign`, and `Set` on a derived or Presto-assigned value throws. Reason: the core knows nothing about the UI framework, and a source is owned by whoever feeds it.
- **Evaluation is lazy, versioned and linked only when observed.** `Set` bumps the source's version; a read recomputes only if a source's version moved and bumps its own only if the result changed; only observed values link into their sources. Reason: a diamond computes once per change, an unchanged result stops propagating, a value read between frames is fresh, and an unobserved chain costs nothing and can be collected.
- **Sinks flush from an engine after-update list, not as tickables.** They flush after the children, `Additive.Update()` and the moving values, each raising `Changed` at most once per frame and only on a real change; the queue is double-buffered, and the engine stays awake only while sinks are queued or values move. Reason: as tickables, their place in the child list would decide whether they saw this frame's or last frame's values, and they would inherit timer state (play, pause, completion) a cell does not have; the frame source stops once springs and velocities settle. Rejected: sinks as tickables in the engine's child list.
- **`Spring` is a closed-form oscillator stepped by engine time.** It keeps position and velocity on retarget and lands exactly inside `Spring.RestThreshold`. Reason: frame-rate independence.
- **No XAML extension for building graphs.** Graphs are built in code and view models and reach markup through the bridge's `Follow` extension. Reason: not recorded.
- **Each engine owns its time base, and dilation is separate from `Speed`.** See CORE-ENGINE.

## Non-goals

- No XAML extension for building graphs; derived values are built in code.
- No thread marshalling.
- No sink scheduling as tickables: sinks flush from an engine after-update list.

## Open items

- No core test for `MotionObjectValue`'s lazy encode or its subscribe-only-while-observed rule, `IsAnimating`/`FollowSpring`/`SpringTarget`, velocity rebasing after sleep, `Set` throwing on `ProgressValue`/`ValueOf`, or a second `Scrub` replacing the first.
- No thread-affinity check on `Set`, unlike the engine's control members.
