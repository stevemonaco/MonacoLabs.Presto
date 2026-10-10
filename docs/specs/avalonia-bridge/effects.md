---
id: BRIDGE-EFFECTS
title: Effect, reveal and 3D shorthands, and Tilt
package: MonacoLabs.Presto.Avalonia
namespaces:
  - MonacoLabs.Presto
sources:
  - MonacoLabs.Presto.Avalonia/Effects
  - MonacoLabs.Presto.Avalonia/Tilt.cs
  - MonacoLabs.Presto.Avalonia/Transforms/RenderTransformComposer.cs
types:
  - Tilt
tests:
  - EffectTests
  - RevealTests
  - RevealRenderTests
  - TiltTests
  - TransformsTests
  - FollowTests
  - AnimationBuilderBridgeTests
  - BridgeAllocationTests
upstream: []
depends:
  - CORE-ANIMATABLE
  - CORE-REDUCED
  - BRIDGE-TARGETS
  - BRIDGE-TRANSFORMS
  - BRIDGE-CODECS
---

# Effect, reveal and 3D shorthands, and Tilt

## Purpose

Shorthands a `Visual` resolves (BRIDGE-TARGETS-002) that animate a bridge-owned object on one of its properties, as the transform shorthands own `RenderTransform`: `Blur` and `Shadow*` on `Visual.Effect`, `ClipInset` and `ClipCircle` on `Visual.Clip`, and `MaskSweep` on `Visual.OpacityMask`. The 3D shorthands `RotateX`, `RotateY` and `Perspective` join the transform group (BRIDGE-TRANSFORMS). `Tilt` is an attached property that turns an element towards the pointer through those 3D shorthands. The warp shorthands of a split `TextBlock` (`Warp`, `WarpPhase`, `WarpOrigin`, `WarpRadius`) follow the same scalar-plus-companions pattern and are specified in BRIDGE-WARP.

## Requirements

### Effect shorthands

- **BRIDGE-EFFECTS-001** — When a `Blur` frame is written, the bridge shall set `Visual.Effect` at `Animation` priority to a `BlurEffect` it owns for that visual and set its `Radius`, mutating the same instance on later frames.
  - Tests: `EffectTests.BlurAnimatesTheRadius`
- **BRIDGE-EFFECTS-002** — The `Shadow*` shorthands of one visual shall all drive one bridge-owned `DropShadowEffect`.
  - Tests: `EffectTests.TheShadowShorthandsShareOneDropShadowEffect`
- **BRIDGE-EFFECTS-003** — When an effect kind starts, its values shall start from the user's own effect of that kind (the property's value below `Animation` priority), or else from the identities, so animating `ShadowBlur` alone shows a black, opaque, unoffset shadow.
  - Tests: `EffectTests.BlurStartsFromTheUsersBlurRadius`
- **BRIDGE-EFFECTS-004** (inherited) — `ShadowColor` shall take a `Color` or a color string (BRIDGE-CODECS), and `ShadowOpacity` shall be clamped to 0–1.
  - Tests: `EffectTests.ShadowColorTakesAColor`, `EffectTests.ShadowOpacityIsClamped`
- **BRIDGE-EFFECTS-005** — While a blur frame's radius is 0 or less, the bridge shall remove its effect from `Visual.Effect`, putting it back on the next frame above 0.
  - Tests: `EffectTests.BlurBackToZeroRestoresTheUsersEffectOrNone`, `EffectTests.AHoverBlurReturnsToRestAndRemovesTheEffect`, `EffectTests.ABlurRendersAndBackAtZeroMatchesTheOriginal`
- **BRIDGE-EFFECTS-006** — When a `ShadowOpacity` tween ends at 0 or less, the bridge shall remove the shadow effect.
  - Tests: `EffectTests.AShadowFadedOutIsRemoved`
