---
id: CORE-KEYFRAMES
title: Keyframes
package: Monaco.Presto
namespaces:
  - Monaco.Presto
sources:
  - Monaco.Presto/Keyframe.cs
  - Monaco.Presto/Keyframes.cs
  - Monaco.Presto/KeyframeBuilder.cs
  - Monaco.Presto/KeyframesBuilder.cs
  - Monaco.Presto/TweenKeyframesBuilder.cs
  - Monaco.Presto/Core/KeyframeExpander.cs
  - Monaco.Presto/Animation.cs
types:
  - Keyframe
  - Keyframes
  - KeyframeBuilder
  - KeyframesBuilder
  - TweenKeyframesBuilder
tests:
  - KeyframesTests
  - KeyframeExpanderTests
  - ColorKeyframesTests
  - AnimationBuilderTests
upstream:
  - src/animation/animation.js
  - tests/suites/keyframes.test.js
depends:
  - CORE-ANIMATION
  - CORE-BUILDER
  - CORE-COLORS
  - CORE-EASES
  - CORE-SPRING
---

# Keyframes

## Purpose

Keyframes animate a property through a sequence of values instead of straight to one. There are three forms: per-property keyframes (an array of `TweenParams`), animation-level keyframes that are duration-based (`Keyframe[]`), and animation-level keyframes that are percentage-based (a `"N%"` dictionary). The two animation-level forms expand into per-property arrays before CORE-ANIMATION builds the tweens. The builders write the same shapes, and `TweenKeyframesBuilder.Color` expands one color tween into eased segments along a color-space path. Also specifies `AnimationParams.Keyframes`, `AnimationBuilder.Keyframes`, `PropertyBuilder<TSelf>.Property(PropertyKey, Action<TweenKeyframesBuilder>)` and `TweenValue.Keyframes`. The `Keyframe` indexer takes a `PropertyKey` (CORE-TARGETS).

## Requirements

### Per-property keyframes

- **CORE-KEYFRAMES-001** — When a property's value is an array of `TweenParams`, the animation shall build one tween per entry, each starting from the previous one's end and the first from the target's current value unless it has a `From`.
  - Tests: `KeyframesTests.AnArrayOfTwoObjectValuesBecomesKeyframes`, `AnimationBuilderTests.BareKeyframesStartFromTheCurrentValueRatherThanTheFirstFrame`
- **CORE-KEYFRAMES-002** — When a keyframe has no `Duration`, it shall take the animation's duration divided by the number of keyframes, while keyframes with a `Duration` keep theirs.
  - Tests: `KeyframesTests.UnspecifiedKeyframeDurationIsTheInstanceDurationDividedByTheKeyframeCount`, `KeyframesTests.MixedUnspecifiedKeyframeDurationsKeepTheSpecifiedOnes`
- **CORE-KEYFRAMES-003** — When there is a single keyframe, it shall take the animation's whole duration.
  - Tests: `KeyframesTests.ASingleKeyframeInheritsTheWholeInstanceDuration`
- **CORE-KEYFRAMES-004** — A keyframe's `Delay` shall wait after the previous keyframe ends; the animation's `Delay` shall apply to the first keyframe only and move into the animation's own delay.
  - Tests: `TweensTests.KeyframesTweenTimings`, `KeyframesTests.TheFirstKeyframeDelayIsTransferredToTheAnimationDelay`, `AnimationsTests.TweensTimingInheritance`
- **CORE-KEYFRAMES-005** — Each keyframe shall inherit the animation's ease, modifier and composition unless it sets its own.
  - Tests: `KeyframesTests.GeneralKeyframesInheritTheInstanceParameters`
- **CORE-KEYFRAMES-006** — When a keyframe's value has no unit, it shall take the unit of the keyframe before it.
  - Tests: `KeyframesTests.ArrayKeyframesInheritUnitsFromTheKeyframeBeforeThem`
- **CORE-KEYFRAMES-007** — When a keyframe has a relative from or to value, it shall resolve against the previous keyframe's end value.
  - Tests: `ValuesTests.RelativeValuesInsideKeyframes`
- **CORE-KEYFRAMES-008** — When a keyframe's values, duration, delay or ease are function-based or staggered, each target shall resolve its own.
  - Tests: `FunctionBasedValuesTests.FunctionBasedKeyframesValues`
