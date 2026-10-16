---
id: CORE-EASES
title: Eases
package: MonacoLabs.Presto
namespaces:
  - MonacoLabs.Presto
sources:
  - MonacoLabs.Presto/Ease.cs
  - MonacoLabs.Presto/Eases.cs
  - MonacoLabs.Presto/Eases.CubicBezier.cs
  - MonacoLabs.Presto/Eases.Irregular.cs
  - MonacoLabs.Presto/Eases.Linear.cs
  - MonacoLabs.Presto/Eases.Pack.cs
  - MonacoLabs.Presto/Eases.Path.cs
  - MonacoLabs.Presto/Eases.Steps.cs
  - MonacoLabs.Presto/EaseValue.cs
  - MonacoLabs.Presto/LinearStop.cs
  - MonacoLabs.Presto/RoughTaper.cs
  - MonacoLabs.Presto/WiggleType.cs
  - MonacoLabs.Presto/Core/EasePathParser.cs
types:
  - Ease
  - Eases
  - EaseValue
  - LinearStop
  - RoughTaper
  - WiggleType
tests:
  - EasesTests
  - EaseFixtureTests
  - EasePackTests
  - CustomEaseTests
  - IntrospectionTests
upstream:
  - src/easings/eases/parser.js
  - src/easings/none.js
  - src/easings/cubic-bezier/index.js
  - src/easings/linear/index.js
  - src/easings/steps/index.js
  - src/easings/irregular/index.js
  - tests/suites/eases.test.js
depends:
  - CORE-SPRING
  - CORE-EASEPARSER
  - CORE-VALUES
  - CORE-DIAGNOSTICS
---

# Eases

## Purpose

The `Ease` delegate and the `Eases` catalog: the anime.js 4.5.0 built-in eases with their `in`/`out`/`inOut`/`outIn` composition, the `Linear`, `Steps`, `Irregular` and `CubicBezier` factories, and the GSAP-style path, wiggle, rough, slow-mo and expo-scale eases. `EaseValue` is the ease parameter every params type takes; it resolves to an `Ease` per target, and to a `Spring` (CORE-SPRING) or a parsed string (CORE-EASEPARSER) where given one.

## Requirements

### Built-in eases

- **CORE-EASES-001** — `Eases.None` shall return its input unchanged.
  - Tests: `EasesTests.NoneIsIdentity`
- **CORE-EASES-002** — Every fixed ease and every power, `Back` and `Elastic` family member shall match anime.js 4.5.0 within 1e-6 at t = 0, 0.01, …, 1.
  - Tests: `EaseFixtureTests.EaseMatchesUpstream`
- **CORE-EASES-003** — `In(power)` shall return t^power, with power 1.68 when omitted.
  - Tests: `EasesTests.CustomPowerEase`, `EaseFixtureTests.EaseMatchesUpstream`
- **CORE-EASES-004** — `Out(power)`, `InOut(power)` and `OutIn(power)` shall be the `Out`, `InOut` and `OutIn` compositions of `In(power)`.
  - Tests: `EasesTests.BuiltInEaseTypesAtMidpoint`, `EaseFixtureTests.EaseMatchesUpstream`
- **CORE-EASES-005** — The `Quad`, `Cubic`, `Quart` and `Quint` eases shall be the power family at 2, 3, 4 and 5.
  - Tests: `EaseFixtureTests.EaseMatchesUpstream`
- **CORE-EASES-006** — The ease-in curves shall be `1 − cos(tπ/2)` for `Sine`, `1 − √(1 − t²)` for `Circ`, `2^(10t − 10)` for `Expo` (exactly 0 at t = 0), and Penner's piecewise bounce for `Bounce`.
  - Tests: `EaseFixtureTests.EaseMatchesUpstream`
- **CORE-EASES-007** — The `Back` ease-in shall be `(o + 1)t³ − o·t²` with overshoot o defaulting to 1.7 and not clamped.
  - Tests: `EaseFixtureTests.EaseMatchesUpstream`
- **CORE-EASES-008** — The `Elastic` eases shall clamp amplitude to [1, 10] and period to [1e-11, 2], defaulting to 1 and 0.3.
  - Tests: untested
- **CORE-EASES-009** — The `Elastic` ease-in shall return exactly t at t = 0 and t = 1.
  - Tests: `EaseFixtureTests.EaseMatchesUpstream`
- **CORE-EASES-010** — The fixed eases shall be singletons: each property read returns the same delegate instance.
  - Tests: untested
