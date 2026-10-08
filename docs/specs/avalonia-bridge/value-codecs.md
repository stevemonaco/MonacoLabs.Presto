---
id: BRIDGE-CODECS
title: Avalonia value codecs
package: Monaco.Presto.AvaloniaBridge
namespaces:
  - Monaco.Presto.AvaloniaBridge
sources:
  - Monaco.Presto.AvaloniaBridge/Values
  - Monaco.Presto.AvaloniaBridge/ColorExtensions.cs
  - Monaco.Presto.AvaloniaBridge/PrestoAvalonia.cs
types:
  - ColorExtensions
tests:
  - CodecTests
  - TargetAdapterTests
  - TargetOwnerTests
  - AllocationTests
upstream:
  - src/core/colors.js
  - src/core/values.js
  - tests/suites/colors.test.js
depends:
  - CORE-VALUES
  - CORE-COLORS
  - CORE-TARGETS
  - BRIDGE-INIT
  - BRIDGE-TARGETS
  - BRIDGE-ACCORDION
---

# Avalonia value codecs

## Purpose

The codecs (CORE-TARGETS `IValueCodec`, `IReconcilingValueCodec`) that turn Avalonia value types into the channels a tween interpolates and back. `EnsureInitialized` registers them in `Presto.Codecs` by type (BRIDGE-INIT-004), and the property accessor picks one by the property's exact type (BRIDGE-TARGETS-018). `double`, `float`, `int` and `string` properties need no codec; the accessor writes them directly (BRIDGE-TARGETS-017). Value parsing and interpolation themselves are the core's (CORE-VALUES, CORE-COLORS). The pixel `GridLength` codec is specified with the pane that introduced it (BRIDGE-ACCORDION).

## Requirements

### Color

- **BRIDGE-CODECS-001** — A `Color` shall decode into the core's color channels: R, G, B as 0–255 numbers and A as 0–1.
  - Tests: `CodecTests.ColorTweensFromAHexStringAndFromATypedColor`
- **BRIDGE-CODECS-002** — A `Color` property shall accept a typed `Color` or any color string the core parses as its end value.
  - Tests: `CodecTests.ColorTweensFromAHexStringAndFromATypedColor`
- **BRIDGE-CODECS-003** — Each frame of a `Color` tween shall be written as the core's interpolated channels (premultiplied alpha, CORE-COLORS) converted by `ToAvalonia`.
  - Tests: `CodecTests.ColorTweensFromAHexStringAndFromATypedColor`
- **BRIDGE-CODECS-004** (inherited) — `ToAvalonia` shall round R, G and B and `A × 255` to the nearest integer and clamp each to 0–255; `ToPresto` shall widen the bytes and divide the alpha by 255.
  - Tests: `CodecTests.ColorsConvertBetweenPrestoAndAvaloniaChannels`

### Brushes

- **BRIDGE-CODECS-005** — An `ISolidColorBrush` shall decode as its color alone, so a tween between two solid brushes is a color tween.
  - Tests: `CodecTests.SolidColorBrushTweenWritesANewBrushAndLeavesTheOriginalUntouched`
- **BRIDGE-CODECS-006** — A string end value for a brush property shall be parsed by the core as a color and written as a solid brush.
  - Tests: `TargetAdapterTests.PropertiesWithACodecAnimateThroughIt`
- **BRIDGE-CODECS-007** — A solid brush tween shall create one `SolidColorBrush` on its first frame, write that same brush on every later frame with its color changed, and never modify the brush it started from.
  - Tests: `CodecTests.SolidColorBrushTweenWritesANewBrushAndLeavesTheOriginalUntouched`
- **BRIDGE-CODECS-008** (inherited) — The solid brush a color tween writes shall have `Opacity` 1, whatever the opacity of the brush it started from.
  - Tests: untested
- **BRIDGE-CODECS-009** — A linear, radial or conic gradient shall decode into its kind, spread method, point units, opacity, geometry (linear start and end points; radial center, origin and both radii; conic center and angle) and its stops sorted by offset as `(offset, r, g, b, a)`.
  - Tests: `CodecTests.LinearGradientInterpolatesItsStopsAndPoints`, `CodecTests.RadialAndConicGradientsInterpolateTheirGeometry`
- **BRIDGE-CODECS-010** — A gradient tween shall interpolate the opacity, geometry, stop offsets and stop colors, and take the kind, spread method and units from the end value.
  - Tests: `CodecTests.LinearGradientInterpolatesItsStopsAndPoints`, `CodecTests.RadialAndConicGradientsInterpolateTheirGeometry`
- **BRIDGE-CODECS-011** — When the two ends are different kinds of gradient, the start shall take the end's geometry, so only the stops interpolate.
  - Tests: `CodecTests.AGradientTweenedToAnotherKindTakesTheEndGeometryAndInterpolatesItsStops`
