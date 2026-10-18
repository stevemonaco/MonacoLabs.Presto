---
id: BRIDGE-SHAPES
title: Shapes (drawable, motion path, morph)
package: MonacoLabs.Presto.Avalonia
namespaces:
  - MonacoLabs.Presto
  - MonacoLabs.Presto.Shapes
sources:
  - MonacoLabs.Presto.Avalonia/Shapes/DrawAccessor.cs
  - MonacoLabs.Presto.Avalonia/Shapes/ContourMorph.cs
  - MonacoLabs.Presto.Avalonia/Values/GeometryCodec.cs
  - MonacoLabs.Presto.Avalonia/PrestoAvalonia.cs
  - MonacoLabs.Presto.Avalonia/PropertyBuilderExtensions.cs
types: []
tests:
  - ShapesTests
  - BridgeAllocationTests
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
  - BRIDGE-GLYPHS
---

# Shapes

## Purpose

Ports of anime.js's `svg` helpers onto Avalonia shapes and geometries: `CreateDrawable` and the `"Draw"` pseudo-property reveal a slice of a `Shape`'s stroke; `CreateMotionPath` returns the `"X"`/`"Y"`/`"Rotate"` tweens that ride a shape's contour or a spline through points; `MorphTo` morphs a `Geometry` `Data` property into another by resampling both. The geometry value codec itself is BRIDGE-CODECS'; the spline is CORE-MOTIONPATH's. `TextPath` (BRIDGE-TEXTPATH) is the live form of the motion path, re-reading the geometry as it changes. Also specifies `PrestoAvalonia.CreateDrawable`, `CreateMotionPath`, `MotionPathGeometry`, `MorphTo` and `MorphPointCount`, and `PropertyBuilderExtensions.Draw` and `Data`.

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
- **BRIDGE-SHAPES-005** — When the contour length or the stroke thickness is not positive, applying a slice shall record it without writing the dash properties.
  - Tests: `ShapesTests.ASliceOnAZeroLengthOrThicknessIsRecordedWithoutWritingTheDashes`
- **BRIDGE-SHAPES-006** — While start equals end, a shape whose line cap is not `Flat` shall draw with a `Flat` cap, and otherwise with the cap it had when it entered the draw state.
  - Tests: `ShapesTests.LineCapIsFlattenedWhileTheStartAndEndMatch`
- **BRIDGE-SHAPES-007** — Reverting a `"Draw"` tween shall restore the `StrokeDashArray`, `StrokeDashOffset` and `StrokeLineCap` the shape had when it first entered the draw state, and forget that state.
  - Tests: `ShapesTests.RevertRestoresTheStrokeStateTheShapeHadBeforeDrawing`
- **BRIDGE-SHAPES-008** — Each frame shall write the dashes into the same dash list rather than a new one.
  - Tests: `BridgeAllocationTests.DrawTweenTicksWithinBudget`
- **BRIDGE-SHAPES-035** — While a shape is in the draw state, a change to its `StrokeThickness` shall apply the slice last applied again at the new thickness, and a revert shall stop following it.
  - Tests: `ShapesTests.AThicknessChangeAfterADrawKeepsOneDashPerContour`, `ShapesTests.ARevertedDrawNoLongerFollowsTheThickness`
- **BRIDGE-SHAPES-031** — On a character outline piece (BRIDGE-TEXT-055), `L` shall be the perimeter of the piece's longest flattened figure, so a `0 1` slice traces every contour of the outline.
  - Tests: `ShapesTests.DrawOnAnOutlinePieceWalksTheDashOverItsLongestFigure`
- **BRIDGE-SHAPES-009** — The builder's `Draw(start, end)` shall animate `"Draw"` to `"start end"` formatted with the invariant culture.
  - Tests: `ShapesTests.TheDrawBuilderFormatsItsSliceInTheInvariantCulture`

### Motion path along a shape

- **BRIDGE-SHAPES-010** — `CreateMotionPath(Shape)` shall return `X`, `Y` and `Rotate` tweens from 0 to the contour length of the shape's rendered geometry, whose modifiers map the tweened distance to the point's x, the point's y, and the tangent angle in degrees plus `autoRotate`.
  - Tests: `ShapesTests.MotionPathTranslatesAndRotatesAlongTheContour`