- **CORE-KEYFRAMES-009** — When the playhead is seeked to any time, forwards or backwards, each keyframed property shall hold the value of the keyframe active at that time.
  - Tests: `KeyframesTests.ArrayBasedKeyframesWithFloatingPointDurations`, `KeyframesTests.BackwardSeekUpdatesASingleObjectFormKeyframeAlongsideAMultiKeyframeProperty`, `DiscreteValuesTests.KeyframesSwitchHalfwayThroughEachSegment`

### Duration-based animation keyframes

- **CORE-KEYFRAMES-010** — When `Keyframes` is a `Keyframe[]`, the animation shall expand it into one keyframe array per property named by any frame, in order of first appearance, with one entry per frame carrying that frame's `Duration`, `Delay`, `Ease`, `Modifier` and `Composition`.
  - Tests: `KeyframesTests.ArrayKeyframesInheritTheInstanceParameters`, `KeyframeExpanderTests.ParametersPropertiesFollowGeneratedOnesUnlessAlreadyGenerated`
- **CORE-KEYFRAMES-011** — When a frame does not name a property, that property shall hold its previous value for the frame's duration.
  - Tests: `KeyframesTests.ArrayKeyframesInheritTheInstanceParameters`, `KeyframesTests.ArrayKeyframesWithPlaybackEase`
- **CORE-KEYFRAMES-012** — Frames without a `Duration` shall split the animation's duration evenly over the frame count, for every property alike.
  - Tests: `KeyframesTests.ArrayKeyframesInheritTheInstanceParameters`
- **CORE-KEYFRAMES-013** — When the parameters also name properties through the indexer, those the keyframes did not produce shall follow the generated ones, and those they did produce shall be ignored.
  - Tests: `KeyframeExpanderTests.ParametersPropertiesFollowGeneratedOnesUnlessAlreadyGenerated`
- **CORE-KEYFRAMES-014** — When `PlaybackEase` is set, the keyframes' tweens shall default to linear while a frame's own ease is kept.
  - Tests: `KeyframesTests.ArrayKeyframesWithPlaybackEase`

### Percentage-based animation keyframes

- **CORE-KEYFRAMES-015** — When `Keyframes` is a dictionary, the animation shall parse each `"N%"` key with the invariant culture, sort the frames by offset, and end each property's segment at offset times the total duration.
  - Tests: `KeyframesTests.PercentageBasedKeyframesValues`, `KeyframesTests.PercentageBasedKeyframesWithFloatPercentageValues`, `KeyframeExpanderTests.PercentageKeyframesSortByOffsetAndShiftEasesToTheFollowingSegment`
- **CORE-KEYFRAMES-016** — The total duration shall be the parameters' `Duration` when it is a plain number, and otherwise the engine defaults' duration, not the parent timeline's.
  - Tests: `KeyframesTests.PercentageKeyframesSpanTheEngineDefaultDurationAndNotTheParentTimelines`
- **CORE-KEYFRAMES-017** — A frame's `Ease` shall apply to the segment that starts at that frame.
  - Tests: `KeyframeExpanderTests.PercentageKeyframesSortByOffsetAndShiftEasesToTheFollowingSegment`
- **CORE-KEYFRAMES-018** — When a property's first frame is at 0%, its value shall become the from value of the next segment rather than a segment of its own.
  - Tests: `KeyframesTests.PercentageBasedKeyframesValues`, `KeyframeExpanderTests.PercentageKeyframesSortByOffsetAndShiftEasesToTheFollowingSegment`
- **CORE-KEYFRAMES-019** — When a property's first frame is after 0%, its first segment shall run from the target's current value from 0 to that offset.
  - Tests: untested
- **CORE-KEYFRAMES-020** — A percentage frame's `Duration`, `Delay`, `Modifier` and `Composition` shall be ignored.
  - Tests: untested
- **CORE-KEYFRAMES-021** (inherited) — When a key does not parse as a number followed by an optional `%`, the animation constructor shall throw `FormatException`.
  - Tests: untested

### Builders

- **CORE-KEYFRAMES-022** — `KeyframesBuilder.Frame` shall add the next duration-based frame, and `At` shall place a frame at that percentage, replacing any frame already there; each `KeyframeBuilder` sets its frame's properties and settings.
  - Tests: `AnimationBuilderTests.DurationBasedKeyframesMatchTheIndexer`, `AnimationBuilderTests.PercentageBasedKeyframesMatchTheIndexerAndTheLastAtAndKeyframesCallWin`, `AnimationBuilderTests.KeyframedBuilderAndIndexerAnimationsReadTheSameValues`
- **CORE-KEYFRAMES-023** — When `Frame` and `At` are mixed on one builder, the second call shall throw `InvalidOperationException`.
  - Tests: `AnimationBuilderTests.MixingFrameAndAtThrows`
