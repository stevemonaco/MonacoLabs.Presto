---
id: BRIDGE-DISTORT
title: Raster distortion
package: Monaco.Presto.AvaloniaBridge
namespaces:
  - Monaco.Presto.AvaloniaBridge.Controls
sources:
  - Monaco.Presto.AvaloniaBridge/Controls/Distort.cs
  - Monaco.Presto.AvaloniaBridge/Controls/DistortDrawOperation.cs
types:
  - Distort
tests:
  - DistortTests
upstream: []
depends:
  - BRIDGE-IMAGE
  - CORE-REDUCED
---

# Raster distortion

## Purpose

`Distort` is a `Decorator` that draws its child through three raster effects built from Skia's image filters, as BRIDGE-IMAGE's effects are: a noise `Turbulence` displacement that `TurbulencePhase` makes flow, a `Goo` that fuses nearby shapes like metaballs, and a `Glitch` of split color channels and shifted bands. It works on any content but is aimed at display text. Each effect is a plain styled property, so every Presto animation path drives it.

## Requirements

### Drawing

- **BRIDGE-DISTORT-001** — While every effect is at identity, the control shall draw its child live, holding no snapshot and pushing no layer.
  - Tests: `DistortTests.AtIdentityTheChildDrawsLiveWithNoSnapshot`
- **BRIDGE-DISTORT-002** (inherited) — The effects shall be at identity when `Turbulence` is 0, `Goo` is at most 0 and `Glitch` is 0.
  - Tests: `DistortTests.AtIdentityTheChildDrawsLiveWithNoSnapshot`, `DistortTests.AnAnimatedEffectDrawsBetweenFramesAndRevertsToTheLocalValue`
- **BRIDGE-DISTORT-003** — When an effect leaves identity, the control shall snapshot its child at the render scaling and draw the snapshot where the child is, through the effects, instead of the child.
  - Tests: `DistortTests.EachEffectDrawsASnapshotThatChangesPixelsInsteadOfTheChild`, `DistortTests.TheSnapshotIsTakenAtTheRenderScaling`
- **BRIDGE-DISTORT-004** — While the snapshot is drawn, the child shall keep its layout and its own property values.
  - Tests: untested
- **BRIDGE-DISTORT-005** — The effects shall draw within the control's bounds, so its `Padding` is the room they have around the child.
  - Tests: untested
- **BRIDGE-DISTORT-006** — When the child or any of its visual descendants redraws while an effect is active, including through a brush or transform changed in place, the control shall take a new snapshot within a frame, and it shall not take one while nothing in the child redraws.
  - Tests: `DistortTests.AChildThatRedrawsWhileActiveIsSnapshottedAgain`
- **BRIDGE-DISTORT-007** — When every effect returns to identity, the control shall dispose the snapshot and draw the child live again.
  - Tests: `DistortTests.AnAnimatedEffectDrawsBetweenFramesAndRevertsToTheLocalValue`
- **BRIDGE-DISTORT-008** — When the control detaches from the visual tree, it shall dispose the snapshot.
  - Tests: `DistortTests.DetachingDropsTheSnapshot`
- **BRIDGE-DISTORT-009** (inherited) — When `Child` changes, the control shall dispose the snapshot and draw the previous child live again.
  - Tests: untested
- **BRIDGE-DISTORT-010** (inherited) — When the drawing context offers no Skia API lease, the control shall draw the snapshot unfiltered and not throw.
  - Tests: untested

### Effects

- **BRIDGE-DISTORT-011** — The effects shall apply in the order Turbulence → Goo → Glitch, each stage skipped at its identity value.
  - Tests: untested
- **BRIDGE-DISTORT-012** — `Turbulence` shall displace pixels by up to `Turbulence` DIPs through fractal noise of feature size `TurbulenceScale`, seeded with 0 and moved `TurbulencePhase × TurbulenceScale` DIPs along x, with the edges extended as BRIDGE-IMAGE's `Ripple` extends them.
  - Tests: `DistortTests.EachEffectDrawsASnapshotThatChangesPixelsInsteadOfTheChild`
- **BRIDGE-DISTORT-013** — `Goo` shall blur by a radius of `Goo` DIPs, converted to a sigma as `BlurEffect` converts its radius, and then map alpha to `19·a − 9`, so shapes closer than about the radius fuse with hard edges.
  - Tests: `DistortTests.EachEffectDrawsASnapshotThatChangesPixelsInsteadOfTheChild`
- **BRIDGE-DISTORT-014** — `Glitch` shall cut the bounds into 8 horizontal bands of heights seeded by its value, shift each band by a seeded offset within ±`Glitch` DIPs, then draw red shifted `Glitch` DIPs left and blue `Glitch` DIPs right of green, added together.
  - Tests: `DistortTests.EachEffectDrawsASnapshotThatChangesPixelsInsteadOfTheChild`
- **BRIDGE-DISTORT-015** (inherited) — A `TurbulenceScale` below 1e-3 shall be treated as 1e-3.
  - Tests: untested

### Animating the properties

- **BRIDGE-DISTORT-016** — Every effect and its tuning shall be an ordinary styled property that affects render, so `Animate`, builders, `Motion` states, bindings, style setters and `Revert` work on it unchanged.
  - Tests: `DistortTests.AnAnimatedEffectDrawsBetweenFramesAndRevertsToTheLocalValue`

### Defaults

- **BRIDGE-DISTORT-017** — When not set, `Turbulence`, `TurbulencePhase`, `Goo` and `Glitch` shall be 0 and `TurbulenceScale` 32 DIPs.
  - Tests: `DistortTests.TheDefaultsAreIdentityWithA32DipTurbulenceScale`

### Reduced motion