- **CORE-EASES-011** — Each call of a parameterized factory shall return a new delegate, except where a requirement below says it returns `None`.
  - Tests: untested

### Composition

- **CORE-EASES-012** — `In(Ease)` shall return a delegate that evaluates the given ease unchanged.
  - Tests: untested
- **CORE-EASES-013** — `Out(Ease f)` shall return `1 − f(1 − t)`.
  - Tests: `EasesTests.CompositionMirrorsAndHalvesACustomEase`
- **CORE-EASES-014** — `InOut(Ease f)` shall return `f(2t)/2` for t < 0.5 and `1 − f(2 − 2t)/2` otherwise.
  - Tests: `EasesTests.CompositionMirrorsAndHalvesACustomEase`, `EasesTests.BuiltInEaseTypesAtMidpoint`
- **CORE-EASES-015** — `OutIn(Ease f)` shall return `(1 − f(1 − 2t))/2` for t < 0.5 and `(f(2t − 1) + 1)/2` otherwise.
  - Tests: `EasesTests.BuiltInEaseTypesAtMidpoint`
- **CORE-EASES-016** — The engine's default ease shall be `out(2)`.
  - Tests: `EasesTests.DefaultEaseIsOutTwo`

### CubicBezier

- **CORE-EASES-017** — `CubicBezier(x1, y1, x2, y2)` shall solve x by bisection (to 1e-7 or 100 iterations) and return the curve's y, defaulting to (0.5, 0, 0.5, 1).
  - Tests: `EasesTests.CubicBezierIn`, `EasesTests.CubicBezierOut`, `EasesTests.CubicBezierInOut`, `EaseFixtureTests.EaseMatchesUpstream`
- **CORE-EASES-018** — When x1 = y1 and x2 = y2, `CubicBezier` shall return the `Eases.None` instance itself.
  - Tests: `EasesTests.CubicBezierOnTheDiagonalIsNone`
- **CORE-EASES-019** — A `CubicBezier` ease shall return exactly t at t = 0 and t = 1.
  - Tests: `EaseFixtureTests.EaseMatchesUpstream`

### Linear and LinearStop

- **CORE-EASES-020** — `Linear()` with no stops shall return the `Eases.None` instance.
  - Tests: `EasesTests.LinearWithoutStopsIsNone`
- **CORE-EASES-021** — `Linear(stops)` shall interpolate linearly between stops placed evenly at i/(n − 1).
  - Tests: `EasesTests.LinearZeroToOne`, `EasesTests.CustomLinear`, `EaseFixtureTests.EaseMatchesUpstream`
- **CORE-EASES-022** — When an interior stop has a `Position`, `Linear` shall place it at that progress instead of its even position.
  - Tests: `EasesTests.CustomUnevenLinear`
- **CORE-EASES-023** — `Linear` shall place the first stop at 0 and the last at 1, ignoring their `Position`.
  - Tests: untested
- **CORE-EASES-024** — When t is past the last stop, a `Linear` ease shall return the last stop's value.
  - Tests: `EasesTests.LinearReturnsLastValuePastTheEnd`
- **CORE-EASES-025** — When t < 0, a `Linear` ease shall extrapolate along its first segment.
  - Tests: untested
- **CORE-EASES-026** — `Linear` shall copy the stops when it is created, so later changes to the array do not change the ease.
  - Tests: untested
- **CORE-EASES-027** — A `LinearStop` converted from a number shall have that value and no `Position`.
  - Tests: `EasesTests.CustomLinear`
- **CORE-EASES-028** — A `LinearStop` converted from a string shall read `"value"` or `"value percent"` (whitespace-separated, trailing `%` optional), the percent divided by 100 as its `Position`.
  - Tests: `EasesTests.LinearStopParsesValueAndPercent`
- **CORE-EASES-029** — When the string has other than one or two parts or a part is not a number, the conversion shall throw `FormatException`.
  - Tests: `EasesTests.LinearStopRejectsGarbage`
- **CORE-EASES-030** — The string conversion shall parse numbers with the invariant culture.
  - Tests: `EasesTests.LinearStopParsesCultureInvariantly`

### Steps and Irregular

- **CORE-EASES-031** — `Steps(steps, fromStart: false)` shall return `floor(clamp(t, 0, 1)·steps)/steps`, jumping at the end of each interval.
  - Tests: `EasesTests.StepsFromEnd`, `EaseFixtureTests.EaseMatchesUpstream`
