---
id: BRIDGE-SHAPES
title: Shapes (drawable, motion path, morph)
package: Monaco.Presto.AvaloniaBridge
namespaces:
  - Monaco.Presto.AvaloniaBridge
  - Monaco.Presto.AvaloniaBridge.Shapes
sources:
  - Monaco.Presto.AvaloniaBridge/Shapes/DrawAccessor.cs
  - Monaco.Presto.AvaloniaBridge/Values/GeometryCodec.cs
  - Monaco.Presto.AvaloniaBridge/PrestoAvalonia.cs
  - Monaco.Presto.AvaloniaBridge/PropertyBuilderExtensions.cs
types: []
tests:
  - ShapesTests
  - AllocationTests
  - MotionPathTests
  - MorphLabPageTests
  - ChartMorphPageTests
upstream:
  - src/svg/drawable.js
  - src/svg/motionpath.js
  - src/svg/morphto.js
  - src/svg/helpers.js
  - tests/suites/svg.test.js
depends:
  - BRIDGE-INIT
  - BRIDGE-TARGETS
  - BRIDGE-TRANSFORMS
  - BRIDGE-CODECS
  - CORE-MOTIONPATH
  - CORE-ANIMATION
  - CORE-KEYFRAMES
  - CORE-TIMELINE
  - CORE-VALUES
---

# Shapes

## Purpose

Ports of anime.js's `svg` helpers onto Avalonia shapes and geometries: `CreateDrawable` and the `"Draw"` pseudo-property reveal a slice of a `Shape`'s stroke; `CreateMotionPath` returns the `"X"`/`"Y"`/`"Rotate"` tweens that ride a shape's contour or a spline through points; `MorphTo` morphs a `Geometry` `Data` property into another by resampling both. The geometry value codec itself is BRIDGE-CODECS'; the spline is CORE-MOTIONPATH's. Also specifies `PrestoAvalonia.CreateDrawable`, `CreateMotionPath`, `MotionPathGeometry`, `MorphTo` and `MorphPointCount`, and `PropertyBuilderExtensions.Draw` and `Data`.

## Requirements

### Drawable

- **BRIDGE-SHAPES-001** — `CreateDrawable` shall initialize the bridge, put the shape into the draw state for `start`–`end` at once and return the same shape.
  - Tests: `ShapesTests.DrawWalksTheDashOffsetAsTheStartMoves`, `ShapesTests.LineCapIsFlattenedWhileTheStartAndEndMatch`
- **BRIDGE-SHAPES-002** — On a `Shape` target, `"Draw"` shall be a tween property whose value is two numbers, the start and end of the visible slice of the contour as fractions 0–1.
  - Tests: `ShapesTests.DrawWalksTheDashArrayFromHiddenToFullyDrawn`
- **BRIDGE-SHAPES-003** — Reading `"Draw"` shall return the pair last applied to the shape, or `0 0` for a shape never drawn.
  - Tests: `ShapesTests.DrawWalksTheDashOffsetAsTheStartMoves`
- **BRIDGE-SHAPES-004** — Applying a slice shall set `StrokeDashOffset` to `−start × L / T` and `StrokeDashArray` to `[(end − start) × L / T, gap / T]`, where `L` is the rendered geometry's contour length, `T` the stroke thickness and `gap` is `L + 10 − dash`, without the spare 10 when the slice is `0 1` or `1 0`.
  - Tests: `ShapesTests.DrawWalksTheDashArrayFromHiddenToFullyDrawn`, `ShapesTests.DrawWalksTheDashOffsetAsTheStartMoves`
- **BRIDGE-SHAPES-005** (inherited) — When the contour length or the stroke thickness is not positive, applying a slice shall record it without writing the dash properties.
  - Tests: untested
- **BRIDGE-SHAPES-006** — While start equals end, a shape whose line cap is not `Flat` shall draw with a `Flat` cap, and otherwise with the cap it had when it entered the draw state.
  - Tests: `ShapesTests.LineCapIsFlattenedWhileTheStartAndEndMatch`
- **BRIDGE-SHAPES-007** — Reverting a `"Draw"` tween shall restore the `StrokeDashArray`, `StrokeDashOffset` and `StrokeLineCap` the shape had when it first entered the draw state, and forget that state.
  - Tests: `ShapesTests.RevertRestoresTheStrokeStateTheShapeHadBeforeDrawing`
