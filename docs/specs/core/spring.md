---
id: CORE-SPRING
title: Spring
package: Monaco.Presto
namespaces:
  - Monaco.Presto
sources:
  - Monaco.Presto/Spring.cs
  - Monaco.Presto/Eases.cs
types:
  - Spring
tests:
  - SpringTests
  - EaseFixtureTests
upstream:
  - src/easings/spring/index.js
  - tests/suites/eases.test.js
depends:
  - CORE-EASES
  - CORE-ENGINE
  - CORE-ANIMATION
---

# Spring

## Purpose

A damped-harmonic-oscillator ease whose length is computed, not supplied: the solver finds when the spring comes to rest and that `SettlingDuration` becomes the duration of any tween eased by it. It is configured either physically (mass, stiffness, damping, velocity) or perceptually (bounce, duration), each side derived from the other. It is an ease (CORE-EASES); animations adopt its duration (CORE-ANIMATION), and `Animatable` and `Inertia` sample its `PureEase`. Also specifies `Eases.Spring`.

## Requirements

### Construction

- **CORE-SPRING-001** — A spring constructed without arguments shall have mass 1, stiffness 100, damping 10, velocity 0, bounce 0.5 and duration 628.
  - Tests: `EaseFixtureTests.SpringMatchesUpstream`
- **CORE-SPRING-002** — The constructor shall clamp mass to [1, 10000], stiffness and damping to [1e-11, 10000], velocity to [−10000, 10000], bounce to [−1, 1] and duration to [10, 10000] times the time scale.
  - Tests: `SpringTests.SpringParametersMustBeClampedAt10000`, `SpringTests.SpringVelocityCanBeNegative`
- **CORE-SPRING-003** — When `bounce` or `duration` is given, the constructor shall derive stiffness and damping from them and set mass to 1 and velocity to 0, whatever physical arguments were passed.
  - Tests: `EaseFixtureTests.SpringMatchesUpstream`
- **CORE-SPRING-004** — When only physical arguments are given, the constructor shall leave `Bounce` at 0.5 and `Duration` at 628 rather than derive them.
  - Tests: `EaseFixtureTests.SpringMatchesUpstream`
- **CORE-SPRING-005** — `Eases.Spring(...)` shall return a new spring constructed from the same arguments.
  - Tests: `SpringTests.SpringEaseParametersAffectSettlingDuration`

### Two-way derivation

- **CORE-SPRING-006** — Deriving from bounce and duration shall set stiffness to (2π/D)², with D the duration in seconds, clamped to [1e-11, 10000] and rounded to 3 decimals.
  - Tests: `EaseFixtureTests.SpringMatchesUpstream`
- **CORE-SPRING-007** — Deriving from bounce and duration shall set damping to (1 − bounce)·4π/D for bounce ≥ 0 and 4π/(D·(1 + bounce)) for bounce < 0, clamped to [1e-11, 300] and then rounded to 3 decimals.
  - Tests: `EaseFixtureTests.SpringMatchesUpstream`, `SpringTests.BounceSignPicksTheDampingRegime`
- **CORE-SPRING-008** — When `Bounce` or `Duration` is set, the spring shall clamp it, set mass to 1 and velocity to 0, re-derive stiffness and damping, and recompute.
  - Tests: `SpringTests.SettingBounceAfterConstructionRederivesStiffnessAndDamping`
- **CORE-SPRING-009** — When `Stiffness` or `Damping` is set, the spring shall clamp it, re-derive `Duration` as 2π/√stiffness (in the engine unit) and `Bounce` as 1 − damping·D/4π when damping/(2√stiffness) ≤ 1, else 4π/(damping·D) − 1, each clamped and rounded to 3 decimals, and recompute.
  - Tests: untested
- **CORE-SPRING-010** — The derivation from stiffness and damping shall assume mass 1, whatever `Mass` is.
  - Tests: untested
- **CORE-SPRING-011** — When `Mass` or `Velocity` is set, the spring shall clamp it and recompute, leaving `Bounce` and `Duration` unchanged.
  - Tests: `SpringTests.SettingASpringParameterAfterCreationShouldUpdateItsDuration`
- **CORE-SPRING-012** — Every setter shall clamp to the constructor's ranges.
  - Tests: `SpringTests.SpringParametersMustBeClampedAt10000`, `SpringTests.SpringVelocityCanBeNegative`

