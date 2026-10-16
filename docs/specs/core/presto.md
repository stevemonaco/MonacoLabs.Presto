---
id: CORE-PRESTO
title: Presto facade
package: MonacoLabs.Presto
namespaces:
  - MonacoLabs.Presto
sources:
  - MonacoLabs.Presto/Presto.cs
types:
  - Presto
tests:
  - UtilsTargetsTests
  - HostHookTests
  - TargetsTests
  - ObjectTargetAdapterTests
  - AnimationBuilderTests
  - EnginesTests
  - LeaksTests
  - ScopeTests
  - StaggerTests
  - DiscreteValuesTests
  - TimelinesTests
  - PrestoDelayTests
upstream:
  - src/index.js
  - src/utils/target.js
  - tests/suites/utils.test.js
depends:
  - CORE-ENGINE
  - CORE-TIMER
  - CORE-TIMELINE
  - CORE-ANIMATION
  - CORE-ANIMATABLE
  - CORE-SCOPE
  - CORE-STAGGER
  - CORE-TARGETS
  - CORE-VALUES
  - CORE-BUILDER
  - CORE-MOTIONVALUE
  - CORE-REDUCED
  - CORE-DIAGNOSTICS
  - CORE-SCRAMBLE
---

# Presto facade

## Purpose

`Presto` is the static entry point: the main engine and its defaults, the factories (`Animate`, `CreateTimer`, `CreateTimeline`, `CreateAnimatable`, `CreateScope`, `Stagger`, `Build`, `ScrambleText`), the engine-time wait `Delay`, the target utilities (`Set`, `Get`, `Remove`, `GetAnimations`) and the adapter and codec registries. Each factory's product is specified by its own spec; this spec covers what the facade itself does: how it constructs and announces timers, `Delay`, the `Set` fast path, `Get`/`Remove`/`GetAnimations` semantics (each naming its property by `PropertyKey`, CORE-TARGETS), and adapter resolution order.

## Requirements

### Engine and defaults

- **CORE-PRESTO-001** — The facade shall expose one main engine for the process, driven by a `StopwatchFrameSource` until a host replaces its frame source, which `Engine.Current` returns outside any `MakeCurrent` block.
  - Tests: `EnginesTests.CurrentIsTheMainEngineUntilMakeCurrentNestsOne`
- **CORE-PRESTO-002** — `Presto.Defaults` shall return the main engine's `Defaults` as they stand at the read, never another engine's, even while that engine is current.
  - Tests: `EnginesTests.ScopeExecuteSwapsOnlyItsOwnEnginesDefaults`
- **CORE-PRESTO-003** — While a scope created on the main engine executes, `Presto.Defaults` shall return that scope's defaults, since `Scope.Execute` swaps the engine's `Defaults` (see CORE-SCOPE).
  - Tests: untested

### Factories

- **CORE-PRESTO-004** — `CreateTimer` shall construct a top-level timer on `Engine.Current` from `parameters` (an empty `TimerParams` when null), call `Init()`, then raise the engine's internal timer-created hook (see CORE-DIAGNOSTICS), and return it.
  - Tests: `HostHookTests.TimerCreatedFiresOncePerRootAndPerTimelineChild`, `HostHookTests.TimerCreatedFiresAfterInit`
- **CORE-PRESTO-005** — `CreateTimeline` shall construct, `Init()` and announce a timeline the same way, from an empty `TimelineParams` when `parameters` is null.
  - Tests: `HostHookTests.TimerCreatedFiresOncePerRootAndPerTimelineChild`
- **CORE-PRESTO-006** — `Animate(targets, AnimationParams)` shall construct a top-level animation, call `Init()` (so it is already scheduled when autoplay is on), then raise the timer-created hook.
  - Tests: `HostHookTests.TimerCreatedFiresAfterInit`
- **CORE-PRESTO-007** — `Animate(targets, Action<AnimationBuilder>)` and `Set(targets, Action<AnimationBuilder>)` shall run the action on a fresh `AnimationBuilder` and pass its `ToParams()` snapshot to the params overload, behaving exactly as the equivalent `Build()` chain.
  - Tests: `AnimationBuilderTests.LambdaAnimateAndSetMatchTheBuilderForm`
