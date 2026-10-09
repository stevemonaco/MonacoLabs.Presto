---
id: BRIDGE-TARGETS
title: Avalonia property resolution and writes
package: Monaco.Presto.AvaloniaBridge
namespaces:
  - Monaco.Presto.AvaloniaBridge
  - Monaco.Presto.AvaloniaBridge.Targets
sources:
  - Monaco.Presto.AvaloniaBridge/Targets/AvaloniaObjectTargetAdapter.cs
  - Monaco.Presto.AvaloniaBridge/Targets/AnimatedValue.cs
  - Monaco.Presto.AvaloniaBridge/Targets/TargetOwner.cs
  - Monaco.Presto.AvaloniaBridge/Targets/VisibilityHold.cs
  - Monaco.Presto.AvaloniaBridge/AvaloniaAnimationParams.cs
  - Monaco.Presto.AvaloniaBridge/AvaloniaAnimatableParams.cs
types:
  - AvaloniaObjectTargetAdapter
  - AvaloniaAnimationParams
  - AvaloniaAnimatableParams
tests:
  - TargetAdapterTests
  - PropertyNamingTests
  - DiscretePropertyTests
  - TargetOwnerTests
  - VisibilitySpikeTests
  - AnimatableBridgeTests
  - CompositorRegistryTests
  - AllocationTests
upstream:
  - src/core/targets.js
  - src/core/values.js
  - tests/suites/targets.test.js
depends:
  - CORE-TARGETS
  - CORE-ANIMATION
  - CORE-ANIMATABLE
  - CORE-REDUCED
  - BRIDGE-INIT
  - BRIDGE-CODECS
  - BRIDGE-TRANSFORMS
  - BRIDGE-EFFECTS
---

# Avalonia property resolution and writes

## Purpose

`AvaloniaObjectTargetAdapter` is the core target adapter (CORE-TARGETS) for every `AvaloniaObject`: it turns a property name or a typed `AvaloniaProperty` into an accessor, writes animated values at `BindingPriority.Animation` and takes them away again on revert. Bridge shorthands (BRIDGE-TRANSFORMS, BRIDGE-EFFECTS, the `Warp`, `WarpPhase`, `WarpOrigin` and `WarpRadius` shorthands of BRIDGE-WARP, and the `Draw` and `ScrollX`/`ScrollY` shorthands of BRIDGE-SHAPES and BRIDGE-SCROLL) are resolved here before the property registry. `AvaloniaAnimationParams` and `AvaloniaAnimatableParams` add the typed-property indexer to the core parameter objects.

## Requirements

### Resolution

- **BRIDGE-TARGETS-001** — When the target is not an `AvaloniaObject`, the adapter shall return `null` from `Resolve` and `Canonicalize`, leaving it to the next adapter.
  - Tests: untested
- **BRIDGE-TARGETS-002** — When the target is a `Visual`, the adapter shall resolve the transform shorthands (BRIDGE-TRANSFORMS), then the effect, clip and mask shorthands (BRIDGE-EFFECTS), then the warp shorthands on a `TextBlock` (BRIDGE-WARP), then `"Draw"` on a `Shape` and `"ScrollX"`/`"ScrollY"` on a `ScrollViewer`, before looking in the property registry, so a shorthand wins over a registered property of the same name.
  - Tests: `PropertyNamingTests.TransformShorthandsKeepTheirOwnName`, `TransformsTests.XAndYComposeIntoASingleTranslateTransform`
- **BRIDGE-TARGETS-003** — When a shorthand name is used on a target that does not take it (a non-`Visual`, `"Warp"` on a non-`TextBlock`, `"Draw"` on a non-`Shape`, `"ScrollX"` on a non-`ScrollViewer`), the adapter shall look the name up as a registered property like any other.
  - Tests: `ScrollShorthandTests.TheShorthandsResolveOnlyOnAScrollViewer`, `TextWarpTests.TheShorthandsResolveOnlyOnATextBlock`
- **BRIDGE-TARGETS-004** — When the name has no dot, the adapter shall resolve it through `AvaloniaPropertyRegistry.FindRegistered` for the target's runtime type.
  - Tests: `TargetAdapterTests.NamedPropertiesAnimateToCompletion`
