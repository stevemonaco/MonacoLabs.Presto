---
id: CORE-ANIMATABLE
title: Animatable
package: MonacoLabs.Presto
namespaces:
  - MonacoLabs.Presto
sources:
  - MonacoLabs.Presto/Animatable.cs
  - MonacoLabs.Presto/AnimatableParams.cs
  - MonacoLabs.Presto/AnimatableProperty.cs
  - MonacoLabs.Presto/Presto.cs
types:
  - Animatable
  - AnimatableParams
  - AnimatableProperty
tests:
  - AnimatablesTests
  - DiscreteValuesTests
  - PremultipliedColorTests
  - MotionValueDynamicsTests
  - ScopeTests
  - EngineDiagnosticsTests
  - AllocationTests
upstream:
  - src/animatable/animatable.js
  - tests/suites/animatables.test.js
depends:
  - CORE-ANIMATION
  - CORE-TIMER
  - CORE-SPRING
  - CORE-INERTIA
  - CORE-MOTIONVALUE
  - CORE-COMPOSITION
  - CORE-TARGETS
  - CORE-SCOPE
---

# Animatable

## Purpose

An `Animatable` holds one paused `Animation` per declared property and retargets it imperatively: `Set("X", 100)` aims the property's tween from wherever it is now at the new value and resumes it, so a stream of values (a cursor, a scroll offset) is followed with that property's own duration and ease. Initially ported from anime.js `createAnimatable`, it is extended with opt-in spring momentum (`PreserveVelocity`) and inertial coasting (`Fling`). The `AnimatableParams` indexer and every method that names a property take a `PropertyKey` (CORE-TARGETS). It builds on CORE-ANIMATION and is the engine of the bridge's followers and draggables. Also specifies `Presto.CreateAnimatable`.

## Requirements

### Construction

- **CORE-ANIMATABLE-001** — When an animatable is created, it shall build one paused animation per declared property, in declaration order, each holding that property's tweens over all the targets.
  - Tests: `AnimatablesTests.GetAndAnimateAnimatableValues`, `AnimatablesTests.StaggersAnimatableDurationsPerTarget`
- **CORE-ANIMATABLE-002** — Each property animation shall start and end on the target's current value, so creating an animatable moves nothing.
  - Tests: `AnimatablesTests.GetAndAnimateAnimatableValues`, `AnimatablesTests.GetAndAnimateAnimatableComplexValues`
- **CORE-ANIMATABLE-003** — When a property declares a `Unit`, the animatable shall convert the target's current value into that unit and animate and write it in that unit.
  - Tests: `AnimatablesTests.DefinesCustomUnitsForAnimatableValues`
- **CORE-ANIMATABLE-004** — A bare number assigned to a property shall be that property's duration.
  - Tests: `AnimatablesTests.GetAndAnimateAnimatableValues`
- **CORE-ANIMATABLE-005** — A property's own `Duration`, `Ease` and `Modifier` shall win over the ones on `AnimatableParams`, which in turn win over the engine defaults.
  - Tests: `AnimatablesTests.DefinesCustomUnitsForAnimatableValues`
- **CORE-ANIMATABLE-006** — When a property's `Duration` is a function value such as a stagger, each target's tween shall take its own resolved duration and the animation the longest.
  - Tests: `AnimatablesTests.StaggersAnimatableDurationsPerTarget`
- **CORE-ANIMATABLE-007** — The property animations shall compose by `Composition.Replace` unless the property sets its own `Composition`.
  - Tests: untested
- **CORE-ANIMATABLE-008** — `PlaybackRate` and `FrameRate` shall be passed to every property animation.
  - Tests: untested
- **CORE-ANIMATABLE-009** — When the animatable is created inside a scope, it shall register with the current scope, so reverting the scope reverts it.
  - Tests: `ScopeTests.RevertsEverythingCreatedInside`
- **CORE-ANIMATABLE-010** — While engine diagnostics are enabled, a created animatable shall be listed in `EngineDiagnostics.Animatables` (see CORE-DIAGNOSTICS).
  - Tests: `EngineDiagnosticsTests.DisablingClearsEverything`
- **CORE-ANIMATABLE-011** — `Targets` shall list the targets the first property animation resolved, shared by every property.
  - Tests: `AnimatablesTests.RevertAnAnimatable`

### Reading

- **CORE-ANIMATABLE-012** — `Get` shall return the first tween's current number passed through the property's modifier.
  - Tests: `AnimatablesTests.GetAndSetAnimatableValues`, `AnimatablesTests.DefinesCustomUnitsForAnimatableValues`
