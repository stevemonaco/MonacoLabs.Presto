---
id: CORE-STAGGER
title: Stagger
package: MonacoLabs.Presto
namespaces:
  - MonacoLabs.Presto
sources:
  - MonacoLabs.Presto/Stagger.cs
  - MonacoLabs.Presto/StaggerParams.cs
  - MonacoLabs.Presto/StaggerFrom.cs
  - MonacoLabs.Presto/StaggerAxis.cs
  - MonacoLabs.Presto/Presto.cs
types:
  - Stagger
  - StaggerParams
  - StaggerFrom
  - StaggerAxis
tests:
  - StaggerTests
  - FunctionBasedValuesTests
upstream:
  - src/utils/stagger.js
  - tests/suites/stagger.test.js
depends:
  - CORE-ANIMATION
  - CORE-TIMELINE
  - CORE-EASES
  - CORE-UTILS
  - CORE-TARGETS
---

# Stagger

## Purpose

A `Stagger` gives each target its own value from the target's distance to an origin: along the target list, across a grid, or across positions measured from the targets. It is accepted wherever a per-target value is (a delay, a duration, a property value, a keyframe setting) and as the position of timeline children, where it adds one child per target (CORE-TIMELINE). Initially ported from anime.js `stagger()`. Also specifies `Presto.Stagger`.

## Requirements

### Values

- **CORE-STAGGER-001** — For a number `n`, the stagger shall give each target `start + n × d`, `d` being the target's distance from the origin rounded to 2 decimals.
  - Tests: `StaggerTests.StaggersDelaysByAFixedStepPerTarget`, `StaggerTests.StaggersAcrossA2DGrid`
- **CORE-STAGGER-002** — When the value carries a unit, the stagger shall return the spaced number formatted with that unit.
  - Tests: `StaggerTests.StaggersValuesWithAUnitString`
- **CORE-STAGGER-003** — For a `[from, to]` range, the stagger shall spread the values from `from` to `to` in proportion to each distance over the largest distance, taking the unit of `to`.
  - Tests: `StaggerTests.DistributesValuesEvenlyAcrossANumericRange`, `StaggerTests.DistributesValuesAcrossAUnitRange`
- **CORE-STAGGER-004** — The stagger shall be accepted as a delay, a duration, a property value or a keyframe setting, resolved per target with the target's index in its group.
  - Tests: `StaggerTests.StaggersDelaysByAFixedStepPerTarget`, `StaggerTests.DistributesValuesEvenlyAcrossANumericRange`, `FunctionBasedValuesTests.FunctionBasedKeyframesValues`
- **CORE-STAGGER-005** — When `Start` is a non-zero number, every value shall be offset by it; otherwise the offset shall be 0, or `from` for a range.
  - Tests: `StaggerTests.OffsetsStaggerDelaysByTheStartOption`
- **CORE-STAGGER-006** — When a staggered delay has a start offset, the animation shall move the common offset into its own delay and keep the spacing on the tweens.
  - Tests: `StaggerTests.OffsetsStaggerDelaysByTheStartOption`
- **CORE-STAGGER-007** — A staggered delay on zero-duration tweens shall still write every target's end value.
  - Tests: `StaggerTests.AppliesStaggerToZeroDurationAnimations`

### Origin and order

- **CORE-STAGGER-008** — The distances shall be measured from the first target by default, from index `(total − 1) / 2` for `Center`, from the last for `Last`, and from the given index for an `int`.
  - Tests: `StaggerTests.StaggersDelaysByAFixedStepPerTarget`, `StaggerTests.StaggersFromTheCenter`, `StaggerTests.StaggersFromTheLastElement`, `StaggerTests.StaggersFromASpecificIndex`
- **CORE-STAGGER-009** — When `From` is `Random`, the computed values shall be shuffled across the targets with the stagger's random generator.
  - Tests: untested
- **CORE-STAGGER-010** — When `Reversed` is true and no `Axis` is set, each value shall become the largest value minus itself.
  - Tests: `StaggerTests.ReversesTheStaggerOrder`
- **CORE-STAGGER-011** — When `Ease` is set, each distance shall be mapped through it as `ease(d / max) × max`, before any reversal; a `Spring` contributes its ease curve.
  - Tests: `StaggerTests.AppliesAnEaseFunctionToStaggerValues`, `StaggerTests.ReversesEasedStaggerValues`
- **CORE-STAGGER-012** — When `Use` is set, the stagger shall place each target at the index it returns (truncated), and `UseProperty` shall read that index from the named target property.
  - Tests: `StaggerTests.ReadsTheStaggerOrderFromATargetProperty`
- **CORE-STAGGER-013** — When the custom index falls outside `[0, total)`, the stagger shall use the target's natural index.
  - Tests: `StaggerTests.HandlesUseReturningOutOfRangeIndex`
- **CORE-STAGGER-014** — When `Total` is set, the values shall be spaced over that many positions instead of the target count.
  - Tests: `StaggerTests.TotalSpacesARangeOverMoreTargetsThanAreAnimated`
