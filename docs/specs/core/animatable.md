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
  - CORE-REDUCED
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
- **CORE-ANIMATABLE-016** — `ValueOf` shall return the same `MotionValue` for a property on every call, subscribed once to the property's animation: it starts at `Get(property)` and is assigned `Get(property)` every time that animation renders.
  - Tests: `MotionValueDynamicsTests.AnimatableValueOfFollowsSet`, `MotionValueDynamicsTests.AnimatableValueOfReturnsOneValuePerPropertySubscribedOnce`

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
- **CORE-ANIMATABLE-060** — The callbacks animation shall have no end of its own: only CORE-ANIMATABLE-028 completes it, so a burst of any length raises `OnUpdate` until its last property animation stops and `OnComplete` only then (see "The callbacks animation ends with its properties").
  - Tests: `AnimatablesTests.TheCallbacksLastAsLongAsTheSet`
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
- **CORE-ANIMATABLE-049** — The first fling after a `Set` shall save the property's eases and animation duration; the next `Set`, scalar or channel, shall clear and restore them, using the `Set` arguments where given. A flung property is scalar (CORE-ANIMATABLE-061), so a channel `Set` after a fling moves no channel and only restores.
  - Tests: `AnimatablesTests.ASetAfterAFlingUsesTheDeclaredEaseAndDurationAgain`, `AnimatablesTests.AChannelSetAfterAFlingUsesTheDeclaredEaseAndDurationAgain`
- **CORE-ANIMATABLE-061** — When the property is a color or complex one, `Fling` of either overload shall throw `NotSupportedException` before changing anything.
  - Tests: `AnimatablesTests.FlingRejectsAColorProperty`

### Revert

- **CORE-ANIMATABLE-050** — `Revert` shall revert every property animation, restoring each property to the value it held before the animatable ran, revert the callbacks animation, and empty `Targets` and `Animations`.
  - Tests: `AnimatablesTests.RevertAnAnimatable`
- **CORE-ANIMATABLE-051** — After `Revert`, `Set` and `Fling` shall do nothing.
  - Tests: `AnimatablesTests.RevertAFlungAnimatable`
- **CORE-ANIMATABLE-052** (inherited) — After `Revert`, or for a property that was never declared, `Get` shall return 0 and `GetChannels` an empty array.
  - Tests: untested

### ReduceAll

