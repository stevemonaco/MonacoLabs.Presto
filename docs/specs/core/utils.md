---
id: CORE-UTILS
title: Utilities
package: MonacoLabs.Presto
namespaces:
  - MonacoLabs.Presto
sources:
  - MonacoLabs.Presto/Utils.cs
  - MonacoLabs.Presto/ChainedUtil.cs
  - MonacoLabs.Presto/ChainedFunctionValue.cs
  - MonacoLabs.Presto/RandomGenerator.cs
  - MonacoLabs.Presto/Core/Helpers.cs
types:
  - Utils
  - ChainedUtil
  - ChainedFunctionValue
  - RandomGenerator
tests:
  - UtilsTests
  - HelpersTests
  - FunctionBasedValuesTests
  - ScopeTests
upstream:
  - src/utils/number.js
  - src/utils/random.js
  - src/utils/time.js
  - src/utils/chainable.js
  - tests/suites/utils.test.js
depends:
  - CORE-TIMER
  - CORE-ENGINE
  - CORE-SCOPE
  - CORE-ANIMATION
  - CORE-VALUES
---

# Utilities

## Purpose

`Utils` holds numeric helpers (clamp, round, snap, wrap, map, lerp, damp, angle conversion, fixed formatting), random helpers (unseeded and seeded generators, pick, shuffle) and time helpers (`Sync`, `KeepTime`). Every numeric helper has an overload that omits the value and returns a `ChainedUtil`, a composable transform usable wherever a `Modifier` delegate is expected. `ChainedFunctionValue` is the function-based value that also receives the tween it chains off. Target utilities (`get`/`set`/`remove`) are on the facade (CORE-PRESTO).

## Requirements

### Numeric

- **CORE-UTILS-001** — `Clamp` shall return `min` when `v < min`, else `max` when `v > max`, else `v`.
  - Tests: `HelpersTests.ClampClampsToBounds`
- **CORE-UTILS-002** — `Round` shall round to `decimals` places with JavaScript `Math.round` semantics (ties toward positive infinity), and return `v` unchanged when `decimals` is negative.
  - Tests: `HelpersTests.RoundWithZeroDecimalsUsesJavaScriptSemantics`, `HelpersTests.RoundToDecimals`, `HelpersTests.RoundWithNegativeDecimalsReturnsValueUnchanged`
- **CORE-UTILS-003** — `Snap(v, increment)` shall return the multiple of `increment` nearest `v`, rounding `v / increment` as `Round(…, 0)` does, and return `v` when `increment` is 0.
  - Tests: `UtilsTests.SnapToAnIncrement`
- **CORE-UTILS-004** — `Snap(v, points)` shall return the point closest to `v` by absolute distance, the earliest point winning a tie, and `v` when `points` is empty.
  - Tests: `UtilsTests.SnapToTheClosestPoint`
- **CORE-UTILS-005** — `Wrap` shall map `v` into [`min`, `max`) by cycling past either end, so values below `min` wrap from `max` downward.
  - Tests: untested
- **CORE-UTILS-006** — `MapRange` shall re-map `value` linearly from [`inLow`, `inHigh`] onto [`outLow`, `outHigh`], extrapolating outside the input range.
  - Tests: `UtilsTests.ChainedUtilityFunctions`
- **CORE-UTILS-007** — `Lerp` shall return exactly `end` when `factor` is 1, exactly `start` when it is 0, and `start + (end - start) * factor` otherwise, without clamping `factor`.
  - Tests: untested
- **CORE-UTILS-008** — `Damp` shall return `start` when `factor` is 0, `end` when it is 1, and otherwise `Lerp(start, end, 1 - exp(-factor * deltaTime * 0.1))`, with `deltaTime` in milliseconds.
  - Tests: untested
- **CORE-UTILS-009** — `DegToRad` and `RadToDeg` shall convert by `π / 180` and `180 / π`.
  - Tests: untested
- **CORE-UTILS-010** — `RoundPad` shall format `v` with exactly `decimals` decimal places in the invariant culture, like JavaScript `toFixed`.
  - Tests: untested

### Chainable