- **BRIDGE-SHAPES-011** — With `offset` 0 the distance shall clamp to the contour; with any other `offset` it shall be shifted by `offset × length` and wrap around.
  - Tests: `ShapesTests.MotionPathOffsetWrapsAroundInsteadOfClamping`
- **BRIDGE-SHAPES-012** — When the shape has no rendered geometry, `CreateMotionPath` shall throw `ArgumentException`.
  - Tests: `ShapesTests.AMotionPathAlongAShapeWithoutGeometryThrows`
- **BRIDGE-SHAPES-013** — The tweens shall follow the geometry captured at the call, in the shape's own coordinates with no conversion, and yield 0 where the geometry has no point.
  - Tests: `ShapesTests.AMotionPathFollowsTheGeometryCapturedAtTheCallInTheShapesOwnCoordinates`, `ShapesTests.AMotionPathYieldsZeroWhereTheGeometryHasNoPoint`

### Motion path through points

- **BRIDGE-SHAPES-014** — `CreateMotionPath(points, …)` shall return the `X`, `Y` and `Rotate` tweens of `MotionPath.Through(points, curviness, closed)` with the same `offset` and `autoRotate` semantics.
  - Tests: `ShapesTests.PointMotionPathDrivesTheShorthandsThroughThePoints`
- **BRIDGE-SHAPES-015** — `MotionPathGeometry` shall return one unfilled figure of the same spline's cubic Béziers, closed when `closed` is true, so the track drawn matches the path ridden.
  - Tests: `ShapesTests.PointMotionPathDrivesTheShorthandsThroughThePoints`

### Morph

- **BRIDGE-SHAPES-016** — When `precision` is zero or negative, `MorphTo` shall throw `ArgumentOutOfRangeException` at the call.
  - Tests: `ShapesTests.MorphRejectsAPrecisionOfZero`
- **BRIDGE-SHAPES-017** — `MorphTo` shall return a chained function value that resolves, per target, to a from/to pair holding, per contour pair, two polylines with the same point count, spread evenly along their contours; when both geometries have one figure, the pair is one polyline each with `MorphPointCount` points, sampled from the start of the contour.
  - Tests: `ShapesTests.MorphWalksThePointsOntoTheDestinationGeometry`, `ShapesTests.MorphPointCountIsTheNumberOfPointsTheMorphSamples`, `ShapesTests.SingleContourMorphKeepsItsOpenPolylineSamples`
- **BRIDGE-SHAPES-018** — `MorphPointCount` shall be `max(ceil(max(L_from, L_to) × precision), 2)` when both geometries have one figure, and otherwise the sum of that count over the contour pairs, with `L` each contour's perimeter and 0 for a missing partner.
  - Tests: `ShapesTests.MorphPointCountIsTheNumberOfPointsTheMorphSamples`, `ShapesTests.MorphPairsOutersWithOutersAndHolesWithHoles`
- **BRIDGE-SHAPES-032** — When either geometry has more than one figure, each shall be flattened into closed contours classified as outer or hole (BRIDGE-GLYPHS), outer contours paired with outer contours and the holes of each paired outer with the holes of its partner, nearest pair first by the distance between centroids normalized to each geometry's bounds.
  - Tests: `ShapesTests.MorphPairsOutersWithOutersAndHolesWithHoles`, `ShapesTests.MorphKeepsAHoleThroughoutAGlyphMorph`
- **BRIDGE-SHAPES-033** — A contour without a partner shall morph from or to a point repeated as often as the pair's point count: for an outer contour, the centroid of the other geometry's outer contour nearest to it (its own centroid when there is none); for a hole, a point in the ink of the outer contour its own outer contour morphs with (inside that contour and outside its holes), at least half as deep as the deepest such point on a 24 × 24 grid over its bounds, nearest to the hole's centroid mapped between the two geometries' bounds; or, when its outer contour has no partner, the point that outer contour collapses into or grows from.
  - Tests: `ShapesTests.MorphCollapsesAContourWithoutAPartnerToAPoint`, `ShapesTests.AHoleWithoutAPartnerCollapsesIntoInk`
