---
id: CORE-BUILDER
title: Animation builder
package: Monaco.Presto
namespaces:
  - Monaco.Presto
sources:
  - Monaco.Presto/AnimationBuilder.cs
  - Monaco.Presto/PropertyBuilder.cs
types:
  - AnimationBuilder
  - PropertyBuilder
tests:
  - AnimationBuilderTests
  - HostHookTests
  - ColorKeyframesTests
upstream: []
depends:
  - CORE-ANIMATION
  - CORE-KEYFRAMES
  - CORE-TIMELINE
  - CORE-PRESTO
  - CORE-REDUCED
  - CORE-VALUES
---

# Animation builder

## Purpose

`AnimationBuilder` is the fluent, typed spelling of `AnimationParams` and the documented C# form. It only builds params: every terminal snapshots it into fresh `AnimationParams` equal to the indexer form of the same calls, so it adds no runtime type or code path. `PropertyBuilder<TSelf>` is the property half it shares with the keyframe builder (CORE-KEYFRAMES), and the base the bridge's typed property extensions build on. Also specifies `Presto.Build` and the builder and lambda overloads of `Presto.Animate`, `Presto.Set`, `Timeline.Add` and `Timeline.Set`.

## Requirements

### Properties

- **CORE-BUILDER-001** — A new builder, from `Presto.Build()` or the constructor, shall snapshot to params equal to an empty `AnimationParams`.
  - Tests: `AnimationBuilderTests.AnEmptyBuilderMatchesEmptyParams`
- **CORE-BUILDER-002** — `Property(name, to)` shall store `to` as the bare value the indexer `[name] = to` stores, for numbers, strings, staggers, function values and from/to arrays alike.
  - Tests: `AnimationBuilderTests.BareValuesMatchTheIndexer`
- **CORE-BUILDER-003** — When `Property` is given only a non-null `to`, it shall store the bare value; when given any other argument it shall store a `TweenParams` holding exactly the arguments given; given none it shall store an empty `TweenParams`.
  - Tests: `AnimationBuilderTests.PropertyWithOnlyToIsBareAndAnyOtherArgumentMakesTweenParams`, `AnimationBuilderTests.PerPropertySettingsMatchTheIndexer`
- **CORE-BUILDER-004** — A lambda `(target, index, total) => …` passed to `Property` shall bind as a `FunctionValue` without a cast.
  - Tests: `AnimationBuilderTests.FunctionValuesBindWithoutACast`
- **CORE-BUILDER-005** — `Property(name, keyframes)` shall run the action on a fresh `TweenKeyframesBuilder` and store its frames as `TweenValue.Keyframes(...)`, equal to the indexer form.
  - Tests: `AnimationBuilderTests.PerPropertyKeyframesMatchTheIndexer`, `ColorKeyframesTests.TheTweenCompletesExactlyOnTo`
- **CORE-BUILDER-006** — Per-property keyframes built from bare `To` values shall start from the property's current value, not from the first frame.
  - Tests: `AnimationBuilderTests.BareKeyframesStartFromTheCurrentValueRatherThanTheFirstFrame`
- **CORE-BUILDER-007** — Any `TweenValue`, including an `Inertia`, shall pass through `Property` unchanged.
  - Tests: `AnimationBuilderTests.AnInertiaPassesAsAPropertyValue`
- **CORE-BUILDER-008** — Properties shall snapshot in the order first named.
  - Tests: `AnimationBuilderTests.BareValuesMatchTheIndexer`
- **CORE-BUILDER-009** — Naming a property again shall replace its value and keep its first position.
  - Tests: `AnimationBuilderTests.DuplicatePropertyKeepsItsFirstPositionWithTheLastValue`

### Settings

- **CORE-BUILDER-010** — Each setting and callback method shall set the `AnimationParams` member of the same name and return the same builder.
  - Tests: `AnimationBuilderTests.EveryTimerSettingAndCallbackMatchesTheIndexer`
