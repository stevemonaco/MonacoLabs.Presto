---
id: BRIDGE-WARP
title: Text warps
package: MonacoLabs.Presto.Avalonia
namespaces:
  - MonacoLabs.Presto
sources:
  - MonacoLabs.Presto.Avalonia/TextWarp.cs
  - MonacoLabs.Presto.Avalonia/TextWarpKind.cs
  - MonacoLabs.Presto.Avalonia/Text/WarpComposer.cs
  - MonacoLabs.Presto.Avalonia/Text/WarpShorthand.cs
types:
  - TextWarp
  - TextWarpKind
tests:
  - TextWarpTests
  - BridgeAllocationTests
upstream: []
depends:
  - BRIDGE-TEXT
  - BRIDGE-GLYPHS
  - BRIDGE-TARGETS
  - BRIDGE-EFFECTS
  - CORE-REDUCED
---

# Text warps

## Purpose

Deforms the outline pieces of a `TextBlock` split with `SplitTextParams.Outlines` (BRIDGE-TEXT) through one of a closed set of functions of a point's position in the block. `"Warp"` is a shorthand on the split block, its strength (0 = none), with the companion shorthands `"WarpPhase"`, `"WarpOrigin"` and `"WarpRadius"`, following the reveal-shorthand pattern of BRIDGE-EFFECTS; the attached `TextWarp.Kind` picks the function.

## Requirements

### Shorthands

- **BRIDGE-WARP-001** — A `TextBlock` shall resolve the shorthands `Warp`, `WarpPhase`, `WarpOrigin` (a `Point` in block coordinates) and `WarpRadius`, which start from and read as 0, 0, the center of the block's bounds and 100 while not animated.
  - Tests: `TextWarpTests.AtIdentityThePiecesKeepTheirOwnOutlines`, `TextWarpTests.TheShorthandsResolveOnlyOnATextBlock`
- **BRIDGE-WARP-002** — `WarpOrigin` shall take a `Point` and its channels, so an `Animatable` set from a `MotionValue` (such as `PointerValue`) drives it.
  - Tests: `TextWarpTests.AMotionValueDrivesTheOriginThroughAnAnimatable`
- **BRIDGE-WARP-003** (inherited) — When `TextWarp.Kind` is not set, the warp shall be `Wave`.
  - Tests: untested

### Frames

- **BRIDGE-WARP-004** — While `Warp` is not 0 on a split block, the bridge shall map every flattened outline point (BRIDGE-GLYPHS-006) of every outline piece, in block coordinates, through the function of `TextWarp.Kind`, and write the result as the piece's `Data` at `Animation` priority, as closed polygons with the nonzero fill rule.
  - Tests: `TextWarpTests.AFrameWritesEachPieceItsOutlineMappedInBlockCoordinates`
- **BRIDGE-WARP-005** — The bridge shall write a block's warp at most once per engine frame, at the frame's end, however many of its shorthands moved, and again when `TextWarp.Kind` changes.
  - Tests: `TextWarpTests.SeveralShorthandsMovingInOneFrameWriteOnce`
- **BRIDGE-WARP-006** — When `Warp` is 0, the pieces shall show their own outlines, the same geometry instances the split gave them.
  - Tests: `TextWarpTests.AtIdentityThePiecesKeepTheirOwnOutlines`, `TextWarpTests.ReturningToZeroOrRevertingGivesTheOutlinesBack`
- **BRIDGE-WARP-007** — When the last warp shorthand of a block is reverted, the pieces shall get back their own outlines and the bridge shall forget the block's warp state, Jelly's included.
  - Tests: `TextWarpTests.ReturningToZeroOrRevertingGivesTheOutlinesBack`
- **BRIDGE-WARP-008** — When the block is re-split, the bridge shall drop Jelly's state and warp the new pieces with the current shorthands; when the split is reverted, it shall write nothing.
  - Tests: `TextWarpTests.AReSplitWarpsTheNewPieces`
