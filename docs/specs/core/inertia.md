---
id: CORE-INERTIA
title: Inertia, velocity tracking and projectiles
package: Monaco.Presto
namespaces:
  - Monaco.Presto
sources:
  - Monaco.Presto/Inertia.cs
  - Monaco.Presto/Core/InertiaSolver.cs
  - Monaco.Presto/VelocityTracker.cs
  - Monaco.Presto/Projectile.cs
  - Monaco.Presto/Core/ProjectileSolver.cs
  - Monaco.Presto/Animation.cs
  - Monaco.Presto/TweenValue.cs
types:
  - Inertia
  - VelocityTracker
  - Projectile
tests:
  - InertiaTests
  - InertiaMathTests
  - VelocityTrackerTests
  - ProjectileTests
  - ReferencePagesTests
upstream: []
depends:
  - CORE-ANIMATION
  - CORE-VALUES
  - CORE-SPRING
  - CORE-REDUCED
  - CORE-ENGINE
---

# Inertia, velocity tracking and projectiles

## Purpose

`Inertia` and `Projectile` are physics to-values: passed where a tween's `To` goes, each is resolved in closed form once the tween's start number is known, into an ordinary tween (or two) with a computed end, ease and duration. `Inertia` coasts from a release velocity and decays to rest, optionally snapped and bounced off bounds; `Projectile` flies under constant acceleration and friction for a declared time. `VelocityTracker` measures the release velocity from sampled values. They build on CORE-ANIMATION; `Animatable.Fling` (CORE-ANIMATABLE) reuses the inertia solver. Also specifies the `TweenValue` conversions from `Inertia` and `Projectile`.

## Requirements

### Inertia as a to-value

- **CORE-INERTIA-001** — When a tween's to-value is an `Inertia`, the animation shall resolve it once the tween's from-number is known, replacing the tween's to-number, ease and duration with the resolved ones.
  - Tests: `InertiaTests.ThenCompletesAtTheResolvedDuration`
- **CORE-INERTIA-002** — A `Duration` or ease set beside an inertia tween shall be ignored for it; its `Delay` shall apply.
  - Tests: `InertiaTests.ThenCompletesAtTheResolvedDuration`, `InertiaTests.AFunctionValueReturningPerTargetInertiasStaggers`
- **CORE-INERTIA-003** — When a function value returns an `Inertia` per target, each target's tween shall be resolved from its own inertia.
  - Tests: `InertiaTests.AFunctionValueReturningPerTargetInertiasStaggers`
- **CORE-INERTIA-004** — The ideal target shall be `from + Power · Velocity · TimeConstant`, and the motion shall start at `Power · Velocity` when nothing snaps it.
  - Tests: `InertiaMathTests.StartsAtPowerTimesVelocity`, `InertiaMathTests.DoublingTheTimeConstantDoublesTheTravel`, `AnimatablesTests.AFlingTravelsPowerTimesVelocityTimesTimeConstant`
- **CORE-INERTIA-005** — When `ModifyTarget` is set, the landed target shall be `ModifyTarget(ideal)`, and the start velocity shall follow from the modified travel.
  - Tests: `InertiaMathTests.SnapIncrementLandsOnTheNearestMultiple`, `InertiaMathTests.SnapPointsLandOnTheNearestPoint`
- **CORE-INERTIA-006** — The value shall follow `x(t) = target − (target − from) · e^(−t/TimeConstant)`, normalized so the tween lands exactly on the target.
  - Tests: `InertiaMathTests.DecaysWithinRestDeltaByTheDurationAndLandsExactly`, `InertiaMathTests.TrajectoryMatchesNumericalIntegration`
- **CORE-INERTIA-007** — The duration shall be `TimeConstant · ln(|travel| / RestDelta)`, clamped to [0, 60000].
  - Tests: `InertiaMathTests.DecaysWithinRestDeltaByTheDurationAndLandsExactly`
- **CORE-INERTIA-008** — A negative velocity shall travel backward, monotonically.
  - Tests: `InertiaMathTests.NegativeVelocityTravelsBackward`
- **CORE-INERTIA-009** — When the modified travel is at most `RestDelta`, the tween shall stay at its from-value and last `Constants.MinValue`.
  - Tests: `InertiaMathTests.VelocityBelowRestSetsTheValue`

### Bounds

- **CORE-INERTIA-010** — When the target lies beyond `Max` while travelling up, or beyond `Min` while travelling down, the tween's to-value shall be that bound.
  - Tests: `InertiaTests.BoundsSettleOnTheBound`, `InertiaMathTests.SnappedBeyondABoundStillHitsTheBound`