- **CORE-PRESTO-008** — A target-typed `new() { … }` argument to `Animate` or `Set` shall bind to the `AnimationParams` overload, not the lambda overload.
  - Tests: `AnimationBuilderTests.TargetTypedParamsStillBindBesideTheLambdaOverloads`
- **CORE-PRESTO-009** — `Build()` shall return a new, empty `AnimationBuilder` on every call.
  - Tests: `AnimationBuilderTests.AnEmptyBuilderMatchesEmptyParams`
- **CORE-PRESTO-010** (inherited) — `CreateAnimatable` shall construct the animatable directly, without raising the timer-created hook for it.
  - Tests: untested
- **CORE-PRESTO-011** — `CreateScope` shall return a new scope on `Engine.Current` built from `parameters`, or with no root and the engine's defaults when null.
  - Tests: `ScopeTests.KeepsTheRootItIsGiven`
- **CORE-PRESTO-012** — `Stagger(value, parameters)` shall return a new `Stagger` over `value`, with a default `StaggerParams` when `parameters` is null.
  - Tests: `StaggerTests.StaggersDelaysByAFixedStepPerTarget`
- **CORE-PRESTO-013** — `CreateMotionValue(initial)` shall return a new source motion value holding `initial` (0 by default).
  - Tests: untested

### Set

- **CORE-PRESTO-014** — `Set` shall write every named property to every target before it returns, and return the zero-duration animation that wrote them.
  - Tests: `UtilsTargetsTests.SetObjectProperties`, `AnimationBuilderTests.SetWritesImmediately`, `DiscreteValuesTests.SetWritesABoolAndAnEnum`
- **CORE-PRESTO-015** — `Set` shall write a bare property value as a from/to pair of that value, so the target receives the value with its own unit instead of one converted to the target's current unit.
  - Tests: `UtilsTargetsTests.SetObjectPropertiesToSpecificUnit`
- **CORE-PRESTO-016** — `Set` shall give its tweens a duration of `Constants.MinValue` regardless of the params' `Duration` and the defaults; a per-property `TweenParams.Duration` still applies.
  - Tests: untested
- **CORE-PRESTO-017** — `Set` shall compose with `Composition.Overlap` unless the params name a composition, so it neither replaces nor cancels tweens already running on those properties.
  - Tests: untested
- **CORE-PRESTO-018** — `Set` shall skip `Init()` and resume the animation instead, which renders it at once and leaves nothing registered with the engine.
  - Tests: `LeaksTests.EngineShouldNotContainAnyActiveTickable`
- **CORE-PRESTO-019** — `Set` shall raise the timer-created hook once, after it has written the values, so the hook receives an already completed animation.
  - Tests: `HostHookTests.TimerCreatedFiresForSet`
- **CORE-PRESTO-020** — `Set` shall register its animation with the current scope like any timer, so the scope's `Revert` restores the values it overwrote.
  - Tests: untested

### Get

- **CORE-PRESTO-021** — `Get(targets, property)` shall return the raw value of `property` on the first parsed target, read through the first registered adapter that resolves it, or the built-in object adapter when none does.
  - Tests: `UtilsTargetsTests.GetObjectProperties`
- **CORE-PRESTO-022** — When `targets` parses to no targets, every `Get` overload shall return null.
  - Tests: `UtilsTargetsTests.UndefinedTargetsHaveNothingToGetOrRemove`
- **CORE-PRESTO-023** — `Get(targets, property, withUnit: false)` shall return the value's number as a `double` when it decomposes to a number or a number with a unit, and null otherwise.
  - Tests: `UtilsTargetsTests.GetObjectPropertiesAndConvertUnit`
- **CORE-PRESTO-024** — `Get(targets, property, withUnit: true)` shall return what `Get(targets, property)` returns.
  - Tests: untested
- **CORE-PRESTO-025** — `Get(targets, property, unit)` shall convert a number or unit value to `unit` (angle units are scaled; any other unit is adopted as is, see CORE-VALUES) and return it as the number rounded to 4 decimals in invariant JavaScript formatting followed by the unit.
  - Tests: `UtilsTargetsTests.GetObjectPropertiesAndConvertUnit`
- **CORE-PRESTO-026** — When the value does not decompose to a single number, `Get(targets, property, unit)` shall return null.
  - Tests: untested