- **BRIDGE-WARP-009** — Every kind but `Jelly` shall be a function of the shorthands, the block's size and its font size only, so seeking or reversing a warp tween back to a time writes the points that time wrote before.
  - Tests: `TextWarpTests.SeekingAPureKindBackReproducesTheFrame`, `TextWarpTests.ReversingAPureKindPastAFrameReproducesIt`

### Kinds

With `a` the `Warp` amount, `R` the `WarpRadius`, `o` the `WarpOrigin`, `W` × `H` the block's size and `F` its font size:

- **BRIDGE-WARP-010** — `Wave` shall move each point down by `a × F / 2 × sin(2π x / R − WarpPhase)`, a sine of wavelength `R` travelling towards +x as the phase grows.
  - Tests: `TextWarpTests.WaveDisplacesVerticallyAlongASineThatTravelsWithThePhase`
- **BRIDGE-WARP-011** — `Bend` shall bend the block's horizontal center line into a circular arc through the block's center, sweeping `a × π` radians over the width `W` (a half circle at 1, arching up for positive `a`), each point keeping its distance from the center line along the arc's normal.
  - Tests: `TextWarpTests.BendArcsTheCenterLineSweepingHalfACircleAtOne`
- **BRIDGE-WARP-012** — `Bulge` shall scale a point's offset from `o` by `1 + a × (1 − d / R)²`, `d` its distance from `o`, within `R` of `o`, and leave points farther away unchanged, so a negative `a` pinches.
  - Tests: `TextWarpTests.BulgeMagnifiesWithinTheRadiusAndANegativeAmountPinches`
- **BRIDGE-WARP-013** — `Twist` shall rotate each point about `o` by `a × d / R` radians, `d` its distance from `o`.
  - Tests: `TextWarpTests.TwistTurnsEachPointByItsDistanceFromTheOrigin`
- **BRIDGE-WARP-014** — `Melt` shall move each point down by `a × F × drip(x) × clamp(y / H, 0, 1)`, where `drip` eases smoothly between fixed seeded values in 0.25–1 at columns `F / 2` apart.
  - Tests: `TextWarpTests.MeltDripsDownByColumnGrowingTowardsTheBottomAndWithTheAmount`
- **BRIDGE-WARP-015** — `Perspective` shall scale each point's horizontal offset from the block's center line by `1 − a × (1 − clamp(y / H, 0, 1))`, so the top edge is `1 − a` times as wide as the bottom.
  - Tests: `TextWarpTests.PerspectiveNarrowsTheTopRelativeToTheBottom`
- **BRIDGE-WARP-016** — Under `Jelly`, each point shall carry a spring offset of stiffness 400 s⁻² and damping 8 s⁻¹ (unit mass), and each move of `WarpOrigin` by Δ shall add `a × max(0, 1 − d / R) × Δ × 10 s⁻¹` to its velocity, `d` its distance from the new origin; the springs are stepped every engine frame while any moves, and each point is drawn at its outline position plus its offset.
  - Tests: `TextWarpTests.JellyWobblesWhileTheOriginMovesAndSettlesBackToTheOutlines`
- **BRIDGE-WARP-017** — When every Jelly offset is below 0.01 DIPs and every velocity below 0.01 DIPs per second, the springs shall stop stepping and the pieces shall show their own outlines until the origin moves again.
  - Tests: `TextWarpTests.JellyWobblesWhileTheOriginMovesAndSettlesBackToTheOutlines`

### Reduced motion

- **BRIDGE-WARP-018** — While `ReducedMotion` is set on `Engine.Current`, a frame of a warp shorthand shall set it to its identity instead of its value, so the text stays straight and `Jelly` is never kicked.
  - Tests: `TextWarpTests.UnderReducedMotionTheShorthandsHoldTheirIdentities`

## Invariants

- A block has at most one warp composer, holding the pieces' `Data` at `Animation` priority only while it draws a warp.
- A warped piece's geometry has exactly the points of its flattened outline, in the same order.

## Edge cases

- Character pieces without an outline (emoji, color glyphs) are text pieces and are not warped.
- A warp animated on a block that is not split yet writes nothing; it shows from the next frame in which a shorthand moves after the split.
- `Twist` and a large `Bulge` can fold the outline over itself; the nonzero fill draws the overlap as ink.
- `WarpOrigin` and the block-relative kinds read the block's bounds at each frame, so a resize moves the identity origin with the block's center.