- **BRIDGE-SHAPES-034** — Each pair's second polyline shall start at the sample that minimizes the summed squared distance between the pair's points.
  - Tests: `ShapesTests.MorphWindsEachContourByItsRoleAndRotatesItToTheLeastTravel`
- **BRIDGE-SHAPES-036** — Both polylines of each pair shall run clockwise on screen for an outer contour and counter-clockwise for a hole.
  - Tests: `ShapesTests.MorphWindsEachContourByItsRoleAndRotatesItToTheLeastTravel`, `ShapesTests.OverlappingOuterContoursStayFilledMidMorph`
- **BRIDGE-SHAPES-019** — The source shall be the value the previous tween of the same property ended on when there is one (a timeline child or keyframe before), and otherwise the target's `"Data"` geometry.
  - Tests: `ShapesTests.MorphInATimelineStartsFromThePreviousChildsShape`, `ShapesTests.MorphKeyframesStartFromThePreviousFramesShape`
- **BRIDGE-SHAPES-020** — `MorphTo` shall morph the `Geometry` `Data` property of any target that has one, such as `Path` and `PathIcon`.
  - Tests: `ShapesTests.MorphReadsTheDataOfAnyGeometryProperty`
- **BRIDGE-SHAPES-021** — When the target has no `Geometry` `"Data"`, resolving the morph shall throw `ArgumentException`.
  - Tests: `ShapesTests.MorphingATargetWithoutGeometryDataThrows`
- **BRIDGE-SHAPES-022** — When both geometries have one figure, each frame shall write a new open polyline geometry through the interpolated points, so the morph ends on a polyline through the destination's contour, sharing its endpoints; otherwise, one closed figure per contour pair with the nonzero fill rule.
  - Tests: `ShapesTests.MorphWalksThePointsOntoTheDestinationGeometry`, `ShapesTests.SingleContourMorphKeepsItsOpenPolylineSamples`, `ShapesTests.MorphKeepsAHoleThroughoutAGlyphMorph`
- **BRIDGE-SHAPES-023** — Reverting a morph shall put back the `Data` instance the target had before it.
  - Tests: `ShapesTests.MorphRevertRestoresTheDataThePathStartedWith`
- **BRIDGE-SHAPES-024** — Sampled coordinates shall be rounded to 3 decimals and written with invariant formatting.
  - Tests: `ShapesTests.SingleContourMorphKeepsItsOpenPolylineSamples`

### Defaults

- **BRIDGE-SHAPES-025** — When `start` and `end` are not given, `CreateDrawable` shall put the shape into the `0 0` slice, drawing none of its stroke.
  - Tests: `ShapesTests.CreateDrawableDefaultsToTheEmptySlice`
- **BRIDGE-SHAPES-026** — When `offset` is not given, both `CreateMotionPath` overloads shall start at the path's start and clamp to its ends.
  - Tests: `ShapesTests.MotionPathTranslatesAndRotatesAlongTheContour`
- **BRIDGE-SHAPES-027** — When `autoRotate` is not given, the `Rotate` tween shall be the tangent angle unchanged.
  - Tests: `ShapesTests.MotionPathTranslatesAndRotatesAlongTheContour`
- **BRIDGE-SHAPES-028** — When `curviness` is not given, the points overload of `CreateMotionPath` and `MotionPathGeometry` shall use 1, as GSAP MotionPath does.
  - Tests: `ShapesTests.PointMotionPathsDefaultToCurvinessOneAndAnOpenPath`
- **BRIDGE-SHAPES-029** — When `closed` is not given, the points overload of `CreateMotionPath` and `MotionPathGeometry` shall build an open path.
  - Tests: `ShapesTests.PointMotionPathsDefaultToCurvinessOneAndAnOpenPath`
- **BRIDGE-SHAPES-030** — When `precision` is not given, `MorphTo` and `MorphPointCount` shall use .33.
  - Tests: `ShapesTests.MorphDefaultsToAPrecisionOfAThird`

## Invariants

- A shape has at most one draw state, created on its first draw write or read and removed by a revert.
- Both ends of each `MorphTo` contour pair always have the same number of points.

## Edge cases