- **CORE-KEYFRAMES-024** — When `At` is given a percentage outside 0 to 100, it shall throw `ArgumentOutOfRangeException`.
  - Tests: `AnimationBuilderTests.AtOutsideZeroToHundredThrows`
- **CORE-KEYFRAMES-025** — `AnimationBuilder.Keyframes` shall replace any keyframes set before, and each `ToParams` call shall build fresh `Keyframe` objects.
  - Tests: `AnimationBuilderTests.PercentageBasedKeyframesMatchTheIndexerAndTheLastAtAndKeyframesCallWin`, `AnimationBuilderTests.ToParamsBuildsFreshKeyframesEachCall`
- **CORE-KEYFRAMES-026** — `TweenKeyframesBuilder.To` shall add a `TweenParams` frame, never a bare value, so the first frame is a to value and not a from value.
  - Tests: `AnimationBuilderTests.PerPropertyKeyframesMatchTheIndexer`, `AnimationBuilderTests.BareKeyframesStartFromTheCurrentValueRatherThanTheFirstFrame`

### Color segments

- **CORE-KEYFRAMES-027** — `Color(from, to, space, hue)` shall add frames whose ends lie on `PrestoColor.Mix(from, to, ease(t), space, hue)` and whose eases are slices of the one ease, so the rendered color stays near that path with the ease's exact timing.
  - Tests: `ColorKeyframesTests.TheRenderedColorStaysNearThePath`
- **CORE-KEYFRAMES-028** — When both ends, the duration and the ease are fixed and `segments` is unset, the builder shall halve the worst segment until every segment is within deltaEOK 0.02 of the path, up to 16 segments.
  - Tests: `ColorKeyframesTests.FittedSegmentsStayFewOnEasyPathsAndCappedOnHardOnes`
- **CORE-KEYFRAMES-029** — The segment durations shall add up exactly to the duration.
  - Tests: `ColorKeyframesTests.FittedSegmentsSplitTheDurationExactly`
- **CORE-KEYFRAMES-030** — When `segments` is given, the builder shall add exactly that many segments, evenly spaced in time.
  - Tests: `ColorKeyframesTests.AnExplicitSegmentCountIsKept`
- **CORE-KEYFRAMES-031** — The last frame shall end on `to` exactly, also under `ReduceAll`.
  - Tests: `ColorKeyframesTests.TheTweenCompletesExactlyOnTo`
- **CORE-KEYFRAMES-032** — When the ease does not move across a segment, that segment shall hold its start color until its end.
  - Tests: `ColorKeyframesTests.StepsHoldBetweenSteps`
- **CORE-KEYFRAMES-033** — When the space is `Oklch` with `Increasing` or `Decreasing` hue and both ends are the same chromatic color, the tween shall circle the hue once.
  - Tests: `ColorKeyframesTests.AnOklchFullTurnCirclesTheHue`
- **CORE-KEYFRAMES-034** — When an end, the duration or the ease is a `FunctionValue`, the builder shall add `segments`, or else 4 segments, or in `Oklch` one per 30° of hue arc clamped to 4 to 24, assuming a full 360° arc when an end is per-target.
  - Tests: `ColorKeyframesTests.PerTargetSegmentCountsFollowTheHueArc`
- **CORE-KEYFRAMES-035** — When ends are per-target, each target shall resolve them once and reuse them for every segment.
  - Tests: `ColorKeyframesTests.FunctionValuedEndsResolvePerTargetUnderAStagger`
- **CORE-KEYFRAMES-036** — Only the first frame shall carry `from` and `delay`, so the animation's delay, staggered or not, still applies when `delay` is unset.
  - Tests: `ColorKeyframesTests.FunctionValuedEndsResolvePerTargetUnderAStagger`
- **CORE-KEYFRAMES-037** — When `duration` or `ease` is unset, the builder shall use `Engine.Current`'s defaults at the time of the call; a `Spring` ease shall set the duration to its settling duration.
  - Tests: `ColorKeyframesTests.TheRenderedColorStaysNearThePath`
- **CORE-KEYFRAMES-038** (inherited) — When a fixed end is neither a `PrestoColor` nor a parsable color string, `Color` shall throw `ArgumentException`.
  - Tests: `ColorKeyframesTests.AnEndThatIsNotAColorThrows`
- **CORE-KEYFRAMES-039** (inherited) — When `segments` is below 1, `Color` shall throw `ArgumentOutOfRangeException`.
  - Tests: untested