- **CORE-EASES-032** — When `fromStart` is true, `Steps` shall round up instead, jumping at the start of each interval.
  - Tests: `EasesTests.StepsFromStart`
- **CORE-EASES-033** — `Steps` shall clamp its input to [0, 1].
  - Tests: `EasesTests.StepsClampsOutOfRangeInput`
- **CORE-EASES-077** — When `steps` is below 1, `Steps` shall throw `ArgumentOutOfRangeException`.
  - Tests: `EasesTests.StepsBelowOneThrows`
- **CORE-EASES-034** — `Irregular(length, randomness)` shall be a `Linear` ease through `length` stops, 0 first and 1 last, each interior stop drawn from its segment, mixed with the even spacing by `randomness` and clamped to [previous stop, 1], so it never decreases.
  - Tests: `EasesTests.IrregularStartsAtZeroEndsAtOneAndNeverDecreases`
- **CORE-EASES-035** — When `randomness` is 0, or `length` is 2 or less, `Irregular` shall be the identity curve.
  - Tests: `EasesTests.IrregularWithoutRandomnessIsIdentity`, `EasesTests.IrregularWithTwoStepsIsIdentity`
- **CORE-EASES-036** — `Irregular` shall draw from the given `Random`, or `Random.Shared` when none is given, only while it is created.
  - Tests: `EasesTests.IrregularIsDeterministicForASeed`

### FromPath

- **CORE-EASES-037** — `FromPath` shall read the M, L, H, V and C commands of SVG path data, absolute or relative, with x as progress and y as the eased value pointing up (see Decisions).
  - Tests: `CustomEaseTests.StraightLineIsLinear`, `CustomEaseTests.SingleCubicMatchesCubicBezier`, `CustomEaseTests.RelativeCommandsMatchAbsolute`, `CustomEaseTests.HorizontalAndVerticalCommandsDrawSteps`
- **CORE-EASES-038** — Operands after a moveto shall be read as linetos, and further operands after any other command shall repeat it.
  - Tests: `CustomEaseTests.StraightLineIsLinear`, `CustomEaseTests.RelativeCommandsMatchAbsolute`
- **CORE-EASES-039** — `FromPath` shall map the path's start to (0, 0) and its end to (1, 1).
  - Tests: `CustomEaseTests.PathIsNormalizedToTheUnitSquare`
- **CORE-EASES-040** — `FromPath` shall keep y overshoot between the ends; the curve is not forced to be monotone.
  - Tests: `CustomEaseTests.OvershootingPathExceedsOne`
- **CORE-EASES-041** — A `FromPath` ease shall return exactly 0 for t ≤ 0 and the normalized end value for t ≥ 1.
  - Tests: `CustomEaseTests.EndpointsAreExact`
- **CORE-EASES-042** — `FromPath` shall flatten each cubic into 64 chords, sample the polyline at 513 equal x steps, and interpolate that table linearly, matching the equivalent `CubicBezier` within 1e-3.
  - Tests: `CustomEaseTests.SingleCubicMatchesCubicBezier`
- **CORE-EASES-043** — When the start and end y are equal, `FromPath` shall offset y without scaling it.
  - Tests: untested
- **CORE-EASES-044** — When x decreases by more than 1e-9 along the path, `FromPath` shall throw `ArgumentException`; a smaller decrease is clamped.
  - Tests: `CustomEaseTests.BackwardsXThrows`
- **CORE-EASES-045** — When the path has fewer than two points or no positive x width, `FromPath` shall throw `ArgumentException`.
  - Tests: untested
- **CORE-EASES-046** — When the path uses another command, does not start with a command, or has a missing or malformed number, `FromPath` shall throw `FormatException`.
  - Tests: `CustomEaseTests.MalformedPathThrows`

### Wiggle, Rough, SlowMo, ExpoScale

- **CORE-EASES-047** — `Wiggle(wiggles, type)` shall return `envelope(t)·sin(2π·wiggles·t)`, exactly 0 for t ≤ 0 and t ≥ 1, so the to-value is the swing amplitude (see Decisions).
  - Tests: `EasePackTests.WiggleRestsAtBothEndsAndCrossesZeroTwicePerWiggle`
- **CORE-EASES-048** — The `Wiggle` envelopes shall be `1 − t` for `EaseOut`, `sin(πt)` for `EaseInOut`, `−(1 − t)³` for `Anticipate` and 1 for `Uniform`.
  - Tests: `EasePackTests.EaseOutWigglePeaksDecay`, `EasePackTests.AnticipateSwingsAgainstTheValueFirst`, `EasePackTests.UniformWigglePeaksAreEqual`