- A zero-length contour or zero stroke thickness records the slice but draws nothing new (005).
- A geometry animated to a plain `Geometry` value (`Data(Geometry)`) rather than `MorphTo` goes through the codec, which samples each end separately at the default precision and pads the shorter (BRIDGE-CODECS).
- A `Polyline`'s or `Polygon`'s `Points` is not a `Geometry` and cannot be morphed.
- A geometry whose figures cannot be read (a renderer other than Skia) morphs as one figure (032).
- A collapsed contour stays in the morph's end value as a figure of identical points, so a chained morph reads it as a zero-area contour.
- A multi-figure morph closes every contour, so an open figure among several (a stroke-only line) is morphed and drawn closed.
- A path that stretches its data (`Stretch` other than `None`) is drawn scaled while the morph and motion path read unscaled coordinates.

## Threading and lifetime

- UI thread only. Draw states live in a weak table keyed by the shape, so they never keep a shape alive.
- `Follow`'s draw target, `ProgressRing` and `SubmitButton` write through the same draw state (BRIDGE-FOLLOW, BRIDGE-SUBMIT).

## Reduced motion

- Not affected: these are value helpers; the animation that plays them decides its reduced form (CORE-REDUCED). The Morph lab page's reduced loops are pinned by `MorphLabPageTests.UnderReducedMotionEveryLoopPlaysOnceAndTheSignatureIsLeftDrawn`.

## Allocation

- A `"Draw"` tween ticks within the allocation budget, writing into one reused dash list (008).
- A morph allocates a new `StreamGeometry` per frame by design, single or multi-contour alike; the motion path modifiers allocate nothing. Contour pairing, sampling and alignment happen once, when the morph resolves.

## Provenance

- `CreateDrawable` takes one `Shape` and returns it, not a selector and proxies; it always applies the initial slice, where upstream skips it once `pathLength` is already 1000.
- Dashes are measured against `Geometry.ContourLength` divided by the stroke thickness, Avalonia's dash unit, instead of `pathLength="1000"`; upstream's `non-scaling-stroke` scale factor has no counterpart.
- `CreateMotionPath` returns `TweenParams` rather than function values, uses the geometry's own tangent rather than points ±1 apart, does no CTM conversion, throws instead of returning `undefined`, and gains a points overload (see "Motion paths are typed and fail fast"). It adds `autoRotate` degrees to the tangent rather than switching rotation on, as GSAP's `autoRotate: 90` does (CORE-MOTIONPATH).
- `MorphTo` pairs, closes and aligns the contours of multi-figure geometries, where upstream samples one contour of each end from its start.
- `MorphTo` rejects `precision` 0, which upstream reads as its raw-attribute morph; it never samples fewer than 2 points; it reads the previous tween's value instead of writing it back to the element; errors are `ArgumentException`s.

## Decisions