- **CORE-STAGGER-015** (inherited) — When `Total` is below the target count, the targets past it shall get the start value.
  - Tests: `StaggerTests.TotalBelowTheTargetCountLeavesTheRemainingTargetsAtTheStartValue`

### Grids

- **CORE-STAGGER-016** — When `Grid` is `[columns, rows]`, the distance shall be the Euclidean distance between cells, target `i` sitting at column `i % columns` and row `floor(i / columns) % rows`.
  - Tests: `StaggerTests.StaggersAcrossA2DGrid`
- **CORE-STAGGER-017** — When `Grid` has three entries, the distance shall include the layer `floor(i / (columns × rows))`.
  - Tests: `StaggerTests.StaggersAcrossA3DGrid`
- **CORE-STAGGER-018** — On a grid, `Center` shall be the grid's center, an index shall be that cell, and `StaggerFrom.At(x, y, z)` shall be the normalized position in the grid, `z` defaulting to 0.5 on a 3D grid.
  - Tests: `StaggerTests.StaggersFromAnXYOriginInAGrid`, `StaggerTests.StaggersFromAnXYZOriginInA3DGrid`
- **CORE-STAGGER-019** — When `Axis` is set, the value shall be the signed offset from the origin along that axis, positive to the right, below or behind, instead of the distance.
  - Tests: `StaggerTests.StaggersAcrossA2DGridAlongAnAxis`, `StaggerTests.ComputesAutoGridAlongTheZAxis`
- **CORE-STAGGER-020** — When `Axis` is set and `Reversed` is true, each value shall be negated.
  - Tests: `StaggerTests.MirrorsStaggerOrderWhenReversedAlongAnAxis`, `StaggerTests.MirrorsStaggerOrderWhenReversedAlongAnAxisFromLast`

### Auto grid

- **CORE-STAGGER-021** — When `AutoGrid` is true, the stagger shall measure each target's position from `Coordinates`, or, when it is unset or returns fewer than two values, from the target's `X`, `Y` and `Z` properties.
  - Tests: `StaggerTests.ComputesAutoGridFromObjectCoordinates`, `StaggerTests.ComputesAutoGridFromMeasuredCoordinates`, `StaggerTests.ReadsTheXYPropertiesWhenMeasuringCoordinatesReturnsNull`
- **CORE-STAGGER-022** — The auto-grid distances shall be scaled so the closest distinct pair of positions is 1 apart, matching an explicit grid's spacing.
  - Tests: `StaggerTests.ComputesAutoGridFromObjectCoordinates`
- **CORE-STAGGER-023** — When any target yields a third coordinate, the auto grid shall measure in 3D, other targets taking a depth of 0.
  - Tests: `StaggerTests.SwitchesAutoGridTo3DWhenTargetsExposeZ`
- **CORE-STAGGER-024** — On an auto grid, `At` shall be the normalized position in the targets' bounding box, `Center` its center, `Last` and an index those targets' positions, and the default the first target's position.
  - Tests: `StaggerTests.ComputesAutoGridFromAnXYOrigin`, `StaggerTests.ComputesAutoGridFromObjectCoordinates`
- **CORE-STAGGER-025** — When `Axis` is set on an auto grid, the value shall be the signed scaled offset along that axis.
  - Tests: `StaggerTests.ComputesAutoGridAlongASingleAxis`
- **CORE-STAGGER-026** — When any target has no position, or `Total` exceeds the target count, the auto grid shall fall back to distances along the target list.
  - Tests: `StaggerTests.FallsBackTo1DAutoGridWithoutSpatialData`
- **CORE-STAGGER-027** — When reading a coordinate property the target does not have, the stagger shall treat it as missing instead of throwing.
  - Tests: `StaggerTests.FallsBackTo1DAutoGridWithoutSpatialData`

### Jitter, seed, modifier

- **CORE-STAGGER-028** — When `Jitter` is set, each value shall gain a random sample in `[-1, 1]` (4 decimals) times a magnitude that ramps from `Jitter[0]` to `Jitter[1]` (or stays `Jitter[0]`) by the value's distance over the largest distance.
  - Tests: `StaggerTests.RampsJitterMagnitudeAcrossOrder`
- **CORE-STAGGER-029** — When `Seed` is set, the jitter and random order shall come from a generator seeded with it, so equal seeds give equal values; otherwise from the shared generator.
  - Tests: `StaggerTests.ReproducesJitterDelaysForAGivenSeed`
- **CORE-STAGGER-030** — When `Modifier` is set, it shall map every value after the jitter and before the unit is appended.
  - Tests: `StaggerTests.ModifierPostProcessesEveryStaggeredValue`

### Caching and timelines

- **CORE-STAGGER-031** — The stagger shall compute its distances on first use and reuse them for every target of a group of the same size, recomputing them when a group of another size uses it.
  - Tests: `StaggerTests.AStaggerReusedOnALargerGroupStaggersEveryTarget`
- **CORE-STAGGER-032** — The stagger shall resolve its start offset on each use: once per group added to a timeline, against that timeline as it is when the group is added, and on every call outside a timeline (see "A stagger's start offset is computed per use").
  - Tests: `StaggerTests.AStaggerReusedAsAPositionOnAnotherTimelineUsesThatTimelinesEnd`, `StaggerTests.AStaggerReusedAfterItsTimelineGrowsUsesTheNewEnd`
