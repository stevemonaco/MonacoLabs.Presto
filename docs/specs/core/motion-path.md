---
id: CORE-MOTIONPATH
title: Motion path through points
package: Monaco.Presto
namespaces:
  - Monaco.Presto
sources:
  - Monaco.Presto/MotionPath.cs
types:
  - MotionPath
tests:
  - MotionPathTests
  - ShapesTests
upstream: []
depends:
  - CORE-ANIMATION
  - CORE-VALUES
---

# Motion path through points

## Purpose

`MotionPath` is a spline through a list of points (a uniform Catmull-Rom curve with GSAP MotionPath's `curviness`), sampled at constant speed along its arc length. Its `Tweens` give the `"X"`, `"Y"` and `"Rotate"` tween parameters that make an animation ride the path, as anime.js `svg.createMotionPath` does for an SVG path. It builds on CORE-ANIMATION's tween modifiers; the bridge's shape-based motion path and its point overload build on it (see BRIDGE-SHAPES).

## Requirements

### Construction

- **CORE-MOTIONPATH-001** — `Through` shall build one cubic Bézier segment per pair of consecutive points, plus one from the last point back to the first when `closed` is true.
  - Tests: `MotionPathTests.TheCurvePassesThroughEveryPoint`
- **CORE-MOTIONPATH-002** — The curve shall pass through every point: segment `i` starts at point `i` and ends at point `i + 1`.
  - Tests: `MotionPathTests.TheCurvePassesThroughEveryPoint`
- **CORE-MOTIONPATH-003** — Each segment's control points shall be `p1 + (p2 − p0) · curviness / 6` and `p2 − (p3 − p1) · curviness / 6`, the uniform Catmull-Rom tangents scaled by `curviness`.
  - Tests: `MotionPathTests.TheCurvePassesThroughEveryPoint`, `MotionPathTests.CurvinessZeroIsThePolyline`
- **CORE-MOTIONPATH-004** — When `curviness` is 0, the path shall be the straight polyline through the points.
  - Tests: `MotionPathTests.CurvinessZeroIsThePolyline`
- **CORE-MOTIONPATH-005** — An open path shall use its first and last points as their own missing neighbors.
  - Tests: untested
- **CORE-MOTIONPATH-006** — A closed path shall wrap its neighbors, so its end meets its start with the same tangent.
  - Tests: `MotionPathTests.AClosedPathsEndTangentMeetsItsStart`
- **CORE-MOTIONPATH-007** (inherited) — When fewer than two points are given, `Through` shall throw `ArgumentException`.
  - Tests: untested
- **CORE-MOTIONPATH-008** — `BezierPoints` shall list the start point, then each segment's two control points and end point.
  - Tests: `MotionPathTests.TheCurvePassesThroughEveryPoint`

### Sampling

- **CORE-MOTIONPATH-009** — `Length` shall be the path's arc length, measured from 64 chords per segment.
  - Tests: `MotionPathTests.CurvinessZeroIsThePolyline`
- **CORE-MOTIONPATH-010** — `Sample(distance)` shall return the point that distance along the path, so equal distance steps give equal chords.
  - Tests: `MotionPathTests.EqualDistanceStepsGiveEqualChords`, `MotionPathTests.CurvinessZeroIsThePolyline`
- **CORE-MOTIONPATH-011** (inherited) — `Sample` shall clamp `distance` to [0, `Length`].
  - Tests: `MotionPathTests.ZeroOffsetClampsAndAnyOtherWraps`
- **CORE-MOTIONPATH-012** — `Sample` shall return the tangent's angle in degrees, clockwise from the positive X axis when Y points down, taken from the curve's derivative.
  - Tests: `MotionPathTests.AHorizontalSegmentsTangentIsZeroAndAutoRotateAddsToIt`
- **CORE-MOTIONPATH-013** — Where the derivative vanishes (a zero-length control arm at a segment end, as with `curviness` 0), `Sample` shall take the angle from the segment's chord.
  - Tests: untested

### Tweens

- **CORE-MOTIONPATH-014** — `Tweens` shall return `"X"`, `"Y"` and `"Rotate"` tween parameters that each run from 0 to `Length` and map that progress through a modifier to the path's x, y and tangent angle.
  - Tests: `MotionPathTests.AnAnimationThroughTheTweensLandsOnTheLastPoint`
- **CORE-MOTIONPATH-015** — When `offset` is 0, the tweens shall clamp the progress at the two ends of the path.
  - Tests: `MotionPathTests.ZeroOffsetClampsAndAnyOtherWraps`
- **CORE-MOTIONPATH-016** — When `offset` is not 0, the tweens shall start `offset × Length` along the path and wrap around it.
  - Tests: `MotionPathTests.ZeroOffsetClampsAndAnyOtherWraps`
- **CORE-MOTIONPATH-017** — The `"Rotate"` tween shall add `rotateOffset` degrees to the tangent angle, and is always returned (see Decisions: rotation is degrees, not a switch).
  - Tests: `MotionPathTests.AHorizontalSegmentsTangentIsZeroAndAutoRotateAddsToIt`
- **CORE-MOTIONPATH-018** — An animation over the three tweens shall land on the last point.
  - Tests: `MotionPathTests.AnAnimationThroughTheTweensLandsOnTheLastPoint`, `ShapesTests.PointMotionPathDrivesTheShorthandsThroughThePoints`

### Defaults

- **CORE-MOTIONPATH-019** — When `curviness` is not given, `Through` shall use 1, the standard Catmull-Rom curve, as GSAP MotionPath does.
  - Tests: untested
- **CORE-MOTIONPATH-020** (inherited) — When `closed` is not given, `Through` shall build an open path.
  - Tests: `MotionPathTests.ZeroOffsetClampsAndAnyOtherWraps`
- **CORE-MOTIONPATH-021** (inherited) — When `offset` is not given, the tweens shall start at the path's first point and clamp at its ends.
  - Tests: `MotionPathTests.ZeroOffsetClampsAndAnyOtherWraps`
- **CORE-MOTIONPATH-022** (inherited) — When `rotateOffset` is not given, the `"Rotate"` tween shall be the tangent angle unchanged.
  - Tests: untested

## Invariants

- `BezierPoints[3·i]` is the input point `i` for every input point.
- `Length` and `BezierPoints` never change after `Through`.
- `Sample` is a pure function of `distance`.

## Edge cases

- Two points give one straight segment whatever the curviness.
- Repeated consecutive points give a zero-length segment; sampling there returns the point, with an angle of 0 when the chord is zero too.
- Negative or very large `curviness` is not validated; large values loosen the curve and may loop.
- A wrapped tween at progress `Length` is back at its start offset.

## Threading and lifetime

- Immutable after construction, so a path may be shared between animations; it holds no engine reference.

## Reduced motion

- Not affected directly: the tweens are ordinary tweens and follow the animation's reduced form (`ReduceAll` lands on the end; see CORE-REDUCED).

## Allocation

- `Sample` and the modifiers `Tweens` returns allocate nothing per call. `Through` and `Tweens` allocate once.

## Deviations from anime.js

- No upstream counterpart: anime.js 4.5.0 motion paths follow an SVG path, not a list of points. The spline follows GSAP MotionPath's `curviness`, and `rotateOffset` is a number of degrees (GSAP `autoRotate: 90`), not a switch, to match GSAP (see Decisions).

## Decisions

- **The path is a uniform Catmull-Rom spline sampled at constant speed.** `curviness` scales the tangents: 0 is the polyline and 1, the default, the standard curve. Reason: curviness 1 is GSAP MotionPath's default.
- **Rotation is degrees, not a switch.** `rotateOffset`/`autoRotate` is a number of degrees added to the tangent angle, and the `Rotate` tween is always returned. Reason: degrees match GSAP's `autoRotate: 90`; a caller that wants no rotation leaves the tween out. Rejected: `autoRotate` as an on/off switch.

## Non-goals

- No non-uniform (centripetal or chordal) Catmull-Rom parameterization.
- No 3D paths.
- No way to omit the rotate tween; a caller that does not want rotation leaves it out.

## Open items

- The `ArgumentException` for fewer than two points, open-path end neighbors and the chord fallback for the angle are untested.