- **BRIDGE-TARGETS-005** (inherited) — When the name is `"Owner.Name"`, the adapter shall match it against the attached properties registered for the target's type first, then its other registered properties, comparing `Name` and the simple name of `OwnerType`.
  - Tests: `TargetAdapterTests.AttachedPropertiesResolveThroughTheirOwnerType`, `PropertyNamingTests.TheQualifiedSpellingSharesTheSameKey`
- **BRIDGE-TARGETS-006** — When no property matches, `Resolve` shall throw `ArgumentException` naming the target type and the property, from the `Animate`/`Set` call that resolved it.
  - Tests: `TargetAdapterTests.AnUnknownPropertyNameThrows`
- **BRIDGE-TARGETS-007** — The bridge shall key a typed `AvaloniaProperty` as `"{OwnerType.Name}.{Name}"` everywhere it accepts one: both params indexers, `PropertyBuilderExtensions.Property`, the typed `Animatable` extensions and the `PrestoAvalonia` `Get`/`Remove`/`GetAnimations` overloads.
  - Tests: `TargetAdapterTests.TypedAndNamedPropertiesMixInOneInitializer`, `TargetAdapterTests.GetAnimationsFiltersByAvaloniaProperty`
- **BRIDGE-TARGETS-008** — `Canonicalize` shall map every spelling of one registered property (plain, qualified, typed) to that encoded key, so tweens declared with different spellings on one target replace, compose with and remove each other as one property.
  - Tests: `PropertyNamingTests.ATypedPropertyOverridesATweenDeclaredByName`, `PropertyNamingTests.ANamedPropertyOverridesATweenDeclaredByHandle`, `PropertyNamingTests.RemoveByNameCancelsATweenDeclaredByHandle`, `PropertyNamingTests.RemoveByHandleCancelsATweenDeclaredByName`
- **BRIDGE-TARGETS-009** — `Canonicalize` shall return a shorthand name unchanged.
  - Tests: `PropertyNamingTests.TransformShorthandsKeepTheirOwnName`
- **BRIDGE-TARGETS-010** — When `Resolve` is called for any property of a `Visual`, the adapter shall record that visual as the frame source's last animated visual (BRIDGE-INIT-013).
  - Tests: `AvaloniaFrameSourceTests.FramesComeFromTheTopLevelOfAnAnimatedControl`