- **CORE-STAGGER-033** — As a timeline position, the stagger's offset shall be `Start`, as a number or a label, or else the timeline's end when the group is added.
  - Tests: `StaggerTests.StaggersTimelinePositions`, `StaggerTests.StaggersTimelinePositionsWithANumericStart`, `StaggerTests.StaggersTimelinePositionsStartingAtALabel`
- **CORE-STAGGER-034** — A stagger used as a property value inside a staggered timeline group shall resolve against the whole group.
  - Tests: `StaggerTests.StaggersATweenValueInATimeline`

### Defaults

- **CORE-STAGGER-035** — When `Total` is not set, the values shall be spaced over the target count.
  - Tests: `StaggerTests.StaggersDelaysByAFixedStepPerTarget`
- **CORE-STAGGER-036** — When neither `Grid` nor `AutoGrid` is set, the distances shall be measured along the target list.
  - Tests: `StaggerTests.StaggersDelaysByAFixedStepPerTarget`
- **CORE-STAGGER-037** — When `Axis` is not set, the value shall be the distance from the origin rather than a signed offset.
  - Tests: `StaggerTests.StaggersAcrossA2DGrid`
- **CORE-STAGGER-038** — When `Ease` is not set, the distances shall be used unmapped.
  - Tests: `StaggerTests.StaggersDelaysByAFixedStepPerTarget`

## Invariants

- For a fixed group size and parameters, a target's value does not change between calls, except with an unseeded `Random` or `Jitter`, which are drawn once when the distances are computed.
- Without a `Use`, `Total`, `Jitter` or `Modifier`, the first target from the origin gets exactly the offset.

## Edge cases

- A single target with `Ease` divides 0 by 0; its value is the offset.
- A `Start` of 0 or an empty string counts as unset, so a range then starts at `from`.
- A label `Start` outside a timeline is not a number, so the values become NaN.
- One stagger shared by groups of the same size but different targets keeps the first group's measured auto-grid distances.
- An index `From` past the end of the targets on an auto grid throws `IndexOutOfRangeException`.

## Threading and lifetime

- A stagger is mutable cache state with no engine of its own; it runs on whichever engine thread builds the animation that uses it. Sharing one across engines on different threads is unsupported.

## Reduced motion

- Not affected: a stagger only computes values. Under `ReduceAll`, staggered delays are dropped with every other delay (CORE-ANIMATION); a `WhenReduced` form keeps the stagger it does not replace.

## Allocation

- Not on the tick path. The first call allocates the distance cache (and jitter samples); later calls allocate only a unit string when the value has a unit.

## Provenance

- A stagger met by a group of a different size recomputes its distances; upstream keeps the first count and reads past its cache, which gave every target past the first group's size a zero delay (see "A stagger rebuilds when the target count changes").
- The start offset is resolved per use; upstream caches the first one it resolves, so a reused stagger kept the first timeline's offset (see "A stagger's start offset is computed per use").
- `Coordinates` lets the host measure positions; upstream reads `getBoundingClientRect` or `x`/`y`/`z`. The core cannot measure layout, so the bridge measures visuals through it (see "Auto grids measure through a coordinates provider").
- `Use` is a delegate and `UseProperty` a property name, where upstream takes either in one `use` option, because a typed parameter cannot hold both; a fractional `Use` index is truncated.
- `Seed` is an `int`; upstream also accepts `true` for seed 0, which a typed `int` parameter cannot express.
- `From.At` replaces upstream's `[x, y(, z)]` array, because `From` is a typed value rather than a JS union.

## Decisions

- **A stagger rebuilds when the target count changes.** A stagger recomputes its cache when a group of another size uses it, instead of sizing it once by the first group, which upstream's closure does. Reason: a builder's shared stagger read past its cache once a batch grew and gave every later element a zero delay.
- **A stagger's start offset is computed per use.** Each timeline group resolves it once against its own timeline before placing its children, and a non-timeline use reads `Start` on every call; upstream caches the offset on first use. Reason: the cache made reuse depend on first use, so a stagger reused on a second timeline, or after the first grew, kept the first offset. Resolving it per group, not per child, keeps a relative `Start` such as `"<"` from cascading within the group.
- **Unset options leave an even spacing by list position.** Without `Total`, `Grid`, `AutoGrid`, `Axis` or `Ease`, values step evenly by each target's distance from the origin along the list (CORE-STAGGER-035 to 038). Reason: `Presto.Stagger(50)` then spaces a group 50 apart, and each option adds one shape to that.
- **Auto grids measure through a coordinates provider.** The core exposes `StaggerParams.Coordinates` as a provider, and the bridge fills it by measuring each visual. Reason: the core cannot measure layout.
- **Function values receive the group's target list.** See CORE-ANIMATION.
- **`FunctionValue` takes three arguments.** See CORE-VALUES.

## Non-goals

- No staggering of anything other than numbers; colors are staggered through function values.
- No layout measuring in the core.

## Open items

- `From = Random` and a fractional `Use` index have no test.
