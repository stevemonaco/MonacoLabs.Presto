---
id: CORE-PARTICLES
title: Particle simulation
package: MonacoLabs.Presto
namespaces:
  - MonacoLabs.Presto
sources:
  - MonacoLabs.Presto/ParticleSystem.cs
  - MonacoLabs.Presto/EmitterParams.cs
  - MonacoLabs.Presto/ParticleState.cs
  - MonacoLabs.Presto/ParticleCurve.cs
types:
  - ParticleSystem
  - EmitterParams
  - ParticleState
  - ParticleCurve
tests:
  - ParticleSystemTests
  - AllocationTests
upstream: []
depends:
  - CORE-EASES
  - CORE-COLORS
  - CORE-UTILS
---

# Particle simulation

## Purpose

`ParticleSystem` is a pooled particle emitter whose particles are integrated by `Step(dt)` rather than tweened, so particles can be spawned and retired every frame without allocating. `EmitterParams` describes how particles spawn, move and change over their lives; over-life values are `ParticleCurve`s shaped by Presto eases; each live particle is a `ParticleState` a renderer reads. The core only simulates; drawing, scene-wide caps and the confetti/sparkle/burst recipes are the bridge's (see BRIDGE-PARTICLES).

## Requirements

### Emission

- **CORE-PARTICLES-001** — A new system shall be stopped, with no live particles and `IsFinished` true.
  - Tests: `ParticleSystemTests.ANewSystemIsStoppedAndFinished`
- **CORE-PARTICLES-002** — `Start` shall spawn `Burst` particles at once.
  - Tests: `ParticleSystemTests.BurstSpawnsItsCount`
- **CORE-PARTICLES-003** — When `Rate` is positive, `Start` shall begin emitting `Rate` particles per second, accumulating fractional particles across steps.
  - Tests: `ParticleSystemTests.RateEmitsForDurationAccumulatingFractions`
- **CORE-PARTICLES-004** — When `Duration` is positive, emission shall stop once that much time has been stepped; when it is 0, emission shall continue until `Stop`.
  - Tests: `ParticleSystemTests.RateEmitsForDurationAccumulatingFractions`, `ParticleSystemTests.StopEndsEmissionButLetsLiveParticlesFinish`
- **CORE-PARTICLES-005** (inherited) — The last step of a bounded emission shall emit only for the part of the step inside `Duration`.
  - Tests: `ParticleSystemTests.RateEmitsForDurationAccumulatingFractions`
- **CORE-PARTICLES-006** (inherited) — `Start` on a running or finished system shall restart the emission clock and spawn another burst, leaving live particles alive.
  - Tests: `ParticleSystemTests.ParticleDiesAtItsLifetimeAndItsSlotIsReused`
- **CORE-PARTICLES-007** — `Stop` shall end emission and let the live particles finish their lives.
  - Tests: `ParticleSystemTests.StopEndsEmissionButLetsLiveParticlesFinish`
- **CORE-PARTICLES-008** — `Clear` shall remove every live particle at once and leave emission as it was.
  - Tests: `ParticleSystemTests.ClearRemovesLiveParticlesAtOnce`
- **CORE-PARTICLES-009** — `IsFinished` shall be true exactly when emission is over and no particle is live.
  - Tests: `ParticleSystemTests.IsFinishedOnceEmissionStoppedAndNoneIsLive`, `ParticleSystemTests.ClearRemovesLiveParticlesAtOnce`

### Spawning

- **CORE-PARTICLES-010** — Each spawned particle shall start at the origin (`X`, `Y` at spawn time) offset by a uniform draw within `Area` centered on it.
  - Tests: `ParticleSystemTests.WithoutForcesParticleTravelsSpeedTimesTimeAlongItsAngle`, `ParticleSystemTests.ParticlesSpawnUniformlyWithinTheAreaCenteredOnTheOrigin`
- **CORE-PARTICLES-011** — Each particle shall launch at `Angle ± Spread` degrees (0 right, positive clockwise with Y down) with a speed drawn from `Speed`.
  - Tests: `ParticleSystemTests.WithoutForcesParticleTravelsSpeedTimesTimeAlongItsAngle`
