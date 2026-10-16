---
id: CORE-EASEPARSER
title: Ease strings
package: MonacoLabs.Presto
namespaces:
  - MonacoLabs.Presto
sources:
  - MonacoLabs.Presto/EaseParser.cs
types:
  - EaseParser
tests:
  - EaseParserTests
  - EaseFixtureTests
  - IntrospectionTests
upstream:
  - src/easings/eases/parser.js
depends:
  - CORE-EASES
  - CORE-DIAGNOSTICS
---

# Ease strings

## Purpose

`EaseParser.Parse` turns anime.js ease strings such as `"inOutQuint"`, `"out(2)"` or `"outElastic(1.5, .5)"`, plus the forms without an upstream string (`fromPath`, `wiggle`, `rough`, `slowMo`, `expoScale`), into `Ease` delegates from CORE-EASES, caching each result by its input. Every `EaseValue` built from a string resolves through it, which is how defaults and declarative markup name eases.

## Requirements

### Lookup and cache

- **CORE-EASEPARSER-001** (inherited) — `Parse` shall trim leading and trailing whitespace from its input before reading it.
  - Tests: `EaseParserTests.ParsesToMatchingEase`
- **CORE-EASEPARSER-002** — `Parse` shall cache its result by the trimmed input, so the same string returns the same instance while it stays cached.
  - Tests: `EaseParserTests.SameStringReturnsSameInstance`
- **CORE-EASEPARSER-003** (inherited) — The cache shall be process-wide and safe for concurrent callers.
  - Tests: untested
- **CORE-EASEPARSER-036** — When the cache holds 256 entries, `Parse` shall clear it before caching a new result, and a string parsed again after a clear shall give an equivalent ease (see "The ease parser cache is bounded").
  - Tests: `EaseParserTests.TheCacheIsClearedWhenFullAndReparsesAfterwards`
- **CORE-EASEPARSER-004** (inherited) — When parsing throws, `Parse` shall cache nothing, so the same string throws again.
  - Tests: untested

### Names

- **CORE-EASEPARSER-005** — When the trimmed input is empty, `Parse` shall return `Eases.None`; when the name is `irregular` or `linear` followed by `(`, it shall throw `FormatException` naming the `Eases` factory to build it with.
  - Tests: `EaseParserTests.ResolvesToEasesNone`, `EaseParserTests.UnknownNamesAndCodeOnlyFormsThrow`
- **CORE-EASEPARSER-006** — `Parse` shall take the name as the text before the first `(` with trailing whitespace removed, or the whole input when there is no `(`.
  - Tests: `EaseParserTests.ParsesToMatchingEase`
- **CORE-EASEPARSER-007** — When the name starts with an upper-case letter, `Parse` shall lower that one letter, so the PascalCase spellings of `Eases` members are accepted.
  - Tests: `EaseParserTests.ParsesToMatchingEase`
- **CORE-EASEPARSER-008** — When the name is `linear`, `none` or one of the 32 fixed eases, `Parse` shall return that singleton.
  - Tests: `EaseParserTests.ResolvesToEasesNone`, `EaseFixtureTests.EaseMatchesUpstream`
- **CORE-EASEPARSER-009** (inherited) — When a fixed ease is given a non-empty argument list, `Parse` shall throw `FormatException`.
  - Tests: `EaseParserTests.MalformedStringsThrow`
- **CORE-EASEPARSER-010** (inherited) — When a fixed ease is given empty parentheses, `Parse` shall return that ease.
  - Tests: untested
- **CORE-EASEPARSER-011** — When the name is neither fixed nor parameterized, `Parse` shall throw `FormatException` naming it.
  - Tests: `EaseParserTests.UnknownNamesAndCodeOnlyFormsThrow`

### Arguments

- **CORE-EASEPARSER-012** (inherited) — When the input contains `(` but does not end with `)`, `Parse` shall throw `FormatException`.
  - Tests: `EaseParserTests.MalformedStringsThrow`
- **CORE-EASEPARSER-013** — `Parse` shall read the argument list as the text between the first `(` and the final `)`, trimmed, split on commas with each token trimmed.
  - Tests: `EaseParserTests.ParsesToMatchingEase`
- **CORE-EASEPARSER-014** (inherited) — When the argument list is empty or missing, a parameterized ease shall use its factory's defaults.
  - Tests: `EaseParserTests.ParsesToMatchingEase`
- **CORE-EASEPARSER-015** (inherited) — `Parse` shall read numeric arguments as invariant-culture doubles, and throw `FormatException` for a token that is not a number.
  - Tests: `EaseParserTests.ParsesArgumentsCultureInvariantly`, `EaseParserTests.MalformedStringsThrow`
- **CORE-EASEPARSER-016** — `Parse` shall ignore arguments past the ones the factory reads, without parsing them.
  - Tests: untested

### Parameterized forms