- **CORE-INERTIA-011** — A crossing shall be continuous in value and velocity: the decay runs until it reaches the bound, then a spring with `BounceStiffness` and `BounceDamping` takes over there with the crossing velocity, overshoots and settles on the bound, within one tween.
  - Tests: `InertiaMathTests.BoundCrossingIsContinuousThenPeaksPastAndSettlesOnTheBound`, `InertiaMathTests.TrajectoryMatchesNumericalIntegration`
- **CORE-INERTIA-012** — A bounce's duration shall be the crossing time plus the thrown spring's settling duration.
  - Tests: `InertiaMathTests.BoundCrossingIsContinuousThenPeaksPastAndSettlesOnTheBound`
- **CORE-INERTIA-013** — The bound shall apply to the snapped target: a snapped target beyond a bound still hits the bound.
  - Tests: `InertiaMathTests.SnappedBeyondABoundStillHitsTheBound`
- **CORE-INERTIA-014** — When the from-value is outside the bounds, the motion shall skip the decay and `ModifyTarget` and spring to the nearest bound with the release velocity, as one tween lasting the spring's settling duration.
  - Tests: `InertiaMathTests.StartOutsideTheBoundsSpringsBack`
- **CORE-INERTIA-015** — When the crossing velocity normalized by the distance to the bound exceeds `10 · Constants.K` (including a release on a bound moving outward), the motion shall become two tweens: from the bound out to the spring's first peak along its impulse response, then back to the bound with a spring from rest.
  - Tests: `InertiaMathTests.ReleaseOnABoundMovingOutwardGoesOutAndBack`, `InertiaTests.AReleaseOnABoundMovingOutwardGoesOutAndBack`

### Playback

- **CORE-INERTIA-016** — An inertia tween shall complete, resolve `Then()` and land on its resolved target at its resolved duration.
  - Tests: `InertiaTests.ThenCompletesAtTheResolvedDuration`
- **CORE-INERTIA-017** — Seeking an inertia animation shall reproduce the trajectory at that time.
  - Tests: `InertiaTests.SeekMidFlightReproducesTheTrajectory`
- **CORE-INERTIA-018** — Reverting an inertia animation shall restore the property's start value.
  - Tests: `InertiaTests.RevertRestoresTheStart`
- **CORE-INERTIA-019** — The same inertia on the same start shall produce identical values frame by frame across runs.
  - Tests: `InertiaTests.FrameByFrameValuesAreIdenticalAcrossRuns`
- **CORE-INERTIA-020** — While the engine runs in seconds, an inertia with velocity and `TimeConstant` converted to seconds shall give the same trajectory, scaled in time.
  - Tests: `InertiaTests.SecondsModeGivesTheSameTrajectoryScaled`

### Projectile

- **CORE-INERTIA-021** — When a tween's to-value is a `Projectile`, the animation shall resolve it once the from-number is known, solving `x' = v`, `v' = Acceleration − k·v` in closed form, with `k = −ln(1 − Friction)` per millisecond in either time unit.
  - Tests: `ProjectileTests.ConstantVelocityWithoutGravityMovesLinearly`, `ProjectileTests.GravityFollowsTheBallisticFormula`, `ProjectileTests.FrictionReducesTheTravel`
- **CORE-INERTIA-022** — The flight time shall be `Projectile.Duration`, or the tween's own duration when that is null; it is declared, not derived from the physics.
  - Tests: `ProjectileTests.ProjectileDurationOverridesTheTweenDuration`
- **CORE-INERTIA-023** — The tween shall end on the physical end point, and its ease shall be the displacement normalized over its span, so a seek at any time matches the closed-form trajectory.
  - Tests: `ProjectileTests.GravityFollowsTheBallisticFormula`, `ProjectileTests.ArcSeeksMatchTheBallisticFormulaOnBothSidesOfTheApex`
- **CORE-INERTIA-024** — When the flight turns back strictly inside the flight time, the motion shall be split at the apex into two monotonic tweens, so a throw landing on its start arcs and ends exactly there.
  - Tests: `ProjectileTests.ATossLandingOnItsStartArcsAndEndsExactlyThere`, `ProjectileTests.ArcSeeksMatchTheBallisticFormulaOnBothSidesOfTheApex`, `ReferencePagesTests.TossArcsTheBallUpAndLandsItBackOnTheFloor`
- **CORE-INERTIA-025** — When the projectile ends where it started without turning back inside the flight, the tween shall stay at its from-value with `Eases.None`.
  - Tests: untested