- **CORE-ANIMATABLE-013** — `GetChannels` shall return the first tween's current channels for a color or complex property, and an empty array for a scalar one.
  - Tests: `AnimatablesTests.GetAndAnimateAnimatableComplexValues`
- **CORE-ANIMATABLE-014** — `Get`, `GetChannels`, `Set`, `Fling` and `ValueOf` shall resolve the property key's name to its canonical spelling on the first target, so a property declared by one spelling is read and set by any.
  - Tests: `PropertyNamingTests.AnAnimatableDeclaredByNameIsReadAndSetByHandle`
- **CORE-ANIMATABLE-015** (inherited) — When the property is not declared, `Get` shall return 0, `GetChannels` an empty array, and `Set` and `Fling` shall do nothing and return the animatable.
  - Tests: untested
- **CORE-ANIMATABLE-016** — `ValueOf` shall return a new `MotionValue` that starts at `Get(property)` and is assigned `Get(property)` every time the property's animation renders.
  - Tests: `MotionValueDynamicsTests.AnimatableValueOfFollowsSet`

### Set

- **CORE-ANIMATABLE-017** — When `Set` is called with a number, every tween of the property shall restart from its current modified value towards `to`, and the animation shall be reset and resumed.
  - Tests: `AnimatablesTests.GetAndAnimateAnimatableValues`
- **CORE-ANIMATABLE-018** — When the property's duration is 0, `Set` shall write the new value before it returns.
  - Tests: `AnimatablesTests.GetAndSetAnimatableValues`
- **CORE-ANIMATABLE-019** — When `Set` is given a `duration`, the animatable shall stretch the property animation to it; the stretched duration stays for later calls that pass none.
  - Tests: `AnimatablesTests.OverrideAnAnimatableDurationAndEaseOnSet`, `AnimatablesTests.RetargetingAZeroDurationPropertyLongAfterTheEngineStartedStillEases`
- **CORE-ANIMATABLE-020** — When `Set` is given an `ease`, the animatable shall resolve it once per tween and use it for this and later calls that pass none.
  - Tests: `AnimatablesTests.OverrideAnAnimatableDurationAndEaseOnSet`
- **CORE-ANIMATABLE-021** — `Set` shall return the animatable, so calls chain.
  - Tests: `AnimatablesTests.GetAndSetAnimatableValues`
- **CORE-ANIMATABLE-022** — When the property is discrete, `Set` shall coerce `to` to the property's value type and set both ends of the tween to it, so the property snaps.
  - Tests: `DiscreteValuesTests.AnAnimatableRetargetSnaps`
- **CORE-ANIMATABLE-023** — When `Set` is called with a channel list, each tween channel with an index below both the list's and the tween's channel count shall restart from its current modified value towards the given one; the remaining channels keep their ends.
  - Tests: `AnimatablesTests.GetAndAnimateAnimatableComplexValues`
- **CORE-ANIMATABLE-024** — A retarget long after the engine started shall still ease every frame after stretching from a zero duration (see CORE-TIMER, "Stretch keeps a root timer's offset").
  - Tests: `AnimatablesTests.RetargetingAZeroDurationPropertyLongAfterTheEngineStartedStillEases`
- **CORE-ANIMATABLE-054** — When the property's duration is 0, `Get` shall return the new value as soon as `Set` returns.
  - Tests: `AnimatablesTests.GetAndSetAnimatableValues`

### Callbacks

- **CORE-ANIMATABLE-025** — The animatable's callbacks shall run on one animation of their own that spans the whole burst, not on the property animations.
  - Tests: `AnimatablesTests.AnimatableOnBeginCallbacks`
- **CORE-ANIMATABLE-026** — When a property animation begins and the callbacks animation has completed, the animatable shall reset and replay it, so `OnBegin` fires once per burst.
  - Tests: `AnimatablesTests.AnimatableOnBeginCallbacks`
- **CORE-ANIMATABLE-027** — While the callbacks animation is playing, `OnUpdate` shall be raised on its frames.
  - Tests: `AnimatablesTests.AnimatableOnUpdateCallbacks`
- **CORE-ANIMATABLE-028** — When a property animation completes or pauses and every property animation is paused, the animatable shall complete the callbacks animation, raising `OnComplete` once.
  - Tests: `AnimatablesTests.AnimatableOnCompleteCallbacks`
- **CORE-ANIMATABLE-029** — Each callback shall receive the animatable.
  - Tests: untested

### PreserveVelocity