- **BRIDGE-EFFECTS-007** — When an `Animate` or `Set` call would animate a blur and a shadow shorthand on one visual at once (in one animation, or while the other kind has a live animation on it), the call shall throw `InvalidOperationException` naming both shorthands.
  - Tests: `EffectTests.BlurAndShadowInOneAnimationThrow`, `EffectTests.AShadowWhileABlurRunsThrows`
- **BRIDGE-EFFECTS-008** — A pending claim from a resolution no frame rendered (a `Presto.Get`) shall lapse at the end of the current dispatcher job.
  - Tests: untested
- **BRIDGE-EFFECTS-009** — When the other kind starts after the first has no live animation, the bridge shall switch `Visual.Effect` to the new kind, starting it afresh.
  - Tests: `EffectTests.BlurThenShadowSwitchesKind`
- **BRIDGE-EFFECTS-010** — When one effect shorthand is reverted while others of its kind still animate, its channel shall return to the user's value; when the last is reverted, the bridge shall dispose its `Animation`-priority value, uncovering the user's own effect, or a followed blur (BRIDGE-FOLLOW) if there is one.
  - Tests: `EffectTests.RevertRestoresTheUsersOwnEffect`, `FollowTests.AnAnimatedBlurLayersOverTheFollowedBlurAndRevertsToIt`
- **BRIDGE-EFFECTS-011** — Reading an effect shorthand shall return the bridge effect's value while its kind is animated, a followed blur's radius, or else the user's effect value or the identity.
  - Tests: `EffectTests.BlurStartsFromTheUsersBlurRadius`

### Clip reveals

- **BRIDGE-EFFECTS-012** — `ClipInset` shall drive a bridge-owned `RectangleGeometry` on `Visual.Clip` at `Animation` priority, inset from the visual's bounds by left, top, right and bottom.
  - Tests: `RevealTests.ADipInsetAnimatesTheRectangle`
- **BRIDGE-EFFECTS-013** — `ClipInset` values shall be a `Thickness`, a number (DIPs on every side) or a string of 1, 2 or 4 values in `Thickness` order, each in DIPs (bare or `px`) or `%` of the visual's width or height; each side is written in the end value's unit without converting the start.
  - Tests: `RevealTests.ADipInsetAnimatesTheRectangle`, `RevealTests.APercentInsetFollowsAResize`
- **BRIDGE-EFFECTS-014** — While a clip is installed, the bridge shall re-place its geometry whenever the visual's `Bounds` change, and invalidate the visual's rendering on every frame.
  - Tests: `RevealTests.APercentInsetFollowsAResize`, `RevealRenderTests.EachFrameOfARevealReachesTheScreen`
- **BRIDGE-EFFECTS-015** — `ClipCircle` shall drive a bridge-owned `EllipseGeometry` circle centered at `ClipCircleCenter` (resolved against the bounds, default the center) with radius `max(0, ClipCircle) ×` the distance from the center to the farthest corner.
  - Tests: `RevealTests.TheCircleCenterMovesTheEllipse`
- **BRIDGE-EFFECTS-016** — When a reveal's tween ends fully visible (every inset 0, or a radius of 1 or more), the bridge shall remove its clip.
  - Tests: `RevealTests.AnInsetBackToZeroEndsWithNoClip`, `RevealTests.ACircleRevealEndsWithNoClip`
- **BRIDGE-EFFECTS-017** — When an `Animate` or `Set` call would animate `ClipInset` and `ClipCircle` or `ClipCircleCenter` on one visual at once, the call shall throw `InvalidOperationException`; one after the other switches the kind.
  - Tests: `RevealTests.AnInsetAndACircleTogetherThrow`
- **BRIDGE-EFFECTS-018** — Clip shorthands shall start from their identities, not from the user's own clip, and reverting the last one shall dispose the bridge clip, uncovering the user's `Clip`.
  - Tests: `RevealTests.RevertRestoresTheUsersClip`

### Mask sweep

