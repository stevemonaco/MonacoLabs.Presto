---
id: CORE-REDUCED
title: Reduced motion
package: MonacoLabs.Presto
namespaces:
  - MonacoLabs.Presto
sources:
  - MonacoLabs.Presto/PrestoDefaults.cs
  - MonacoLabs.Presto/AnimationBuilder.cs
  - MonacoLabs.Presto/AnimationParams.cs
  - MonacoLabs.Presto/Animation.cs
  - MonacoLabs.Presto/MotionPreference.cs
  - MonacoLabs.Presto/Engine.cs
types:
  - MotionPreference
tests:
  - ReducedMotionTests
  - AnimationBuilderTests
  - HostHookTests
  - InertiaTests
  - ProjectileTests
  - ColorKeyframesTests
upstream: []
depends:
  - CORE-SCOPE
  - CORE-BUILDER
  - CORE-ANIMATION
  - CORE-TIMELINE
  - CORE-TIMER
  - CORE-KEYFRAMES
  - CORE-INERTIA
  - CORE-PRESTO
---

# Reduced motion

## Purpose

Reduced motion in the core is two app-set flags on `PrestoDefaults`. `ReducedMotion` makes an animation play its authored reduced form: `AnimationBuilder.WhenReduced` for builders, `AnimationParams.WhenReduced` for dictionary-form params. `ReduceAll` turns every other animation into an instant landing on its end values. Presto never sets either flag itself; following the operating system's setting, and the reduced forms of bridge features, are specified by BRIDGE-REDUCED. Also specifies `PrestoDefaults.ReducedMotion`, `PrestoDefaults.ReduceAll`, `Engine.ReducedMotionChanged`, `AnimationBuilder.WhenReduced` and `AnimationParams.WhenReduced`.

## Requirements

### Flags

- **CORE-REDUCED-001** — Both flags shall default to false, and the core shall never set either on its own.
  - Tests: `AnimationBuilderTests.WhenReducedIsPickedOnlyWhileReducedMotionIsSet`
- **CORE-REDUCED-002** — The flags shall belong to one `PrestoDefaults`, so each engine, and each scope given its own defaults, has its own pair, in force while that scope executes.
  - Tests: `ReducedMotionTests.AScopeKeepsItsOwnFlags`
- **CORE-REDUCED-003** — The flags shall be read when an animation is constructed; changing them, raising `ReducedMotionChanged` included, shall not alter an animation that already exists.
  - Tests: `ReducedMotionTests.TheSignalLeavesARunningAnimationInItsForm`

### Change signal

- **CORE-REDUCED-034** — When `ReducedMotion` or `ReduceAll` of an engine's own defaults changes value, the engine shall raise `ReducedMotionChanged` once, with itself, synchronously after the write.
  - Tests: `ReducedMotionTests.ChangingEitherFlagRaisesTheSignalOnceWithTheEngine`
- **CORE-REDUCED-035** — Writing a flag its current value shall raise nothing.
  - Tests: `ReducedMotionTests.WritingTheCurrentValueRaisesNothing`