- **CORE-UTILS-011** — Each value-less overload of `Clamp`, `Round`, `Snap`, `Wrap`, `MapRange`, `DegToRad` and `RadToDeg` shall return a `ChainedUtil` whose `Invoke(v)` equals the full call with `v`.
  - Tests: `UtilsTests.ChainedUtilityFunctions`
- **CORE-UTILS-012** — `Utils.Lerp(start, end)` and `Utils.Damp(start, end, deltaTime)` shall return chains in which the chained value becomes the factor.
  - Tests: untested
- **CORE-UTILS-013** — Each `ChainedUtil` method shall return a new chain that applies the existing chain first and the named transform to its result, so a chain reads left to right.
  - Tests: `UtilsTests.ChainedUtilityFunctions`
- **CORE-UTILS-014** — Chaining shall leave the original chain unchanged, so one chain can branch into several.
  - Tests: untested
- **CORE-UTILS-015** — A `ChainedUtil` shall convert implicitly to `Func<double, double>`, so it can be passed as a `Modifier`.
  - Tests: `UtilsTests.ChainedUtilityFunctions`
- **CORE-UTILS-016** — `ChainedUtil.RoundPad(decimals)` shall end the chain with a `Func<double, string>` that formats the chain's result as `Utils.RoundPad` does.
  - Tests: untested
- **CORE-UTILS-017** (inherited) — `Invoke` on a `default(ChainedUtil)` shall throw `NullReferenceException`.
  - Tests: untested

### Random

- **CORE-UTILS-018** — `Random(min, max, decimals)` shall return `floor((u * (max - min + 10^-decimals) + min) * 10^decimals) / 10^decimals` with `u` uniform in [0, 1) from the shared thread-safe generator, so both ends are reachable at that precision.
  - Tests: untested
- **CORE-UTILS-019** — A generator from `CreateSeededRandom(seed)` shall reproduce anime.js's seeded sequence for the same seed and arguments bit for bit.
  - Tests: `UtilsTests.SeededRandomReproducesTheJavaScriptSequence`
- **CORE-UTILS-020** — Two generators created with the same seed shall produce the same sequence independently of each other.
  - Tests: `UtilsTests.SeededRandomReproducesTheJavaScriptSequence`
- **CORE-UTILS-021** — When `seed` is omitted, `CreateSeededRandom` shall take the next value of a process-wide counter starting at 0, incremented atomically per such call.
  - Tests: untested
- **CORE-UTILS-022** — `RandomPick` shall return `items[(int)Random(0, items.Count - 1)]`, every element equally likely; an empty list fails in the list's indexer.
  - Tests: untested
- **CORE-UTILS-023** — `Shuffle` shall shuffle `items` in place with Fisher-Yates, drawing indices from `rnd` (or `Utils.Random` when null), and return the same instance.
  - Tests: `UtilsTests.ShuffleReordersWithoutLosingItems`
- **CORE-UTILS-024** — When `Shuffle` is given a seeded generator, the resulting order shall be determined by the seed.
  - Tests: untested

### Time