- **CORE-ANIMATABLE-030** — A property shall keep its momentum only when `PreserveVelocity` is true on the property, or on the params with the property leaving it null.
  - Tests: `AnimatablesTests.ARetargetedSpringKeepsItsMomentumWhenVelocityIsPreserved`, `AnimatablesTests.ARetargetedSpringStartsFromRestWithoutPreservedVelocity`
- **CORE-ANIMATABLE-031** — When a momentum property is retargeted and its spring (the `Set` ease when that is a `Spring`, otherwise the declared ease when that is one) exists, each tween shall get a copy of the spring with the same mass, stiffness and damping whose initial velocity is the tween's current velocity.
  - Tests: `AnimatablesTests.ARetargetedSpringKeepsItsMomentumWhenVelocityIsPreserved`, `AnimatablesTests.ASpringPassedToSetCarriesTheMomentumOfTheDeclaredOne`, `AnimatablesTests.AMomentumSpringCarriesOnPastWhereItTurnedBeforeHeadingToItsNewTarget`
- **CORE-ANIMATABLE-032** — When a momentum property's `Set` ease is not a spring, or neither ease is one, `Set` shall retarget it as a plain `Set` does.
  - Tests: `AnimatablesTests.PreservedVelocityIgnoresAPropertyThatIsNotSpringEased`, `AnimatablesTests.PreservedVelocityIgnoresChannelsThatAreNotSpringEased`
- **CORE-ANIMATABLE-033** — When the property animation is paused or settled, or the retarget is to the current value, the thrown spring shall start at rest.
  - Tests: `AnimatablesTests.RetargetingASettledMomentumSpringStartsFromRest`, `AnimatablesTests.RetargetingSettledChannelsStartsFromRest`
- **CORE-ANIMATABLE-034** — The velocity shall be measured from the running ease without raising a spring's `OnComplete`.
  - Tests: `AnimatablesTests.AMomentumSpringSettlesOnItsTargetAndCompletesOnce`
- **CORE-ANIMATABLE-035** (inherited) — Repeated retargets within one frame shall keep the momentum measured before them.
  - Tests: `AnimatablesTests.RepeatedRetargetsWithinOneFrameKeepTheMomentum`
- **CORE-ANIMATABLE-036** — A momentum retarget shall set the animation's duration to the longest settling duration of its thrown springs, overriding the property's declared duration, unless `Set` is given a duration, which compresses the springs and their velocity with them.
  - Tests: `AnimatablesTests.AMomentumSpringSettlesOnItsTargetAndCompletesOnce`
- **CORE-ANIMATABLE-037** — When the thrown springs of a property settle at different times, every tween shall be sampled over the longest one's solver span, and each shall end exactly on its target.
  - Tests: `AnimatablesTests.AMomentumSpringSettlesOnItsTargetAndCompletesOnce`
- **CORE-ANIMATABLE-038** — When a momentum property is retargeted with a channel list, each channel of each tween shall get its own thrown spring carrying that channel's velocity.
  - Tests: `AnimatablesTests.AChannelRetargetKeepsEachComplexChannelsMomentum`, `AnimatablesTests.AChannelRetargetKeepsEachColorChannelsMomentum`
- **CORE-ANIMATABLE-039** — A color's RGB channels shall carry their velocity in the squared, premultiplied space the renderer blends them in, so a retarget between alphas continues without a jump in value or velocity.
  - Tests: `PremultipliedColorTests.RetargetingAChannelFadeBetweenAlphasCarriesOnWithoutAJump`, `AnimatablesTests.AChannelRetargetKeepsEachColorChannelsMomentum`
- **CORE-ANIMATABLE-040** — A channel that does not move in a momentum channel retarget shall stay exactly at its value for the whole run.
  - Tests: `AnimatablesTests.ChannelsThatDoNotMoveStayExactlyAtRest`
- **CORE-ANIMATABLE-041** (inherited) — A momentum channel retarget shall settle every channel on its target and complete once; with no channel moving, its duration is the declared spring's settling duration.
  - Tests: `AnimatablesTests.ChannelMomentumSettlesOnItsTargetAndCompletesOnce`
- **CORE-ANIMATABLE-042** — Without preserved velocity, a channel retarget shall start every channel from rest.
  - Tests: `AnimatablesTests.ChannelsStartFromRestWithoutPreservedVelocity`

### Fling

- **CORE-ANIMATABLE-043** — `Fling(property, velocity, settings)` shall coast every tween of the property from its current modified value at `velocity` units per engine-time unit, resolved by the inertia solver with `settings` (CORE-INERTIA); `settings.Velocity` is ignored.
  - Tests: `AnimatablesTests.AFlingTravelsPowerTimesVelocityTimesTimeConstant`