- **CORE-EASES-049** — For `WiggleType.Random`, `Wiggle` shall draw one amplitude per half swing (2·wiggles) from the given `Random`, or `Random.Shared`, while it is created, each multiplied by `1 − t`.
  - Tests: `EasePackTests.WiggleRestsAtBothEndsAndCrossesZeroTwicePerWiggle`
- **CORE-EASES-050** — When `wiggles` < 1 or `type` is not a defined `WiggleType`, `Wiggle` shall throw `ArgumentOutOfRangeException`.
  - Tests: untested
- **CORE-EASES-051** — `Rough` shall be a `Linear` ease through `points` points jittered around `template` (default `None`), exactly 0 at t = 0 and 1 at t = 1.
  - Tests: `EasePackTests.RoughIsExactAtTheEnds`, `EasePackTests.RoughIsJagged`
- **CORE-EASES-052** — `Rough` shall jitter each point by up to half of 0.4·`strength`: randomly in x and y when `randomize` is true, else at x = i/points with alternating signs.
  - Tests: `EasePackTests.RoughFollowsItsTemplateWithZeroStrength`, `EasePackTests.RoughIsJagged`
- **CORE-EASES-053** — `RoughTaper` shall scale the jitter by x² (`In`), (1 − x)² (`Out`) or half of (2·min(x, 1 − x))² (`Both`).
  - Tests: untested
- **CORE-EASES-054** — When `clamp` is true, `Rough` shall clamp each jittered point to [0, 1].
  - Tests: `EasePackTests.ClampedRoughStaysInTheUnitRange`
- **CORE-EASES-055** — `Rough` shall draw from the given `Random`, or `Random.Shared`, only while it is created, so one seed gives one curve.
  - Tests: `EasePackTests.RoughWithTheSameSeedIsIdentical`
- **CORE-EASES-056** — When `points` < 1, `Rough` shall throw `ArgumentOutOfRangeException`.
  - Tests: untested
- **CORE-EASES-057** — `SlowMo(linearRatio, power)` shall run a linear middle over `linearRatio` of the time with slope `1 − power`, eased in and out at the ends, from 0 at t = 0 to 1 at t = 1.
  - Tests: `EasePackTests.SlowMoHasConstantSlopeOverItsLinearSection`
- **CORE-EASES-058** — `SlowMo` shall clamp `linearRatio` to at most 1 and ignore `power` at 1.
  - Tests: untested
- **CORE-EASES-059** — When `yoyoMode` is true, `SlowMo` shall rise to 1, hold it through the linear section, and fall back to exactly 0 at t ≥ 1.
  - Tests: `EasePackTests.YoyoSlowMoReturnsToZero`
- **CORE-EASES-060** — `ExpoScale(start, end, ease)` shall return `(start·(end/start)^e − start)/(end − start)` where e is `ease(t)` (default `None`), so the scale is interpolated geometrically.
  - Tests: `EasePackTests.ExpoScaleMidpointIsTheGeometricMean`, `EasePackTests.ExpoScaleAppliesItsEaseToProgress`
- **CORE-EASES-061** — When a scale is not positive or the two are equal, `ExpoScale` shall throw `ArgumentOutOfRangeException`.
  - Tests: `EasePackTests.ExpoScaleRejectsInvalidScales`

### EaseValue

- **CORE-EASES-062** — When an `EaseValue` holds an `Ease`, it shall resolve to that delegate.
  - Tests: `EasesTests.AnAnimationAppliesItsEase`
- **CORE-EASES-063** — When an `EaseValue` holds a `Spring`, it shall resolve to the spring's `Ease` and report the spring, so its `SettlingDuration` becomes the tween's duration (CORE-SPRING).
  - Tests: `SpringTests.SpringEaseOverridesTheTweenDurationParameter`
- **CORE-EASES-064** — When an `EaseValue` holds a string, it shall resolve through `EaseParser.Parse` each time it is resolved.
  - Tests: `TweenValueTests.EaseValueResolvesEachForm`
- **CORE-EASES-065** — When an `EaseValue` holds a `FunctionValue`, it shall call it once per target with (target, index, total) and resolve its result as above.
  - Tests: `TweenValueTests.EaseValueFunctionsResolvePerTarget`