- **CORE-INERTIA-026** — While the engine runs in seconds, a projectile with velocity and acceleration converted to seconds shall give the same trajectory, scaled in time.
  - Tests: `ProjectileTests.SecondsModeGivesTheSameTrajectoryScaled`
- **CORE-INERTIA-027** — `Projectile.Launch` shall return an X projectile with velocity `speed · cos(angle)` and a Y projectile with velocity `speed · sin(angle)` and acceleration `gravity`, both with `friction`, the angle in degrees clockwise from +X with positive Y down.
  - Tests: `ProjectileTests.LaunchStraightDownHasNoHorizontalVelocity`

### Errors

- **CORE-INERTIA-028** — When an `Inertia` or `Projectile` appears in a keyframe array or a from/to pair, creating the animation shall throw `ArgumentException`.
  - Tests: `InertiaTests.AnInertiaInAKeyframeArrayThrows`, `ProjectileTests.AKeyframeOrFromToPairThrows`
- **CORE-INERTIA-029** — When an `Inertia` or `Projectile` targets a color or complex property, creating the animation shall throw `ArgumentException`.
  - Tests: `InertiaTests.AColorPropertyThrows`, `ProjectileTests.AColorPropertyThrows`

### Reduced motion

- **CORE-INERTIA-030** — While `ReduceAll` is set, an inertia shall still be resolved, then collapsed to a `Constants.MinValue` duration, so the property lands on the snapped, clamped target in one frame.
  - Tests: `InertiaTests.ReduceAllLandsOnTheSnappedClampedTargetInOneFrame`
- **CORE-INERTIA-031** — While `ReduceAll` is set, a projectile shall be resolved from its declared flight time before it collapses, so the property lands on the physical end at once.
  - Tests: `ProjectileTests.ReduceAllLandsOnTheEndAtOnce`

### VelocityTracker

- **CORE-INERTIA-032** — The first `Add` after construction or `Reset` shall only record the baseline value and time.
  - Tests: `VelocityTrackerTests.ResetZeroesAndStartsANewBaseline`
- **CORE-INERTIA-033** — When `Add` is called at least `interval` after the last accepted sample, the tracker shall record `(value − baseline) / elapsed` as a sample and make `value` the new baseline.
  - Tests: `VelocityTrackerTests.ConstantRateFeedGivesTheRate`
- **CORE-INERTIA-034** — When `Add` is called less than `interval` after the last accepted sample, the tracker shall ignore it, leaving the baseline and `Velocity` unchanged.
  - Tests: `VelocityTrackerTests.SamplesCloserThanTheIntervalAreIgnored`
- **CORE-INERTIA-035** — `Velocity` shall be the signed sample of largest magnitude among the last `samples` samples, zero seeds included.
  - Tests: `VelocityTrackerTests.IsTheStrongestOfTheLastSamples`, `VelocityTrackerTests.KeepsTheSign`
- **CORE-INERTIA-036** — `Reset` shall clear the samples, the baseline and `Velocity`, and keep the time of the last sample.
  - Tests: `VelocityTrackerTests.ResetZeroesAndStartsANewBaseline`
- **CORE-INERTIA-037** (inherited) — When `samples` is less than 1, the constructor shall throw `ArgumentOutOfRangeException`.
  - Tests: untested
- **CORE-INERTIA-038** — `Add` shall not allocate.
  - Tests: `VelocityTrackerTests.AddDoesNotAllocate`
- **CORE-INERTIA-039** — The tracker's internal displacement sampler (the one the draggable uses) shall reproduce upstream's `computeVelocity` exactly: each accepted sample scaled, clamped and rounded to 5 digits, and the velocity the plain maximum of the window.
  - Tests: `VelocityTrackerTests.ReproducesTheDraggableSampler`

### Defaults

- **CORE-INERTIA-040** — When `Power` is not set, an inertia shall use 1.
  - Tests: untested
- **CORE-INERTIA-041** — When `TimeConstant` is not set, an inertia shall use 350 engine-time units.
  - Tests: untested
- **CORE-INERTIA-042** — When `RestDelta` is not set, an inertia shall come to rest within 0.5 units, as Framer Motion does.
  - Tests: untested
- **CORE-INERTIA-043** — When `BounceStiffness` and `BounceDamping` are not set, a bound crossing shall use a spring of stiffness 500 and damping 10, as Framer Motion does.
  - Tests: untested
