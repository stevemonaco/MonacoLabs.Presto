---
id: BRIDGE-TEXTPATH
title: Text on a path
package: Monaco.Presto.AvaloniaBridge
namespaces:
  - Monaco.Presto.AvaloniaBridge
sources:
  - Monaco.Presto.AvaloniaBridge/TextPath.cs
  - Monaco.Presto.AvaloniaBridge/TextPathAlign.cs
types:
  - TextPath
  - TextPathAlign
tests:
  - TextPathTests
  - AllocationTests
upstream: []
depends:
  - BRIDGE-TEXT
  - BRIDGE-FOLLOW
  - BRIDGE-TRANSFORMS
  - BRIDGE-SHAPES
---

# Text on a path

## Purpose

`TextPath` is a set of attached properties that lay a `TextBlock`'s characters along a `Shape`'s contour: `presto:TextPath.Path="{Binding #Curve}" presto:TextPath.Offset="0.1" presto:TextPath.Align="Center"`. It splits the block with `TextSplitter` (BRIDGE-TEXT), samples the shape's rendered geometry as `CreateMotionPath` does (BRIDGE-SHAPES), and writes each character's placement as followed transform bases (BRIDGE-FOLLOW, BRIDGE-TRANSFORMS), so animations of the characters layer on top. It is the live form of the motion path: the text stays on the shape while the shape's geometry, size, position or `Offset` animates.

## Requirements

### Splitting and placement

- **BRIDGE-TEXTPATH-001** — Setting `TextPath.Path` to a `Shape` shall split the block into characters, each word's following space joined to its last character; clearing it shall revert the split, restoring the block's text.
  - Tests: `TextPathTests.CharactersSitOnAStraightPathAtTheirAdvances`, `TextPathTests.ClearingThePathRevertsTheText`
- **BRIDGE-TEXTPATH-002** — Each character's center shall sit `s = start + advance + width / 2` along the contour, where `width` is its slot's width (`TextSplitter.SlotOf`), `advance` is the sum of the previous characters' widths and `start` is `Offset × length`, minus the text's total advance for `End` and half of it for `Center`.
  - Tests: `TextPathTests.CharactersSitOnAStraightPathAtTheirAdvances`, `TextPathTests.AlignAnchorsTheTextAtTheOffsetAndShiftLiftsItOffTheContour`
- **BRIDGE-TEXTPATH-003** — Each character shall be rotated to the contour's tangent at `s` and moved `Shift` DIPs along the normal towards its top (to the left of the direction of travel).
  - Tests: `TextPathTests.RotationFollowsTheTangentOnAnArc`, `TextPathTests.AlignAnchorsTheTextAtTheOffsetAndShiftLiftsItOffTheContour`
- **BRIDGE-TEXTPATH-004** — The placement shall be written as the character's followed `X`, `Y` and `Rotate` bases (BRIDGE-FOLLOW-038), measured from its slot about its `RenderTransformOrigin`, so an animation of those shorthands layers over it and reverting the animation shows the placement again.
  - Tests: `TextPathTests.AYAnimationLayersOverThePlacementAndRevertsToIt`

### Ends and coordinates

- **BRIDGE-TEXTPATH-005** — On an open contour, a character whose `s` lies outside [0, length] shall be hidden by an `Opacity` of 0 at `LocalValue`, below the `Animation` priority animations write at; on a closed contour, one whose start and end points coincide, `s` shall wrap modulo the length.
  - Tests: `TextPathTests.CharactersPastTheEndOfAnOpenPathHide`, `TextPathTests.AClosedContourWraps`
- **BRIDGE-TEXTPATH-006** — The contour shall be read in the shape's coordinates and transformed into the block's. When the two share no visual root, or the shape has no geometry, the text shall stay where it is.
  - Tests: `TextPathTests.CharactersSitOnAStraightPathAtTheirAdvances`

### Re-placement