- **CORE-EASES-066** — When an `EaseValue` is default or holds a null string, or its function returns no value, it shall resolve to `Eases.None`.
  - Tests: `TweenValueTests.EaseValueResolvesEachForm`, `TweenValueTests.AnEaseFunctionReturningNothingResolvesToNone`
- **CORE-EASES-078** — When an `EaseValue`'s function returns a value that is not an ease, a spring or a string, resolving it shall throw `InvalidOperationException`.
  - Tests: `TweenValueTests.AnEaseFunctionReturningANonEaseThrows`

### Names

- **CORE-EASES-067** — `TryGetName` shall always report the anime.js name of a fixed ease, and `"linear"` for `Eases.None`.
  - Tests: `IntrospectionTests.EaseNamesForFactoriesAreKeptOnlyWhileDiagnosticsAreActive`
- **CORE-EASES-068** — `TryGetName` shall always report `"out(2)"` for the engine's default ease.
  - Tests: `IntrospectionTests.TheDefaultEaseAndEasesParsedBeforeEnablingAreNamed`
- **CORE-EASES-069** — While diagnostics are active (CORE-DIAGNOSTICS), a factory shall name its ease in call form with JavaScript number formatting, such as `"out(3)"` or `"outElastic(1.25, 0.5)"`; created while inactive, the ease has no name.
  - Tests: `IntrospectionTests.EaseNamesForFactoriesAreKeptOnlyWhileDiagnosticsAreActive`
- **CORE-EASES-070** — `TryGetName` shall return false for a delegate no factory created.
  - Tests: `IntrospectionTests.EaseNamesForFactoriesAreKeptOnlyWhileDiagnosticsAreActive`

### Defaults

- **CORE-EASES-071** — When `power` is not set, `Out`, `InOut` and `OutIn` shall use power 1.68.
  - Tests: `EasesTests.BuiltInEaseTypesAtMidpoint`
- **CORE-EASES-072** — When `steps` is not set, `Steps` shall use 10 steps.
  - Tests: untested
- **CORE-EASES-073** — When `length` and `randomness` are not set, `Irregular` shall use 10 stops and randomness 1.
  - Tests: untested
- **CORE-EASES-074** — When `wiggles` and `type` are not set, `Wiggle` shall swing 10 times with the `EaseOut` envelope, GSAP's defaults.
  - Tests: `EasePackTests.EaseOutWigglePeaksDecay`
- **CORE-EASES-075** — When its options are not set, `Rough` shall use 20 points, strength 1, no clamping, `RoughTaper.None` and randomized points, GSAP's defaults.
  - Tests: untested
- **CORE-EASES-076** — When its options are not set, `SlowMo` shall use a linear ratio of 0.7, power 0.7 and no yoyo, GSAP's defaults.
  - Tests: untested

## Invariants

- Every ease is a pure function of t once created, `Spring.Ease` excepted (CORE-SPRING): randomness is drawn at creation.
- Ease delegates other than `Steps` do not clamp t; callers pass progress in [0, 1].
- The name registry holds eases weakly and never keeps one alive.

## Edge cases

- Power eases with a fractional power return NaN for t < 0.
- `Linear` stop positions are not validated or sorted; two stops at the same position, or positions out of order, give an ease that jumps or divides by zero.
- A `Linear` ease with one stop is that value everywhere.
- `Irregular` does not validate `randomness`; values outside [0, 1] still pass through the [previous, 1] clamp.
- A flat `FromPath` path (equal start and end y) ends at its offset end value, 0, not 1.
- `ExpoScale` with an inner ease that overshoots extrapolates the geometric scale past `end`.

## Threading and lifetime

- The eases are pure and stateless once created (`Spring.Ease` aside), so they can be called from any thread. The name registry is a `ConditionalWeakTable`, safe for concurrent use.
- `Random`-based factories draw from the given `Random` only while they run; pass a `Random` used by one thread, or none (`Random.Shared`).

## Reduced motion

- Not affected: an ease is a curve. Reduced motion acts on the animations that use it (CORE-REDUCED).

## Allocation

- Calling an ease allocates nothing: the factories precompute their tables, envelopes and stops. Pinned by `EasePackTests.InvokingDoesNotAllocate` and `CustomEaseTests.InvokingDoesNotAllocate`.
- Creating one allocates its closure, and naming it while diagnostics are active formats a string.

## Provenance