### Solver

- **CORE-SPRING-013** — `DampingRatio` shall be damping/(2√(stiffness·mass)), updated whenever a parameter changes.
  - Tests: `SpringTests.DampingRatioIsDampingOverCriticalDamping`
- **CORE-SPRING-014** — The solver shall use natural frequency √(stiffness/mass) clamped to [1e-11, 1000].
  - Tests: untested
- **CORE-SPRING-015** — The spring shall oscillate when `DampingRatio` < 1, be critically damped at exactly 1, and approach the target without crossing it when above 1.
  - Tests: `SpringTests.BounceSignPicksTheDampingRegime`, `EaseFixtureTests.SpringMatchesUpstream`
- **CORE-SPRING-016** — The overdamped solution shall stay finite for parameters at their clamps.
  - Tests: `SpringTests.SpringParametersMustBeClampedAt10000`
- **CORE-SPRING-017** — The initial velocity shall launch the weight toward the target when positive and away from it when negative.
  - Tests: `SpringTests.SpringVelocityCanBeNegative`, `EaseFixtureTests.SpringMatchesUpstream`
- **CORE-SPRING-018** — `SolverDuration` shall be found by stepping solver time by 0.02 s until the displacement stays below 0.0005 for 11 consecutive steps or 3001 steps have run, as the time of the last step sampled.
  - Tests: `EaseFixtureTests.SpringMatchesUpstream`
- **CORE-SPRING-019** — `SettlingDuration` shall be `SolverDuration` in milliseconds, rounded to a whole number, times the time scale.
  - Tests: `SpringTests.SpringEaseParametersAffectSettlingDuration`, `SpringTests.SecondsModeScalesSettlingDuration`
- **CORE-SPRING-020** — A spring that never settles shall have a `SettlingDuration` of 60000 ms.
  - Tests: `EaseFixtureTests.SpringMatchesUpstream`
- **CORE-SPRING-021** — Setting any parameter shall recompute `SolverDuration`, `SettlingDuration` and `DampingRatio` before the setter returns.
  - Tests: `SpringTests.SettingASpringParameterAfterCreationShouldUpdateItsDuration`

### The ease

- **CORE-SPRING-022** — `PureEase` shall return exactly t at t = 0 and t = 1, and otherwise the spring's position at solver time t·`SolverDuration`.
  - Tests: `EaseFixtureTests.SpringMatchesUpstream`
- **CORE-SPRING-023** — `Ease` shall return the same values as `PureEase`.
  - Tests: `EaseFixtureTests.SpringMatchesUpstream`
- **CORE-SPRING-024** — `Ease` and `PureEase` shall each be one delegate for the spring's lifetime, following later parameter changes.
  - Tests: `SpringTests.EaseIsOneDelegateSharedWithTheImplicitConversion`
- **CORE-SPRING-025** — The implicit conversion to `Ease` shall return the spring's `Ease`, which carries no `SettlingDuration`.
  - Tests: `SpringTests.EaseIsOneDelegateSharedWithTheImplicitConversion`

### Completion

- **CORE-SPRING-026** — When `Ease` is sampled at a t where t·`SettlingDuration` ≥ `Duration` while `Completed` is false, the spring shall set `Completed` and invoke `OnComplete` with `Parent`, once.
  - Tests: `SpringTests.OnCompleteFiresOnceWhenSampledPastThePerceivedDuration`
- **CORE-SPRING-027** — When `Ease` is sampled below that threshold while `Completed` is true, the spring shall clear `Completed` without a callback, so the next crossing fires again.
  - Tests: `SpringTests.OnCompleteFiresOnceWhenSampledPastThePerceivedDuration`
- **CORE-SPRING-028** — Sampling `PureEase` shall neither change `Completed` nor invoke `OnComplete`.
  - Tests: untested
- **CORE-SPRING-029** — `Parent` shall be null until an animation adopts the spring.
  - Tests: `SpringTests.OnCompleteFiresOnceWhenSampledPastThePerceivedDuration`
- **CORE-SPRING-030** — When a spring is an animation's own `Ease`, the animation shall set `Parent` to itself; a spring on a tween or returned per target is not parented.
  - Tests: untested

### Driving durations

