---
id: CORE-REDUCED
title: Reduced motion
package: Monaco.Presto
namespaces:
  - Monaco.Presto
sources:
  - Monaco.Presto/PrestoDefaults.cs
  - Monaco.Presto/AnimationBuilder.cs
  - Monaco.Presto/AnimationParams.cs
  - Monaco.Presto/Animation.cs
types: []
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

Reduced motion in the core is two app-set flags on `PrestoDefaults`. `ReducedMotion` makes an animation play its authored reduced form: `AnimationBuilder.WhenReduced` for builders, `AnimationParams.WhenReduced` for dictionary-form params. `ReduceAll` turns every other animation into an instant landing on its end values. Presto never sets either flag itself; following the operating system's setting, and the reduced forms of bridge features, are specified by BRIDGE-REDUCED. Also specifies `PrestoDefaults.ReducedMotion`, `PrestoDefaults.ReduceAll`, `AnimationBuilder.WhenReduced` and `AnimationParams.WhenReduced`.

## Requirements

### Flags

- **CORE-REDUCED-001** — Both flags shall default to false, and the core shall never set either on its own.
  - Tests: `AnimationBuilderTests.WhenReducedIsPickedOnlyWhileReducedMotionIsSet`
- **CORE-REDUCED-002** — The flags shall belong to one `PrestoDefaults`, so each engine, and each scope given its own defaults, has its own pair, in force while that scope executes.
  - Tests: `ReducedMotionTests.AScopeKeepsItsOwnFlags`
- **CORE-REDUCED-003** — The flags shall be read when params are built or an animation is constructed; changing them shall not alter an animation that already exists.
  - Tests: untested

### Builder reduced form

- **CORE-REDUCED-004** — When `ToParams` runs while `Engine.Current.Defaults.ReducedMotion` is set and the builder has a `WhenReduced` action, it shall build the reduced form; otherwise it shall build the main form.
  - Tests: `AnimationBuilderTests.WhenReducedIsPickedOnlyWhileReducedMotionIsSet`
- **CORE-REDUCED-005** — The reduced form shall start from a new builder carrying every setting and callback of this one, including those chained after `WhenReduced`, and none of its properties or keyframes, then apply the action.
  - Tests: `AnimationBuilderTests.SettingsChainedAfterWhenReducedCarryIntoTheReducedFormButMainPropertiesDoNot`
- **CORE-REDUCED-006** — A setting the action sets shall override the carried one in the reduced form.
  - Tests: `AnimationBuilderTests.SettingsChainedAfterWhenReducedCarryIntoTheReducedFormButMainPropertiesDoNot`, `ReducedMotionTests.ReduceAllLeavesAReducedAlternativeAndAPlainTimerAlone`
- **CORE-REDUCED-007** — The reduced form shall inherit the builder's host lifetime unless the action sets its own.
  - Tests: `HostHookTests.ReducedFormInheritsHostLifetimeUnlessItSetsItsOwn`
- **CORE-REDUCED-008** — The action shall run on every `ToParams` call that builds the reduced form, on a fresh builder each time.
  - Tests: untested
- **CORE-REDUCED-009** — A second `WhenReduced` call shall replace the first action.
  - Tests: `AnimationBuilderTests.CloneAndWithCarryTheReducedFormAndWithLeavesTheSourceAlone`
- **CORE-REDUCED-010** — A `WhenReduced` called inside the reduced action shall be ignored.
  - Tests: untested
- **CORE-REDUCED-011** — `Clone` and `With` shall carry the reduced action, and `With` replacing it shall leave the source's action unchanged.
  - Tests: `AnimationBuilderTests.CloneAndWithCarryTheReducedFormAndWithLeavesTheSourceAlone`