- **BRIDGE-CODECS-012** — When the two gradients' stop offsets differ, both shall be resampled onto the union of their offsets, each new stop colored as its gradient is at that offset: interpolated between its neighbors with premultiplied alpha, or the nearest end stop's color beyond the ends.
  - Tests: `CodecTests.GradientsWithDifferentStopCountsInterpolateOnTheUnionOfTheirOffsets`, `CodecTests.AResampledStopBetweenTransparentAndOpaqueTakesTheOpaqueStopsColor`
- **BRIDGE-CODECS-013** — When a solid brush tweens to a gradient, the solid end shall take the gradient's shape with every stop in its own color.
  - Tests: `CodecTests.ASolidBrushTweensIntoAGradient`
- **BRIDGE-CODECS-014** — When a missing (`null`) brush tweens to a gradient, it shall start as that gradient with every stop at alpha 0.
  - Tests: `CodecTests.AMissingBrushFadesIntoAGradientInItsOwnColors`
- **BRIDGE-CODECS-015** — When a gradient tweens to a color or solid brush, every stop shall ease toward that color in the gradient's shape, and the tween's final frame shall write a `SolidColorBrush` of that color.
  - Tests: `CodecTests.AGradientTweenedToAColorEndsOnASolidBrush`
- **BRIDGE-CODECS-016** — A gradient tween shall create one brush of the kind it draws and reuse it on later frames, growing or shrinking its `GradientStops` in place, and never modify the brush it started from.
  - Tests: `CodecTests.AGradientTweenReusesOneBrushAndLeavesTheOriginalUntouched`
- **BRIDGE-CODECS-017** — Every brush a codec creates shall have its opacity clamped to 0–1 and be recorded as owned by the tween's target (BRIDGE-TARGETS-024).
  - Tests: `TargetOwnerTests.ACodecCreatedBrushResolvesToItsVisual`
- **BRIDGE-CODECS-018** — A brush's `Transform` shall not be decoded or carried to the written brush.
  - Tests: untested

### Box shadows

- **BRIDGE-CODECS-019** — `BoxShadows` shall decode into `(inset, offsetX, offsetY, blur, spread, r, g, b, a)` per shadow, and strings shall parse through `BoxShadows.Parse`, with `"none"` an empty list.
  - Tests: `CodecTests.BoxShadowStringsParseAndInterpolate`
- **BRIDGE-CODECS-020** (inherited) — When `BoxShadows.Parse` rejects a string, the codec shall decline it and leave the string to the core's parsing.
  - Tests: untested
- **BRIDGE-CODECS-021** — When the two ends have different numbers of shadows, the shorter list shall be padded with zero shadows in the other end's color at alpha 0, so a shadow grows or fades in its own color.
  - Tests: `CodecTests.AnAddedShadowGrowsInItsOwnColorFromNothing`, `CodecTests.ABorderWithoutAShadowTweensOneIn`
- **BRIDGE-CODECS-022** — Each shadow's `IsInset` shall come from the end value, and its blur shall be written no lower than 0.
  - Tests: `CodecTests.AShadowTakesItsInsetFromTheEndValue`
- **BRIDGE-CODECS-029** (inherited) — When a `BoxShadows` end is missing (an unset value the core reads as the number 0), it shall count as an empty list and be padded as BRIDGE-CODECS-021 says, so a control without a shadow tweens one in from nothing.
  - Tests: `CodecTests.ABorderWithoutAShadowTweensOneIn`

### Structs and points

- **BRIDGE-CODECS-023** — `Thickness` (left, top, right, bottom), `CornerRadius` (top-left, top-right, bottom-right, bottom-left), `Point`, `Size`, `Rect` and `Vector` shall interpolate every component independently.
  - Tests: `CodecTests.GeometryStructsInterpolateEveryChannel`
- **BRIDGE-CODECS-024** — A single number given for one of those structs shall fill every component.
  - Tests: `CodecTests.ANumberFillsEveryChannelOfAGeometryStruct`
- **BRIDGE-CODECS-025** — A `RelativePoint` shall interpolate its two coordinates and be written in the end value's unit, without converting a start value written in the other unit.
  - Tests: `CodecTests.RelativePointTweensItsCoordinatesAndTakesTheUnitOfItsEndValue`

### Geometry

- **BRIDGE-CODECS-026** — A `Geometry` shall decode into `max(⌈contour length × 0.33⌉, 2)` points spread evenly along its contour, rounded to 3 decimals; a path-data string end value is parsed by the core as its numbers.
  - Tests: untested