- **CORE-BUILDER-011** — `Loop()` shall set an infinite loop, and `Loop(LoopCount)` the count `LoopCount`'s conversions give (`Loop(3)` three, `Loop(false)` none).
  - Tests: `AnimationBuilderTests.LoopOverloadsMatchLoopCountConversions`
- **CORE-BUILDER-012** — `Alternate()` and `Reversed()` without an argument shall set true.
  - Tests: `AnimationBuilderTests.EveryTimerSettingAndCallbackMatchesTheIndexer`
- **CORE-BUILDER-013** — Setting a value or callback again shall replace it; callbacks never chain.
  - Tests: `AnimationBuilderTests.DuplicateSettingsAndCallbacksKeepTheLastValue`
- **CORE-BUILDER-014** — `Keyframes(action)` shall run the action on a fresh `KeyframesBuilder` and replace any animation-level keyframes set before, duration- or percentage-based alike.
  - Tests: `AnimationBuilderTests.DurationBasedKeyframesMatchTheIndexer`, `AnimationBuilderTests.PercentageBasedKeyframesMatchTheIndexerAndTheLastAtAndKeyframesCallWin`
- **CORE-BUILDER-015** — When the keyframes action mixes `Frame` and `At`, or passes `At` a percent outside 0–100, `Keyframes` shall throw `InvalidOperationException` or `ArgumentOutOfRangeException` respectively (CORE-KEYFRAMES).
  - Tests: `AnimationBuilderTests.MixingFrameAndAtThrows`, `AnimationBuilderTests.AtOutsideZeroToHundredThrows`

### Snapshots and terminals

- **CORE-BUILDER-016** — `ToParams` shall return new params on every call, with their own property dictionary, so later changes to the builder affect neither earlier snapshots nor each other.
  - Tests: `AnimationBuilderTests.ToParamsReturnsIndependentSnapshots`
- **CORE-BUILDER-017** — `ToParams` shall build new animation-level keyframes on every call.
  - Tests: `AnimationBuilderTests.ToParamsBuildsFreshKeyframesEachCall`
- **CORE-BUILDER-018** (inherited) — Snapshots shall share the stored values and delegates by reference; only the containers are copied.
  - Tests: `AnimationBuilderTests.EveryTimerSettingAndCallbackMatchesTheIndexer`, `AnimationBuilderTests.SettingsChainedAfterWhenReducedCarryIntoTheReducedFormButMainPropertiesDoNot`
- **CORE-BUILDER-019** — An animation built from a builder shall render the same values on every frame as one built from the equivalent indexer params, across loops, alternation and keyframes.
  - Tests: `AnimationBuilderTests.BuilderAndIndexerAnimationsReadTheSameValuesAcrossLoopsAndAlternation`, `AnimationBuilderTests.KeyframedBuilderAndIndexerAnimationsReadTheSameValues`
- **CORE-BUILDER-020** — `ToParams` shall build the reduced form instead while the current engine's `ReducedMotion` is set and a `WhenReduced` action exists (CORE-REDUCED).
  - Tests: `AnimationBuilderTests.WhenReducedIsPickedOnlyWhileReducedMotionIsSet`
- **CORE-BUILDER-021** — `Animate(targets)` shall return `Presto.Animate(targets, ToParams())`.
  - Tests: `AnimationBuilderTests.LambdaAnimateAndSetMatchTheBuilderForm`
- **CORE-BUILDER-022** — `Set(targets)` shall return `Presto.Set(targets, ToParams())`, writing at once.
  - Tests: `AnimationBuilderTests.SetWritesImmediately`
- **CORE-BUILDER-023** (inherited) — The builder's internal host lifetime shall survive `ToParams`, `Clone` and `With`.
  - Tests: `HostHookTests.HostLifetimeSurvivesBuilderCloneWithAndToParams`

### Clone and With

- **CORE-BUILDER-024** — `Clone` shall return a new builder with this one's settings, properties in order, a deep copy of its keyframes and its reduced action, independent of the source in both directions.
  - Tests: `AnimationBuilderTests.CloneIsIndependentOfItsSourceBothWays`, `AnimationBuilderTests.CloneAndWithCarryTheReducedFormAndWithLeavesTheSourceAlone`