- **CORE-PARTICLES-012** — Each particle shall draw its lifetime from `Lifetime`, its spin from `Spin`, and its base color uniformly from `Colors`.
  - Tests: `ParticleSystemTests.ColorMixesTowardEndColor`
- **CORE-PARTICLES-013** — A spinning particle shall start at a random rotation and a non-spinning one at 0; every particle shall start at a random flutter phase.
  - Tests: `ParticleSystemTests.SpinningParticlesStartAtARandomRotationAndEveryParticleAtARandomFlutterPhase`
- **CORE-PARTICLES-014** — A spawned particle shall be sampled at age 0, so its size, opacity and color are the curves' start values.
  - Tests: `ParticleSystemTests.CurvesSampleFromEasedMidpointAndTo`

### Caps and pooling

- **CORE-PARTICLES-015** — The system shall never hold more than `MaxParticles` live particles; spawns over the cap shall be dropped, not queued.
  - Tests: `ParticleSystemTests.NeverExceedsMaxParticles`
- **CORE-PARTICLES-016** — While the scene-wide spawn budget the bridge sets is exhausted, spawns shall be dropped; each spawn spends one.
  - Tests: `ParticleSystemTests.SpawnBudgetDropsSpawns`
- **CORE-PARTICLES-017** — A particle shall die when its age reaches its lifetime, and its slot shall be reused by later spawns.
  - Tests: `ParticleSystemTests.ParticleDiesAtItsLifetimeAndItsSlotIsReused`
- **CORE-PARTICLES-018** — `Particles` shall expose exactly the `Live` particles.
  - Tests: `ParticleSystemTests.BurstSpawnsItsCount`

### Motion

- **CORE-PARTICLES-019** (inherited) — `Step(dt)` shall age every live particle by `dt` ms, retire those past their lifetime, integrate the rest, then emit.
  - Tests: `ParticleSystemTests.ParticleDiesAtItsLifetimeAndItsSlotIsReused`, `ParticleSystemTests.IsFinishedOnceEmissionStoppedAndNoneIsLive`
- **CORE-PARTICLES-020** — Without forces, a particle shall travel `speed × time` along its launch angle.
  - Tests: `ParticleSystemTests.WithoutForcesParticleTravelsSpeedTimesTimeAlongItsAngle`
- **CORE-PARTICLES-021** — Gravity shall follow the closed-form parabola exactly at any step size.
  - Tests: `ParticleSystemTests.GravityFollowsTheClosedFormParabola`
- **CORE-PARTICLES-022** — Drag shall decay velocity by `e^(−Drag · seconds)`, independent of the step size.
  - Tests: `ParticleSystemTests.DragHalvesSpeedAfterLn2OverDragSecondsAtAnyStepSize`
- **CORE-PARTICLES-023** — Rotation shall advance by `Spin × dt`, and the flutter phase by `Flutter × 2π` radians per second.
  - Tests: `ParticleSystemTests.RotationAdvancesBySpinAndFlutterPhaseByFlutterTurnsPerSecond`

### Over-life curves

- **CORE-PARTICLES-024** — On every step, each live particle's `Size` and `Opacity` shall be the `Size` and `Opacity` curves at its normalized age.
  - Tests: `ParticleSystemTests.CurvesSampleFromEasedMidpointAndTo`
- **CORE-PARTICLES-025** — `ParticleCurve.At(age)` shall return `From + (To − From) × ease(age)`, with the ease resolved once when set and linear when unset; a spring's settling duration is ignored.
  - Tests: `ParticleSystemTests.CurvesSampleFromEasedMidpointAndTo`
- **CORE-PARTICLES-026** — When both `ColorMix` and `EndColor` are set, a particle's `Color` shall be its `BaseColor` mixed channel by channel, alpha included, toward `EndColor` by `ColorMix` at its age; otherwise `Color` shall be `BaseColor`.
  - Tests: `ParticleSystemTests.ColorMixesTowardEndColor`

### Determinism

- **CORE-PARTICLES-027** — Every random draw shall come from `Utils.CreateSeededRandom(Seed)`, so two systems with the same parameters, seed and step sequence produce identical states, and different seeds differ.
  - Tests: `ParticleSystemTests.SameSeedAndStepsGiveIdenticalStates`