- **CORE-REDUCED-036** — Writing a flag of a copy of the defaults (`with`, the copy a new engine starts from, a scope's configured defaults) shall raise nothing on the engine it was copied from; a new engine's starting copy is that engine's own instance.
  - Tests: `ReducedMotionTests.AWithCopyAndAScopesDefaultsRaiseNothing`, `ReducedMotionTests.ANewEnginesCopyRaisesOnTheNewEngineOnly`
- **CORE-REDUCED-037** — A `PrestoDefaults` copy shall compare equal to its source until a value differs, whether or not the source belongs to an engine.
  - Tests: `ReducedMotionTests.ACopyOfAnEnginesDefaultsStillEqualsIt`

### Preference

- **CORE-REDUCED-039** — `MotionPreference` shall have the values `System`, `Full` and `Reduced`, in that order, `System` being the default; the core shall not act on it.
  - Tests: untested

### Builder reduced form

- **CORE-REDUCED-004** — When the builder has a `WhenReduced` action, `ToParams` shall return the main form carrying the reduced form in `AnimationParams.WhenReduced`, and an animation built from them shall play the reduced form exactly when its owner engine's `ReducedMotion` is set at construction, whatever `Engine.Current` is.
  - Tests: `AnimationBuilderTests.WhenReducedIsPickedOnlyWhileReducedMotionIsSet`, `AnimationBuilderTests.ATimelinePicksABuildersReducedFormAgainstItsOwnEngine`
- **CORE-REDUCED-005** — The reduced form shall start from a new builder carrying every setting and callback of this one, including those chained after `WhenReduced`, and none of its properties or keyframes, then apply the action.
  - Tests: `AnimationBuilderTests.SettingsChainedAfterWhenReducedCarryIntoTheReducedFormButMainPropertiesDoNot`
- **CORE-REDUCED-006** — A setting the action sets shall override the carried one in the reduced form.
  - Tests: `AnimationBuilderTests.SettingsChainedAfterWhenReducedCarryIntoTheReducedFormButMainPropertiesDoNot`, `ReducedMotionTests.ReduceAllLeavesAReducedAlternativeAndAPlainTimerAlone`
- **CORE-REDUCED-007** — The reduced form shall inherit the builder's host lifetime unless the action sets its own.
  - Tests: `HostHookTests.ReducedFormInheritsHostLifetimeUnlessItSetsItsOwn`
- **CORE-REDUCED-008** — The action shall run on every `ToParams` call, on a fresh builder each time.
  - Tests: untested
- **CORE-REDUCED-009** — A second `WhenReduced` call shall replace the first action.
  - Tests: `AnimationBuilderTests.CloneAndWithCarryTheReducedFormAndWithLeavesTheSourceAlone`
- **CORE-REDUCED-010** — A `WhenReduced` called inside the reduced action shall be ignored.
  - Tests: untested
- **CORE-REDUCED-011** — `Clone` and `With` shall carry the reduced action, and `With` replacing it shall leave the source's action unchanged.
  - Tests: `AnimationBuilderTests.CloneAndWithCarryTheReducedFormAndWithLeavesTheSourceAlone`
- **CORE-REDUCED-012** — When the builder has no `WhenReduced` action, `ToParams` shall leave `AnimationParams.WhenReduced` null.
  - Tests: `AnimationBuilderTests.WhenReducedIsPickedOnlyWhileReducedMotionIsSet`

### Dictionary reduced form

- **CORE-REDUCED-013** — When an animation is constructed while its owner engine's `ReducedMotion` is set and its params carry `WhenReduced`, the reduced params' properties and keyframes shall replace the main ones, even when the reduced ones are empty.
  - Tests: `ReducedMotionTests.WhenReducedReplacesThePropertiesAndKeepsTheCallbacks`
- **CORE-REDUCED-014** — Each setting or callback the reduced params set shall override the main one, and each they leave null (callbacks, a stagger delay, `Loop`, `Ease`…) shall survive from the main params.
  - Tests: `ReducedMotionTests.WhenReducedKeepsTheStaggerAndTheSettingsItDoesNotSet`, `ReducedMotionTests.WhenReducedReplacesThePropertiesAndKeepsTheCallbacks`, `ReducedMotionTests.WhenReducedOverridesTheEaseItSets`
- **CORE-REDUCED-015** (inherited) — The reduced params' `Priority` shall override only when it differs from the default priority.
  - Tests: untested
- **CORE-REDUCED-016** — The reduced params' host lifetime shall override the main one only when set.
  - Tests: `HostHookTests.ReducedFormInheritsHostLifetimeUnlessItSetsItsOwn`
- **CORE-REDUCED-017** — While the flag is clear, `WhenReduced` shall be ignored.
  - Tests: `ReducedMotionTests.WhenReducedIsIgnoredWhileTheFlagIsClear`
- **CORE-REDUCED-018** — A `WhenReduced` inside the reduced params shall be ignored.
  - Tests: untested
- **CORE-REDUCED-019** — Each timeline child shall pick its own form from its own params, against the timeline's engine; the timeline lays out the forms it gets.
  - Tests: `ReducedMotionTests.ATimelineChildPicksItsOwnReducedForm`
- **CORE-REDUCED-020** — `Presto.Set` shall resolve a dictionary reduced form the same way before writing.
  - Tests: untested
- **CORE-REDUCED-021** — `TimerParams` and `TimelineParams` shall have no reduced form: a timer or timeline plays as authored under `ReducedMotion`.
  - Tests: `ReducedMotionTests.ReduceAllLeavesAReducedAlternativeAndAPlainTimerAlone`

### ReduceAll

- **CORE-REDUCED-022** — When an animation is constructed while its owner engine's `ReduceAll` is set, every tween shall get a `Constants.MinValue` duration and no delay, and the animation one iteration, so it lands on its end values on its first frame and completes.
  - Tests: `ReducedMotionTests.ReduceAllWritesTheEndValueOnTheFirstFrameAndCompletes`
- **CORE-REDUCED-023** — A collapsed animation shall still fire `OnBegin` and `OnComplete`, once each.
  - Tests: `ReducedMotionTests.ReduceAllWritesTheEndValueOnTheFirstFrameAndCompletes`
- **CORE-REDUCED-024** — A keyframed property, per-property or animation-level, shall land on its last keyframe.
  - Tests: `ReducedMotionTests.ReduceAllEndsAKeyframedPropertyOnItsLastKeyframe`, `ColorKeyframesTests.TheTweenCompletesExactlyOnTo`
- **CORE-REDUCED-025** — A spring ease shall not lengthen a collapsed tween.
  - Tests: untested
- **CORE-REDUCED-026** — An `Inertia` to-value shall resolve first and land on its snapped, clamped end.
  - Tests: `InertiaTests.ReduceAllLandsOnTheSnappedClampedTargetInOneFrame`
- **CORE-REDUCED-027** — A `Projectile` to-value shall resolve its trajectory with its declared flight time before collapsing, landing on its physical end.
  - Tests: `ProjectileTests.ReduceAllLandsOnTheEndAtOnce`
- **CORE-REDUCED-028** — Timeline children shall collapse like any animation.
  - Tests: `ReducedMotionTests.ReduceAllCollapsesTimelineChildren`
- **CORE-REDUCED-029** — `ReduceAll` shall leave params built from a builder's or dictionary's reduced form alone.
  - Tests: `ReducedMotionTests.ReduceAllLeavesAReducedAlternativeAndAPlainTimerAlone`, `ReducedMotionTests.ReduceAllLeavesADictionaryReducedFormAlone`
- ~~**CORE-REDUCED-030**~~ — Removed: `ReduceAll` now collapses timeline positions and gaps (see "`ReduceAll` collapses timeline positions"); the rest moved to CORE-REDUCED-032.
- **CORE-REDUCED-031** — `ReduceAll` shall not change `Presto.Set`, which is instant already.
  - Tests: untested
- **CORE-REDUCED-032** — `ReduceAll` shall leave a plain timer's duration and the timeline's loop alone.
  - Tests: `ReducedMotionTests.ReduceAllLeavesAReducedAlternativeAndAPlainTimerAlone`
- **CORE-REDUCED-033** — While `ReduceAll` is set, a timeline shall place every child, `Call` and label added at 0, a zero-duration or collapsed child `Constants.MinValue` before it (CORE-TIMELINE-017), so it lands every collapsed child's end state and fires its `Call`s on its first frame, in add order.
  - Tests: `ReducedMotionTests.ReduceAllCollapsesTimelinePositions`

## Invariants

- An animation's form is fixed at construction; no flag change re-forms it.
- A reduced form is never collapsed by `ReduceAll`.

## Edge cases

- A reduced form with no properties produces an animation with no tweens, which behaves as CORE-ANIMATION specifies for an animation without properties.
- A looping main form keeps looping in its reduced form unless the reduced form sets `Loop`.

## Threading and lifetime

- The flags live on per-engine (or per-scope) defaults and are read on that engine's thread; a change of an engine's own flags raises `ReducedMotionChanged` synchronously on the writer's thread, which is that engine's thread (see Decisions: the signal is raised synchronously, on the writer's thread, after the write).

## Reduced motion

- This spec is the core's reduced-motion rule set.
- An `Animatable` builds its animations without the `ReduceAll` collapse and applies the flag on each retarget instead (CORE-ANIMATABLE, "An animatable honors `ReduceAll` on every retarget").

## Allocation

- Not on the tick path; resolving a dictionary reduced form allocates one params copy at construction.

## Provenance

- No upstream counterpart; anime.js has no reduced-motion support.

## Decisions

- **Reduced motion is an app-set flag with a per-builder alternative.** `PrestoDefaults.ReducedMotion` is a `bool` the app sets; `AnimationBuilder.WhenReduced` stores an alternative built on a fresh builder that carries this one's settings and callbacks (those chained after it too) but none of its properties or keyframes, picked when the builder becomes params; every recipe supplies one. Reason: the app decides whether to follow the operating system's setting, and carrying the settings lets a stagger or callback survive while the reduced form states its own values. Earlier Presto shipped no way to follow the system setting; following it is now an opt-in in the bridge (BRIDGE-REDUCED), and the core still never sets the flag.
- **`ReduceAll` collapses everything without an alternative.** It makes the `Animation` constructor give every tween a minimal duration and no delay, keyframes and springs included, and force one iteration; params built from a reduced form, `Presto.Set` and plain timers are left alone, and timeline children collapse like any animation. Reason: not recorded.
- **`ReduceAll` collapses timeline positions.** While the flag is set, a timeline resolves the position of every child, `Call` and label added to 0, after parsing it so an unknown label still throws; plain timers keep their durations, and a reduced alternative plays from 0. Labels and `Call`s follow so callbacks keep their add order against the collapsed children. Reason: a fully reduced timeline lands every child's end state at once, as `ReduceAll` promises. Rejected: keeping positions, which still takes the full length.
- **The signal is an event on `Engine`, not on `PrestoDefaults`.** The engine raises `ReducedMotionChanged` when a flag of its own defaults instance changes value. It finds its instance through a reference-keyed weak link that neither `with` copies nor record equality sees. Reason: an event on the record would be compared by record equality and carried into every `with` copy, and a subscriber would not know which copy to watch. Subscribers already know the engine (`Presto.Engine`, or `Engine.Current` when a trigger plays), and the engine has exactly one instance that is its own. Rejected: an event or `INotifyPropertyChanged` on `PrestoDefaults` (the reasons above, and INPC would also report every other default); polling the flag each frame (an idle engine has no frames, and it costs the tick path); a static process-wide event (it cannot say which engine changed, and scene engines run off the main thread).
- **The signal is raised synchronously, on the writer's thread, after the write.** Reason: the core is single-threaded and the flag is written on its engine's thread, so a handler can read the new value and replay in the same call, and a test can assert straight after setting the flag. A consumer never depends on another subscriber having run first, since the source of the change raises the signal itself. Rejected: deferring the signal to the next frame (a change while the engine is idle would wait for an unrelated frame).
- **Only a change of value raises, and only on the engine's own instance.** Writing the current value raises nothing. A scope with configured defaults, a `with` copy, or a write that reaches a scope's copy while that scope executes raises nothing either; the link is fixed when the engine is created, not when `Scope.Execute` swaps `Engine.Defaults`. Reason: a scope's defaults are in force only during `Execute`, and nothing persistent resolves against them afterwards, while hosts read `Engine.Current.Defaults` when their triggers fire. Rejected: signalling scope copies, which would need a subscription per scope for no consumer.
- **`MotionPreference` is a core enum with `System` first.** It names where an app's flag comes from; the core never acts on it. Reason: a view model, which must stay free of host UI references, holds and persists the setting. `System` is the default value because following the OS is the right default for an app setting. Rejected: a host-side enum (it would pull the UI framework into view models); `Full` first.
- **Dictionary params take `WhenReduced` with builder semantics.** `AnimationParams.WhenReduced` replaces the properties and keyframes and overrides only the settings it sets; `ReduceAll` leaves the result alone, a nested `WhenReduced` is ignored, `ToParams` puts a builder's reduced form in it, and `TimerParams` and `TimelineParams` have none. Reason: a timer animates nothing, and each timeline child picks its own form.
- **Reduced motion lands on the resolved end at once.** See CORE-INERTIA.
- **Projectile is a closed-form to-value with a declared flight time.** See CORE-INERTIA.
- **Builders resolve against what they are added under.** A builder's reduced form travels in `AnimationParams.WhenReduced` and is picked against the owner engine when the animation is constructed, as a dictionary form is; the bridge, which always animates on the current engine, resolves builders against it before reading their params. Reason: a builder added to a timeline on another engine, or snapshotted outside a scope's `Execute`, must play the form its owner asks for. Rejected: `ToParams` picking against `Engine.Current` when it runs.

## Non-goals

- Detecting the operating system's setting (BRIDGE-REDUCED).
- Reduced forms for timers and timelines, and re-forming running animations when a flag changes.
- A signal for scope copies.

## Open items

- Spring collapse, nested `WhenReduced`, `Priority` override, and `Set` under either flag have no test.