- **CORE-PRESTO-027** (inherited) — When the built-in object adapter resolves a property that the target type has no public readable and writable instance property or field for, `Get` shall throw `ArgumentException`; a dictionary target with no such key yields null.
  - Tests: `ObjectTargetAdapterTests.UnknownMemberThrows`, `ObjectTargetAdapterTests.ReadsMembersAndTreatsMissingDictionaryKeysAsNull`
- **CORE-PRESTO-056** — Every `Get` overload shall canonicalize the property name against the first parsed target (CORE-PRESTO-043) before resolving it, as `Remove` and `GetAnimations` do, so an alias reads the canonical property.
  - Tests: `UtilsTargetsTests.GetReadsTheCanonicalPropertyOfAnAlias`

### Remove

- **CORE-PRESTO-028** — When `renderable` is null, `Remove` shall remove the tweens of `targets` from every animation on `Engine.Current`, timeline children included.
  - Tests: `UtilsTargetsTests.RemoveTarget`, `UtilsTargetsTests.RemoveTargetsWithObjectsRef`, `UtilsTargetsTests.RemoveTargetsFromMultipleAnimationsAtOnce`, `UtilsTargetsTests.RemoveTargetsFromTimeline`
- **CORE-PRESTO-029** — When removal leaves an animation with no tweens, `Remove` shall cancel it, leaving it `Canceled` and `Paused`.
  - Tests: `UtilsTargetsTests.CancelAnimationsWithNoTweensLeftAfterCallingRemove`, `UtilsTargetsTests.RemoveTarget`
- **CORE-PRESTO-030** — When an animation keeps at least one tween, `Remove` shall leave it running and not canceled.
  - Tests: `UtilsTargetsTests.DoNotCancelAnimationsIfTweensLeftAfterCallingRemove`
- **CORE-PRESTO-031** — When removal unlinks timeline children, `Remove` shall recompute the timeline's `IterationDuration` from the survivors; when it empties a timeline of children, it shall clear its children flag and cancel the timeline.
  - Tests: `UtilsTargetsTests.RemoveTargetsFromTimeline`, `TimelineCallbacksTests.RemoveNestedAnimations`
- **CORE-PRESTO-032** — When `renderable` is given, `Remove` shall remove tweens only from that timer and its descendants.
  - Tests: `UtilsTargetsTests.RemoveTargetsOnASpecificAnimation`
- **CORE-PRESTO-033** — When a `property` key is given, `Remove` shall remove only the tweens whose canonical property name equals the key's canonical form on that target.
  - Tests: `UtilsTargetsTests.RemoveSpecificTweenProperty`, `UtilsTargetsTests.RemoveSpecificTweenPropertyAndCancelTheAnimationIfNoTweensAreLeft`
- **CORE-PRESTO-034** — When `renderable` is null, `Remove` shall then call `Removed(targets, property)` on every registered adapter, in registration order.
  - Tests: `TargetsTests.RemovingEverythingTellsTheAdapters`
- **CORE-PRESTO-035** — When `renderable` is given, `Remove` shall not notify the adapters.
  - Tests: `TargetsTests.RemovingOneRenderableLeavesTheAdaptersAlone`
- **CORE-PRESTO-036** — `Remove` shall return the parsed targets (flattened, nulls skipped, duplicates kept once), which is empty for null targets.
  - Tests: `UtilsTargetsTests.UndefinedTargetsHaveNothingToGetOrRemove`
- **CORE-PRESTO-037** — When `Remove` runs from a callback during a tick, the engine and timeline child lists shall stay intact and the remaining children keep rendering.
  - Tests: `TimelinesTests.RemovingTheNextChildsTargetFromAChildCallbackKeepsTheRestRendering`

### GetAnimations

- **CORE-PRESTO-038** — `GetAnimations(target)` shall return, in engine child order and depth first through timelines, every animation on `Engine.Current` with a tween whose target is reference-equal to `target`.
  - Tests: `UtilsTargetsTests.GetAnimationsListsTheRunningAnimationsOfATarget`, `UtilsTargetsTests.GetAnimationsFindsTimelineChildren`
- **CORE-PRESTO-039** — When the `property` key's name is non-empty, `GetAnimations` shall return only animations with a tween on its canonical name for that target; a null key or an empty name means any property.
  - Tests: `UtilsTargetsTests.GetAnimationsListsTheRunningAnimationsOfATarget`