- **CORE-UTILS-025** — `Sync(callback)` shall create a top-level timer on `Engine.Current` with a duration of 1 ms (scaled by the engine's time unit) and `callback` as its `OnComplete`, resume it without `Init()`, and return it, so the callback runs on the next engine tick.
  - Tests: `UtilsTests.SyncRunsTheCallbackOnTheNextTick`
- **CORE-UTILS-026** — `Sync` shall accept a null callback and still run and complete the timer.
  - Tests: untested
- **CORE-UTILS-027** — The first call of a `KeepTime` wrapper shall return what `constructor` returns, untouched.
  - Tests: `UtilsTests.TrackInstanceLoopAlternateProgressWithATimekeeper`
- **CORE-UTILS-028** — Each later call shall revert the timer the previous call returned before calling `constructor`.
  - Tests: `ScopeTests.KeepTimeKeepsThePlaybackPositionAcrossARefresh`
- **CORE-UTILS-029** — Each later call shall give the new timer the previous one's `CurrentIteration`, `IterationProgress` and start time, so it continues at the same fraction of the same iteration whatever its new duration.
  - Tests: `UtilsTests.TrackInstanceLoopAlternateProgressWithATimekeeper`, `ScopeTests.KeepTimeKeepsThePlaybackPositionAcrossARefresh`
- **CORE-UTILS-030** — When the previous timer was reversed, or was alternating and on an odd iteration (but not both), the handed-over progress shall be mirrored to `1 - progress`.
  - Tests: `UtilsTests.TrackInstanceLoopAlternateProgressWithATimekeeper`
- **CORE-UTILS-031** — Each `KeepTime` wrapper shall track only the timers it returned itself.
  - Tests: untested

### Chained function values

- **CORE-UTILS-032** — A `ChainedFunctionValue` used as a keyframe value shall receive the tween of the previous keyframe as `previous`, null for the first keyframe of a standalone animation.
  - Tests: `FunctionBasedValuesTests.ChainedFunctionValuesInKeyframesReadThePreviousKeyframeEndValue`
- **CORE-UTILS-033** — A `ChainedFunctionValue` on a timeline child shall receive the last tween the same timeline built on that target and property, from an earlier sibling.
  - Tests: `FunctionBasedValuesTests.ChainedFunctionValuesOnTimelineChildrenReadThePreviousChildEndValue`
- **CORE-UTILS-034** — A `ChainedFunctionValue` shall receive the target's index and the total target count exactly as a `FunctionValue` does (see CORE-VALUES).
  - Tests: untested

### Defaults

- **CORE-UTILS-035** (inherited) — When `Random` is not given `min`, `max` or `decimals`, it shall use 0, 1 and 0 respectively.
  - Tests: untested
- **CORE-UTILS-036** (inherited) — When a seeded `RandomGenerator` is not given `min`, `max` or `decimals`, it shall use 0, 1 and 0 respectively.
  - Tests: untested

## Invariants

- The numeric helpers are pure and allocate nothing; a `ChainedUtil` allocates only when it is built.
- A seeded generator's sequence depends only on its seed and the arguments of each call.

## Edge cases

- `Wrap` with `min == max` and `MapRange` with `inLow == inHigh` divide by zero and return NaN or an infinity.
- `Damp` with a NaN factor returns NaN; upstream's `!factor` returns `start`.
- `Snap(v, [])` returns `v`; upstream's `reduce` throws on an empty array.
- `KeepTime` reverts the previous timer even when it has completed; reverting restores the targets it wrote (see CORE-ANIMATION).

## Threading and lifetime

- `Random` uses the thread-safe shared generator; the seed counter is atomic. A seeded generator holds mutable state and must not be shared across threads.
- `Sync` and `KeepTime` timers belong to `Engine.Current` and register with its current scope, as any timer (CORE-TIMER, CORE-SCOPE).
- A `KeepTime` wrapper keeps its last timer reachable until the next call.

## Reduced motion

- Not affected: the helpers compute values; `Sync`'s timer is a plain timer, which `ReduceAll` leaves alone (CORE-REDUCED).

## Allocation

- Not on the tick path, except a `ChainedUtil` used as a modifier, which runs per tween per frame; invoking it allocates nothing, but each link adds a delegate call.

## Provenance

- `padStart`/`padEnd` are omitted; `string.PadLeft`/`PadRight` cover them.
- `CreateSeededRandom` drops `seededMin`/`seededMax`/`seededDecimalLength`: C# optional arguments must be constants, so the delegate could never default to them.
- `ChainedUtil` replaces the JavaScript `Proxy` chain, which C# has no counterpart for; there is no value-less `Utils.RoundPad(decimals)` to start a chain with, and `RoundPad` can only end one.
- `Utils.KeepTime` takes a one-argument constructor returning a timer; upstream's variadic form and its branch for constructors returning a cleanup function are not ported, because that branch serves scopes, which `Scope.KeepTime` covers (CORE-SCOPE).
- `RandomPick` takes a list only, not a string. Reason: not recorded.

## Decisions

None recorded.

## Non-goals

- No general random-distribution helpers beyond upstream's set.

## Open items

- `Wrap`, `Lerp`, `Damp`, `DegToRad`/`RadToDeg`, `RoundPad`, `Random`, `RandomPick`, the unseeded seed counter and the chainable `Lerp`/`Damp` have no direct test.
- `Damp` treats a NaN factor differently from upstream (`!factor`); no decision records it.
