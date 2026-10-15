---
id: CORE-SCOPE
title: Scope and defaults
package: MonacoLabs.Presto
namespaces:
  - MonacoLabs.Presto
sources:
  - MonacoLabs.Presto/Scope.cs
  - MonacoLabs.Presto/ScopeParams.cs
  - MonacoLabs.Presto/PrestoDefaults.cs
  - MonacoLabs.Presto/DefaultsParams.cs
  - MonacoLabs.Presto/IRevertible.cs
types:
  - Scope
  - ScopeParams
  - PrestoDefaults
  - DefaultsParams
  - IRevertible
tests:
  - ScopeTests
  - EnginesTests
  - TimelinesTests
  - EngineTests
  - EasesTests
  - KeyframesTests
  - OriginTests
  - ReducedMotionTests
upstream:
  - src/scope/scope.js
  - src/core/globals.js
  - tests/suites/scope.test.js
depends:
  - CORE-ENGINE
  - CORE-TIMER
  - CORE-ANIMATION
  - CORE-TIMELINE
  - CORE-ANIMATABLE
  - CORE-UTILS
  - CORE-REDUCED
  - CORE-DIAGNOSTICS
---

# Scope and defaults

## Purpose

`PrestoDefaults` holds the parameter values every timer and animation falls back to; each engine owns one, and `DefaultsParams` overrides it per timeline. `Scope`, initially ported from anime.js `createScope`, is a lifetime that everything constructed while it is current registers with, so one `Revert` undoes it all, with its own defaults in force while it executes and constructors that `Refresh` re-runs. `IRevertible` is the contract a scope reverts through. Bridge scopes tied to a control's lifetime build on this (see BRIDGE-HELPERS).

## Requirements

### PrestoDefaults

- **CORE-SCOPE-001** — A new `PrestoDefaults` shall hold the values listed under Defaults, plus an `out(2)` ease, a 1000 ms duration and `Replace` composition.
  - Tests: `EasesTests.DefaultEaseIsOutTwo`, `ScopeTests.AppliesScopedDefaultsToChildrenCreatedInside`
- **CORE-SCOPE-002** — A timer shall take each setting its params leave null (callbacks, `Loop`, `LoopDelay`, `Reversed`, `Alternate`, `Autoplay`, `FrameRate`, `PlaybackRate`, a numeric `Delay`) from its parent timeline's child defaults, or else its engine's `Defaults`, read once at construction.
  - Tests: `TimelinesTests.DefaultParametersAreInheritedByChildren`
- **CORE-SCOPE-003** — A plain timer shall not take `Duration` from the defaults (it is infinite unless set), and shall fall back to a 0 delay when the default `Delay` is function-based.
  - Tests: untested
- **CORE-SCOPE-004** — An animation shall additionally take `Duration`, `Delay`, `Ease`, `PlaybackEase`, `Composition`, `Modifier` and `OnRender` from the same defaults when its params leave them null.
  - Tests: `ScopeTests.AppliesScopedDefaultsToChildrenCreatedInside`
- **CORE-SCOPE-005** — Changing a `PrestoDefaults` member shall affect only timers constructed afterwards.
  - Tests: untested
- **CORE-SCOPE-006** — When the engine's time unit changes, the engine shall rescale a numeric default `Duration` to the new unit (see CORE-ENGINE).
  - Tests: `EngineTests.ChangingTheTimeUnitShouldAffectDurationValues`
- **CORE-SCOPE-007** — A new engine shall start from a `with` copy of `Presto.Defaults`, after which the two change independently.
  - Tests: `EnginesTests.DefaultsStartAsACopyOfTheMainEnginesAndThenDiverge`
- **CORE-SCOPE-008** — Percentage-based keyframes shall span the engine's default duration, not the parent timeline's merged one.
  - Tests: `KeyframesTests.PercentageKeyframesSpanTheEngineDefaultDurationAndNotTheParentTimelines`