- **CORE-INERTIA-044** — When `ModifyTarget`, `Min` and `Max` are not set, an inertia shall land on its ideal target, unsnapped and unbounded.
  - Tests: untested
- **CORE-INERTIA-045** (inherited) — When `Acceleration` or `Friction` is not set, a projectile shall use 0 for it, and `Projectile.Launch` without `friction` shall use 0.
  - Tests: `ProjectileTests.ConstantVelocityWithoutGravityMovesLinearly`
- **CORE-INERTIA-046** — When `VelocityTracker` is constructed without arguments, it shall use an interval of 17 and a window of 3 samples, as anime.js draggable's sampler does.
  - Tests: `VelocityTrackerTests.SamplesCloserThanTheIntervalAreIgnored`

## Invariants

- A resolved physics tween is an ordinary tween: it composes by the animation's composition, seeks, reverses, stretches and hands over to the next animation like any other.
- Every inertia tween ends exactly on its to-number (the snapped target or a bound); every projectile tween on its physical end.
- Resolution happens once, at construction; nothing is simulated per frame.

## Edge cases

- Velocity 0, or a travel within `RestDelta`: the value stays where it is.
- `ModifyTarget` mapping the ideal target back within `RestDelta` of the start: the value stays at the start, not on the modified target.
- A bound equal to the from-value with the velocity pointing inside: an ordinary decay; pointing outward: the two-tween rebound.
- `Min` greater than `Max`, `Friction` of 1 or more (infinite `k`) and a non-positive `VelocityTracker` interval (every sample accepted) are not validated.
- A `Projectile` whose apex falls exactly at 0 or at the flight time stays one tween.

## Threading and lifetime

- Resolution runs on the engine thread inside the `Animation` constructor. `Inertia` and `Projectile` are immutable records and can be shared between animations.
- `VelocityTracker` holds no engine reference and is not thread-safe; feed it from one thread.

## Reduced motion

- Under `ReduceAll`, both physics values are resolved first and collapse to an instant set on the resolved end (CORE-INERTIA-030, CORE-INERTIA-031). A builder `WhenReduced` alternative behaves as for any other property (CORE-REDUCED).

## Allocation

- `VelocityTracker.Add` and the internal displacement sampler are allocation-free.
- The solvers allocate the plan and ease closures once per tween at construction; the resolved eases allocate nothing per frame.

## Deviations from anime.js

- No upstream counterpart. `Inertia` follows Framer Motion's `inertia`, with `Power` 1 and `TimeConstant` 350 ms instead of Framer's 0.8 and 700, so the motion starts at exactly the release velocity (see Exponential decay with Power 1 and TimeConstant 350); `Projectile` follows GSAP Physics2D, solved in closed form so it stays an ordinary tween (see Projectile is a closed-form to-value with a declared flight time). `VelocityTracker` is extracted from anime.js draggable's `computeVelocity`, but its public form takes values and keeps the sign, so apps can feed it positions and `Animatable.Fling` gets a direction (see VelocityTracker is a public core type).

## Decisions