- **BRIDGE-SHAPES-008** — Each frame shall write the dashes into the same dash list rather than a new one.
  - Tests: `AllocationTests.DrawTweenTicksWithinBudget`
- **BRIDGE-SHAPES-009** (inherited) — The builder's `Draw(start, end)` shall animate `"Draw"` to `"start end"` formatted with the invariant culture.
  - Tests: untested

### Motion path along a shape

- **BRIDGE-SHAPES-010** — `CreateMotionPath(Shape)` shall return `X`, `Y` and `Rotate` tweens from 0 to the contour length of the shape's rendered geometry, whose modifiers map the tweened distance to the point's x, the point's y, and the tangent angle in degrees plus `autoRotate`.
  - Tests: `ShapesTests.MotionPathTranslatesAndRotatesAlongTheContour`
- **BRIDGE-SHAPES-011** — With `offset` 0 the distance shall clamp to the contour; with any other `offset` it shall be shifted by `offset × length` and wrap around.
  - Tests: `ShapesTests.MotionPathOffsetWrapsAroundInsteadOfClamping`
- **BRIDGE-SHAPES-012** (inherited) — When the shape has no rendered geometry, `CreateMotionPath` shall throw `ArgumentException`.
  - Tests: untested
- **BRIDGE-SHAPES-013** (inherited) — The tweens shall follow the geometry captured at the call, in the shape's own coordinates with no conversion, and yield 0 where the geometry has no point.
  - Tests: untested

### Motion path through points

- **BRIDGE-SHAPES-014** — `CreateMotionPath(points, …)` shall return the `X`, `Y` and `Rotate` tweens of `MotionPath.Through(points, curviness, closed)` with the same `offset` and `autoRotate` semantics.
  - Tests: `ShapesTests.PointMotionPathDrivesTheShorthandsThroughThePoints`
- **BRIDGE-SHAPES-015** — `MotionPathGeometry` shall return one unfilled figure of the same spline's cubic Béziers, closed when `closed` is true, so the track drawn matches the path ridden.
  - Tests: `ShapesTests.PointMotionPathDrivesTheShorthandsThroughThePoints`

### Morph

- **BRIDGE-SHAPES-016** (inherited) — When `precision` is zero or negative, `MorphTo` shall throw `ArgumentOutOfRangeException` at the call.
  - Tests: `ShapesTests.MorphRejectsAPrecisionOfZero`
- **BRIDGE-SHAPES-017** — `MorphTo` shall return a chained function value that resolves, per target, to a from/to pair of polylines with `MorphPointCount` points each, spread evenly along the source and destination contours.
  - Tests: `ShapesTests.MorphWalksThePointsOntoTheDestinationGeometry`, `ShapesTests.MorphPointCountIsTheNumberOfPointsTheMorphSamples`
- **BRIDGE-SHAPES-018** — `MorphPointCount` shall be `max(ceil(max(L_from, L_to) × precision), 2)`.
  - Tests: `ShapesTests.MorphPointCountIsTheNumberOfPointsTheMorphSamples`
- **BRIDGE-SHAPES-019** — The source shall be the value the previous tween of the same property ended on when there is one (a timeline child or keyframe before), and otherwise the target's `"Data"` geometry.
  - Tests: `ShapesTests.MorphInATimelineStartsFromThePreviousChildsShape`, `ShapesTests.MorphKeyframesStartFromThePreviousFramesShape`
- **BRIDGE-SHAPES-020** — `MorphTo` shall morph the `Geometry` `Data` property of any target that has one, such as `Path` and `PathIcon`.
  - Tests: `ShapesTests.MorphReadsTheDataOfAnyGeometryProperty`
- **BRIDGE-SHAPES-021** (inherited) — When the target has no `Geometry` `"Data"`, resolving the morph shall throw `ArgumentException`.
  - Tests: untested
- **BRIDGE-SHAPES-022** — Each frame shall write a new open polyline geometry through the interpolated points, so the morph ends on a polyline through the destination's contour, sharing its endpoints.
  - Tests: `ShapesTests.MorphWalksThePointsOntoTheDestinationGeometry`
- **BRIDGE-SHAPES-023** — Reverting a morph shall put back the `Data` instance the target had before it.
  - Tests: `ShapesTests.MorphRevertRestoresTheDataThePathStartedWith`
- **BRIDGE-SHAPES-024** — Sampled coordinates shall be rounded to 3 decimals and written with invariant formatting.
  - Tests: untested

