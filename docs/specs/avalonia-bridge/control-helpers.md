---
id: BRIDGE-HELPERS
title: Control helpers, typed builder methods, scopes and WhenDetached
package: MonacoLabs.Presto.Avalonia
namespaces:
  - MonacoLabs.Presto
sources:
  - MonacoLabs.Presto.Avalonia/PrestoAvalonia.cs
  - MonacoLabs.Presto.Avalonia/AnimateExtensions.cs
  - MonacoLabs.Presto.Avalonia/AnimationBuilderExtensions.cs
  - MonacoLabs.Presto.Avalonia/PropertyBuilderExtensions.cs
  - MonacoLabs.Presto.Avalonia/PrestoKeys.cs
  - MonacoLabs.Presto.Avalonia/ControlScope.cs
  - MonacoLabs.Presto.Avalonia/WhenDetached.cs
  - MonacoLabs.Presto.Avalonia/Targets/DetachTracker.cs
types:
  - PrestoAvalonia
  - AnimateExtensions
  - AnimationBuilderExtensions
  - PropertyBuilderExtensions
  - PrestoKeys
  - ControlScope
  - WhenDetached
tests:
  - TargetUtilsTests
  - AnimationBuilderBridgeTests
  - PrestoKeysTests
  - PropertyNamingTests
  - AnimatableBridgeTests
  - ControlScopeTests
  - WhenDetachedTests
  - StaggerLayoutTests
  - TargetAdapterTests
upstream:
  - src/utils/target.js
  - src/scope/scope.js
  - tests/suites/utils.test.js
  - tests/suites/scope.test.js
depends:
  - CORE-PRESTO
  - CORE-UTILS
  - CORE-BUILDER
  - CORE-ANIMATABLE
  - CORE-SCOPE
  - CORE-STAGGER
  - CORE-TIMELINE
  - BRIDGE-INIT
  - BRIDGE-TARGETS
---

# Control helpers, typed builder methods, scopes and WhenDetached

## Purpose

The everyday C# surface of the bridge: `control.Animate(...)` and the other `AnimateExtensions`, the typed builder methods (`.X(320)`, `.Blur(4)`), which are the documented typed spelling of property keys, `PrestoAvalonia`'s `Get`/`Set`/`Remove` that install the bridge first, `ControlScope` that reverts with a control, and `WhenDetached`, which pauses or reverts animations whose controls leave the visual tree. Each entry point installs the bridge (BRIDGE-INIT) and resolves properties through BRIDGE-TARGETS.

## Requirements

### Get, Set, Remove

- **BRIDGE-HELPERS-001** — `PrestoAvalonia.Get`, `Set` and both `Remove` overloads shall call `EnsureInitialized` and then do what the core `Presto.Get`, `Presto.Set` and `Presto.Remove` do (CORE-UTILS), so shorthand names resolve even before anything else has installed the bridge.
  - Tests: `TargetUtilsTests.GetReadsBackAComposedTransformShorthand`, `TargetUtilsTests.RemoveStopsAnAnimationFromWritingATarget`
- **BRIDGE-HELPERS-002** — `Get` with a unit shall return the value converted to that unit as a string (`"0.25turn"` for a `Rotate` of 90).
  - Tests: `TargetUtilsTests.GetReadsBackAComposedTransformShorthand`
- **BRIDGE-HELPERS-003** — The `AvaloniaProperty` overloads of `Get`, `Remove` and `GetAnimations` shall act on the property's encoded key (BRIDGE-TARGETS-007).
  - Tests: `TargetUtilsTests.GetResolvesATypedProperty`, `TargetUtilsTests.RemoveStopsAnAnimationFromWritingATarget`, `TargetAdapterTests.GetAnimationsFiltersByAvaloniaProperty`
- **BRIDGE-HELPERS-004** — `Set` shall write the values at once at `Animation` priority and return an `Animation` whose `Revert` uncovers the values underneath.
  - Tests: `TargetUtilsTests.SetWritesAtAnimationPriorityAndRevertUncoversTheLocalValue`
- **BRIDGE-HELPERS-005** — `Remove` shall return the targets it removed tweens from and cancel animations left with no tweens, leaving the property at its last written value.
  - Tests: `TargetUtilsTests.RemoveStopsAnAnimationFromWritingATarget`

### Animate extensions