### DefaultsParams

- **CORE-SCOPE-009** — When a timeline is given `Defaults`, it shall build its children's defaults once, at construction, as a copy of its engine's defaults with every non-null `DefaultsParams` member replacing the engine's value.
  - Tests: `TimelinesTests.DefaultParametersAreInheritedByChildren`
- **CORE-SCOPE-010** — When a timeline is given no `Defaults`, its children shall read the engine's defaults instance itself, as it stands when each child is added.
  - Tests: untested
- **CORE-SCOPE-011** — A child's own params shall override the timeline's defaults member by member.
  - Tests: `TimelinesTests.DefaultParametersAreInheritedByChildren`
- **CORE-SCOPE-012** (inherited) — `DefaultsParams` shall carry no reduced-motion flags; the merged copy keeps the engine's flags, and animations read `ReduceAll` from their engine's defaults in any case (CORE-REDUCED).
  - Tests: untested

### Scope creation

- **CORE-SCOPE-013** — A scope shall bind to `Engine.Current` at construction and register itself with that engine's current scope, if any.
  - Tests: `ScopeTests.RevertsNestedScopes`, `EnginesTests.ScopeExecuteMakesItsOwnEngineAmbientOutsideAMakeCurrentBlock`
- **CORE-SCOPE-014** — A scope shall keep `ScopeParams.Root` as given, null by default; the core never reads it.
  - Tests: `ScopeTests.KeepsTheRootItIsGiven`
- **CORE-SCOPE-015** — When `ScopeParams.Defaults` is set, the scope's `Defaults` shall be a `with` copy of its engine's defaults at construction, mutated by that action, leaving the engine's defaults unchanged.
  - Tests: `ScopeTests.AppliesScopedDefaultsToChildrenCreatedInside`, `ReducedMotionTests.AScopeKeepsItsOwnFlags`
- **CORE-SCOPE-016** — When `ScopeParams.Defaults` is null, the scope's `Defaults` shall be its engine's defaults instance itself, so changes through either are seen by both.
  - Tests: untested

### Execute

- **CORE-SCOPE-017** — `Execute` shall run `callback` with this scope as its engine's current scope, the scope's `Defaults` as the engine's `Defaults` and its engine as `Engine.Current`, and return the callback's result.
  - Tests: `ScopeTests.AppliesScopedDefaultsToChildrenCreatedInside`, `EnginesTests.ScopeExecuteMakesItsOwnEngineAmbientOutsideAMakeCurrentBlock`
- **CORE-SCOPE-018** — `Execute` shall change only its own engine's current scope and defaults, leaving every other engine's alone.
  - Tests: `EnginesTests.ScopeExecuteSwapsOnlyItsOwnEnginesDefaults`
- **CORE-SCOPE-019** (inherited) — When `Execute` returns or throws, it shall restore the previous current scope, defaults and ambient engine, and let the exception propagate.
  - Tests: `ScopeTests.RestoresTheGlobalsWhenTheCallbackThrows`
- **CORE-SCOPE-020** — A nested `Execute` of another scope shall restore the outer scope as current when it ends.
  - Tests: untested

### Registration

- **CORE-SCOPE-021** — Every timer, animation, animatable and scope constructed while a scope is its engine's current scope shall register with it.
  - Tests: `ScopeTests.RevertsEverythingCreatedInside`, `ScopeTests.RevertsNestedScopes`
- **CORE-SCOPE-022** — `Register` shall record a timer's scope in its diagnostics when diagnostics are enabled (CORE-DIAGNOSTICS).
  - Tests: `OriginTests.TheScopeIsRecorded`
- **CORE-SCOPE-023** — After an `AddOnce` or `KeepTime` call and until the next `Add`, registrations shall go to the scope's once-list, which `Refresh` does not revert.
  - Tests: untested