- **CORE-ANIMATABLE-055** — The property and callbacks animations shall be built without the `ReduceAll` collapse, and the animatable shall read `ReduceAll` at each retarget from the `PrestoDefaults` in force when it was created (its scope's when created inside one), so a change of the flag takes effect at the next retarget, a retarget under it stretching and saving fling state as without it (CORE-ANIMATABLE-019, -049).
  - Tests: `AnimatablesTests.AnAnimatableCreatedInAReduceAllScopeLandsItsRetargetsOutsideIt`, `AnimatablesTests.ClearingReduceAllRestoresTheDurationsASetWouldHaveLeft`
- **CORE-ANIMATABLE-056** — While `ReduceAll` is set, every retarget, scalar or channel `Set` with or without a duration and momentum retargets included, shall seek its property animation to the end instead of resuming it, so the property lands on its target at once and the engine idles.
  - Tests: `AnimatablesTests.ReduceAllLandsASetWithADurationOnTheNextFrame`, `AnimatablesTests.ReduceAllLandsAMomentumSetOnTheNextFrame`
- **CORE-ANIMATABLE-057** — While `ReduceAll` is set, `Fling` of either overload shall land the property at once on the inertia solver's resolved rest value, bounds and snap applied.
  - Tests: `AnimatablesTests.ReduceAllLandsAFlingOnItsProjectedRestOnTheNextFrame`
- **CORE-ANIMATABLE-058** — Under `ReduceAll`, each retarget shall raise `OnBegin` as CORE-ANIMATABLE-026 says, then exactly one `OnUpdate` and one `OnComplete`.
  - Tests: `AnimatablesTests.ReduceAllRaisesOneUpdateAndOneCompletePerRetarget`
- **CORE-ANIMATABLE-059** — `ReducedMotion` alone shall not change an animatable.
  - Tests: `AnimatablesTests.ReducedMotionAloneLeavesASetWithADurationEasing`

### Defaults

- **CORE-ANIMATABLE-053** — When `PreserveVelocity` is set on neither the property nor the params, a retargeted spring shall start from rest (see "Spring momentum on retarget is opt-in").
  - Tests: untested

## Invariants

- `Animations` holds exactly one animation per declared property until `Revert`, and none after.
- Every tween of a property animation ends exactly on the value last passed to `Set`, or on the resolved fling target.
- The callbacks animation is playing whenever any property animation is running.

## Edge cases

- A property declared twice by two spellings keeps the last declaration under the canonical key.
- A channel list longer than the property's channels ignores the extra entries; a shorter one leaves the rest unchanged.
- `Set` on a property that is mid-fling discards the fling ease unless the property has preserved velocity, which then measures the fling's velocity.
- A fling whose travel is within `RestDelta` sets the value at once.

## Threading and lifetime

- UI-thread (engine thread) only, like every timer. Created on `Engine.Current`; its animations belong to that engine.
- The animatable's property animations are ordinary top-level animations: each completes and leaves the engine's list when it settles, and is revived by the next `Set`.
- `ValueOf` subscribes one cached value per property to its animation's render for the animatable's lifetime; `Revert` drops the cache.

## Reduced motion

- `ReduceAll` is honored on every retarget rather than at construction: each `Set`, momentum retarget and `Fling` lands at once on its end value, the callbacks raising one update and one complete, while the durations evolve as without the flag (CORE-ANIMATABLE-055 to CORE-ANIMATABLE-058).
- `ReducedMotion` changes nothing and `WhenReduced` does not apply: an animatable has no builder form (CORE-ANIMATABLE-059).

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

- **An animatable honors `ReduceAll` on every retarget.** Its animations are built uncollapsed, and while the flag is set a retarget aims and stretches as usual, then seeks to the end instead of resuming. Reason: `ReduceAll` promises no motion and retargets are the motion; seeking keeps the durations, staggers and saved fling state as they would be, so nothing leaks once the flag clears, and an animatable created under the flag eases normally after it. Rejected: honoring it only at creation, where a `Set` with a duration, a momentum retarget or a `Fling` still animated and the callbacks completed on their first frame; collapsing each retarget through a zero-duration stretch, which loses per-target durations and needs the real one saved and restored.
- **Spring momentum on retarget is opt-in.** `PreserveVelocity` (`bool?` on the params and the property, the property winning, default false) hands the current velocity to a fresh `Spring` with the same mass, stiffness and damping when a spring-eased property is retargeted, in the scalar and the channel `Set` alike; earlier only the scalar `Set` carried it, extended because colors, thicknesses and points keep momentum too. Reason: momentum carried across a retarget is what makes a spring feel physical (Framer Motion, React Spring, Compose); opt-in because it changes what a spring-eased animatable does, and because Draggable drives its release springs through an `Animatable` and sets their velocity itself. Rejected: upstream's reset, where a spring always starts at rest, as the only behavior.
- **Preserved velocity is measured from the running ease.** Only a `Spring` ease (the `Set`'s, the property's or the animatable's) preserves velocity; velocity is a central difference of the running ease, a spring sampled through its `PureEase`, normalized to the solver's progress per second; a paused, completed or unstarted tween, or a retarget to the current value, starts at rest; the animation lasts the longest settling duration (or the `Set` duration compresses them) and an early-settling tween holds its end. Nothing new is public on `Spring`. Reason: sampling through `PureEase` never raises the spring's `OnComplete`; a zero-distance retarget cannot carry velocity in an ease-based model; each tween's settling duration depends on its velocity.
- **`Fling` retargets with an inertia.** `Fling(property, velocity, settings)` coasts the property under the inertia solver; without a velocity it measures the running tween's (0 when paused or finished); the animatable's ease and duration are replaced for that run and restored by the next `Set`; differing per-target durations are sampled over the longest. Reason: a value that is already moving keeps coasting; it mirrors `Set(…, ease)` replacing the ease for one run.
- **`Fling` settings carry no velocity.** Both overloads ignore `Inertia.Velocity`: the velocity is the argument or the property's measured one, and `settings` supplies only the decay, snap and bounds; settings are written `new Inertia { … }`. Reason: one source of velocity per call, and a fallback to `settings.Velocity` at rest would jump between a near-still property (coasts almost nowhere) and a still one (full kick). Rejected: `settings.Velocity` as an at-rest fallback.
- **The callbacks animation ends with its properties.** It is built with a `Constants.MaxValue` duration, so only CORE-ANIMATABLE-028 (every property animation paused or completed) completes it, `ReduceAll` included. Reason: with the engine's default duration a burst longer than 1000 ms raised `OnComplete` while properties still moved and stopped `OnUpdate`. Rejected: upstream's default duration; stretching it to the longest running property animation on each begin, which a retarget mid-burst outlives.
- **`Fling` coasts scalars only.** A color or complex property throws `NotSupportedException`. Reason: the inertia solver resolves one number, and an `Inertia` to-value already throws for channels.
- **Out-and-back bounces fold into one ease.** When a fling's inertia becomes a degenerate bounce, `Fling` folds its two parts, out to the spring's first peak and back to the bound, into one ease. Reason: an animatable has one tween per target.
- **A degenerate bounce goes out and back.** See CORE-INERTIA.
- **`ValueOf` caches one value per property.** Repeated calls return the same `MotionValue` and add no subscriptions. Reason: no new API and no lifetime trap. Rejected: weak subscriptions (`ValueOf("X").Bind(...)` with no stored reference stops after a GC) and a disposable handle (API and burden on every caller).
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

- No core test for canonical-name lookup (only a bridge test pins it), unknown properties, `PlaybackRate`/`FrameRate` passthrough, or per-property `Composition`.