- **BRIDGE-HELPERS-006** — `target.Animate(parameters)` shall install the bridge and return `Presto.Animate(target, parameters)`, and a target-typed `new() { … }` argument shall bind to its `AvaloniaAnimationParams` overload.
  - Tests: `AnimationBuilderBridgeTests.TargetTypedParamsStillBindToTheParamsOverloads`, `TargetAdapterTests.NamedPropertiesAnimateToCompletion`
- **BRIDGE-HELPERS-007** — `target.Animate(builder)` shall install the bridge and animate a snapshot of the builder, even one built with core methods only.
  - Tests: `AnimationBuilderBridgeTests.AnimatingAControlWithACoreOnlyBuilderInstallsTheBridge`
- **BRIDGE-HELPERS-008** — `target.Animate(build)` shall animate exactly as the builder the lambda configures on a fresh `Presto.Build()`.
  - Tests: `AnimationBuilderBridgeTests.ALambdaAnimatesAsItsBuilderDoes`
- **BRIDGE-HELPERS-009** — `CreateAnimatable` shall install the bridge and return `Presto.CreateAnimatable(target, parameters)`.
  - Tests: `AnimatableBridgeTests.AnAnimatableDrivesTransformShorthandsAndABrush`
- **BRIDGE-HELPERS-010** (inherited) — The `Animatable` extensions `Set`, `Get` and `GetChannels` taking an `AvaloniaProperty` shall act on the property's encoded key, so a property declared by name is reached by its typed handle.
  - Tests: `PropertyNamingTests.AnAnimatableDeclaredByNameIsReadAndSetByHandle`

### Typed builder methods

- **BRIDGE-HELPERS-011** — Each named builder method shall add the property under the same key the string indexer uses, each one whose key `PrestoKeys` holds shall write that `PrestoKeys` key, and every overload of one name shall write that one key.
  - Tests: `AnimationBuilderBridgeTests.TypedMethodsWriteTheIndexerKeys`, `AnimationBuilderBridgeTests.EveryOverloadWritesTheSameKey`, `AnimationBuilderBridgeTests.EffectAndRevealOverloadsWriteTheSameKey`, `AnimationBuilderBridgeTests.ImageFilterOverloadsWriteTheSameKey`
- **BRIDGE-HELPERS-012** — The typed value overloads shall wrap their value as the indexer would (`TweenValue.Of`), `Background`/`Foreground(Color)` as an immutable solid brush and `Draw(start, end)` as the invariant-culture string `"start end"`.
  - Tests: `AnimationBuilderBridgeTests.TypedValuesAreWrappedAsTheIndexerWouldWrapThem`, `AnimationBuilderBridgeTests.TypedEffectValuesAreWrappedAsTheIndexerWouldWrapThem`, `AnimationBuilderBridgeTests.BackgroundAnimatesToAColor`
- **BRIDGE-HELPERS-013** — The image filter methods shall write the keys of the matching `PrestoImageBase` properties (BRIDGE-IMAGE).
  - Tests: `AnimationBuilderBridgeTests.ImageFilterMethodsWriteTheIndexerKeysAndValues`, `AnimationBuilderBridgeTests.AFilterMethodAnimatesEveryImageOnTheBase`
- **BRIDGE-HELPERS-014** — `Property(AvaloniaProperty, …)` shall write the property's encoded key, so it replaces a string-keyed tween of the same property.
  - Tests: `AnimationBuilderBridgeTests.ATypedPropertyReplacesAStringKeyedAnimationOfTheSameProperty`
- **BRIDGE-HELPERS-015** — The methods shall work on every `PropertyBuilder<T>`, keyframe frames included, and each shall call `EnsureInitialized`.
  - Tests: `AnimationBuilderBridgeTests.ShorthandsChainOnKeyframeFrames`
- **BRIDGE-HELPERS-016** — `AnimationBuilderExtensions.WhenDetached` shall set the builder's host-lifetime slot, which `ToParams` carries into the parameters.
  - Tests: `WhenDetachedTests.TheBuilderOverrideWinsOverTheDefault`

### PrestoKeys

- **BRIDGE-HELPERS-034** — `PrestoKeys` shall hold one key for every shorthand `AvaloniaObjectTargetAdapter` resolves before the property registry (the transform, effect, clip, mask and warp shorthands, `Draw`, `ScrollX` and `ScrollY`), one for each of the `Text` and `Data` pseudo-properties the typed builder methods write, and nothing else (see PrestoKeys names shorthands and pseudo-properties).
  - Tests: `PrestoKeysTests.HoldsOneKeyPerShorthandAndPseudoPropertyAndNothingElse`