- **BRIDGE-EFFECTS-019** — `MaskSweep` shall drive a bridge-owned `LinearGradientBrush` on `Visual.OpacityMask` at `Animation` priority, opaque behind an edge and transparent ahead of it, the edge's center at `progress × (1 + feather) − feather / 2` along the `MaskSweepAngle` direction.
  - Tests: `RevealTests.TheSweepEdgeSitsAtProgressAlongTheAngle`
- **BRIDGE-EFFECTS-020** — `MaskSweepFeather` shall set the edge's width (no less than 0.001), and `MaskSweepAngle` shall take degrees, converting `turn` and `rad`.
  - Tests: `RevealTests.TheFeatherWidensTheEdgeAndTheAngleTakesTurns`
- **BRIDGE-EFFECTS-021** — When a `MaskSweep` tween ends at 1 or more, the bridge shall remove its mask.
  - Tests: `RevealTests.AMaskSweepEndsWithNoMask`
- **BRIDGE-EFFECTS-022** — Each mask shorthand shall animate, revert to its identity and read its identity when not animated on its own; reverting the last shall uncover the user's `OpacityMask`.
  - Tests: `RevealTests.RevertRestoresTheUsersMask`
- **BRIDGE-EFFECTS-023** — The mask shorthands shall never conflict with each other or with the effect or clip shorthands.
  - Tests: untested

### 3D shorthands

- **BRIDGE-EFFECTS-024** — `RotateX`, `RotateY` and `Perspective` shall drive one `Rotate3DTransform` in the visual's transform group (BRIDGE-TRANSFORMS-004), angles in degrees with units converted and `Perspective` as its `Depth` (0 = none).
  - Tests: `TransformsTests.RotateXRotateYAndPerspectiveComposeIntoOneRotate3DTransformBetweenScaleAndRotate`, `TransformsTests.RotateYAcceptsAngleUnits`

### Compositor and builder

- **BRIDGE-EFFECTS-025** — An animation on the compositor naming any of these shorthands shall throw `NotSupportedException` (BRIDGE-THREAD).
  - Tests: `RevealTests.TheCompositorRejectsTheShorthandsOnlyTheBridgeRenders`
- **BRIDGE-EFFECTS-026** — Every effect and reveal shorthand shall have typed builder methods writing the same key and value as the indexer, including `Shadow(x, y, blur, color)` setting the four shadow shorthands.
  - Tests: `AnimationBuilderBridgeTests.EffectAndRevealMethodsWriteTheIndexerKeysAndValues`, `AnimationBuilderBridgeTests.EffectAndRevealOverloadsWriteTheSameKey`, `AnimationBuilderBridgeTests.TypedEffectValuesAreWrappedAsTheIndexerWouldWrapThem`

### Tilt

- **BRIDGE-EFFECTS-027** — While `Tilt.Enabled` is true and the control is in the visual tree, the bridge shall keep an `Animatable` over the control's `RotateX` and `RotateY` (springs with bounce 0.25 over 500 ms, preserving velocity) and set its `Perspective` to 800.
  - Tests: `TiltTests.PointerMovesTurnTheElementTowardsThePointerWithinTheStrength`
- **BRIDGE-EFFECTS-028** — When the pointer moves over the control, `RotateY` shall go to `clamp((x − centerX) / (width / 2), −1, 1) × Strength` and `RotateX` to the negated equivalent for y, both measured in the parent against the control's untransformed layout slot.
  - Tests: `TiltTests.PointerMovesTurnTheElementTowardsThePointerWithinTheStrength`
- **BRIDGE-EFFECTS-029** — When the pointer leaves, both angles shall spring back to 0.
  - Tests: `TiltTests.LeavingSpringsBothAxesBackToZero`
- **BRIDGE-EFFECTS-030** — When the control detaches or `Enabled` turns false, the bridge shall revert the `Animatable`, restoring the control's own render transform; reattaching while enabled creates a new one.
  - Tests: `TiltTests.DetachingOrDisablingRevertsToTheElementsOwnRenderTransform`
