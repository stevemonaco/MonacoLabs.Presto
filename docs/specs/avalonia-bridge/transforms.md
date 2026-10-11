---
id: BRIDGE-TRANSFORMS
title: Transform shorthands
package: MonacoLabs.Presto.Avalonia
namespaces:
  - MonacoLabs.Presto
sources:
  - MonacoLabs.Presto.Avalonia/Transforms/TransformShorthands.cs
  - MonacoLabs.Presto.Avalonia/Transforms/RenderTransformComposer.cs
  - MonacoLabs.Presto.Avalonia/Targets/AvaloniaObjectTargetAdapter.cs
types: []
tests:
  - TransformsTests
  - TargetUtilsTests
  - TargetOwnerTests
  - FollowTests
  - CompositorRegistryTests
  - BridgeAllocationTests
upstream:
  - src/core/consts.js
  - src/core/transforms.js
  - tests/suites/transforms.test.js
depends:
  - CORE-VALUES
  - CORE-TARGETS
  - BRIDGE-TARGETS
---

# Transform shorthands

## Purpose

The transform shorthands `X`, `Y`, `Rotate`, `Scale`, `ScaleX`, `ScaleY`, `SkewX`, `SkewY`, `RotateX`, `RotateY` and `Perspective` animate a `Visual` through one bridge-owned `TransformGroup` on `Visual.RenderTransform`, the counterpart of the CSS transform string upstream rebuilds in `transforms.js`. Separate animations on one visual aggregate into that group instead of replacing each other. Resolution is BRIDGE-TARGETS; the 3D shorthands are introduced with the effects (BRIDGE-EFFECTS) but composed here.

## Requirements

### Composition

- **BRIDGE-TRANSFORMS-001** — When a shorthand of a visual is first written, the bridge shall set `Visual.RenderTransform` at `BindingPriority.Animation` to a `TransformGroup` it owns for that visual.
  - Tests: `TransformsTests.XAndYComposeIntoASingleTranslateTransform`
- **BRIDGE-TRANSFORMS-002** — The group shall contain only the transforms of the shorthands currently animated or followed, so `X` and `Y` share one `TranslateTransform`, `Scale`/`ScaleX`/`ScaleY` one `ScaleTransform`, `SkewX`/`SkewY` one `SkewTransform` and `RotateX`/`RotateY`/`Perspective` one `Rotate3DTransform`.
  - Tests: `TransformsTests.XAndYComposeIntoASingleTranslateTransform`, `TransformsTests.SkewXAndSkewYComposeIntoOneSkewTransformAheadOfTheTranslation`
- **BRIDGE-TRANSFORMS-003** — The group's children shall be ordered: the visual's previous render transform, skew, scale, 3D rotation, 2D rotation, translation, so translation applies last and the others act on the visual's own box.
  - Tests: `TransformsTests.SeparateAnimationsOnTheSameVisualAggregate`, `TransformsTests.RotateXRotateYAndPerspectiveComposeIntoOneRotate3DTransformBetweenScaleAndRotate`, `TransformsTests.SkewXAndSkewYComposeIntoOneSkewTransformAheadOfTheTranslation`
- **BRIDGE-TRANSFORMS-004** — The `Rotate3DTransform` shall be added only once `RotateX`, `RotateY` or `Perspective` is used, so 2D-only visuals never carry one.
  - Tests: `TransformsTests.RotateXRotateYAndPerspectiveComposeIntoOneRotate3DTransformBetweenScaleAndRotate`
- **BRIDGE-TRANSFORMS-005** — Separate animations of different shorthands on one visual shall write into the same group, each keeping its own transform.
  - Tests: `TransformsTests.SeparateAnimationsOnTheSameVisualAggregate`
- **BRIDGE-TRANSFORMS-006** — The written scale on each axis shall be `Scale` multiplied by `ScaleX` or `ScaleY`.
  - Tests: `TransformsTests.AUniformScaleMultipliesThePerAxisScales`
- **BRIDGE-TRANSFORMS-007** — When the visual already has a render transform, the bridge shall keep it as the group's first child (an immutable `ITransform` wrapped in a `MatrixTransform`) rather than replace it.
  - Tests: `TransformsTests.RevertRestoresTheRenderTransformTheVisualAlreadyHad`