- **BRIDGE-HELPERS-035** — Each shorthand key shall resolve on a target of the type it applies to and canonicalize to its own name; `Text` and `Data` shall resolve on a `TextBlock` and a `Path` (see Text and Data keys resolve by name).
  - Tests: `PrestoKeysTests.EachShorthandKeyResolvesOnItsTargetAndCanonicalizesToItsOwnName`, `PrestoKeysTests.TextAndDataResolveOnATextBlockAndAPath`
- **BRIDGE-HELPERS-036** — `PrestoKeys.Of(property)` shall return the key BRIDGE-TARGETS-007 encodes, and reading any `PrestoKeys` member shall not install the bridge.
  - Tests: `PrestoKeysTests.OfReturnsTheEncodedName`, `PrestoKeysTests.AccessingTheKeysDoesNotInstallTheBridge`

### Stagger

- **BRIDGE-HELPERS-017** — `PrestoAvalonia.Stagger` shall return the core stagger with `AutoGrid` on and each target's coordinates measured as the center of its laid-out bounds in its visual root.
  - Tests: `StaggerLayoutTests.ComputesAutoGridFromLaidOutControlPositions`
- **BRIDGE-HELPERS-018** (inherited) — Visuals with no layout yet shall all measure at the origin.
  - Tests: `StaggerLayoutTests.ControlsWithoutALayoutShareTheOrigin`

### ControlScope

- **BRIDGE-HELPERS-019** — `PrestoAvalonia.CreateScope(root, parameters)` shall create a scope (CORE-SCOPE) with `Root` set to the control, whose defaults apply to what is created inside it.
  - Tests: `ControlScopeTests.AppliesScopedDefaultsToAnimationsCreatedInside`
- **BRIDGE-HELPERS-020** — When the root raises `DetachedFromVisualTree`, the scope shall revert everything created inside it, including for a root detached before its `Loaded` ran.
  - Tests: `ControlScopeTests.RevertsWhatWasCreatedInsideWhenTheRootIsDetached`, `ControlScopeTests.RevertsARootDetachedBeforeItEverLoads`
- **BRIDGE-HELPERS-021** (inherited) — Once the scope is reverted, by hand, by its detach or by an enclosing scope reverting it, it shall stop listening, so a later detach does nothing.
  - Tests: `ControlScopeTests.RevertingByHandMakesTheDetachANoOp`, `ControlScopeTests.RevertingAnEnclosingScopeMakesTheDetachANoOp`

### WhenDetached

- **BRIDGE-HELPERS-022** — When an animation or timeline is created on `Presto.Engine`, its mode shall be its own `WhenDetached` (params or builder, inherited by its reduced form), else `PrestoAvalonia.WhenDetached` read at that moment, which defaults to `Pause`.
  - Tests: `WhenDetachedTests.TheParamsOverrideWinsOverTheDefault`, `WhenDetachedTests.TheBuilderOverrideWinsOverTheDefault`, `WhenDetachedTests.TheReducedFormKeepsTheOverride`
- **BRIDGE-HELPERS-023** — Under `Continue`, an animation shall keep playing when its visuals leave the tree.
  - Tests: `WhenDetachedTests.ContinueKeepsPlaying`
- **BRIDGE-HELPERS-024** — When an animation has a target that maps to no visual (BRIDGE-TARGETS-024), it, and a timeline holding it, shall continue for good, resuming if the tracker had paused it.
  - Tests: `WhenDetachedTests.AVisualAndAPlainObjectTogetherContinue`, `WhenDetachedTests.ATimelineWithAPlainObjectChildContinues`, `WhenDetachedTests.AnAnimationOnABrushTheBridgeMadeFollowsItsVisual`
- **BRIDGE-HELPERS-025** — Under `Pause`, when the last attached visual of a tracked root leaves the tree, the root shall pause unless already paused, and leave the engine so the engine can idle.
  - Tests: `WhenDetachedTests.ADetachedLoopPausesAndTheEngineIdles`
- **BRIDGE-HELPERS-026** — Only a transition from attached to detached shall count, so an animation created before its control attaches runs until a later detach.
  - Tests: `WhenDetachedTests.AnAnimationCreatedBeforeAttachKeepsRunningAndPausesOnALaterDetach`
- **BRIDGE-HELPERS-027** — When a visual of a root the tracker paused is attached again, the root shall resume from the same progress, and an awaited `Then` completes afterwards.
  - Tests: `WhenDetachedTests.ReattachingResumesAtTheSameProgress`, `WhenDetachedTests.AnAwaitedThenCompletesAfterReattach`