## Threading and lifetime

- UI thread only. The composer lives in a `ConditionalWeakTable` keyed by the block and is removed when its last shorthand is reverted. Jelly's springs step on an engine timer that pauses when they settle and is reverted with the composer.
- Each splitter the composer meets gets one effect asking for a write after every re-split; it does nothing once the composer is gone.

## Reduced motion

- The shorthands hold their identities (BRIDGE-WARP-018).

## Allocation

- A warp frame allocates each warped piece's new `StreamGeometry` (with its context and Skia path) and the `Data` change it raises, nothing else: about 640 bytes per character (`BridgeAllocationTests.ATextWarpFrameAllocatesOnlyItsGeometries`). Jelly's spring buffers are allocated when the composer takes up a split's pieces.

## Provenance

- No upstream counterpart: DOM text has no outline.

## Decisions

- **Warps are a closed set of kinds behind shorthands.** `"Warp"` is the strength on the split block with `"WarpPhase"`, `"WarpOrigin"` and `"WarpRadius"` as companion shorthands, and the kind is an attached property. Reason: the reveal shorthands (`ClipCircle` plus `ClipCircleCenter`) set the pattern of a scalar shorthand with companions; a closed set lets each kind be tuned, tested and given a reduced form. Rejected: a public `Func<Point, Point>` warp delegate, which users did not ask for and which could not be kept allocation-free or reduced-motion safe.
- **Warps are applied to a flattened copy of the outline.** The outline is flattened once at split time with straight edges subdivided (BRIDGE-GLYPHS), and each frame maps those points into a new geometry. Reason: warps must bend straight stems, which Bézier control-point warping cannot do. The per-frame geometry is an accepted allocation, as for `MorphTo` (BRIDGE-SHAPES-022).
- **`Jelly` is stateful; the other kinds are pure functions of their shorthands.** Reason: a jelly response needs velocity, but keeping every other kind pure means seek, reverse and timelines work on them exactly.
- **Jelly is underdamped and settles in about two seconds.** Stiffness 400 s⁻², damping 8 s⁻¹ (a damping ratio of 0.2, ringing at about 3 Hz) and a kick of 10 s⁻¹ per DIP of origin travel, so a quick move shakes nearby points by about half its length at strength 1; a settled jelly shows the plain outlines rather than a zero warp. Reason: a visible wobble that is gone before the next interaction, and no geometry written at rest.
- **Warp sizes come from the text.** `Wave` and `Melt` scale with the font size, and `Wave` takes its wavelength and `Twist` its rate from `WarpRadius`. Reason: one strength reads alike across font sizes, and the radius is the one length the shorthands already carry.
- **A warp writes once per frame, at the frame's end.** Each shorthand only records its value, and a motion-value sink writes the geometry after every timer has ticked, as `TextPath` places its characters. Reason: four shorthands moving in one frame cost one geometry per piece, not four.
- **Reduced motion holds the identities.** Under `ReducedMotion` the shorthands stay at their identities and the text stays straight. Reason: warps are large, continuous motions, which is what reduced motion opts out of.

## Non-goals

- **No extruded warp kind in v1.** Reason: extrusion needs depth sorting and is closer to 3D than to a warp; `Perspective` covers the flat case.
- Warping text pieces, which have no outline.
- Typed builder methods for the warp shorthands.

## Open items

- BRIDGE-WARP-003 is untested.
- A warp's field spans one block, so a headline of several `TextBlock`s (Warp studio's three lines) gets one field per line: `Twist` reads as three small turns rather than a whirlpool, and `Perspective` as three trapezoids rather than one receding plane. Nothing yet lets blocks share an origin and bounds.
- The Warp shorthands animate a whole block, so a warp cannot be staggered letter by letter; Warp studio's stagger slider delays the warp line by line instead.
- manual — the pointer-driven warps (Warp studio's Magnifier, Jelly and Follow pointer, Typography's Bulge wordmark) need a real pointer on Desktop; DevTools sends no pointer moves.