- **CORE-SCOPE-024** — When a registration brings a list to a power-of-two count of at least 32, the scope shall drop each canceled, uncompleted animation whose every captured original value it can hand to a later registered animation of the same target and property, and hand them over.
  - Tests: `ScopeTests.ReplacedAnimationsDoNotAccumulateAndRevertStillRestoresTheOriginal`
- **CORE-SCOPE-025** — Pruning shall keep completed animations, so their targets are still restored on revert.
  - Tests: `ScopeTests.CompletedAnimationsStayRegistered`

### Constructors and methods

- **CORE-SCOPE-026** — `Add(constructor)` shall store the constructor, run it at once inside `Execute`, keep any cleanup it returns, and return the scope.
  - Tests: `ScopeTests.RevertsEverythingCreatedInside`, `ScopeTests.RefreshRevertsThenReRunsTheConstructors`
- **CORE-SCOPE-027** — `Add(name, method)` shall store in `Methods[name]` a wrapper that runs `method` inside `Execute`, replacing any method of that name.
  - Tests: `ScopeTests.RunsMethodsInsideTheScope`
- **CORE-SCOPE-028** — `AddOnce` shall run its constructor inside `Execute` the first time its position is reached and skip it on every later pass, positions being counted from 0 per `Refresh`.
  - Tests: `ScopeTests.AddOnceRunsOnceAcrossRefreshes`
- **CORE-SCOPE-029** — A cleanup returned by an `AddOnce` constructor shall run on `Revert` only.
  - Tests: untested
- **CORE-SCOPE-030** — `KeepTime` shall, the first time its position is reached, wrap `constructor` with `Utils.KeepTime`, run it inside `Execute` and return its timer; on later passes it shall call the stored wrapper, which reverts the previous timer and hands its iteration and progress to the new one.
  - Tests: `ScopeTests.KeepTimeKeepsThePlaybackPositionAcrossARefresh`

### Refresh and revert

- **CORE-SCOPE-031** — `Refresh` shall, inside `Execute`, revert the registered revertibles newest first, run the stored cleanups newest first, clear both, then re-run every `Add` constructor in order, keeping their new cleanups.
  - Tests: `ScopeTests.RefreshRevertsThenReRunsTheConstructors`
- **CORE-SCOPE-032** — `Refresh` shall leave once-registered revertibles and once cleanups in place.
  - Tests: untested
- **CORE-SCOPE-033** — `Revert` shall revert the revertibles newest first, run the cleanups newest first, then do the same for the once-lists, and return the scope.
  - Tests: `ScopeTests.RevertsEverythingCreatedInside`
- **CORE-SCOPE-034** — `Revert` shall then clear every list, the stored constructors, the once position, `Methods` and `Data`, so a later `Refresh` re-runs nothing.
  - Tests: untested
- **CORE-SCOPE-035** — Reverting a scope shall revert the scopes registered with it, and through them what they created.
  - Tests: `ScopeTests.RevertsNestedScopes`
- **CORE-SCOPE-036** — A scope shall stay usable after `Revert`: later `Add` calls register and run as on a new scope.
  - Tests: untested
- **CORE-SCOPE-037** — The scope shall revert any `IRevertible` passed to `Register`, not only the core types.
  - Tests: untested

### Defaults

- **CORE-SCOPE-038** — When `PlaybackRate` is not set, a new `PrestoDefaults` shall hold 1.
  - Tests: untested
- **CORE-SCOPE-039** — When `FrameRate` is not set, a new `PrestoDefaults` shall hold 240.
  - Tests: `ClockTests.FpsHigherThanDefaultFrameRateRaisesDefault`
- **CORE-SCOPE-040** — When `Loop` is not set, a new `PrestoDefaults` shall hold 0 (one iteration).
  - Tests: untested
- **CORE-SCOPE-041** — When `Reversed` and `Alternate` are not set, a new `PrestoDefaults` shall hold false for both.
  - Tests: untested