- **CORE-SPRING-031** — When a spring is an animation's `Ease`, every tween's duration shall be its `SettlingDuration`, overriding the animation's `Duration`.
  - Tests: `SpringTests.SpringEaseOverridesTheAnimationDurationParameter`, `SpringTests.SpringEaseParametersAffectTheAnimationDuration`
- **CORE-SPRING-032** — When a spring is a tween's `Ease`, that tween's duration shall be its `SettlingDuration`, overriding both the tween's and the animation's `Duration`.
  - Tests: `SpringTests.SpringEaseOverridesTheTweenDurationParameter`
- **CORE-SPRING-033** — A spring shall read the process-wide time scale (CORE-ENGINE) when it computes, so switching the time unit later does not rescale existing springs.
  - Tests: untested
- **CORE-SPRING-034** — In seconds mode, `Duration` shall be read and derived in seconds.
  - Tests: untested

### Defaults

- **CORE-SPRING-035** — When `onComplete` is not given, the spring shall invoke nothing when it completes, as anime.js's `noop` does.
  - Tests: untested

## Invariants

- `SettlingDuration`, `SolverDuration` and `DampingRatio` always match the current parameters.
- `Ease(0)` = `PureEase(0)` = 0 and `Ease(1)` = `PureEase(1)` = 1 exactly.
- Mass ≥ 1; stiffness > 0; damping ≥ 0 (a derived damping can round to 0).

## Edge cases

- `bounce: 1` derives damping 0 after rounding: the spring never settles and `SettlingDuration` is 60000 ms.
- `bounce: -1` divides by zero; the infinite damping clamps to 300.
- In seconds mode the default `Duration` of 628 clamps to 10 s, so a physically configured spring's `OnComplete` threshold lies past its `SettlingDuration` and never fires; and a spring given only `bounce` derives from a 10 s duration.
- `Ease` and `PureEase` do not clamp t; past 1 they keep solving the curve.
- Passing physical arguments with `bounce` or `duration` silently discards the physical ones.

## Threading and lifetime

- A spring is mutable and not thread-safe. `Ease` changes `Completed` and invokes `OnComplete`, so it belongs on the thread that drives its animation; `PureEase` is pure and safe for any thread once no setter runs.
- `Parent` holds the adopting animation until another adopts the spring; one spring shared by several animations reports only the last.

## Reduced motion

- Not affected by the spring itself. `ReduceAll` gives spring-eased tweens a minimal duration in the animation (CORE-REDUCED).

## Allocation

- Calling `Ease` or `PureEase` allocates nothing.
- Every constructor and setter runs the settling search, up to 3001 solver steps; parameter changes are not meant for the tick path.

## Deviations from anime.js

- `PureEase` has no upstream; it serves samplers that must not raise `OnComplete`, such as velocity measurement.
- `DampingRatio` has no upstream; it is public so a caller can tell the damping regime without a velocity API on the ease (No core velocity API for an ease).
- Parameters are nullable constructor arguments instead of a params object; `OnComplete` may be null instead of `noop`.
- The deprecated `createSpring()` is not ported.

## Decisions

- **No core velocity API for an ease; `DampingRatio` is public.** Callers that need the ease's velocity compute it numerically from `PureEase`. Reason: no core API until a use shows it is generally useful.
- **Preserved velocity is measured from the running ease.** See CORE-ANIMATABLE.

## Non-goals

- No velocity readout of the ease; callers differentiate `PureEase` numerically.
- No string syntax (CORE-EASEPARSER).
- No re-derivation of bounce and duration from mass or velocity.

## Open items

- In seconds mode, a physically configured spring's `OnComplete` never fires and a bounce-only spring derives from a 10 s duration, because the default perceived duration 628 is not scaled (upstream parity).
- The two-way derivation is meant to keep physical and designer settings in step, but setting `Mass` or `Velocity` leaves `Bounce` and `Duration` stale, and setting `Bounce` or `Duration` silently resets `Mass` and `Velocity` (upstream parity; the XML docs mention neither).
- `Duration` and `Bounce` of a spring built from physical arguments stay at 628 and 0.5, not the values its parameters imply.
- Untested: the stiffness/damping → bounce/duration derivation (009, 010), the natural-frequency clamp (014), `PureEase` leaving completion alone (028), `Parent` assignment by an animation (030), and time-unit behavior beyond `SettlingDuration` (033, 034).