- **CORE-PARTICLES-028** (inherited) — When `Seed` is null, the system shall take the next seed from the process-wide seeded-random counter.
  - Tests: `ParticleSystemTests.ANullSeedTakesTheNextSeedFromTheCounter`

### Allocation

- **CORE-PARTICLES-029** — `Step`, spawning and retiring shall allocate nothing, with or without diagnostics.
  - Tests: `AllocationTests.ParticleSystemStepsWithoutAllocating`

### Defaults

- **CORE-PARTICLES-030** (inherited) — When `x` and `y` are not given, the system's origin shall be (0, 0).
  - Tests: `ParticleSystemTests.GravityFollowsTheClosedFormParabola`
- **CORE-PARTICLES-031** — When `Rate` is not set, the system shall emit only its burst.
  - Tests: `ParticleSystemTests.ParticleDiesAtItsLifetimeAndItsSlotIsReused`
- **CORE-PARTICLES-032** — When `Burst` is not set, `Start` shall spawn no particles at once.
  - Tests: `ParticleSystemTests.WithoutABurstStartSpawnsNothingAtOnce`
- **CORE-PARTICLES-033** — When `Duration` is not set, emission shall continue until `Stop`.
  - Tests: `ParticleSystemTests.StopEndsEmissionButLetsLiveParticlesFinish`
- **CORE-PARTICLES-034** — When `Lifetime` is not set, each particle shall live between 800 and 1400 ms.
  - Tests: `ParticleSystemTests.DefaultLifetimeSpeedAndSpreadDrawFromTheirRanges`
- **CORE-PARTICLES-035** — When `Speed` is not set, each particle shall launch at between 0.2 and 0.6 units per ms.
  - Tests: `ParticleSystemTests.DefaultLifetimeSpeedAndSpreadDrawFromTheirRanges`
- **CORE-PARTICLES-036** — When `Angle` is not set, particles shall launch around −90 degrees (up).
  - Tests: `ParticleSystemTests.DefaultAngleLaunchesUp`
- **CORE-PARTICLES-037** — When `Spread` is not set, particles shall launch within 60 degrees either side of `Angle`.
  - Tests: `ParticleSystemTests.DefaultLifetimeSpeedAndSpreadDrawFromTheirRanges`
- **CORE-PARTICLES-038** — When `Gravity` and `Drag` are not set, particles shall feel no force.
  - Tests: `ParticleSystemTests.WithoutForcesParticleTravelsSpeedTimesTimeAlongItsAngle`
- **CORE-PARTICLES-039** — When `Spin` is not set, particles shall not rotate.
  - Tests: `ParticleSystemTests.WithoutSpinOrFlutterParticlesNeitherRotateNorFlutter`
- **CORE-PARTICLES-040** — When `Flutter` is not set, the flutter phase shall not advance.
  - Tests: `ParticleSystemTests.WithoutSpinOrFlutterParticlesNeitherRotateNorFlutter`
- **CORE-PARTICLES-041** (inherited) — When `Size` is not set, every particle shall be 6 units for its whole life.
  - Tests: `ParticleSystemTests.DefaultSizeOpacityAndColorAreSixUnitsLinearFadeAndOpaqueWhite`
- **CORE-PARTICLES-042** (inherited) — When `Opacity` is not set, every particle shall fade linearly from 1 to 0 over its life.
  - Tests: `ParticleSystemTests.DefaultSizeOpacityAndColorAreSixUnitsLinearFadeAndOpaqueWhite`
- **CORE-PARTICLES-043** (inherited) — When `Colors` is not set, every particle's base color shall be opaque white.
  - Tests: `ParticleSystemTests.DefaultSizeOpacityAndColorAreSixUnitsLinearFadeAndOpaqueWhite`
- **CORE-PARTICLES-044** — When `MaxParticles` is not set, the system shall hold at most 500 live particles.
  - Tests: `ParticleSystemTests.DefaultMaxParticlesIsFiveHundred`
- **CORE-PARTICLES-045** (inherited) — When `Area` is not set, every particle shall spawn exactly at the origin.
  - Tests: `ParticleSystemTests.WithoutForcesParticleTravelsSpeedTimesTimeAlongItsAngle`