- **BRIDGE-DISTORT-018** — While `ReducedMotion` is set, read at each draw, the control shall draw as at identity: the child live, with no snapshot.
  - Tests: `DistortTests.UnderReducedMotionEveryEffectDrawsAsIdentity`

## Invariants

- The control holds a snapshot exactly while it draws one: from the first draw with an effect active until a draw at identity, a `Child` change or a detach.
- While a snapshot is held, the child's composition visual is scaled to nothing, and its previous scale is restored when the snapshot is released.
- Each draw operation retains the snapshot it draws, and the last holder disposes it, so the render thread never draws a disposed bitmap.

## Edge cases

- A negative `Turbulence` displaces the other way and is not identity; a negative `Glitch` mirrors the shifts and is not identity; a negative `Goo` is identity.
- The snapshot covers the child's bounds, so anything the child draws outside them (a render transform or overflow) is cut off while an effect is active.
- A new snapshot is taken from the frame after the child redraws, so for that frame the previous snapshot shows.
- Toggling `ReducedMotion` takes effect at the control's next draw; it does not redraw by itself.
- A compositor animation of the child's scale (`AnimateOnCompositor`) conflicts with the hiding while an effect is active.

## Threading and lifetime

- UI thread only: properties, snapshots and `Render` run on the UI thread; the draw operation runs on the render thread and owns its native filters and paint, disposed with the operation.
- While a snapshot is held, the control checks the child's subtree for redraws once per frame through `TopLevel.RequestAnimationFrame`, and stops when the snapshot is released.

## Reduced motion

- Every effect draws as identity (BRIDGE-DISTORT-018); the property values themselves are untouched, so they apply again once `ReducedMotion` is cleared and the control redraws.

## Allocation

- Not budgeted. Each draw with an effect active creates native filters, as BRIDGE-IMAGE's do; each redraw of the child allocates a new snapshot bitmap; the per-frame check walks the child's subtree.

## Deviations from anime.js

- No upstream counterpart. anime.js leaves raster effects to CSS and SVG filters; `Goo` is the classic SVG goo filter.

## Decisions

- **Raster distortion uses Skia built-in filters, as image effects do.** Reason: BRIDGE-IMAGE already decided against SkSL until built-ins prove insufficient; turbulence, displacement, blur and color matrix cover liquid, goo and glitch. `Distort` is a decorator rather than filters on arbitrary visuals because a draw operation cannot render child visuals, so it draws a snapshot while active.
- **Identity draws the child live.** Reason: at rest the content stays crisp and costs nothing, as BRIDGE-IMAGE's identity draws plain.
- **The child is hidden by scaling its composition visual to nothing.** Reason: the child keeps its layout and every property a user or Presto may animate (`Opacity`, `Clip`, `OpacityMask`, `RenderTransform`), and Avalonia re-syncs every other composition property from the control whenever it redraws, which would show the child again. Rejected: writing the child's `Opacity`, `OpacityMask` or `Clip` (conflicts with animations at the same priority), and moving it out of the visual tree (a detach completes its animations).
- **A redraw is detected through the composition draw lists of the child's subtree.** Each render of a visual replaces its draw list, read by reflection as `GlyphContours` reads Avalonia.Skia's path (BRIDGE-SHAPES), and compared once per frame while a snapshot is held. Reason: Avalonia raises no event when a visual is invalidated, and Presto changes transforms and brushes in place, which raises no property change. Rejected: property-change subscriptions (miss in-place changes) and a snapshot every frame.
- **The snapshot is drawn at the render scaling through `VisualSnapshot`.** Reason: a 96-DPI bitmap at device resolution draws one pixel per device pixel, so text stays sharp on scaled displays.
- **The filters cover the control's bounds, not the child's.** Reason: displacement, blur and glitch need room around the child; `Padding` gives it.
- **`Turbulence` reuses image effects' `Ripple` displacement.** Fractal noise, edge extension and a crop to the bounds, seeded with 0. Reason: one displacement implementation; fractal noise is centered so the content does not drift. Rejected: a `Seed` property, which nothing asked for.
- **`TurbulencePhase` moves the noise along x in multiples of `TurbulenceScale`.** Reason: Skia's fractal noise has no time axis, translating it makes the distortion flow along a line of text, and scale units tie the speed to the feature size.
- **`Goo` is a blur radius followed by the SVG goo threshold.** The radius converts as `BlurEffect`'s does, and alpha maps to `19·a − 9`. Reason: the well-known metaball look, with a radius that reads like Avalonia's own blur.
- **`Glitch` is seeded by its own value.** Reason: no extra property; a held strength holds still and an animated one flickers, which is what a glitch does. Rejected: a `GlitchSeed` property.
- **Reduced motion draws identity.** Reason: distortion is a large, continuous motion, which is what reduced motion opts out of.

## Non-goals

- Filters on arbitrary visuals without a decorator, SkSL shaders, and compositor-thread animation of the effects.

## Open items

- Untested: the child keeping its layout and values (004), drawing within the bounds (005), a `Child` change (009), no Skia lease (010), the effect order (011) and the `TurbulenceScale` floor (015).
- Whether the scaled-away child is still hit-testable while an effect is active is unspecified; the draw operation hit-tests the control's whole bounds.
- A static non-identity `Distort` keeps requesting animation frames for its redraw check.
- On the Desktop Liquid type page, `Turbulence` frays the edges of display text into hard one-pixel serrations rather than a smooth ripple: the displacement samples the snapshot unfiltered and the second noise octave adds fine roughness. Filtered sampling or a single octave would read more like liquid.
- manual — the Liquid type heading's 300 ms hover glitch needs a real pointer (a held `Glitch` was checked by eye), and the page's frame rate on the Browser head is unmeasured.