- **CORE-ANIMATABLE-044** (inherited) — When `settings` is null, the fling shall use the default `Inertia` settings.
  - Tests: `AnimatablesTests.ASetAfterAFlingUsesTheDeclaredEaseAndDurationAgain`
- **CORE-ANIMATABLE-045** — `Fling(property, settings)` shall coast from each tween's current velocity, or from rest when the property animation is paused; `settings.Velocity` is ignored.
  - Tests: `AnimatablesTests.AFlingWithoutAVelocityContinuesARunningSetAtItsSpeed`, `AnimatablesTests.AFlingWithoutAVelocityIgnoresTheSettingsVelocity`
- **CORE-ANIMATABLE-046** — A fling that crosses a bound shall overshoot it and settle on it.
  - Tests: `AnimatablesTests.AFlingPastABoundSettlesOnIt`
- **CORE-ANIMATABLE-047** — A fling released on a bound moving outward shall go out to a peak and back to the bound within one tween.
  - Tests: `AnimatablesTests.AFlingOutwardFromABoundGoesOutAndBack`
- **CORE-ANIMATABLE-048** — When a property's targets resolve to fling plans of different lengths, the animation shall last the longest and every tween shall be sampled over it, ending on its own plan's end.
  - Tests: untested
- **CORE-ANIMATABLE-049** — The first fling after a `Set` shall save the property's eases and animation duration; the next scalar `Set` shall restore them, using the `Set` arguments where given.
  - Tests: `AnimatablesTests.ASetAfterAFlingUsesTheDeclaredEaseAndDurationAgain`

### Revert

- **CORE-ANIMATABLE-050** — `Revert` shall revert every property animation, restoring each property to the value it held before the animatable ran, revert the callbacks animation, and empty `Targets` and `Animations`.
  - Tests: `AnimatablesTests.RevertAnAnimatable`
- **CORE-ANIMATABLE-051** — After `Revert`, `Set` and `Fling` shall do nothing.
  - Tests: `AnimatablesTests.RevertAFlungAnimatable`
- **CORE-ANIMATABLE-052** (inherited) — After `Revert`, or for a property that was never declared, `Get` shall return 0 and `GetChannels` an empty array.
  - Tests: untested

### Defaults

- **CORE-ANIMATABLE-053** — When `PreserveVelocity` is set on neither the property nor the params, a retargeted spring shall start from rest (see "Spring momentum on retarget is opt-in").
  - Tests: untested

## Invariants

- `Animations` holds exactly one animation per declared property until `Revert`, and none after.
- Every tween of a property animation ends exactly on the value last passed to `Set`, or on the resolved fling target.
- The callbacks animation is playing whenever any property animation is running, except as noted in Open items.

## Edge cases

- A property declared twice by two spellings keeps the last declaration under the canonical key.
- A channel list longer than the property's channels ignores the extra entries; a shorter one leaves the rest unchanged.
- `Set` on a property that is mid-fling discards the fling ease unless the property has preserved velocity, which then measures the fling's velocity.
- A fling whose travel is within `RestDelta` sets the value at once.

## Threading and lifetime

- UI-thread (engine thread) only, like every timer. Created on `Engine.Current`; its animations belong to that engine.
- The animatable's property animations are ordinary top-level animations: each completes and leaves the engine's list when it settles, and is revived by the next `Set`.
- `ValueOf` subscribes the returned value to the animation's render for the animatable's lifetime; there is no unsubscribe.

## Reduced motion

- The property animations are built through `Presto.Animate`, so under `ReduceAll` they are created as setters and a `Set` without a duration snaps. A `Set` with a duration, a momentum retarget and a `Fling` stretch the animation to a real duration and so still animate (untested; see Open items). `WhenReduced` does not apply: an animatable has no builder form.

## Allocation

- A frame of a running property animation allocates nothing, momentum channels included (`AllocationTests.ChannelMomentumTicksWithoutAllocating`).
- `Set` with an ease, momentum retargets and `Fling` allocate closures and springs per call; they are not on the tick path.

## Provenance