### Validation

- **CORE-PARTICLES-046** — The constructor shall throw `ArgumentOutOfRangeException` when `MaxParticles` is negative, when `Lifetime.Min` is not above 0 (NaN included), or when `Lifetime.Max` is not at least `Lifetime.Min`; a `MaxParticles` of 0 is valid and never spawns.
  - Tests: `ParticleSystemTests.ConstructorRejectsNegativeMaxParticlesAndNonPositiveOrInvertedLifetime`
- **CORE-PARTICLES-047** — `Step` shall throw `ArgumentOutOfRangeException` for a negative `dt`; a `dt` of 0 is valid.
  - Tests: `ParticleSystemTests.StepRejectsNegativeDtAndAcceptsZero`

## Invariants

- `0 ≤ Live ≤ MaxParticles`, and `Particles.Length == Live`.
- Every live particle has `Age < Lifetime` after a step.
- The pool array is allocated once, at construction, sized to `MaxParticles`.

## Edge cases

- Particles emitted during a step are spawned at its end with age 0, so emission times are quantized to steps; only gravity and drag are step-size exact.
- `Rate` 0 with `Duration` set emits only the burst.
- `MaxParticles` 0 never spawns.
- A `dt` of 0 resamples without moving.
- Changing `X`/`Y` moves only later spawns.

## Threading and lifetime

- Not thread-safe and not tied to an engine: whoever owns it steps it, on one thread (the bridge steps it on its render thread; see BRIDGE-PARTICLES).
- Holds no references beyond its parameters; it is collected with its owner.

## Reduced motion

- Not affected in the core: `ReducedMotion` and `ReduceAll` do not reach a `ParticleSystem`. The bridge's recipes spawn nothing under reduced motion (see BRIDGE-PARTICLES).

## Allocation

- Allocation-free stepping (CORE-PARTICLES-029). The constructor allocates the pool and the seeded random generator; `ParticleCurve` resolves its ease when set.

## Provenance

- No upstream counterpart.

## Decisions

- **Emitted particles are simulated, not tweened.** Particles are integrated directly: velocity and gravity step with position taking half a step of gravity, so gravity is exact at any step size; drag is exponential (`v *= e^(−drag·dt)`), spin is a constant rate drawn per particle, and size, opacity and color over life are `from → to` curves shaped by an ease at normalized age. Reason: tweens suit a fixed population but allocate an animation and tweens per spawn, while an emitter spawns and retires particles every frame; exponential drag is frame-rate independent; plain semi-implicit Euler drifts from the parabola by 0.5·g·t·dt (it was the first design, replaced for that reason, and a spin-rate over-life curve was dropped as built). Rejected: tweening each particle.
- **Simulation in the core, drawing in the bridge.** `ParticleSystem` and `EmitterParams` are UI-framework-free core types (colors are `PrestoColor`) in the root `MonacoLabs.Presto` namespace, as `Spring` and `VelocityTracker` are. Reason: unit-testable with plain `Step(dt)` calls and usable by a custom renderer, and one `using` still suffices.
- **Particle storage is pooled and allocation-free.** A system owns one preallocated struct array sized to its cap; a spawn takes a dead slot and a death swaps with the last live slot, so steady-state stepping allocates nothing. Reason: emitters spawn and retire particles every frame, on the hot path.
- **Randomness is deterministic and seeded.** Every random draw comes from `Utils.CreateSeededRandom(Seed)`, so the same seed and `dt` sequence give identical states. A null `Seed` takes the next value of the process-wide seed counter, so unseeded systems are deterministic per process order rather than freshly seeded. Reason: not recorded (it makes a seeded burst testable and replayable from the start).
- **Particles are not seekable; pause is per effect.** See BRIDGE-PARTICLES.
- **Reduced motion emits nothing; hand-built scenes are exempt.** See BRIDGE-PARTICLES.
- **Particle caps drop over-cap spawns.** See BRIDGE-PARTICLES.

## Non-goals

- Emitted particles cannot be sought or reversed; a seeded system can be replayed from the start.
- No collisions, attractors or per-particle callbacks.
- No drawing: `Shape` is opaque to the core.

## Open items

- None.