- **CORE-EASEPARSER-017** — `in`, `out`, `inOut` and `outIn` shall take an optional power; the `Back` forms an optional overshoot; the `Elastic` forms an optional amplitude and period.
  - Tests: `EaseParserTests.ParsesToMatchingEase`, `EaseFixtureTests.EaseMatchesUpstream`
- **CORE-EASEPARSER-018** — `fromPath(…)` shall pass the trimmed raw text between the parentheses, commas included, to `Eases.FromPath`.
  - Tests: `EaseParserTests.FromPathTakesTheRawPath`
- **CORE-EASEPARSER-019** (inherited) — `fromPath` shall let `FromPath`'s `ArgumentException` and `FormatException` propagate.
  - Tests: untested
- **CORE-EASEPARSER-020** — `wiggle` shall take an optional count, truncated to an integer, and an optional `WiggleType` name matched case-insensitively.
  - Tests: `EaseParserTests.ParsesToMatchingEase`
- **CORE-EASEPARSER-021** — When the `wiggle` type is not a defined `WiggleType`, `Parse` shall throw `FormatException`.
  - Tests: `EaseParserTests.MalformedStringsThrow`
- **CORE-EASEPARSER-022** — `rough` shall take an optional point count, truncated to an integer, and an optional strength; its other options keep their defaults.
  - Tests: `EaseParserTests.ParsesToMatchingEase`
- **CORE-EASEPARSER-023** — `wiggle` and `rough` shall draw from a new `Random(1)` per parse, so a string always gives the same curve.
  - Tests: `EaseParserTests.ParsesToMatchingEase`
- **CORE-EASEPARSER-024** — `slowMo` shall take an optional ratio, power and yoyo flag, the flag read as `true` or `false` case-insensitively.
  - Tests: `EaseParserTests.ParsesToMatchingEase`
- **CORE-EASEPARSER-025** — When the `slowMo` flag is not a boolean, `Parse` shall throw `FormatException`.
  - Tests: `EaseParserTests.MalformedStringsThrow`
- **CORE-EASEPARSER-026** — `expoScale` shall take a start and an end scale, and parse any further tokens, rejoined with commas, as an ease string for its inner ease.
  - Tests: `EaseParserTests.ParsesToMatchingEase`
- **CORE-EASEPARSER-027** — When `expoScale` has fewer than two arguments, `Parse` shall throw `FormatException`.
  - Tests: `EaseParserTests.MalformedStringsThrow`
- **CORE-EASEPARSER-028** (inherited) — Apart from `steps` (034), `Parse` shall let a factory's `ArgumentOutOfRangeException` propagate, as for `wiggle(0)`, `rough(0)` or `expoScale(1, 1)`.
  - Tests: untested
- **CORE-EASEPARSER-029** — `steps` shall take an optional count, truncated to an integer, and an optional from-start flag; `cubicBezier` shall take exactly four numbers, and throw `FormatException` otherwise. `Parse` shall have no form for `Spring`, `Linear` stops or `Irregular`.
  - Tests: `EaseParserTests.StepsAndCubicBezierParseToTheirFactories`, `EaseParserTests.UnknownNamesAndCodeOnlyFormsThrow`
- **CORE-EASEPARSER-034** — When the `steps` count is below 1 after truncation, `Parse` shall throw `FormatException`, so `TryParse` returns false (see Decisions).
  - Tests: `EaseParserTests.ParsedStepsBelowOneIsAFormatError`
- **CORE-EASEPARSER-033** — `TryParse` shall return what `Parse` returns, or false with a null ease where `Parse` would throw `FormatException`.
  - Tests: `EaseParserTests.UnknownNamesAndCodeOnlyFormsThrow`, `EaseParserTests.StepsAndCubicBezierParseToTheirFactories`
- **CORE-EASEPARSER-035** — When the string is null, `Parse` and `TryParse` shall throw `ArgumentNullException`.
  - Tests: `EaseParserTests.NullEaseStringThrowsArgumentNull`

### Names for diagnostics

- **CORE-EASEPARSER-030** — When diagnostics first become active (CORE-DIAGNOSTICS), every cached ease without a name shall be named after its trimmed input string.
  - Tests: `IntrospectionTests.TheDefaultEaseAndEasesParsedBeforeEnablingAreNamed`
- **CORE-EASEPARSER-031** — While diagnostics are active, a parsed parameterized ease shall carry its factory's call-form name.
  - Tests: `IntrospectionTests.EaseNamesForFactoriesAreKeptOnlyWhileDiagnosticsAreActive`

### Defaults

- **CORE-EASEPARSER-032** — When a parameterized ease is given fewer arguments than it reads, `Parse` shall leave the missing trailing ones to the factory's defaults, `expoScale` then getting no inner ease.
  - Tests: `EaseParserTests.ParsesToMatchingEase`

## Invariants

- A cached string maps to one `Ease` until the cache is cleared, and that ease is deterministic, so a reparse gives an equivalent one.
- `Parse` never returns null.

## Edge cases