- **BRIDGE-CODECS-027** — Each frame of a `Geometry` tween shall be written as a new `StreamGeometry` polyline through the interpolated points, left open.
  - Tests: `ShapesTests.MorphWalksThePointsOntoTheDestinationGeometry`
- **BRIDGE-CODECS-028** (inherited) — When two geometries decode into different point counts outside `MorphTo` (BRIDGE-SHAPES), the core's padding of the shorter value shall apply.
  - Tests: untested

## Invariants

- A codec never modifies the value a tween started from; brushes are copied into a per-tween brush.
- Discrete fields (gradient kind, spread, units, shadow inset) are always read from the end value.

## Edge cases

- A brush whose shape the codec does not know (an image or visual brush) is not decoded; the property then falls to the core's handling of an undecodable value.
- A gradient eased to a solid brush is a gradient on every frame but the last.
- `Size` and `Rect` components are written as interpolated, so an overshooting ease can produce a negative size.
- A `RelativePoint` from a relative start to an absolute end tweens the raw numbers (0.5 → 100 px passes through 50 px).

## Threading and lifetime

- UI thread only. Per-tween brushes live in a `ConditionalWeakTable` keyed by the tween, so they go with it.

## Reduced motion

- Not affected: codecs encode whatever the (possibly reduced) tween computes.

## Allocation

- `Color` and the structs box one value per frame. A brush tween mutates its reused brush; pinned by `AllocationTests.BrushTweenTicksWithinBudget`. `BoxShadows` and `Geometry` allocate a new value every frame.

## Deviations from anime.js

- Upstream interpolates CSS strings and pads mismatched numbers by position; here gradients, box-shadow lists and paths are reconciled by codec (`IReconcilingValueCodec`, CORE-TARGETS), because padding by position is wrong for colors and offsets when the two ends differ in shape.
- Color interpolation uses premultiplied alpha, because upstream's straight alpha shows a transparent color's RGB mid-fade (CORE-COLORS); gradient stop resampling does too, while gradient and shadow color slots lerp as plain channels.

## Decisions

- **A brush tween writes a brush of its own.** A solid or gradient tween creates one brush per tween, reuses it on every frame (a gradient's stops rewritten in place) and never modifies the brush it started from; earlier the solid codec was to animate the brush's own color and copy it only when frozen or shared, replaced because the code always copies. Reason: the brush animated from is often immutable or shared.
- **Gradients are reconciled, not padded.** A solid end takes the gradient's shape in its own color; a missing brush starts as the gradient with every stop at alpha 0; a gradient tweened to a color ends by writing a `SolidColorBrush`; stops are resampled onto the union of both offsets; a kind mismatch snaps the geometry to the end's; a unit mismatch lerps raw numbers in the end's units. Reason: alpha-0 stops fade a missing brush in without a dark fringe, resampling colors each offset as its gradient draws it, geometry does not convert between kinds, ending on a solid brush keeps the property on the brush type it was aimed at, and units follow the `RelativePoint` codec. Rejected: zero-padding stops.
- **No gradient strings and no brush `Transform`.** Strings are not parsed as gradients, so a gradient end value is a brush instance, and a brush's `Transform` is not carried. Reason: Avalonia's `Brush.Parse` reads colors only; the reason for dropping `Transform` is not recorded.
- **Shadows are padded with transparent counterparts.** The shorter `BoxShadows` list gains zero shadows (as CSS pads) in the other end's color at alpha 0; inset is discrete and taken from the end value; strings are claimed through `BoxShadows.Parse`. Reason: a shadow added or dropped fades instead of flashing black.
- **No `Matrix` codec until a property needs one.** Reason: the transform shorthands go through the bridge's own transform composition.
- **Bridge bool properties other than `IsVisible` keep `Midpoint`.** `IsHitTestVisible`, `IsEnabled` and the rest flip at the midpoint; a tween wanting hit-testing off for its whole run sets `Discrete = FalseThroughout`. Reason: not recorded.
- **The `GridLength` codec is pixel-only.** See BRIDGE-ACCORDION.
- **Colors interpolate with premultiplied alpha.** See CORE-COLORS.
- **Codecs can reconcile ends of different shapes.** See CORE-TARGETS.

## Non-goals

- No `Matrix` codec (the transform shorthands cover transforms, BRIDGE-TRANSFORMS).
- No conversion between relative and absolute units.
- No color-space selection per tween (see CORE-COLORS).

## Open items

- BRIDGE-CODECS-008: a solid brush with `Opacity` below 1 loses that opacity when tweened to another solid color. Untested; likely a bug.
- BRIDGE-CODECS-018, -020, -026 and -028 are untested: the geometry tests all go through `MorphTo`, which hands the codec path-data strings rather than `Geometry` objects.