- **BRIDGE-HELPERS-028** — Reattaching shall not resume a root the user paused, and a user resume while detached shall resume it.
  - Tests: `WhenDetachedTests.AUserPauseBeforeDetachIsNotResumedOnReattach`, `WhenDetachedTests.AUserResumeWhileDetachedResumes`
- **BRIDGE-HELPERS-029** — Under `Revert`, when the last attached visual leaves the tree, the root shall revert, writing back the values underneath, every child of a timeline included.
  - Tests: `WhenDetachedTests.RevertWritesTheBaseValuesBack`, `WhenDetachedTests.RevertRevertsEveryChild`
- **BRIDGE-HELPERS-030** — A root timeline shall gather the visuals of the child animations added to it and pause or revert only once every one has detached; a child never pauses on its own.
  - Tests: `WhenDetachedTests.ATimelinePausesOnlyOnceEveryChildsVisualHasDetachedAndResumesOnReattach`
- **BRIDGE-HELPERS-031** — A control detached for good shall be collectable together with its paused animation.
  - Tests: `WhenDetachedTests.AControlDetachedForGoodIsCollectedWithItsAnimation`
- **BRIDGE-HELPERS-032** — Presence, Motion, page transitions and layout transitions shall manage their own lifetime and not be paused by the tracker (see Features that manage their own lifetimes pass `WhenDetached.Continue`).
  - Tests: `WhenDetachedTests.AnExitGhostRunsToCompletionWhenItsWindowCloses`, `WhenDetachedTests.APresenceEnterOnADetachingControlIsRevertedByPresenceNotHeldPaused`, `WhenDetachedTests.AMotionStateAnimationIsLeftToMotion`, `WhenDetachedTests.APageTransitionFinishesWhenItsPagesDetach`, `WhenDetachedTests.ALayoutTransitionFinishesWhenItsControlDetaches`
- **BRIDGE-HELPERS-033** (inherited) — An animation already completed when created (a `Set`) shall not be tracked.
  - Tests: untested

## Invariants

- Every public entry point here, except `GetAnimations` and `PrestoKeys`, installs the bridge before it resolves anything.
- A tracked root is paused by the tracker only while none of its visual targets is attached.

## Edge cases

- `GetAnimations` does not install the bridge; before installation the typed key matches nothing.
- A `ControlScope` reverts at most once on its own: reattaching the root does not re-arm it, and a root that never attaches never reverts automatically.
- `PrestoAvalonia.Set` has no builder overload; a builder is applied with `Presto.Set` or with a zero-duration animation.
- Changing `PrestoAvalonia.WhenDetached` affects only animations created afterwards.
- `Stagger` measures a non-visual target as no coordinates, which leaves it to the core's handling.

## Threading and lifetime

- UI thread only. Detach trackers hang off each visual in an attached value and are dropped once their roots end; the root-to-tracker table is weak.
- Only `Presto.Engine` has the tracker hook; animations on other engines are not tracked.

## Reduced motion

- A reduced form keeps the `WhenDetached` override (BRIDGE-HELPERS-022). Otherwise not affected: the helpers pass parameters to the core, which picks reduced forms (CORE-REDUCED).

## Allocation

- Not on the tick path. Tracking costs one tracker per root and one attached list per visual, at creation.

## Provenance

- `WhenDetached` has no upstream counterpart: upstream animations keep running on removed elements, but here a loop on a removed control would keep the engine awake and the control alive (see Animations pause when their controls leave the tree).
- Upstream `utils.get`/`set`/`remove` take DOM targets; these take Avalonia objects, typed properties and the bridge's shorthands.

## Decisions