- **CORE-PRESTO-040** — `GetAnimations` shall skip completed and canceled animations, and the children of a completed or canceled timeline.
  - Tests: `UtilsTargetsTests.GetAnimationsSkipsCompletedAndCanceledAnimations`
- **CORE-PRESTO-041** — A paused animation shall stay listed until the engine releases it on its next frame, and be listed again once resumed.
  - Tests: `UtilsTargetsTests.GetAnimationsDropsAPausedAnimationOnceTheEngineReleasesIt`

### Adapters and codecs

- **CORE-PRESTO-042** — The facade shall resolve a property by asking `Adapters` in list order and using the first non-null accessor, falling back to the built-in object adapter.
  - Tests: `ObjectTargetAdapterTests.RegisteredAdaptersResolveBeforeTheObjectFallback`
- **CORE-PRESTO-043** — The facade shall canonicalize a property name with the first adapter that returns a non-null name, and otherwise keep the name as given.
  - Tests: untested
- **CORE-PRESTO-044** — When a plain object member's type is not numeric, `string`, `object` or `PrestoColor`, the built-in adapter shall use the codec registered in `Codecs` for that type, or treat the member as discrete when none is registered.
  - Tests: `ObjectTargetAdapterTests.RegisteredCodecDecodesAndEncodesItsType`, `ObjectTargetAdapterTests.MemberTypeWithoutCodecIsDiscrete`

### Argument handling

- **CORE-PRESTO-045** — The facade shall accept null `targets` everywhere it takes them, treating them as no targets.
  - Tests: `TargetsTests.AnimatingNullHasNoTargets`, `UtilsTargetsTests.UndefinedTargetsHaveNothingToGetOrRemove`
- ~~**CORE-PRESTO-046**~~ — Removed: null non-nullable arguments are now rejected where they enter (CORE-PRESTO-055).
- **CORE-PRESTO-055** — When a non-nullable reference argument of a `Presto` method is null, the method shall throw `ArgumentNullException` naming it; nullable parameters keep accepting null.
  - Tests: `TargetsTests.AnimateRejectsNullParamsAndBuilder`, `TargetsTests.SetRejectsNullParamsAndBuilder`, `TargetsTests.CreateAnimatableRejectsNullParams`, `TargetsTests.GetRejectsANullUnit`, `TargetsTests.GetAnimationsRejectsANullTarget`

### Defaults

- **CORE-PRESTO-047** — When no defaults have been changed, `Presto.Defaults` shall hold a fresh `PrestoDefaults` (see CORE-SCOPE).
  - Tests: untested
- **CORE-PRESTO-048** — When `property` is not given, `Remove` shall remove the tweens of every property.
  - Tests: `UtilsTargetsTests.RemoveTarget`

### Delay

- **CORE-PRESTO-049** — `Delay(duration, cancellationToken)` shall return a task that completes once `duration` of `Engine.Current`'s time has passed, counted as a playing timer counts it (including the 12 ms head start of CORE-TIMER-037).
  - Tests: `PrestoDelayTests.CompletesOnceTheDurationOfEngineTimeHasPassed`
- **CORE-PRESTO-050** — The wait shall scale with `Engine.Speed`, so halving the speed doubles the frames it takes; `TimeDilation` slows it the same way.
  - Tests: `PrestoDelayTests.TakesTwiceTheFramesAtHalfSpeed`
- **CORE-PRESTO-051** — When `duration <= 0`, `Delay` shall return an already completed task and create no timer.
  - Tests: `PrestoDelayTests.ANonPositiveDurationIsCompleteOnReturn`
- **CORE-PRESTO-052** — When the token is already canceled, `Delay` shall return a canceled task and create no timer, whatever the duration.
  - Tests: `PrestoDelayTests.AnAlreadyCanceledTokenReturnsACanceledTaskWithoutATimer`
- **CORE-PRESTO-053** — Canceling the token mid-wait shall `Cancel()` the underlying timer, so the engine releases it on its next frame, and complete the task as canceled, so awaiting it throws `OperationCanceledException`.
  - Tests: `PrestoDelayTests.CancelingMidWaitCancelsTheTaskAndReleasesTheTimer`
- **CORE-PRESTO-054** — `Delay` shall dispose its token registration when the timer completes, and its continuations shall never run inline in the engine tick.
  - Tests: untested

## Invariants

