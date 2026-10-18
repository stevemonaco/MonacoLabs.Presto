---
id: CORE-SPRING
title: Spring
package: MonacoLabs.Presto
namespaces:
  - MonacoLabs.Presto
sources:
  - MonacoLabs.Presto/Spring.cs
  - MonacoLabs.Presto/Eases.cs
  - MonacoLabs.Presto/Springs.cs
types:
  - Spring
  - Springs
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

A damped-harmonic-oscillator ease whose length is computed, not supplied: the solver finds when the spring comes to rest and that `SettlingDuration` becomes the duration of any tween eased by it. It is configured either physically (mass, stiffness, damping, velocity) or perceptually (bounce, duration), each side kept in step with the other. It is an ease (CORE-EASES); animations adopt its duration (CORE-ANIMATION), and `Animatable` and `Inertia` sample its `PureEase`. `Springs` names four ready-made springs (`Gentle`, `Snappy`, `Bouncy`, `Stiff`). Also specifies `Eases.Spring`.

## Requirements

### Construction

- **CORE-SPRING-001** — A spring constructed without arguments shall have mass 1, stiffness 100, damping 10 and velocity 0, and report the bounce 0.5 and duration 628.319 ms (0.628 s in seconds mode) those imply (CORE-SPRING-009).
  - Tests: `EaseFixtureTests.SpringMatchesUpstream`, `SpringTests.TheDefaultDurationFollowsTheTimeUnit`
- **CORE-SPRING-002** — The constructor shall clamp mass to [1, 10000], stiffness and damping to [1e-11, 10000], velocity to [−10000, 10000], bounce to [−1, 1] and duration to [10, 10000] times the time scale.
  - Tests: `SpringTests.SpringParametersMustBeClampedAt10000`, `SpringTests.SpringVelocityCanBeNegative`
- **CORE-SPRING-003** — When `bounce` or `duration` is given, the constructor shall derive stiffness and damping from them for the given (or default) mass, keeping the given mass and velocity and discarding any stiffness and damping arguments; a missing one of the two takes its default, 0.5 or 628 times the time scale.
  - Tests: `EaseFixtureTests.SpringMatchesUpstream`, `SpringTests.PhysicalAndDesignerFormsRoundTrip`
- **CORE-SPRING-004** — When neither `bounce` nor `duration` is given, the constructor shall derive `Bounce` and `Duration` from the physical arguments (CORE-SPRING-009).
  - Tests: `SpringTests.PhysicalAndDesignerFormsRoundTrip`, `SpringTests.TheDefaultDurationFollowsTheTimeUnit`
- **CORE-SPRING-005** — `Eases.Spring(...)` shall return a new spring constructed from the same arguments.
  - Tests: `SpringTests.SpringEaseParametersAffectSettlingDuration`

### Two-way derivation

- **CORE-SPRING-006** — Deriving from bounce and duration shall set stiffness to mass·(2π/D)², with D the duration in seconds, clamped to [1e-11, 10000] and rounded to 3 decimals.
  - Tests: `EaseFixtureTests.SpringMatchesUpstream`, `SpringTests.SettingBounceOrDurationKeepsMassAndVelocity`
- **CORE-SPRING-007** — Deriving from bounce and duration shall set damping to mass·(1 − bounce)·4π/D for bounce ≥ 0 and mass·4π/(D·(1 + bounce)) for bounce < 0, so the damping ratio is 1 − bounce or 1/(1 + bounce) at any mass, clamped to [1e-11, 300] and then rounded to 3 decimals.
  - Tests: `EaseFixtureTests.SpringMatchesUpstream`, `SpringTests.BounceSignPicksTheDampingRegime`, `SpringTests.SettingBounceOrDurationKeepsMassAndVelocity`
- **CORE-SPRING-008** — When `Bounce` or `Duration` is set, the spring shall clamp it, re-derive stiffness and damping for the current mass, leave mass and velocity as set, and recompute.
  - Tests: `SpringTests.SettingBounceAfterConstructionRederivesStiffnessAndDamping`, `SpringTests.SettingBounceOrDurationKeepsMassAndVelocity`