- **Inertia is a to-value, shaped like Spring is an ease.** A sealed core `Inertia` converts implicitly to `TweenValue` and is resolved by the `Animation` constructor once the from-number is known, setting the tween's to-number, ease and duration; a `Duration` beside it is ignored, `Delay` applies, non-number properties throw. Reason: its end depends on the start value, and resolving into an ordinary tween keeps callbacks, `Then()`, timelines, composition, `Revert`, `Stretch`, playback rate and determinism unchanged. Rejected: inertia as an ease; a factory reading the target up front.
- **Exponential decay with Power 1 and TimeConstant 350.** The motion is `target − A'·e^(−t/τ)` with `A = P·v·τ`, defaults `Power` 1, `TimeConstant` 350 engine-time units, `RestDelta` 0.5; the duration is `τ·ln(|A'|/RestDelta)` clamped to [0, 60000], and the ease is normalized to land exactly on the target. Reason: with P = 1 the motion starts at exactly the release velocity and a 1 unit/ms fling lands 350 units away; normalizing avoids stopping `RestDelta` short; the defaults were tuned by feel on the sample and kept. Rejected: Framer's 700 and 0.8, which start slightly faster than the pointer.
- **Bounds hand over to a spring at the crossing.** When the decay crosses `Min` or `Max`, a spring (`BounceStiffness` 500, `BounceDamping` 10) takes over at the bound with the crossing velocity and settles on it; a start outside the bounds springs straight to the nearest bound. Reason: as Framer Motion does, defaults included.
- **One tween even with a bounce.** The post-crossing spring is folded into the same tween's ease as `1 + (S_v(s) − S_0(s))` via `Spring.Solve` with and without the crossing velocity, and the to-value is the bound it settles on. Reason: the spring is linear in its initial conditions, so no new solver is needed, and readers of `To` (`Animatable.Get`, composition, `Complete`) see the true rest. Rejected: a second keyframe for the bounce.
- **A degenerate bounce goes out and back.** A release on a bound moving outward, or any crossing whose velocity normalized by the distance to the bound exceeds `10 · Constants.K`, becomes two tweens: out to the spring's first peak and back to the bound. Reason: there is no distance (or almost none) to normalize by, and Framer bounces in this case. Rejected: just stopping at the bound.
- **Snapping is ModifyTarget, applied before bounds.** `ModifyTarget` is a `Func<double, double>` (such as `Utils.Snap`) applied to the ideal target before bounds; a snapped target beyond a bound still bounces, and snapping changes the start velocity, as in Framer. Reason: `Utils.Snap` already returns such a function, and it keeps the closed-form duration. Rejected: GSAP's stretching of the duration to keep velocity continuous.
- **Per-target velocity through function values.** `Velocity` is a plain `double`; per-target or staggered flings use a `FunctionValue` returning a whole `Inertia`. Reason: covers staggers and multi-target flings with no new plumbing. Rejected: a function-valued velocity field on `Inertia`.
- **Inertia cannot be a keyframe.** Keyframe arrays and from/to pairs containing an `Inertia` (or a `Projectile`) throw `ArgumentException`. Reason: its duration is only known after the previous keyframe resolves; rejected until a use appears.
- **Projectile is a closed-form to-value with a declared flight time.** A `Projectile` rides `Inertia`'s slot and is solved in closed form at build into an ordinary tween with the physical end and a normalized displacement ease; its flight time is `Projectile.Duration` or the tween's duration; a throw that turns back is split at its apex with `Plan.Then`; `Friction` is per millisecond in either time unit; `Launch` gives the angle form with +Y down and per-axis velocity, gravity on Y only. Reason: as an ordinary tween it composes, seeks, reverses and hands over like any tween, splitting keeps each tween monotonic, and per-tween normalized eases make a seek match the ballistic formula. Rejected: deriving the duration from the physics as `Inertia` does; a projectile counterpart to `Animatable.Fling`.
- **Reduced motion lands on the resolved end at once.** Under `ReduceAll` an inertia or projectile is resolved first and then collapses like any other tween, landing on the snapped, clamped rest or the physical end. Reason: the target is resolved before the duration is read, so the existing collapse lands on the right value with no special case.
- **Time fields are in the engine's unit.** Time fields are in engine-time units and velocity per engine-time unit; the spring handover multiplies by `Globals.TimeScale`. Reason: switching units converts `TimeConstant` and velocity together, as `Spring.Duration` does.
- **VelocityTracker is a public core type.** Draggable's sampler is extracted as a public, allocation-free `VelocityTracker` (interval 17, 3 samples; the first `Add` after `Reset` is a baseline; `Velocity` is the signed sample of largest magnitude), and the draggable uses it through an internal displacement sampler that keeps upstream's clamp, rounding and plain maximum. Reason: apps with their own gestures can feed it and pass the velocity to an `Inertia`; it needs only numbers and time, so it belongs in the core; strongest-by-magnitude is right for `Animatable.Fling` while the draggable keeps upstream parity.
- **Each axis decays on its own.** There is no two-dimensional inertia with a shared direction, and no scroll-viewer flinging. Reason: Framer Motion decays axes independently; the platform scroll gesture has its own inertia, and replacing it is a control-level change.

## Non-goals

- No two-dimensional inertia with a shared direction; each axis decays on its own.
- No inertia or projectile in keyframes or from/to pairs.
- No per-frame simulation, and no function-valued `Velocity` field: a `FunctionValue` returning whole inertias covers per-target velocities.
- No projectile counterpart to `Animatable.Fling`.

## Open items

- The decay's 60000 cap is in engine-time units and is not scaled in seconds mode, where it allows 60000 s.
- `ModifyTarget` mapping the ideal target within `RestDelta` of the start leaves the value at the start rather than on the modified target; untested.
- A single `TweenParams { To = new Inertia(…) }` (object form, not an array) is accepted but untested.
- The `samples < 1` guard is untested; `interval`, `Projectile.Friction` (documented as below 1) and `Min <= Max` are not validated.
- A non-moving projectile's `Eases.None` tween is untested.