- **Animations pause when their controls leave the tree.** An animation or timeline on `Presto.Engine` is `Continue`, `Pause` or `Revert` when every visual it animates has left the visual tree; the bridge default `PrestoAvalonia.WhenDetached` is `Pause`, resuming from the same progress when one returns, and `Revert` is opt-in auto-scoping. Params and builder overrides win and a reduced form keeps the override; only an attached-to-detached transition counts, and a root the user paused stays paused. Earlier, scoping was explicit only (`ControlScope`); replaced because SwiftUI, Compose and Flutter tie an animation to its view's lifetime and users expect the same. Rejected: `Continue` as the default (a loop on a dropped control kept ticking, the engine never idled and its child list kept the control alive); `Revert` as the default (detaches are often brief, such as a reparent, tab switch or recycling, and reverting loses progress irreversibly).
- **Detach tracking hangs off the control; `TargetOwner` always records.** Trackers live in an attached value on each watched visual with one shared attach/detach handler pair, the root-to-tracker table is weak, and `TargetOwner.Record` runs always, so bridge-made brushes and transforms follow their visual. Reason: a paused animation leaves the engine list, so a static table would leak a control that never returns; recording measured about 90 bytes and 110 ns once per created target, never per frame, so a second, diagnostics-only path was not worth it.
- **Timelines pause as a whole.** A root timeline gathers its children's visuals and pauses once all have detached, resuming when any returns; a child never pauses alone, and a non-visual target makes its root continue for good. Reason: not recorded.
- **Features that manage their own lifetimes pass `WhenDetached.Continue`.** Presence (ghosts included), Motion and page transitions pass it themselves; Connected and the compositor mirrors animate a plain scratch object, which the tracker never pauses. Reason: those features already decide when their animations end.
- **`ControlScope` reverts on detach, not `Unloaded`.** `PrestoAvalonia.CreateScope(control)` reverts when the root detaches from the visual tree, with no `AutoRevert` parameter. Reason: `Unloaded` never fires for a root detached before its `Loaded` job runs. Rejected: reverting on `Unloaded`.
- **Bridge builder extensions self-initialize.** Every typed builder method and `control.Animate(builder)` calls `PrestoAvalonia.EnsureInitialized()`, as `AvaloniaAnimationParams`' constructor does. Reason: a builder naming a shorthand, or using only core `Property(PropertyKey, …)`, must work on a control without `UsePresto()`. `PrestoKeys` access does not install the bridge: a key is only a name, and the API it is passed to installs it.
- **Typed methods cover the most-used properties.** They cover the eight transform shorthands, `Opacity`, `Width`, `Height`, `Background`, `Foreground`, `CornerRadius`, `BorderThickness`, `Margin`, `Padding` and the `Draw`/`Text`/`Data` pseudo-properties; everything else is `Property(SomeControl.SomeProperty, …)`. Reason: chosen by string-key frequency across the samples; the typed `AvaloniaProperty` overload is refactor-safe, so the list need not grow to cover every control.
- **Builder methods write the typed property key.** The image filter methods write the `PrestoImageBase.*Property` key rather than a name, and `TintColor`, `DuotoneShadow` and `DuotoneHighlight` also take a `Color`. Reason: the key resolves on every subclass, as the `AvaloniaAnimationParams` indexer does; the `Color` overloads mirror `ShadowColor`. Rejected: resolving to the `FilteredImage` properties by name.
- **`PrestoKeys` names shorthands and pseudo-properties.** It holds the shorthands, `Text` and `Data`, and no names for real properties such as `Opacity`, `Background` or the image filters. Reason: real properties are already typed and refactor-safe through their `AvaloniaProperty` fields (`PrestoKeys.Of` where a key value is needed), so mirroring them would add a second typed spelling. Rejected: `PrestoKeys.Opacity` and friends.
- **Text and Data keys resolve by name.** `PrestoKeys.Text` and `PrestoKeys.Data` are the plain names the builder's `Text` and `Data` methods write, which resolve through the registry on a `TextBlock` or `Path` and canonicalize to the encoded property, not to their own name. Reason: the keys must stay byte-identical to what the builder already writes, so swapping a method for its key changes nothing.
- **`GetAnimations` omitting paused animations is documented, not fixed.** Reason: the toast host, which keeps each countdown on its `Toast`, removed the only workaround that needed paused animations listed.
- **The builder is the documented, typed spelling; the indexer stays beside it.** See CORE-BUILDER.
- **Core names only what core knows.** See CORE-BUILDER.
- **Params overloads keep priority over builder overloads.** See CORE-BUILDER.
- **Lambdas are `Action<AnimationBuilder>`.** See CORE-BUILDER.
- **Auto grids measure through a coordinates provider.** See CORE-STAGGER.

## Non-goals

- No analyzer for indexer keys.
- No automatic scoping by `Animate` beyond `WhenDetached` (see `ControlScope` reverts on detach, not `Unloaded`).

## Open items

- Phase 71 asked for a test that fails when a shorthand is added without a typed builder method; resolved in part by `PrestoKeysTests.HoldsOneKeyPerShorthandAndPseudoPropertyAndNothingElse` (a shorthand without a key fails); a shorthand without a builder method is still unchecked.
- BRIDGE-HELPERS-033 is untested.