- **CORE-SPRING-009** — When `Mass`, `Stiffness` or `Damping` is set, the spring shall clamp it, re-derive `Duration` as 2π·√(mass/stiffness) (in the engine unit) and `Bounce` as 1 − ζ when ζ ≤ 1, else 1/ζ − 1, with ζ = damping/(2√(stiffness·mass)), each clamped and rounded to 3 decimals, and recompute.
  - Tests: `SpringTests.SettingMassRederivesBounceAndDuration`, `SpringTests.PhysicalAndDesignerFormsRoundTrip`
- ~~**CORE-SPRING-010**~~ — Removed: the derivation from stiffness and damping now uses the current mass (CORE-SPRING-009; see "Spring settings stay in step").
- **CORE-SPRING-011** — When `Velocity` is set, the spring shall clamp it, re-derive `Bounce` and `Duration` from stiffness, damping and mass as CORE-SPRING-009 does (velocity enters neither formula), and recompute.
  - Tests: `SpringTests.SettingASpringParameterAfterCreationShouldUpdateItsDuration`, `SpringTests.SettingBounceOrDurationKeepsMassAndVelocity`
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
  - Tests: `SpringTests.SamplingThePureEaseLeavesCompletionAlone`
- **CORE-SPRING-029** — `Parent` shall be null until an animation adopts the spring.
  - Tests: `SpringTests.OnCompleteFiresOnceWhenSampledPastThePerceivedDuration`
- **CORE-SPRING-030** — When a spring is an animation's own `Ease`, the animation shall set `Parent` to itself; a spring on a tween or returned per target is not parented.
  - Tests: `SpringTests.OnlyAnAnimationsOwnSpringIsParentedToIt`

### Driving durations

- **CORE-SPRING-031** — When a spring is an animation's `Ease`, every tween's duration shall be its `SettlingDuration`, overriding the animation's `Duration`.
  - Tests: `SpringTests.SpringEaseOverridesTheAnimationDurationParameter`, `SpringTests.SpringEaseParametersAffectTheAnimationDuration`
- **CORE-SPRING-032** — When a spring is a tween's `Ease`, that tween's duration shall be its `SettlingDuration`, overriding both the tween's and the animation's `Duration`.
  - Tests: `SpringTests.SpringEaseOverridesTheTweenDurationParameter`
- **CORE-SPRING-033** — A spring shall read the process-wide time scale (CORE-ENGINE) when it computes, so switching the time unit later does not rescale existing springs.
  - Tests: `SpringTests.SwitchingTheTimeUnitDoesNotRescaleAnExistingSpring`
- **CORE-SPRING-034** — In seconds mode, `Duration` shall be read and derived in seconds.
  - Tests: `SpringTests.TheDefaultDurationFollowsTheTimeUnit`, `SpringTests.PresetsKeepTheirPerceivedDurationInSecondsMode`

### Presets

- **CORE-SPRING-036** — `Springs.Gentle` shall be a critically damped spring (bounce 0) over 500 ms, `Snappy` bounce 0.15 over 300 ms, `Bouncy` bounce 0.35 over 500 ms and `Stiff` bounce 0 over 250 ms, each duration converted to the engine's time unit when the preset is read.
  - Tests: `SpringTests.PresetsHaveTheirBounceAndDurationAndAreNewOnEachRead`, `SpringTests.PresetsKeepTheirPerceivedDurationInSecondsMode`
- **CORE-SPRING-037** — Each read of a preset shall return a new `Spring`, since a spring carries completion state.
  - Tests: `SpringTests.PresetsHaveTheirBounceAndDurationAndAreNewOnEachRead`

### Defaults

- **CORE-SPRING-035** — When `onComplete` is not given, the spring shall invoke nothing when it completes.
  - Tests: `SpringTests.ASpringWithoutOnCompleteCompletesSilently`

## Invariants

- `SettlingDuration`, `SolverDuration` and `DampingRatio` always match the current parameters.
- `Ease(0)` = `PureEase(0)` = 0 and `Ease(1)` = `PureEase(1)` = 1 exactly.
- Mass ≥ 1; stiffness > 0; damping ≥ 0 (a derived damping can round to 0).

## Edge cases