### Defaults

- **BRIDGE-SHAPES-025** — When `start` and `end` are not given, `CreateDrawable` shall put the shape into the `0 0` slice, drawing none of its stroke, as anime.js does.
  - Tests: untested
- **BRIDGE-SHAPES-026** — When `offset` is not given, both `CreateMotionPath` overloads shall start at the path's start and clamp to its ends, as anime.js does.
  - Tests: `ShapesTests.MotionPathTranslatesAndRotatesAlongTheContour`
- **BRIDGE-SHAPES-027** (inherited) — When `autoRotate` is not given, the `Rotate` tween shall be the tangent angle unchanged.
  - Tests: `ShapesTests.MotionPathTranslatesAndRotatesAlongTheContour`
- **BRIDGE-SHAPES-028** — When `curviness` is not given, the points overload of `CreateMotionPath` and `MotionPathGeometry` shall use 1, as GSAP MotionPath does.
  - Tests: untested
- **BRIDGE-SHAPES-029** (inherited) — When `closed` is not given, the points overload of `CreateMotionPath` and `MotionPathGeometry` shall build an open path.
  - Tests: untested
- **BRIDGE-SHAPES-030** — When `precision` is not given, `MorphTo` and `MorphPointCount` shall use .33, as anime.js does.
  - Tests: untested

## Invariants

- A shape has at most one draw state, created on its first draw write or read and removed by a revert.
- Both ends of a `MorphTo` pair always have the same number of points.

## Edge cases

- A zero-length contour or zero stroke thickness records the slice but draws nothing new (005).
- A geometry animated to a plain `Geometry` value (`Data(Geometry)`) rather than `MorphTo` goes through the codec, which samples each end separately at the default precision and pads the shorter (BRIDGE-CODECS).
- A `Polyline`'s or `Polygon`'s `Points` is not a `Geometry` and cannot be morphed.
- A path that stretches its data (`Stretch` other than `None`) is drawn scaled while the morph and motion path read unscaled coordinates.

## Threading and lifetime

- UI thread only. Draw states live in a weak table keyed by the shape, so they never keep a shape alive.
- `Follow`'s draw target, `ProgressRing` and `SubmitButton` write through the same draw state (BRIDGE-FOLLOW, BRIDGE-SUBMIT).

## Reduced motion

- Not affected: these are value helpers; the animation that plays them decides its reduced form (CORE-REDUCED). The Morph lab page's reduced loops are pinned by `MorphLabPageTests.UnderReducedMotionEveryLoopPlaysOnceAndTheSignatureIsLeftDrawn`.

## Allocation

- A `"Draw"` tween ticks within the allocation budget, writing into one reused dash list (008).
- A morph allocates a new `StreamGeometry` per frame by design; the motion path modifiers allocate nothing.

## Deviations from anime.js

- `CreateDrawable` takes one `Shape` and returns it, not a selector and proxies; it always applies the initial slice, where upstream skips it once `pathLength` is already 1000.
- Dashes are measured against `Geometry.ContourLength` divided by the stroke thickness, Avalonia's dash unit, instead of `pathLength="1000"`; upstream's `non-scaling-stroke` scale factor has no counterpart.
- `CreateMotionPath` returns `TweenParams` rather than function values, uses the geometry's own tangent rather than points ±1 apart, does no CTM conversion, throws instead of returning `undefined`, and gains a points overload; the reasons are not recorded. It adds `autoRotate` degrees to the tangent rather than switching rotation on, as GSAP's `autoRotate: 90` does (CORE-MOTIONPATH).
- `MorphTo` rejects `precision` 0, which upstream reads as its raw-attribute morph; it never samples fewer than 2 points; it reads the previous tween's value instead of writing it back to the element; errors are `ArgumentException`s.

## Decisions

- **The path is a uniform Catmull-Rom spline sampled at constant speed.** See CORE-MOTIONPATH.
- **Rotation is degrees, not a switch.** See CORE-MOTIONPATH.

## Non-goals

- SVG documents, `pathLength`, polygon and polyline point lists.
- Coordinate conversion between the path's container and the animated control's.

## Open items

- Untested: zero length or thickness (005), `Draw(start, end)` formatting (009), `CreateMotionPath` with no geometry (012), geometry capture and missing points (013), morph of a target without `Data` (021), coordinate rounding (024).