- `Eases.None` is the single identity; upstream's `eases.linear` and `none` are the same function, as here.
- `Linear` takes typed `LinearStop`s, with string and number conversions standing in for upstream's mixed arguments.
- `Irregular`, `Wiggle` and `Rough` take an optional `Random`; upstream's `irregular` uses `Math.random`.
- `FromPath`, `Wiggle`, `Rough`, `SlowMo` and `ExpoScale` have no upstream; they follow GSAP's CustomEase, CustomWiggle and EasePack, because anime.js offers no path, wiggle, rough, slow-mo or expo-scale ease.
- `TryGetName` and the name registry have no upstream counterpart.
- `Steps` throws for a count below 1; upstream returns NaN.
- A chained ease (upstream passes the previous sibling tween into ease resolution) is not ported, because `FunctionValue` takes three arguments (see Decisions).

## Decisions

- **`FromPath` reads y up and normalizes to the unit square, keeping overshoot.** Path data (M, L, H, V, C, absolute or relative) is read with x as progress and y pointing up, not flipped; the start maps to (0, 0) and the end to (1, 1), and the table is not forced monotone. A decreasing x or zero width throws `ArgumentException`, other bad data `FormatException`, and the path is sampled into a table so calls do not allocate. Reason: a path from `0,0` to `1,1` then reads as it would on a graph.
- **`Wiggle` is a displacement ease, 0 at both ends.** The tween's to-value is the swing amplitude and the property ends where it started. Reason: GSAP's CustomWiggle behaves so.
- **`Rough` takes GSAP's full option set, seeded through `Random`.** It takes points, strength, clamp, taper, randomize and template, and draws from `Random.Shared` unless given a `Random`. Reason: GSAP's RoughEase is the rough ease people know, and an injected `Random` makes a curve reproducible in tests and replays. Rejected: a `seeded` flag.
- **Upstream's chained ease is not ported.** Ease resolution receives no previous sibling tween. Reason: no upstream ease reads it (only `scrambleText`'s and `morphTo`'s to-values do, which `ChainedFunctionValue` covers), and carrying momentum across a retarget is `PreserveVelocity`'s job, with the real velocity; a chained `EaseValue` would reshape ease resolution for an unused hook. Rejected: a chained `EaseValue` form.
- **Factory defaults are kept from upstream.** Power 1.68, 10 steps and `Irregular`'s 10 stops with randomness 1 (CORE-EASES-071 to 073) are upstream's numbers. Reason: an animation ported from anime.js looks the same.
- **Eases are immutable delegates, and fixed ones are singletons.** Each fixed ease is one instance; each factory call returns a new delegate; `Linear` copies its stops (CORE-EASES-010, 011, 026). Reason: one ease is shared by many tweens and threads, so it must not change under them, and singletons can be compared by reference and named. Rejected: caching factory results, which needs a table keyed by arguments that grows without bound.
- **Ease strings parse invariantly and fail loudly.** `LinearStop` strings parse with the invariant culture and throw `FormatException` on garbage, and an `EaseValue` string is parsed on each resolution (CORE-EASES-029, 030, 064). Reason: ease strings come from XAML and defaults, which must mean the same on every machine; `EaseParser` caches, so parsing per resolution is cheap.
- **Counts are validated.** `Steps`, `Wiggle` and `Rough` throw `ArgumentOutOfRangeException` for a count below 1, and `Wiggle` for an undefined `WiggleType` (CORE-EASES-050, 056, 077). Reason: such a count is a caller's bug, and upstream's NaN would surface far from it.
- **An ease function must return an ease.** No value means `None`, as a default `EaseValue` does; any other value throws (CORE-EASES-066, 078). Reason: a function returning the wrong thing is a bug, and silently animating linearly hid it. Rejected: resolving every unknown result to `None`.
- **Ease names are registered only while diagnostics are active.** Fixed eases register at static initialization; parameterized factories register only while diagnostics are active, in a weak table. Reason: those factories mint a delegate per call, often in loops, and must not pay a locked table insert in a release app; an overlay can always draw the sampled curve.

## Non-goals

- No validation of curve arguments beyond what upstream and GSAP do, except the counts under "Counts are validated".
- No `Q`, `S`, `T`, `A` or `Z` path commands in `FromPath`.
- No string syntax for `Steps`, `Linear`, `Irregular` or `CubicBezier` (CORE-EASEPARSER).

## Open items

- Untested: `Elastic` argument clamping (008), `Linear` endpoint positions, negative t and stop copying (023, 025, 026), `FromPath` flat and too-short paths (043, 045), `Wiggle`/`Rough` argument errors (050, 056), `RoughTaper` scaling (053), and `SlowMo` ratio clamping (058).