- **Unset options draw from nothing along the path as given.** A drawable starts in the `0 0` slice and a motion path at its start, clamped to its ends (BRIDGE-SHAPES-025, 026). Reason: the common use is drawing a stroke on or moving along a path from its start, so the first tween needs no `from`. The morph precision .33 (030) is kept from upstream, so a ported morph samples as densely. Without `autoRotate` the rotation is the tangent itself, and without `closed` a points path is open (027, 029), the neutral choices.
- **Motion paths are typed and fail fast.** `CreateMotionPath` returns `TweenParams`, follows the geometry captured at the call in the shape's own coordinates, and throws `ArgumentException` when the shape has no rendered geometry; a morph of a target with no `Geometry` `"Data"` throws too (BRIDGE-SHAPES-012, 013, 021). Reason: typed params drop into the builder and `Animate` without a lookup, Avalonia has no document-wide transform to convert through, so the caller places the path, and C# reports a missing geometry where it happens rather than returning upstream's `undefined`. The geometry's own tangent is exact where points ±1 apart are an estimate.
- **Bad slices and precisions are refused where they cannot draw.** A slice on a contour or stroke that is not positive is recorded but writes no dashes, and `MorphTo` throws for a precision of 0 or less at the call (BRIDGE-SHAPES-005, 016). Reason: dash lengths divide by the length and the thickness, and a morph without a point count cannot sample.
- **Draw strings are invariant.** `Draw(start, end)` formats with the invariant culture (BRIDGE-SHAPES-009). Reason: the codec parses the same string back, which must not depend on the machine's locale.
- **The path is a uniform Catmull-Rom spline sampled at constant speed.** See CORE-MOTIONPATH.
- **Rotation is degrees, not a switch.** See CORE-MOTIONPATH.
- **The draw state follows the stroke thickness.** Dashes are written in units of the thickness at the time of the write, and Avalonia scales them by the thickness at render time, so a later thickness tween (`WriteIn`'s stroke thinning into the fill) left them `T/T₀` long and cut each contour into `T₀/T` dashes. Avalonia.Skia measures a stroked shape's bounds by dashing it and running path-ops `TightBounds` on the result, which grows superlinearly with the dash count (15 ms for one glyph at 20k points), so the render thread stalled for seconds as `T` neared 0. Reason: re-applying the slice on a thickness change keeps one dash and one gap per figure at any thickness, and fixes the slice shown, not only the cost. Rejected: tweening `Stroke` to transparent instead of the thickness, which loses the thinning effect and leaves the trap for every other caller; a text control drawing through Skia directly.
- **An outline piece's dashes are measured against its longest figure.** `Geometry.ContourLength` and `TryGetPointAtDistance` read only the first figure (`M0,0 L100,0 M0,10 L50,10` has a length of 100, and the point at 120 clamps to the end of the first line), and Skia restarts the dash pattern on every figure, so every figure runs the same slice of one length; measured against the longest, `0 1` ends with every figure drawn, shorter figures finishing early. Reason: a single dash pattern cannot give each figure its own length, and this is the only length at which the end of the tween is the whole outline. Rejected: the summed length, which finishes every figure early and leaves the rest of the tween idle.

- **Contour pairing is by classification and normalized centroid distance.** Reason: it keeps holes inside letters (the counter of `0` becomes the counter of `8`'s lower bowl), matches what flubber and MorphSVG users fix by hand, and is cheap for glyph-sized contour counts. Rejected: one combined contour (flubber's approach, which drops holes); optimal assignment over shape descriptors (heavier with no visible gain for letters).
- **Single-contour morphs keep the open polyline.** Reason: existing morphs and their tests (022) must not change, and an open stroke is right for drawn lines.
- **A contour pair's point count is its own.** Each pair samples `max(ceil(max(L_from, L_to) × precision), 2)` points and `MorphPointCount` is their sum. Reason: one count for every pair would oversample small contours (a dot, a counter) by the ratio of the perimeters.
- **Morph and warp frames fill nonzero, with each contour wound by its role.** Outer contours run clockwise and holes counter-clockwise, so a pair is never reversed to save travel, and an unpaired hole collapses into ink rather than to a centroid. Reason: even-odd drew any overlap as a hole, so variable fonts' overlapping contours and contours crossing mid-morph flickered, and a hole collapsing to the centroid of an `O` or `0` sat inside the counter, where hole on hole drew as a sliver of ink; the outline at rest is the font's nonzero fill, so motion now matches rest. Rejected: even-odd with reversal allowed (the earlier rule); collapsing a hole in place, which can still land in a counter.
- **Contour breaks travel in the value's template.** A multi-contour end value is path data with an `M…Z` per contour pair and no fill-rule prefix; the codec starts a figure where the template between two coordinates holds `M` and closes every figure, filled nonzero, when it ends in `Z` (BRIDGE-CODECS-027). Reason: the numbers stay one flat run that the core lerps as it does a single polyline, and each frame still writes one `StreamGeometry`. Rejected: an `F0` prefix, whose `0` the core would parse as a coordinate.
- **Figures are read from Skia's path.** `Geometry` exposes no figures, so the morph reads the `SKPath` that Avalonia.Skia keeps on the geometry's internal platform implementation, and falls back to the single-figure morph when there is none. Reason: the morph must split any `Geometry` (a `StreamGeometry` parsed from text, a chained morph's value), not only the outlines BRIDGE-GLYPHS reads from the font.

## Non-goals

- SVG documents, `pathLength`, polygon and polyline point lists.
- Coordinate conversion between the path's container and the animated control's.

## Open items

None.