- `""` and whitespace-only strings resolve to `Eases.None`.
- Only the first letter is lowered: `"INOUTQUAD"` is unknown and throws.
- `"in2)"` has no `(`, so it is an unknown name and throws; `"in(2))"` throws because `"2)"` is not a number.
- `TryParse` catches `FormatException` only; a factory's `ArgumentOutOfRangeException` (028), as from `wiggle(0)`, still propagates.
- `"in(1,)"` parses: the empty second token is never read.
- `wiggle` accepts numeric type names that are defined values, so `"wiggle(4, 1)"` is `EaseInOut`.
- `"wiggle(2.7)"` is `Wiggle(2)`; `"wiggle(0.5)"` truncates to 0 and throws `ArgumentOutOfRangeException`.
- Distinct spellings of one ease (`"out(2)"`, `"Out(2)"`, `"out( 2 )"`) are cached as distinct instances.

## Threading and lifetime

- `Parse` may be called from any thread; the cache is a `ConcurrentDictionary`. Two threads parsing one new string at once may both run the factory, and one result wins.
- Cached eases live until the cache fills and is cleared.

## Reduced motion

- Not affected: parsing produces a curve; reduced motion acts on the animations that use it (CORE-REDUCED).

## Allocation

- Not on the tick path. A cache hit allocates nothing beyond the trimmed string; a miss allocates the tokens and the ease.

## Provenance

- PascalCase names are accepted; upstream is case-sensitive. Reason: the PascalCase spellings of the `Eases` members then work as strings too.
- An empty argument list (`"in()"`) uses the factory defaults; upstream coerces the empty token to 0.
- Arguments on a fixed ease, unparsable numbers and unbalanced parentheses throw `FormatException`; upstream caches a broken ease or returns `none`.
- Arguments may be identifiers, booleans, raw path data or nested ease strings; upstream reads only numbers. Reason: path data contains commas, and the forms without an upstream counterpart take names, flags and inner eases.
- `fromPath`, `wiggle`, `rough`, `slowMo` and `expoScale` have no upstream counterpart.
- `steps(…)` and `cubicBezier(…)` parse, where upstream v4 removed them and returns `none` with a console warning; `irregular(…)`, `linear(…)` and unknown names throw instead (see "Ease strings fail loudly; steps and cubicBezier parse").

## Decisions

- **Ease strings accept PascalCase names.** A leading capital is lowered, so `"InOutQuint"` parses alongside upstream's `"inOutQuint"`. Reason: not recorded; it was flagged as low-confidence when first built and kept.
- **Ease strings take raw and non-numeric arguments.** The factories receive the raw argument text and its comma tokens: `fromPath(...)` gets the whole text between the parentheses, `wiggle` a type name, `slowMo` a flag and `expoScale` a nested ease string; numeric forms are unchanged. Reason: upstream's parser reads only numbers, and path data contains commas.
- **Parsed random eases are seeded.** Parsed `rough` and `wiggle(n, random)` draw from `new Random(1)`, where code defaults to `Random.Shared`. Reason: the parser caches what it returns, so a string must always parse to the same curve.
- **Ease strings fail loudly; steps and cubicBezier parse.** An unknown name, `irregular(…)` and `linear(…)` throw `FormatException`, the code-only two naming their `Eases` factory; `steps(n, fromStart)` and `cubicBezier(x1, y1, x2, y2)` parse to their factories; an empty string is `None`; `TryParse` serves callers that probe a string, as `ScrollObserver`'s `Sync` does to tell an ease from method names. Reason: in XAML a string is the only way to give an ease, so a typo or a removed form silently going linear could not be noticed, and a cubic Bézier or a stepped ease could not be written at all; both take a few plain numbers, while `irregular` is random and `linear` takes a list of stops. Rejected: upstream's silent `none`; throwing for all four removed forms.
- **A parsed `steps(0)` is a `FormatException`.** A `steps` count below 1 after truncation, `steps(0.5)` and `steps(NaN)` included, throws `FormatException` rather than letting `Eases.Steps`' `ArgumentOutOfRangeException` through. Reason: the parser's contract is that a bad string throws `FormatException` and `TryParse` returns false.
- **Missing trailing arguments take the factory defaults.** Reason: `"out"` and `"out(2)"` then mean what `Eases.Out()` and `Eases.Out(2)` do, so a string and the call it names agree.
- **The ease parser cache is bounded.** Past 256 entries it is cleared before inserting. Reason: callers building strings from varying numbers grew it without limit; clear-on-full stays simple, lock-free and allocation-light. Rejected: an LRU, more state for a cache of cheap parses.
- **Ease names are registered only while diagnostics are active.** See CORE-EASES.

## Non-goals

- No string form for springs; a `Spring` is passed as an object.
- No nesting beyond `expoScale`'s inner ease.

## Open items

- Untested: concurrent use and no-cache-on-throw (003, 004), empty parentheses on a fixed ease (010), ignored extra arguments (016), `fromPath` error propagation (019), factory range errors (028).