- **CORE-KEYFRAMES-040** — When the duration is a `Stagger`, `Color` shall throw `ArgumentException`.
  - Tests: untested

### Defaults

- **CORE-KEYFRAMES-041** — When `Color` is given no `space`, it shall follow the path in `Oklab`, the CSS Color 4 default.
  - Tests: untested
- **CORE-KEYFRAMES-042** — When `Color` is given no `hue`, it shall take the `Shorter` hue arc, the CSS Color 4 default.
  - Tests: untested

## Invariants

- Every animation-level keyframe form expands into per-property `TweenParams` arrays before any tween is built; the animation never sees a `Keyframe`.
- In the duration-based form every expanded property has exactly one entry per frame.
- A `KeyframesBuilder` holds frames of one form only.

## Edge cases

- A dictionary built by hand may hold offsets above 100% or negative ones; only the builder's `At` checks the range.
- A percentage-based form whose only frame for a property is at 0% leaves that property with no segment.
- `Color` captures `Engine.Current`'s default ease and duration when the builder runs, not when the animation is built, so it ignores a timeline's `Defaults`.
- A per-target `Color` caches its last resolved target, so interleaving targets within one frame resolves again but stays consistent.

## Threading and lifetime

- Plain data and builders; no engine state. `Color` reads `Engine.Current` on the calling thread.

## Reduced motion

- Not affected beyond CORE-ANIMATION: `ReduceAll` collapses every keyframe tween, so a property lands on its last keyframe (`ReducedMotionTests.ReduceAllEndsAKeyframedPropertyOnItsLastKeyframe`).

## Allocation

- Not on the tick path. Expansion and the builders allocate when the animation is built; a color slice ease is a closure evaluated per frame without allocating.

## Deviations from anime.js

- `TweenKeyframesBuilder.Color` has no upstream counterpart: it is how a color tween follows a color-space path, since tweens carry no color space (CORE-COLORS).
- The builders have no upstream counterpart: they are a discoverable C# spelling of the same parameter objects, beside the indexer (CORE-BUILDER).
- Expansion otherwise follows upstream `generateKeyframes`, including reading the engine defaults' duration for percentage keyframes.

## Decisions

- **A color expansion keeps the ease exact.** Each segment ends on `Mix(from, to, E(tᵢ₊₁))` and its ease is the slice of the tween's ease E rescaled to 0–1; a flat slice holds; a spring first resolves to its ease and settling duration. Unset duration and ease are read from `Engine.Current`'s defaults when `Color` is called. Reason: E on every segment would restart the ease each segment, while slicing makes the timing exactly E and approximates only the color between segment ends; each frame carries its own duration and ease, so the animation's defaults would never reach them. Rejected: the tween's ease applied per segment.
- **Segments are fitted to the path.** From one segment, the one whose rendered blend strays furthest from the mix (deltaEOK at 7 probes) is halved until all are within 0.02, capped at 16; with per-target ends, duration or ease the count is fixed (4, or one per 30° of arc, 4 to 24, in `Oklch`), and an explicit `segments` gives equal-time segments. Reason: equal-time segments do not converge (hard pairs need 48–64, easy ones 2, at about 5.5 µs and 2.3 KB per segment per animation), while fitting costs 0.01–0.5 ms per builder; per-target values are unknown at build time, so their count must be fixed. Rejected: fixed equal-time counts, which strayed to deltaEOK 0.05–0.19.
- **Both ends of a color expansion are known at build time.** `from` is required; function-valued ends, duration or ease resolve once per target and are shared by its frames; a `Stagger` delay works, but a `Stagger` end or duration throws. Reason: the expander runs at build time and cannot read the current color, a `Stagger` end yields numbers rather than colors, and a `Stagger` duration cannot be split per segment. Rejected: starting from the current color, which needs a per-tween color space.
- **Color spaces are a utility, not a per-tween setting.** See CORE-COLORS.

## Non-goals

- No markup form for keyframes.
- No per-tween color space; a color path is expressed only as keyframes (CORE-COLORS).

## Open items

- Percentage keyframes silently drop a frame's `Duration`, `Delay`, `Modifier` and `Composition`, which `KeyframeBuilder` lets the `At` form set; nothing tests or documents it.
- A percentage-based property whose first frame is after 0%, a malformed percentage key, `segments < 1` and a `Stagger` duration in `Color` have no test.
- `Color`'s defaults come from `Engine.Current` at build time rather than the animation's parent defaults, so a timeline's `Defaults.Ease` does not reach it.