- **CORE-BUILDER-025** — `With(change)` shall return a clone with `change` applied, leaving the source untouched; a property it overrides keeps the source's position.
  - Tests: `AnimationBuilderTests.WithChangesOnlyTheCopyAndAnOverrideKeepsItsPosition`

### Overloads elsewhere

- **CORE-BUILDER-026** — Each `Timeline.Add`/`Set` overload taking a builder shall snapshot it with `ToParams()` at the call and lay the child out exactly as the params overload would.
  - Tests: `AnimationBuilderTests.TimelinesBuiltWithBuildersLayOutLikeTimelinesBuiltWithParams`
- **CORE-BUILDER-027** — Each overload taking an `Action<AnimationBuilder>` shall run it on a fresh builder and behave as the builder overload with that builder.
  - Tests: `AnimationBuilderTests.TimelinesBuiltWithLambdasLayOutLikeTimelinesBuiltWithBuilders`, `AnimationBuilderTests.LambdaAnimateAndSetMatchTheBuilderForm`
- **CORE-BUILDER-028** — A target-typed `new() { … }` argument shall keep binding to the `AnimationParams` overloads beside the builder and lambda ones.
  - Tests: `AnimationBuilderTests.TargetTypedParamsStillBindBesideTheLambdaOverloads`

## Invariants

- Anything the builder can express, the indexer can express too; `ToParams` output is indistinguishable from the indexer form of the same calls.
- No terminal mutates the builder; a builder can be reused across targets and calls.

## Edge cases

- When the `Keyframes` action throws, the builder keeps the partly filled keyframes builder it had already installed.
- `Property(name)` with no arguments stores an empty `TweenParams`; how an animation resolves it is CORE-ANIMATION's.
- The builder is mutable and not thread-safe; share it only through snapshots, `Clone` or `With`.

## Threading and lifetime

- Configuration and `ToParams` touch no engine, except that `ToParams` reads `Engine.Current.Defaults.ReducedMotion`; terminals run on the current engine's thread like `Presto.Animate`.
- A builder keeps references to every value and delegate given to it until it is collected.

## Reduced motion

- `WhenReduced` and its selection by `ToParams` are specified in CORE-REDUCED.

## Allocation

- Configuration time only; never on the tick path. Each `ToParams` allocates params, a property dictionary and keyframes.

## Deviations from anime.js

- No upstream counterpart; the builder is a C# spelling over the anime.js parameter object, which it only builds, so it adds no behavior to deviate.

## Decisions