- `Presto.Engine` is the same instance for the life of the process; `Defaults` always reads through it.
- Every timer the factories create belongs to `Engine.Current` at creation and registers with that engine's current scope.
- `Animate`, `CreateTimer` and `CreateTimeline` announce a timer only after `Init()`; `Set` announces after it has written.

## Edge cases

- `Set` forces only the duration; a `Delay` in the params or the defaults still reaches its tweens, and what that does to the immediate write is unpinned.
- `Get` reads only the first target; the rest are ignored.
- `Remove` on targets with no tweens anywhere is a no-op apart from notifying the adapters.
- `GetAnimations` takes one target, not a targets parameter; a collection passed as `target` matches nothing.

## Threading and lifetime

- Every member acts on `Engine.Current` (or the main engine for `Engine`/`Defaults`) and must be called on the thread that ticks that engine (see CORE-THREADING).
- `Adapters` and `Codecs` are plain, unsynchronized collections read from every engine's thread; populate them before any animation starts.
- The `Get` scratch value is thread-static.
- `Delay`'s token must be canceled on the engine's thread, as with every other control: its registration cancels the timer inline.

## Reduced motion

- `Animate` and `CreateTimeline` children follow CORE-REDUCED; an `Animatable` applies `ReduceAll` on each retarget instead (CORE-ANIMATABLE-055). `Set` is exempt from `ReduceAll` (it is instant already) but resolves a `WhenReduced` form while `ReducedMotion` is set. `CreateTimer` is unaffected.

## Allocation

- Not on the tick path. `Get`, `Remove` and `GetAnimations` allocate a parsed target list and, for `GetAnimations`, the result list. `Delay` allocates its timer, a task completion source, the token registration and their closures.

## Provenance

- `utils.get`/`set`/`remove` live on `Presto` rather than a `utils` namespace; `$` (selectors) and `cleanInlineStyles` have no equivalent. Reason: there are no selectors, and animated writes leave no inline styles to clean.
- `Get` with a unit converts only angle units; other units are adopted (no DOM to measure against).
- `Remove` also tells registered adapters so they can stop work they drive outside the engine; upstream has the WAAPI-specific equivalent.
- `GetAnimations` has no upstream counterpart; it omits paused animations once released (see "`GetAnimations` omits paused animations").
- `Set` with null parameters throws `ArgumentNullException` rather than being a no-op like upstream's `set(targets)`; the parameters are required.

## Decisions

- **`Set` is a fast-set animation.** `Presto.Set(targets, parameters)` (upstream `utils.set()`) builds an animation through the constructor's fast-set path: a minimal duration and, unless set, `Overlap` composition, resumed at once. Reason: a set value then goes through the same parsing, codecs, relative values and function values as an animated one, with no second write path.
- **`Set` announces like the factories.** `Set` raises the timer-created hook after it has written. Reason: a host hook that sees every other root missed `Set` animations, so whatever it tracks or lists skipped them. Rejected: keeping `Set` silent because it completes before returning.
- **The params overloads win over their lambda twins.** The `AnimationParams` overloads of `Animate` and `Set` carry `[OverloadResolutionPriority(1)]`. Reason: a target-typed `new() { … }` argument then keeps binding to the params overload beside the `Action<AnimationBuilder>` one.
- **`GetAnimations` omits paused animations.** Reason: the engine drops paused tickables from its children, so listing them would need a per-target registry maintained by every state change, and code that pauses already holds the handle. Rejected: a per-target registry of paused animations.
- **`Delay` waits on a plain timer.** `Presto.Delay` is backed by a `CreateTimer` whose `OnComplete` completes the task; why an engine-time wait exists is in CORE-TIMER's Decisions. Rejected: a dedicated engine-side delay list, which would duplicate timer scheduling.
- **`Remove` without a property removes them all.** Reason: removing a target is the cleanup before discarding it, which must stop everything animating it; the property argument narrows that.
- **`$` and `cleanInlineStyles` are not ported.** Reason: there are no selectors, and animated writes leave no inline styles to clean.

## Non-goals

- No selector strings; targets are objects.
- No per-target registry of paused animations: every state change would have to maintain it, and code that pauses already holds the handle.

## Open items

- `CreateMotionValue`, `CreateAnimatable`'s missing hook, `Set`'s forced duration and `Composition.Overlap` default, and `Get(…, withUnit: true)` have no direct test; nor do `Delay`'s registration disposal and asynchronous continuations.