- **BRIDGE-EFFECTS-031** — While `PrestoDefaults.ReducedMotion` is set on `Engine.Current`, pointer moves shall be ignored, so nothing tilts.
  - Tests: `TiltTests.ReducedMotionLeavesTheElementFlat`
- **BRIDGE-EFFECTS-032** (inherited) — In design mode, setting `Tilt.Enabled` shall do nothing.
  - Tests: untested

### Defaults

- **BRIDGE-EFFECTS-033** — When the visual has no effect of its kind, `Blur` and `ShadowBlur` shall start from and read as 0.
  - Tests: `EffectTests.BlurAnimatesTheRadius`
- **BRIDGE-EFFECTS-034** — When `ClipInset` is not animated, it shall start from and read as `"0"` (no inset).
  - Tests: `RevealTests.ADipInsetAnimatesTheRectangle`
- **BRIDGE-EFFECTS-035** — When `ClipCircle` is not animated, it shall start from and read as 1 (the farthest corner).
  - Tests: untested
- **BRIDGE-EFFECTS-036** — When `MaskSweep` is not animated, it shall start from and read as 1 (fully shown).
  - Tests: untested
- **BRIDGE-EFFECTS-037** — When `MaskSweepAngle` is not set, the sweep shall run left to right (0 degrees).
  - Tests: untested
- **BRIDGE-EFFECTS-038** — When `MaskSweepFeather` is not set, the edge shall be 0.2 of the sweep wide.
  - Tests: `RevealTests.TheSweepEdgeSitsAtProgressAlongTheAngle`
- **BRIDGE-EFFECTS-039** (inherited) — When `Tilt.Enabled` is not set, the control shall not tilt.
  - Tests: untested
- **BRIDGE-EFFECTS-040** (inherited) — When `Tilt.Strength` is not set, the control shall tilt at most 10 degrees on each axis.
  - Tests: `TiltTests.PointerMovesTurnTheElementTowardsThePointerWithinTheStrength`

## Invariants

- A visual has at most one effect composer, one clip composer and one mask composer, each installed at `Animation` priority only while it has something to show.
- At most one effect kind (blur or shadow) and one clip kind (inset or circle) is animated on a visual at any moment.

## Edge cases

- A blur removed at radius 0 mid-tween comes back on the next frame above 0; a shadow is removed only when its opacity tween has ended at 0.
- Animating only `MaskSweepAngle` or `MaskSweepFeather` installs a mask at progress 1 that is never removed until reverted.
- `ClipCircleCenter` alone never marks the reveal done, so its circle at the default radius 1 stays installed until reverted.
- Changing `Tilt.Strength` while hovering takes effect on the next pointer move; turning reduced motion on while tilted leaves the tilt until the pointer leaves.
- Tilt sets `Perspective` on attach, so an enabled, attached control carries a transform group even before the pointer enters.

## Threading and lifetime

- UI thread only. Composers live in `ConditionalWeakTable`s keyed by the visual and are removed when their last shorthand is released.
- A `Tilt` tracker hangs off the control in a private attached value; its `Animatable` lives only while the control is attached.

## Reduced motion

- The shorthands themselves are not affected. `Tilt` ignores pointer moves under reduced motion (BRIDGE-EFFECTS-031).

## Allocation

- A frame mutates the bridge-owned effect, geometry or brush in place, costing Avalonia's property-changed args, a residual allocation accepted as for every animated property (BRIDGE-TARGETS). Pinned by `BridgeAllocationTests.EffectShorthandTweenTicksWithinBudget`, `BridgeAllocationTests.MaskSweepTicksWithinBudget` and `BridgeAllocationTests.AnIdleTiltAllocatesNothingPerFrame`.

## Deviations from anime.js

- No upstream counterpart: CSS filters, `clip-path` and masks are strings upstream animates like any other; these shorthands are bridge-defined.

## Decisions