- **CORE-REDUCED-012** — `ToParams` shall never set `AnimationParams.WhenReduced` on the params it returns, since it has already picked its form.
  - Tests: untested

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
- **CORE-REDUCED-030** — `ReduceAll` shall leave plain timers alone, and the timeline itself: its loop, positions and gaps.
  - Tests: `ReducedMotionTests.ReduceAllLeavesAReducedAlternativeAndAPlainTimerAlone`
- **CORE-REDUCED-031** — `ReduceAll` shall not change `Presto.Set`, which is instant already.
  - Tests: untested

## Invariants

- An animation's form is fixed at construction; no flag change re-forms it.
- A reduced form is never collapsed by `ReduceAll`.

## Edge cases

- A builder reads `Engine.Current`'s flag in `ToParams`, a dictionary form its owner engine's at construction; a builder snapshotted outside a scope's `Execute` ignores that scope's flags.
- A reduced form with no properties produces an animation with no tweens, which behaves as CORE-ANIMATION specifies for an animation without properties.
- A looping main form keeps looping in its reduced form unless the reduced form sets `Loop`.

## Threading and lifetime

- The flags live on per-engine (or per-scope) defaults and are read on that engine's thread; there is no change notification (see Decisions: the flag has no change notification).

## Reduced motion

- This spec is the core's reduced-motion rule set.

## Allocation

- Not on the tick path; resolving a dictionary reduced form allocates one params copy at construction.

## Deviations from anime.js

- No upstream counterpart; anime.js has no reduced-motion support.

## Decisions

- **Reduced motion is an app-set flag with a per-builder alternative.** `PrestoDefaults.ReducedMotion` is a `bool` the app sets; `AnimationBuilder.WhenReduced` stores an alternative built on a fresh builder that carries this one's settings and callbacks (those chained after it too) but none of its properties or keyframes, picked when the builder becomes params; every recipe supplies one. Reason: the app decides whether to follow the operating system's setting, and carrying the settings lets a stagger or callback survive while the reduced form states its own values. Earlier Presto shipped no way to follow the system setting; following it is now an opt-in in the bridge (BRIDGE-REDUCED), and the core still never sets the flag.
- **`ReduceAll` collapses everything without an alternative.** It makes the `Animation` constructor give every tween a minimal duration and no delay, keyframes and springs included, and force one iteration; params built from a reduced form, `Presto.Set` and plain timers are left alone, and timeline children collapse like any animation. Reason: not recorded.
- **The flag has no change notification.** Each consumer reads the flag when its own trigger fires, and an animation keeps the form it was built with. Reason: `PrestoDefaults` is a record copied with `with` into every scope and engine, so an event on it would be compared by record equality and carried into each copy, and a subscriber would not know which copy to watch; an app that must react at once has the bridge's system-following callback. Rejected: an event on `PrestoDefaults`.
- **Dictionary params take `WhenReduced` with builder semantics.** `AnimationParams.WhenReduced` replaces the properties and keyframes and overrides only the settings it sets; `ReduceAll` leaves the result alone, a nested `WhenReduced` is ignored, `ToParams` never sets it, and `TimerParams` and `TimelineParams` have none. Reason: a timer animates nothing, and each timeline child picks its own form.
- **Reduced motion lands on the resolved end at once.** See CORE-INERTIA.
- **Projectile is a closed-form to-value with a declared flight time.** See CORE-INERTIA.

## Non-goals

- Detecting the operating system's setting (BRIDGE-REDUCED).
- Reduced forms for timers and timelines, and re-forming running animations when a flag changes.

## Open items

- A builder passed to `Timeline.Add` on an engine other than `Engine.Current` reads the current engine's `ReducedMotion`, while a dictionary child reads the timeline's engine.
- `Animatable` builds its property animations through `Presto.Animate` at construction, so `ReduceAll` collapses them; what that does to later `Animatable.Set` calls is unpinned (CORE-ANIMATABLE).
- Spring collapse, nested `WhenReduced`, `Priority` override, `Set` under either flag, and flag changes not touching live animations have no test.