- **CORE-SCOPE-042** — When `Autoplay` is not set, a new `PrestoDefaults` shall hold true.
  - Tests: untested
- **CORE-SCOPE-043** — When `Delay` and `LoopDelay` are not set, a new `PrestoDefaults` shall hold 0 for both.
  - Tests: untested
- **CORE-SCOPE-044** — When `PlaybackEase` is not set, a new `PrestoDefaults` shall hold null, so playback is not eased.
  - Tests: untested
- **CORE-SCOPE-045** — When `Modifier` is not set, a new `PrestoDefaults` shall hold the identity function.
  - Tests: untested
- **CORE-SCOPE-046** — When the callbacks are not set, a new `PrestoDefaults` shall hold null for each.
  - Tests: untested
- **CORE-SCOPE-047** — When `parameters` is not given, the `Scope` constructor shall behave as with an empty `ScopeParams`.
  - Tests: untested

## Invariants

- Outside `Execute`, an engine's current scope and defaults are what they were before the outermost `Execute` began.
- A registered revertible is reverted at most once per `Revert` or `Refresh` pass, newest first.

## Edge cases

- A scope created inside another scope's `Execute` but on a different engine registers with that engine's current scope, not the outer one.
- Calling `Methods[name]` after `Revert` throws `KeyNotFoundException`: the dictionary has been cleared.
- `AddOnce` and `KeepTime` positions are counted by call order; a refresh that reaches them in a different order pairs them with the wrong stored constructors.
- `Execute` with a scope whose `Defaults` share the engine's instance swaps nothing observable.

## Threading and lifetime

- A scope and its engine share one thread; `Execute`, `Refresh` and `Revert` must run on it (CORE-THREADING).
- A scope holds strong references to everything registered with it until `Revert` or a prune drops them; it is not released by the engine.
- `PrestoDefaults` has no change notification; consumers read it when their trigger fires (ReducedMotion has no change notification, CORE-REDUCED).

## Reduced motion

- Not affected directly. `ReducedMotion` and `ReduceAll` are `PrestoDefaults` members, so a scope with its own defaults has its own flags (CORE-REDUCED).

## Allocation

- Not on the tick path. Registration is amortized constant time; pruning runs only at power-of-two counts.

## Provenance

- `Execute` restores the scope, defaults and ambient engine in a `finally`; upstream restores them only on return. Reason: a throwing callback would otherwise leave the scope's defaults and current scope installed on the engine.
- Media queries, `matches` and the `handleEvent` refresh are DOM-specific and not ported; the root is stored but not resolved from a selector or ref.
- Registration prunes superseded animations; upstream lets the list grow. Reason: retargeting repeatedly inside a scope (a live feed) grew its list without bound.
- `Methods` take no arguments; upstream's take any. Reason not recorded.
- Cleanups receive no scope argument. Reason not recorded.
- `Revert` returns the scope and is virtual, so a host scope can extend it.

## Decisions

- **The default ease is `out(2)`.** `PrestoDefaults.Ease` starts as `Eases.Out(2)`, not linear. Kept from upstream; no other reason recorded.
- **A new `PrestoDefaults` holds the timer's plain choices.** Its timing values are the ones CORE-TIMER's "Unset timer parameters are the plain choices" gives, the modifier is the identity and no callback is set (CORE-SCOPE-038 to 047). Reason: an unchanged engine then adds nothing to a call, so only the parameters a call sets change it.

## Non-goals

- Scopes do not auto-revert; tying one to a control's lifetime is the bridge's job (Explicit control scoping, BRIDGE-HELPERS).

## Open items

- Clearing `Methods`/`Data` on revert, once-list routing after `AddOnce`/`KeepTime`, `Refresh` leaving the once-lists alone, `AddOnce` cleanups, nested `Execute` and reuse after `Revert` have no test.
- A timeline without `Defaults` reads live engine defaults per child while one with `Defaults` snapshots them; unpinned.