- **Effects are bridge-owned composers.** Per visual, one composer owns `Visual.Effect`, one `Visual.Clip` and one `Visual.OpacityMask`, as `RenderTransformComposer` owns `RenderTransform`: each writes one bridge-owned mutable object at `Animation` priority and mutates it per frame, `Revert` uncovers the user's own value, and a user effect of the same kind supplies the starting values. Reason: no per-frame allocation beyond Avalonia's property-changed args (the `BrushCodec` reuse pattern), and reverting restores what the user set.
- **A visual holds one effect and one clip; animating two kinds at once throws.** Blur and shadow shorthands together, or `ClipInset` and `ClipCircle` together, throw `InvalidOperationException` naming both; one after the other switches the kind; `MaskSweep` has one kind and cannot conflict. Reason: Avalonia has no effect group and a visual holds one clip; for a `Border`, `BoxShadow` is cheaper and not exclusive. Rejected: a documented "last kind wins".
- **Conflicts are caught when the accessor resolves, with same-job claims.** The check runs as the adapter resolves the shorthand, so `Animate`/`Set` throws, and a kind conflicts with the other kind's running animations or with a claim made earlier in the same dispatcher job. Reason: it catches a blur and a shadow in one animation before either has rendered, and a claim that lapses with the job means a `Presto.Get` does not hold one.
- **An effect at rest is removed.** A blur at radius 0 on any frame, a shadow whose opacity tween ends at 0, and a reveal at full visibility revert instead of leaving a zero effect, clip or mask installed. Reason: any effect costs an offscreen pass.
- **A shadow's identity is black at opacity 1.** Without a user `DropShadowEffect`, a shadow starts black, opaque, with blur and offset 0. Reason: animating `ShadowBlur` alone then shows a shadow.
- **3D rotation joins the transform group on first use.** `RotateX`, `RotateY` and `Perspective` (its `Depth`, identity 0) drive one `Rotate3DTransform` inserted between the scale and the 2D rotate only when first animated; the compositor has no counterpart, so they throw there. Reason: 2D-only visuals keep their group and allocations, and the slot is upstream's `validTransforms` order reversed, with perspective at the rotation's slot because one transform carries both angles and the depth.
- **Reveals are shorthands with companion shorthands.** `ClipInset` and `ClipCircle` drive a bridge-owned geometry on `Clip` and `MaskSweep` an `OpacityMask`; their extra parameters (`ClipCircleCenter`, default the center; `MaskSweepAngle`; `MaskSweepFeather`, default 0.2) are shorthands of their own, each with builder methods, an identity and a revert. A reveal starts from its identity, never the user's `Clip` or `OpacityMask`, and each `ClipInset` side takes its end value's unit without converting the start. Reason: one scalar cannot carry a reveal's extra parameters.
- **`MaskSweep` owns a two-stop brush.** One `LinearGradientBrush` with fixed opaque and transparent stops has its start and end points moved each frame; `Shimmer` is a `MaskSweep` loop for the same reason. Reason: Skia clamps stop offsets to 0–1, and the soft edge has to run past both ends of the visual. Rejected: animating stops through the gradient codec.
- **`Tilt` is an attached property over real 3D rotation.** It springs `RotateX`/`RotateY` (bounce 0.25, 500 ms, velocity preserved) at `Perspective` 800 through an `Animatable`, on the UI thread only, ignoring pointer moves under reduced motion. Reason: tilt needs the pointer position, so it cannot be a builder recipe. Rejected: a compositor skew-and-scale approximation.
- **Shorthands are followed through attached properties, as a base layer.** See BRIDGE-FOLLOW.

## Non-goals

- No effect group: a visual cannot carry a blur and a shadow at once; put the blur on an inner element, or use `BoxShadow` on a `Border` (BRIDGE-CODECS).
- No shader or pixel transitions: they need a child composition visual and a shader path.
- No compositor counterpart for any of these shorthands.

## Open items

- BRIDGE-EFFECTS-008, -023 and -032 are untested.
- A mask or circle reveal driven only by its companion shorthand never removes itself at rest (edge cases above), against the decision that an effect at rest is removed.