- **BRIDGE-TEXTPATH-007** — When a property of the shape changes, when a layout pass completes, or when `Offset`, `Align` or `Shift` changes, the block shall re-place at the end of the engine frame, at most once per frame however many changes it saw; a change made during a frame, such as a `MorphTo` of the shape's `Data`, is placed in that same frame.
  - Tests: `TextPathTests.ADataChangeRePlacesOnceOnTheNextFrame`, `TextPathTests.AlignAnchorsTheTextAtTheOffsetAndShiftLiftsItOffTheContour`
- **BRIDGE-TEXTPATH-008** — Every split and re-split shall place the new characters at once.
  - Tests: `TextPathTests.CharactersSitOnAStraightPathAtTheirAdvances`

### Defaults

- **BRIDGE-TEXTPATH-009** — When not set, `Offset` shall be 0, `Align` `Start` and `Shift` 0.
  - Tests: `TextPathTests.CharactersSitOnAStraightPathAtTheirAdvances`

## Invariants

- While `Path` is set and the block is split, every character's `X`, `Y` and `Rotate` bases are the last placement's.
- Placement never changes layout: it writes render transforms and opacity only, so it cannot cause the layout pass that requests it again.

## Edge cases

- A contour of zero length places nothing.
- Wrapped lines continue along the contour in reading order; the space at a wrap point is lost, as the splitter trims lines.
- Changing the block's `Text` while `Path` is set is not followed: the splitter holds the text it split.
- Setting `Path` to another shape reverts the split and splits again.
- An animation of a character's `Opacity` overrides the hiding of a past-the-end character.

## Threading and lifetime

- UI thread only. A placer hangs off the block in a private attached value while `Path` is set. It holds the splitter, a `MotionValue` whose `Changed` sink is the per-frame request, and handlers on the shape's `PropertyChanged` and the block's `LayoutUpdated`. Clearing `Path` removes all of them.

## Reduced motion

- Not affected: placement is layout, not motion.

## Allocation

- Re-placement allocates nothing; a frame costs only the property changes the characters' transforms raise (`AllocationTests.TextPathRePlacementTicksWithinBudget`). Splitting allocates, as BRIDGE-TEXT's does.

## Deviations from anime.js

- No upstream counterpart.

## Decisions

- **`TextPath` writes followed bases, not animated values.** Reason: animations of the same shorthands then layer over the placement and revert back to it (BRIDGE-TRANSFORMS-016), so a wave or scatter works on path text without fighting it. Rejected: writing `X`/`Y`/`Rotate` directly, since any per-character animation would overwrite the placement; a custom panel, which would bypass the splitter's re-split, accessibility and `TextReveal`.
- **`TextPath` samples the live geometry.** It re-reads the shape's rendered geometry on every placement, unlike `CreateMotionPath`, which captures it. Reason: riding a morphing path is the point of the effect. It samples through the same `Geometry.TryGetPointAndTangentAtDistance` as `CreateMotionPath`, so both agree on distances and angles.
- **Past-the-end characters hide; closed contours wrap.** Reason: clamping would pile characters on the end point.
- **`TextPath` is attached properties, not a recipe.** Reason: it is layout, not motion: it holds state, has no duration, and is driven by other animations. Rejected: a `Recipes.AlongPath` builder.
- **Re-placement is a `MotionValue` sink.** Each request bumps a private value whose `Changed` handler places, so requests coalesce to one placement flushed after every timer has ticked. Reason: an animation that moves the shape or `Offset` is followed in the same frame, without a lagging frame or a timer. Rejected: a zero-duration timer, which completes a frame later.
- **Spaces join the previous character.** The split uses `IncludeSpaces`, so the advance formula counts word gaps. Reason: without it, spaces are not characters and the words would run together on the contour.
- **Positive `Shift` moves towards the characters' tops.** Reason: lifting text off its line is the common use, and on a left-to-right path it reads as "up".

## Non-goals

- Variable letter spacing, kerning along the curve or glyph bending: each character is placed rigidly.
- Following text changes while placed.

## Open items

None.