- **BRIDGE-TARGETS-011** — A collection of controls (a panel's `Children`) shall flatten into one target per control.
  - Tests: `TargetAdapterTests.APanelsChildrenFlattenIntoOneTargetPerControl`
- **BRIDGE-TARGETS-012** — Reading a registered property (`Presto.Get`, a tween's from value) shall return its current effective value, animated value included.
  - Tests: `TargetUtilsTests.GetResolvesATypedProperty`

### Writes and revert

- **BRIDGE-TARGETS-013** — On the first frame for a target, the accessor shall bind the property once at `BindingPriority.Animation` and push every later frame through that binding.
  - Tests: `TargetAdapterTests.AnimatedValuesLandAtAnimationPriorityAndRevertUncoversTheLocalValue`, `AllocationTests.StyledPropertyTweenTicksWithinBudget`
- **BRIDGE-TARGETS-014** — After the animation completes, the value shall stay held at `Animation` priority until it is reverted, so a local, style or binding value written underneath meanwhile does not show.
  - Tests: `TargetAdapterTests.AnimatedValuesLandAtAnimationPriorityAndRevertUncoversTheLocalValue`
- **BRIDGE-TARGETS-015** — When a tween on a styled property is reverted, the accessor shall dispose its binding, uncovering whatever value the property has underneath.
  - Tests: `TargetAdapterTests.AnimatedValuesLandAtAnimationPriorityAndRevertUncoversTheLocalValue`, `TargetUtilsTests.SetWritesAtAnimationPriorityAndRevertUncoversTheLocalValue`
- **BRIDGE-TARGETS-016** (inherited) — When the property is a direct property, which ignores binding priority, the accessor shall capture its value before the first frame and write that value back on revert.
  - Tests: `TargetAdapterTests.RevertRestoresADirectPropertyToTheValueItHadBeforeTheAnimation`
- **BRIDGE-TARGETS-017** (inherited) — A `double` property shall be written unboxed through a typed binding, a `float` property as the frame number cast to `float`, an `int` property as the frame number rounded to an integer, and a `string` property as the tween's composed string.
  - Tests: `TargetAdapterTests.NamedPropertiesAnimateToCompletion`
- **BRIDGE-TARGETS-018** — A property of any other type with a codec in `Presto.Codecs` (looked up by the exact property type when the accessor is created) shall be written through that codec (BRIDGE-CODECS).
  - Tests: `TargetAdapterTests.PropertiesWithACodecAnimateThroughIt`
- **BRIDGE-TARGETS-019** — `Visual.OpacityProperty` shall be clamped to 0–1 on every frame, so an overshooting ease holds at the ends.
  - Tests: `TargetAdapterTests.AnOvershootingEaseNeverWritesOpacityOutsideZeroToOne`
- **BRIDGE-TARGETS-020** — A property type with no codec shall be discrete (CORE-TARGETS `DiscreteCodec`), switching at the midpoint of eased progress.
  - Tests: `DiscretePropertyTests.APropertyWithNoCodecSwitchesHalfway`, `DiscretePropertyTests.SetWritesABoolAndAnEnumAtOnce`, `DiscretePropertyTests.ATimelineSetHidesAtItsPosition`
- **BRIDGE-TARGETS-021** — `Visual.IsVisibleProperty` shall use the `TrueThroughout` rule, staying visible for the whole run of a tween that has one `true` end.
  - Tests: `DiscretePropertyTests.IsVisibleStaysTrueThroughoutAFadeEitherWay`
- **BRIDGE-TARGETS-022** — Reverting a discrete property shall uncover the value underneath, as for any styled property.
  - Tests: `DiscretePropertyTests.RevertRestoresTheLocalValue`
- **BRIDGE-TARGETS-023** — When `Presto.Remove` reaches visual targets, the adapter shall also cancel their compositor animations, all of them or only the track of the named property when it has one (BRIDGE-THREAD).
  - Tests: `CompositorRegistryTests.RemoveCancelsACompositorAnimation`, `CompositorRegistryTests.RemovingOnePropertyLeavesTheOtherRunning`

### Owners and visibility holds

- **BRIDGE-TARGETS-024** — The bridge shall map a target back to the visual it draws on: a visual to itself, and every brush the brush codec creates and every transform group and child the transform composer creates to the visual they were made for.
  - Tests: `TargetOwnerTests.AComposedRenderTransformResolvesToItsVisual`, `TargetOwnerTests.ACodecCreatedBrushResolvesToItsVisual`, `TargetOwnerTests.APlainObjectResolvesToNull`, `WhenDetachedTests.AnAnimationOnABrushTheBridgeMadeFollowsItsVisual`
- **BRIDGE-TARGETS-025** — The owner record shall hold the visual weakly and be keyed weakly by the owned object, so it keeps neither alive.
  - Tests: `LifetimeSpikeTests.RecordingAnOwnerForEveryCreatedBrushCostsOneTableEntryAndOneWeakReference`
- **BRIDGE-TARGETS-026** — While any owner holds a visual's `IsVisible` (Presence, BRIDGE-PRESENCE), the bridge shall keep it at `Animation` priority at the value of the owner that changed it last, and dispose the binding once every owner has released, so a value written underneath meanwhile shows then.
  - Tests: `VisibilitySpikeTests.AValueChangedUnderTheHoldRaisesNothingAndWinsWhenTheHoldIsReleased`

### Params

- **BRIDGE-TARGETS-027** — Constructing an `AvaloniaAnimationParams` or `AvaloniaAnimatableParams` shall call `PrestoAvalonia.EnsureInitialized`.
  - Tests: untested
- **BRIDGE-TARGETS-028** (inherited) — The `AvaloniaProperty` indexer getter shall read the encoded key and throw `KeyNotFoundException` when it is absent.
  - Tests: untested
- **BRIDGE-TARGETS-029** — `AvaloniaAnimationParams.WhenReduced` shall be the core `WhenReduced` typed for the indexer, and `WhenDetached` the core host-lifetime slot (BRIDGE-HELPERS).
  - Tests: `AnimationBuilderBridgeTests.ATypedWhenReducedReplacesTheProperties`, `WhenDetachedTests.TheParamsOverrideWinsOverTheDefault`
- **BRIDGE-TARGETS-030** — A property declared through the `AvaloniaAnimatableParams` indexer or by name shall be read and set by either spelling.
  - Tests: `PropertyNamingTests.AnAnimatableDeclaredByNameIsReadAndSetByHandle`, `AnimatableBridgeTests.AnAnimatableDrivesTransformShorthandsAndABrush`

### Defaults

- **BRIDGE-TARGETS-031** — When `AvaloniaAnimationParams.WhenDetached` is not set, the animation shall take `PrestoAvalonia.WhenDetached`.
  - Tests: `WhenDetachedTests.ADetachedLoopPausesAndTheEngineIdles`

## Invariants

- One accessor per `AvaloniaProperty` and one per shorthand, shared by every target; per-target state (binding, captured direct value) lives in weak tables keyed by the target.
- At most one `Animation`-priority binding per (property, target) exists at a time.
- A successful resolution for a (type, name) pair is cached; a failed one is not.

## Edge cases

- A qualified name must use the `OwnerType` of the registered instance, which for a property added with `AddOwner` is the original owner.
- The codec is chosen when the accessor is created, the first time any target resolves that property; a codec registered later is not seen. A `double?` or other nullable property has no codec and is discrete.
- Animating a control through plain `AnimationParams` before anything has initialized the bridge resolves it with the core's reflection adapter, which knows no shorthand.
- Reverting a direct property writes the captured value back with `SetValue`, overwriting a value set by hand during the animation.

## Threading and lifetime

- UI thread only. The per-target tables are `ConditionalWeakTable`s and the owner map holds weak references, so nothing here keeps a target alive.
- A held binding outlives a completed animation until `Revert` (or `WhenDetached.Revert`, BRIDGE-HELPERS).

## Reduced motion

- Not affected here; reduced forms are chosen by the core before resolution (CORE-REDUCED).

## Allocation

- A frame of a `double` property costs only the property-changed args Avalonia raises. Other value types box their value per frame; a discrete property pushes its cached object and costs nothing between flips. Resolution and binding allocate once per (property, target).

## Deviations from anime.js

- Upstream writes inline styles and attributes; here every write is an `Animation`-priority binding, so revert uncovers the underlying value instead of restoring a captured one, except for direct properties. Reason: a local, style or binding value written underneath during the animation then shows once reverted.
- Discrete fallback for types without a codec, where upstream writes non-numeric values as strings: `bool` and enum properties such as `IsVisible` have no string form to write, and timelines, keyframes and `Presto.Set` must still be able to set them.

## Decisions

- **Animated values are written at `Animation` priority.** Reason: animated values sit above local values, and `Revert()` restores the user's values precisely by clearing that priority.
- **One binding per animated property, with residual allocations accepted.** Each (property, target) is animated through one `Animation`-priority binding that every frame pushes into, typed for `double` and untyped otherwise; Avalonia's property-changed args per write and boxing for non-`double` types are accepted. Reason: a `SetValue` per frame at that priority creates a value entry each time (`Opacity`: 288 → 104 → 80 bytes per frame), and the remainder is Avalonia's own structure. Rejected: `SetValue` per frame.
- **Codec-less properties are discrete; there is no `AutoAlpha`.** Any property type without a codec gets a `DiscreteCodec`, `TrueThroughout` for `IsVisible` and `Midpoint` otherwise; compositor animations on one throw. Reason: `IsVisible = false` removes an element from layout (unlike CSS `visibility: hidden`), so a fade ending hidden would jump, and `Presence`'s `Hold` mode already covers fading before a hide. Rejected: a GSAP-style `AutoAlpha` shorthand.
- **Property keys are canonicalized, not keyed on accessors.** See CORE-TARGETS.
- **Types without a codec animate as discrete values.** See CORE-TARGETS.
- **Params are init-only records with a string indexer.** See CORE-ANIMATION.
- **Dictionary params take `WhenReduced`; timers and timelines do not.** See CORE-ANIMATION.
- **Targets map back to visuals through `TargetOwner`.** See DEVTOOLS-OVERLAY.
- **Animations pause when their controls leave the tree.** See BRIDGE-HELPERS.

## Non-goals

- No conversion between property types, and no CSS-style unit conversion for registered properties.
- No typed key constants for shorthands; the builder methods are the typed form (BRIDGE-HELPERS, CORE-BUILDER).

## Open items

- BRIDGE-TARGETS-001, -027 and -028 are untested; `int`, `float` and `string` writes (BRIDGE-TARGETS-017) have no dedicated bridge test.
- BRIDGE-TARGETS-014: nothing pins that a value written underneath a completed, unreverted animation stays masked.