- **BRIDGE-TRANSFORMS-008** (inherited) — When something else displaces the group on `RenderTransform` (a style transition such as Fluent's pressed scale), the bridge shall re-assert the group at `Animation` priority and, in a `Send`-priority post, make the property's new base value the group's first child.
  - Tests: `TransformsTests.PressingAnAnimatedButtonKeepsItDrawnAtItsAnimatedPositionAndComposesThePressedScale`
- **BRIDGE-TRANSFORMS-009** — The group and every transform in it shall map back to their visual for detach tracking (BRIDGE-TARGETS-024).
  - Tests: `TargetOwnerTests.AComposedRenderTransformResolvesToItsVisual`

### Values and units

- **BRIDGE-TRANSFORMS-010** — `X`, `Y` and `Perspective` shall be written as DIPs and the scales as factors, with no unit conversion.
  - Tests: `TransformsTests.XAndYComposeIntoASingleTranslateTransform`
- **BRIDGE-TRANSFORMS-011** (inherited) — `Rotate`, `SkewX`, `SkewY`, `RotateX` and `RotateY` shall take degrees when a value has no unit or `deg`, convert `turn` and `rad` to degrees before writing, and take the number of any other unit as degrees unchanged.
  - Tests: `TransformsTests.AngleUnitsEndInDegrees`, `TransformsTests.RotateYAcceptsAngleUnits`
- **BRIDGE-TRANSFORMS-012** — Reading an angle shorthand shall return a string in degrees (`"45deg"`), so a unit end value converts from it instead of being taken in the end value's unit; the other shorthands read as `double`.
  - Tests: `TransformsTests.ShorthandsReadTheirIdentityValueBeforeBeingAnimated`, `TargetUtilsTests.GetReadsBackAComposedTransformShorthand`
- **BRIDGE-TRANSFORMS-013** — Reading a shorthand that nothing has written shall return its identity (0, or 1 for the scales) without installing a group.
  - Tests: `TransformsTests.ShorthandsReadTheirIdentityValueBeforeBeingAnimated`
- **BRIDGE-TRANSFORMS-014** — Relative values (`"+=…"`, `"*=…"`) shall apply to the shorthand's current value, starting from the identity.
  - Tests: `TransformsTests.ShorthandsReadTheirIdentityValueBeforeBeingAnimated`
- **BRIDGE-TRANSFORMS-015** — The bridge shall not set `RenderTransformOrigin`: rotation, scale and skew pivot about the visual's own origin, which is its center unless the app sets another.
  - Tests: untested

### Revert

- **BRIDGE-TRANSFORMS-016** — When one shorthand's tween is reverted, the bridge shall return it to its followed base (BRIDGE-FOLLOW) or else its identity, and drop its transform from the group unless another shorthand sharing that transform is still held.
  - Tests: `TransformsTests.RevertKeepsTheShorthandsAnotherAnimationStillAnimates`, `FollowTests.AnAnimationOfTheSameShorthandLayersOverTheFollowAndRevertsToIt`
- **BRIDGE-TRANSFORMS-017** — When the last animated or followed shorthand of a visual is released, the bridge shall dispose its `Animation`-priority value, uncovering the visual's own render transform (or none).
  - Tests: `TransformsTests.RevertRestoresTheRenderTransformTheVisualAlreadyHad`, `TransformsTests.RevertKeepsTheShorthandsAnotherAnimationStillAnimates`, `FollowTests.ClearingRestoresTheIdentityAndTheVisualsOwnRenderTransform`
- **BRIDGE-TRANSFORMS-018** — A completed, unreverted animation shall keep its shorthand in the group at its end value.
  - Tests: `TransformsTests.SeparateAnimationsOnTheSameVisualAggregate`

### Interplay with the compositor

- **BRIDGE-TRANSFORMS-019** — When a shorthand first joins the group of a visual whose transform a compositor animation drives, the bridge shall log one Avalonia `Animations` warning that the two transforms multiply.
  - Tests: `CompositorRegistryTests.MixingAShorthandAndACompositorAnimationWarnsOnceEitherWayRound`, `CompositorRegistryTests.AShorthandThatHasEndedDoesNotWarnWhenALayoutTransitionMoves`

## Invariants

- Each visual has at most one composer and one group; the group's children are always in the BRIDGE-TRANSFORMS-003 order.
- While a composer exists, `RenderTransform`'s effective value is its group.
- The value each shorthand reads back equals the value last written to its transform (for `Scale`, the uniform factor before the per-axis multiply).

## Edge cases

- `Scale` and `ScaleX` held together write their product; reverting `Scale` alone leaves `ScaleX` at its own value.
- A local `RenderTransform` set while the group is held is masked and raises no change, so it is not folded into the group; it shows once the group is released.
- A shorthand name on a non-`Visual` `AvaloniaObject` is looked up as a registered property (BRIDGE-TARGETS-003).
- The composer exposes the visual untransformed for snapshots (Presence's ghosts, BRIDGE-PRESENCE) by emptying the group's children for the call.

## Threading and lifetime

- UI thread only. The composer lives in a `ConditionalWeakTable` keyed by the visual and is removed when the last shorthand is released, so it never keeps the visual alive.

## Reduced motion

- Not affected: the shorthands write whatever the (possibly reduced) tween computes.

## Allocation

- A frame writes a reused transform's property, costing only Avalonia's property-changed args (BRIDGE-TARGETS). Allocation happens when a shorthand joins or leaves the group.
  - Pinned by `BridgeAllocationTests.TransformShorthandTweenTicksWithinBudget`.

## Provenance

- `translateZ`, `rotateZ`, `scaleZ`, `skew` and `matrix` have no shorthand; `x`/`y` are `X`/`Y` and `rotate` is `Rotate`.
- Upstream emits `perspective` first in the string; here the depth rides in the `Rotate3DTransform` at the 3D rotation's slot, because one transform carries both angles and the depth.
- Upstream rebuilds a string per frame; here transform objects are mutated in place.

## Decisions

- **Transform shorthands own a `TransformGroup`.** They write a bridge-owned group to `RenderTransform`, and a pre-existing render transform is wrapped as its first child, not replaced. Reason: it mirrors upstream's `transforms.js` managing the CSS transform string, and it preserves the user's own transform.
- **3D rotation joins the transform group on first use.** See BRIDGE-EFFECTS.
- **Shorthands are followed through attached properties, as a base layer.** See BRIDGE-FOLLOW.
- **One binding per animated property, with residual allocations accepted.** See BRIDGE-TARGETS.
- **No `Matrix` codec until a property needs one.** See BRIDGE-CODECS.

## Non-goals

- No `Matrix` codec and no decomposition of a user's transform into shorthands: a pre-existing transform is composed, never read back as `X`/`Rotate`.
- No transform origin shorthand.

## Open items

- BRIDGE-TRANSFORMS-015 (origin) is untested.
- A local `RenderTransform` set while the group is held is not picked up as the first child (edge case above); untested and possibly surprising.
