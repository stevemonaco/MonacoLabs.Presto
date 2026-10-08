---
id: BRIDGE-MAGNETIC
title: Magnetic pull
package: Monaco.Presto.AvaloniaBridge
namespaces:
  - Monaco.Presto.AvaloniaBridge
sources:
  - Monaco.Presto.AvaloniaBridge/Magnetic.cs
types:
  - Magnetic
tests:
  - MagneticTests
upstream: []
depends:
  - BRIDGE-FOLLOW
  - BRIDGE-TRANSFORMS
  - CORE-MOTIONVALUE
  - CORE-SPRING
  - BRIDGE-REDUCED
---

# Magnetic pull

## Purpose

`Magnetic` is an attached property that pulls an element towards a nearby pointer and springs it back when the pointer leaves. It is a motion value graph over `PrestoAvalonia.PointerOffsetValue` and `PrestoAvalonia.PointerProximityValue` (BRIDGE-FOLLOW) written into the `Follow.X`/`Follow.Y` shorthands, shaped like `Tilt` (BRIDGE-EFFECTS).

## Requirements

- **BRIDGE-MAGNETIC-001** — While `Magnetic.Enabled` is true and the control is in the visual tree, the bridge shall move the control's `"X"` and `"Y"` shorthands to `PointerOffsetValue × Strength × PointerProximityValue` on each axis (proximity from the control's edge over `Radius`, tracked over its `TopLevel`), through a `Springs.Snappy` follower, as a followed base layer (BRIDGE-FOLLOW-038).
  - Tests: `MagneticTests.ThePullGrowsAsThePointerNearsAndIsCappedByTheStrength`, `MagneticTests.AnXAnimationLayersOverThePullAndRevertingItShowsThePull`
- **BRIDGE-MAGNETIC-002** — When the pointer becomes absent or moves beyond `Radius`, the control shall spring back to an offset of 0.
  - Tests: `MagneticTests.LeavingSpringsBackToRest`
- **BRIDGE-MAGNETIC-003** — When `Magnetic.Enabled` becomes false or the control detaches, the bridge shall stop its values and clear `Follow.X` and `Follow.Y`, restoring the control's own `RenderTransform` once nothing else holds it; reattaching while enabled builds the pull again.
  - Tests: `MagneticTests.DisablingOrDetachingRestoresTheElementsOwnRenderTransform`
- **BRIDGE-MAGNETIC-004** — While `PrestoDefaults.ReducedMotion` is set on `Engine.Current` as the pointer moves, the pull shall be 0, so the control stays at rest.
  - Tests: `MagneticTests.ReducedMotionStaysAtRest`
- **BRIDGE-MAGNETIC-005** — When `Magnetic.Strength` changes, the new value shall apply on each axis from the next pointer move that changes that axis's offset or the proximity.
  - Tests: `MagneticTests.AStrengthChangeAppliesFromTheNextMoveAndARadiusChangeAtOnce`
- **BRIDGE-MAGNETIC-009** — When `Magnetic.Radius` changes while the pointer is present, the bridge shall rebuild the graph, so the new pull applies at once, without a spring.
  - Tests: `MagneticTests.AStrengthChangeAppliesFromTheNextMoveAndARadiusChangeAtOnce`
- **BRIDGE-MAGNETIC-006** (inherited) — In design mode, setting `Magnetic.Enabled` shall do nothing.
  - Tests: `MagneticTests.DoesNothingInDesignMode`

### Defaults

- **BRIDGE-MAGNETIC-007** — When `Magnetic.Enabled` is not set, the control shall not be pulled.
  - Tests: untested
- **BRIDGE-MAGNETIC-008** — When `Magnetic.Strength` is not set, it shall be 0.35, and when `Magnetic.Radius` is not set, it shall be 48 DIPs.
  - Tests: `MagneticTests.ThePullGrowsAsThePointerNearsAndIsCappedByTheStrength`

## Invariants

- The pull on an axis never exceeds the pointer's offset × `Strength`, reached with the pointer on or inside the control's edge.
- The pull is measured against the control's layout slot, so the pull itself never changes what is measured (BRIDGE-FOLLOW-068).

## Edge cases

- `Magnetic` owns `Follow.X` and `Follow.Y` while enabled and attached: a value the app set on them is replaced, and cleared on disable or detach.
- A `Radius` change rebuilds the graph, so the spring restarts at the current pull instead of springing to it.
- Turning reduced motion on while pulled leaves the pull until the next pointer move, which springs it back to 0.

## Threading and lifetime

- UI thread only. A tracker hangs off the control in a private attached value; its source values and springs live in a `Scope` that exists only while the control is attached and enabled.

## Reduced motion

- The pull is 0 under reduced motion (BRIDGE-MAGNETIC-004), so the control stays at rest.

## Allocation

- Building the graph allocates on attach, `Radius` change and enable. A pointer move allocates nothing beyond Avalonia's routing (BRIDGE-FOLLOW-071), and a frame costs the `MotionValue`-driven `Follow.X` budget per axis (BRIDGE-FOLLOW Allocation); neither is pinned by a `Magnetic` test.

## Deviations from anime.js

- No upstream counterpart.

## Decisions

- **`Magnetic` is an attached-property class like `Tilt`, not an `Interactions` builder.** Reason: `Interactions.*` return `AnimationBuilder`s played once on a trigger, while a magnet is continuous pointer-driven state with an attach lifetime, which is `Tilt`'s shape; attached properties already work from XAML, so no markup extension is needed.
- **`Magnetic` owns its spring inside the motion value graph** (`Spring(Springs.Snappy)` before `Follow.X`/`Follow.Y`) rather than through `Follow.Timing`. Reason: a `MotionValue` on a shorthand ignores `Follow.Timing` (BRIDGE-FOLLOW-042), and a spring that keeps velocity on retarget suits a pointer that never stops moving.
- **`Magnetic` uses a fixed `Springs.Snappy`.** Reason: no sample needs another; a configurable `Magnetic.Spring` is in the backlog.
- **`Strength` and reduced motion are read as the sources move; `Radius` rebuilds the graph.** Reason: `PointerProximityValue` fixes its radius when made, and rebuilding on a rare `Radius` change keeps the tracker untouched. Rejected: a live-radius overload of the proximity value for one caller.
- **Magnets keep tracking a pointer captured elsewhere.** A `Draggable` drag elsewhere still drives magnets, as the shared tracker sees handled and captured moves (BRIDGE-FOLLOW-064). Reason: relaxing magnets during another element's drag needs a notion of the drag the tracker does not have, and no sample asks for it.

## Non-goals

- A configurable spring.
- Relaxing magnets while another element is being dragged.

## Open items

None.