- **The builder only builds params.** Every terminal snapshots the builder into an `AnimationParams` and hands it to `Presto.Animate` (or `Set`); there is no new runtime type, no second code path and no change to `Animation`, `Tween` or the tick, and `Property(name, to)` alone writes a bare value while any other argument writes a `TweenParams`, exactly as the indexer does. Reason: the builder had to layer on without changing the model, and equivalence with the indexer is what the tests pin.
- **Terminals return a live animation.** A terminal returns the `Animation` (the bridge's compositor terminal its `CompositorAnimation`), so `Seek`, `Reverse`, `Stretch` and `Then()` work as with params; the call shape (per-property methods with `from`/`to`, per-property timing, nested keyframes, a reusable schedule) is borrowed from the Community Toolkit's `AnimationBuilder`. Reason: Presto's model is a live, controllable animation. Rejected: the Toolkit's fire-and-forget `Start(element)` with `CancellationToken`-only control, the one deliberate divergence from it.
- **The builder is the documented, typed spelling; the indexer stays beside it.** Docs lead with the builder and its typed bridge methods; the string indexer remains the anime.js-shaped layer underneath, for run-time names, anime.js ports and `Animatable`, which keeps string names because its params read a bare number as a duration. Earlier the indexer was the primary spelling; replaced because the builder already types every shorthand and settings were already typed members. Rejected: replacing the indexer (it mirrors the JS object literal and every ported upstream test uses it); a `Shorthands` class of key constants (built and withdrawn: it typed only the indexer and added a third spelling); a key struct (needs a second indexer overload); an analyzer (could check only shorthands, since any registered bridge property is a valid key).
- **A builder is a reusable recipe; terminals snapshot.** A builder is mutable while configured, and reusing or changing it after a call never affects an animation already created. Reason: `AnimationParams`' property dictionary is shared by `with` copies, so handing out the builder's own dictionary would leak later changes.
- **Core names only what core knows.** The core offers the generic `Property(name, …)`, also used for plain-object targets; typed methods for transform shorthands and bridge properties are bridge extension methods generic over `PropertyBuilder<TSelf>`, so they chain on both `AnimationBuilder` and `KeyframeBuilder`. Reason: shorthands and UI-framework properties are bridge concepts.
- **Last write wins, first position stays.** Naming a property or setting again replaces the earlier value and keeps its original position; callbacks replace, they never chain. Reason: matches a duplicate key in a JS object literal and the ordered dictionary behind the indexer.
- **`Presto.Build()` and a public constructor, with hand-written overloads.** The entry point is `Presto.Build()` beside `CreateTimer`/`CreateTimeline`, plus a public constructor; the per-property overloads (bare, full, function value, keyframes) are written by hand. Reason: hand-written is fine at this size. Rejected: a source generator for the overloads, not worth it unless the list grows.
- **Params overloads keep priority over builder overloads.** The `AnimationParams` overloads of `Timeline.Add` and `Timeline.Set` carry `[OverloadResolutionPriority(1)]`, as do the bridge's params overloads of `control.Animate` and `AnimateOnCompositor`. Reason: adding builder and lambda overloads made every existing target-typed `new() { … }` call ambiguous; the attribute keeps them binding as before.
- **The params overloads win over their lambda twins.** See CORE-PRESTO.
- **No builders for the other params types.** There are no builders for timer, timeline, animatable, draggable or scroll-observer params. Reason: their object initializers have no string keys, so a builder buys little; revisit if users ask.
- **`AnimationBuilder` is the value lifecycle surfaces and recipes carry.** Every lifecycle surface and every recipe takes or returns an `AnimationBuilder`; lambdas are sugar at one-shot call sites only. Reason: a lambda cannot be an attached-property value, an `x:Static` or markup-extension result, or a dictionary entry.
- **Lambdas are `Action<AnimationBuilder>`.** The lambda overloads of `Presto.Animate`/`Set` and `Timeline.Add`/`Set` run the action on a fresh builder, and an expression lambda's value is discarded. Reason: matches the existing `Keyframes(Action<KeyframesBuilder>)` and `Property(name, Action<TweenKeyframesBuilder>)` shapes.
- **No "base builder plus modifier" overloads.** Each call site gets one builder overload and one lambda overload. Reason: recipes are factories, so a recipe call already returns a fresh builder to modify, and `With` covers shared static builders.
- **`Clone` and `With` vary a shared builder without mutating it.** `Clone` is a deep copy (settings, properties, keyframes, reduced alternative) and `With(change)` is clone-then-change. Reason: the only alternatives were mutating the shared builder for everyone, or `ToParams() with { … }`, which cannot add or override a property and yields params that builder-only surfaces reject.
- **Builders are not freezable.** No `Freeze`; the library's own shared default builders hand out a `Clone()` from their getters instead. Reason: mutating a shared builder is sometimes the intent (a slider retuning a recipe for later plays), and guarding every setter, keyframes and `WhenReduced` is not worth it; `Freeze()` can be added later without a break. Rejected: `Freeze()`, cost not worth it now.
- **Reduced motion is an app-set flag with a per-builder alternative.** See CORE-REDUCED.

## Non-goals

- Builders for `TimerParams`, `TimelineParams`, `AnimatableParams` or other parameter objects.
- Typed property names in the core; typed shorthands are bridge extension methods over `Property` (see BRIDGE-HELPERS).

## Open items

- A builder passed to a timeline on another engine reads the current engine's `ReducedMotion` (see CORE-REDUCED).