- `bounce: 1` derives damping 0 after rounding: the spring never settles and `SettlingDuration` is 60000 ms.
- `bounce: -1` divides by zero; the infinite damping clamps to 300.
- `Ease` and `PureEase` do not clamp t; past 1 they keep solving the curve.
- Passing `stiffness` or `damping` with `bounce` or `duration` silently discards the stiffness and damping.
- Clamping and rounding can leave the two views slightly apart: a bounce of −1 clamps damping to 300, and a set `Bounce` or `Duration` is kept as given, not re-derived from the rounded stiffness and damping.

## Threading and lifetime

- A spring is mutable and not thread-safe. `Ease` changes `Completed` and invokes `OnComplete`, so it belongs on the thread that drives its animation; `PureEase` is pure and safe for any thread once no setter runs.
- `Parent` holds the adopting animation until another adopts the spring; one spring shared by several animations reports only the last.

## Reduced motion

- Not affected by the spring itself. `ReduceAll` gives spring-eased tweens a minimal duration in the animation (CORE-REDUCED).

## Allocation

- Each preset read allocates a `Spring` and runs the settling search; read presets when building, not per frame.
- Calling `Ease` or `PureEase` allocates nothing.
- Every constructor and setter runs the settling search, up to 3001 solver steps; parameter changes are not meant for the tick path.

## Provenance

- `PureEase` has no upstream; it serves samplers that must not raise `OnComplete`, such as velocity measurement.
- `DampingRatio` has no upstream; it is public so a caller can tell the damping regime without a velocity API on the ease (No core velocity API for an ease).
- Parameters are nullable constructor arguments instead of a params object; `OnComplete` may be null instead of `noop`.
- The deprecated `createSpring()` is not ported.
- `Springs` has no upstream: anime.js names no spring presets.
- Both derivations take mass into account and keep mass and velocity, and a physically configured spring reports its implied bounce and duration, where upstream assumes mass 1, resets mass and velocity, and leaves bounce 0.5 and duration 628 (see "Spring settings stay in step").
- The default perceived duration is scaled by the time unit, where upstream leaves 628 unscaled in seconds mode (see "Preset and default durations follow the time unit").

## Decisions

- **No core velocity API for an ease; `DampingRatio` is public.** Callers that need the ease's velocity compute it numerically from `PureEase`. Reason: no core API until a use shows it is generally useful.
- **Preserved velocity is measured from the running ease.** See CORE-ANIMATABLE.
- **Spring presets live in the core, fresh per read.** `Springs` is a static class in `MonacoLabs.Presto`, each read returning a new `Spring`; `Eases.Spring` stays the way to build a custom one. Reason: a preset is just a `Spring`, a core type, and a `Spring` carries completion state, so it cannot be shared. Replaces BRIDGE-EASES's `Spring presets live in the bridge, fresh per read`, whose reasons (completion state, use from XAML) did not depend on where the class lives.
- **Spring settings stay in step.** Setting `Mass`, `Stiffness`, `Damping` or `Velocity` re-derives `Bounce` and `Duration`; setting `Bounce` or `Duration` re-derives `Stiffness` and `Damping` for the current `Mass`, leaving `Mass` and `Velocity` as set; a spring built from physical arguments reports the `Bounce` and `Duration` they imply. Both derivations use ω0 = √(stiffness/mass) and ζ = damping/(2√(stiffness·mass)), so at mass 1 they match upstream exactly. `Velocity` enters neither formula, so setting it only re-snaps `Bounce` and `Duration` to what the rounded stiffness and damping imply. Reason: the physical and designer settings are two views of one spring, and a view that goes stale or resets the other misreports it. Rejected: documenting upstream's one-way derivation.
- **Preset and default durations follow the time unit.** A preset names a perceived duration in milliseconds and converts it on each read, so `Springs.Gentle` feels the same in seconds mode instead of clamping to 10 s, and the default 628 is scaled the same way, to 0.628 s. Reason: an unscaled preset made the default `Timer.Settle` crawl in seconds mode, and the unscaled default clamped to 10 s, so a physically configured spring's `OnComplete` never fired and a bounce-only spring derived from a 10 s duration. Rejected: upstream's unscaled 628.

## Non-goals

- No velocity readout of the ease; callers differentiate `PureEase` numerically.
- No string syntax (CORE-EASEPARSER).

## Open items

- Untested: the natural-frequency clamp (014), which the parameter clamps keep out of reach: √(stiffness/mass) always lies within [√(1e-15), 100].