- `animatable.x(100)` and `animatable.x()` are `Set("X", 100)` and `Get("X")`/`GetChannels("X")`, because C# has no per-property members (see CORE-BUILDER).
- `PreserveVelocity` is not in anime.js, whose springs always start at rest; momentum carried across a retarget is what makes a spring feel physical (see "Spring momentum on retarget is opt-in").
- `Fling` and `ValueOf` are additions with no upstream counterpart: a value that is already moving keeps coasting, and `ValueOf` exposes the property as a motion value (see "`Fling` retargets with an inertia" and CORE-MOTIONVALUE).
- A discrete property starts with no to-value and snaps on `Set`, because upstream has no discrete values and there is no arithmetic on them (see CORE-TARGETS).
- `Stretch` on the top-level property animation leaves its offset alone, because scaling the creation-time offset lost precision (see CORE-TIMER, "Stretch keeps a root timer's offset").

## Decisions

- **Spring momentum on retarget is opt-in.** `PreserveVelocity` (`bool?` on the params and the property, the property winning, default false) hands the current velocity to a fresh `Spring` with the same mass, stiffness and damping when a spring-eased property is retargeted, in the scalar and the channel `Set` alike; earlier only the scalar `Set` carried it, extended because colors, thicknesses and points keep momentum too. Reason: momentum carried across a retarget is what makes a spring feel physical (Framer Motion, React Spring, Compose); opt-in because it changes what a spring-eased animatable does, and because Draggable drives its release springs through an `Animatable` and sets their velocity itself. Rejected: upstream's reset, where a spring always starts at rest, as the only behavior.
- **Preserved velocity is measured from the running ease.** Only a `Spring` ease (the `Set`'s, the property's or the animatable's) preserves velocity; velocity is a central difference of the running ease, a spring sampled through its `PureEase`, normalized to the solver's progress per second; a paused, completed or unstarted tween, or a retarget to the current value, starts at rest; the animation lasts the longest settling duration (or the `Set` duration compresses them) and an early-settling tween holds its end. Nothing new is public on `Spring`. Reason: sampling through `PureEase` never raises the spring's `OnComplete`; a zero-distance retarget cannot carry velocity in an ease-based model; each tween's settling duration depends on its velocity.
- **`Fling` retargets with an inertia.** `Fling(property, velocity, settings)` coasts the property under the inertia solver; without a velocity it measures the running tween's (0 when paused or finished); the animatable's ease and duration are replaced for that run and restored by the next scalar `Set`; differing per-target durations are sampled over the longest. Reason: a value that is already moving keeps coasting; it mirrors `Set(…, ease)` replacing the ease for one run.
- **`Fling` settings carry no velocity.** Both overloads ignore `Inertia.Velocity`: the velocity is the argument or the property's measured one, and `settings` supplies only the decay, snap and bounds; settings are written `new Inertia { … }`. Reason: one source of velocity per call, and a fallback to `settings.Velocity` at rest would jump between a near-still property (coasts almost nowhere) and a still one (full kick). Rejected: `settings.Velocity` as an at-rest fallback.
- **Out-and-back bounces fold into one ease.** When a fling's inertia becomes a degenerate bounce, `Fling` folds its two parts, out to the spring's first peak and back to the bound, into one ease. Reason: an animatable has one tween per target.
- **A degenerate bounce goes out and back.** See CORE-INERTIA.
- **Property keys are canonicalized, not keyed on accessors.** See CORE-TARGETS.
- **Discrete values reject relative values and `Additive`.** See CORE-TARGETS.
- **The builder is the documented, typed spelling; the indexer stays beside it.** See CORE-BUILDER.
- **Stretch keeps a root timer's offset.** See CORE-TIMER.
- **Colors interpolate with premultiplied alpha.** See CORE-COLORS.

## Non-goals

- No projectile counterpart to `Fling`.
- No typed property keys; names stay strings, because in `Animatable` a bare number is a duration (see CORE-BUILDER).
- No per-call callbacks: callbacks belong to the animatable, not to a `Set`.

## Open items

- The callbacks animation has the engine's default duration (1000 ms): a burst longer than that completes it early, raising `OnComplete` while properties still move and stopping `OnUpdate` (kept from upstream). Untested.
- Under `ReduceAll`, `Set` with a duration, momentum retargets and `Fling` animate rather than snap, and the callbacks animation completes on its first frame. Untested; decide whether an animatable should honor `ReduceAll` on every retarget.
- The channel `Set` neither consults nor clears a saved fling; the next scalar `Set` still restores the pre-fling eases.
- `Fling` on a color or complex property is not rejected (an `Inertia` to-value throws for one); its behavior is unspecified and untested.
- `ValueOf` never unsubscribes from the animation's render, so each call keeps a value alive for the animatable's lifetime.
- No core test for canonical-name lookup (only a bridge test pins it), unknown properties, `PlaybackRate`/`FrameRate` passthrough, or per-property `Composition`.
