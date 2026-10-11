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
- **CORE-EASEPARSER-002** — `Parse` shall cache its result by the trimmed input, so the same string returns the same instance.
  - Tests: `EaseParserTests.SameStringReturnsSameInstance`
- **CORE-EASEPARSER-003** (inherited) — The cache shall be process-wide, safe for concurrent callers, and never evicted.
  - Tests: untested
- **CORE-EASEPARSER-004** (inherited) — When parsing throws, `Parse` shall cache nothing, so the same string throws again.
  - Tests: untested

### Names

- **CORE-EASEPARSER-005** — When the trimmed input starts with `steps(`, `irregular(`, `linear(` or `cubicBezier(` (ordinal, case-sensitive), `Parse` shall return `Eases.None`.
  - Tests: `EaseParserTests.ResolvesToEasesNone`
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
- **CORE-EASEPARSER-011** — When the name is neither fixed nor parameterized, `Parse` shall return `Eases.None`.
  - Tests: `EaseParserTests.ResolvesToEasesNone`

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
- **CORE-EASEPARSER-028** (inherited) — `Parse` shall let a factory's `ArgumentOutOfRangeException` propagate, as for `wiggle(0)`, `rough(0)` or `expoScale(1, 1)`.
  - Tests: untested
- **CORE-EASEPARSER-029** — `Parse` shall have no form for `Spring`, `Steps`, `Linear`, `Irregular` or `CubicBezier`; their names resolve to `Eases.None` unless a requirement above throws.
  - Tests: `EaseParserTests.ResolvesToEasesNone`

### Names for diagnostics

- **CORE-EASEPARSER-030** — When diagnostics first become active (CORE-DIAGNOSTICS), every cached ease without a name shall be named after its trimmed input string.
  - Tests: `IntrospectionTests.TheDefaultEaseAndEasesParsedBeforeEnablingAreNamed`
- **CORE-EASEPARSER-031** — While diagnostics are active, a parsed parameterized ease shall carry its factory's call-form name.
  - Tests: `IntrospectionTests.EaseNamesForFactoriesAreKeptOnlyWhileDiagnosticsAreActive`

### Defaults

- **CORE-EASEPARSER-032** — When a parameterized ease is given fewer arguments than it reads, `Parse` shall leave the missing trailing ones to the factory's defaults, `expoScale` then getting no inner ease.
  - Tests: `EaseParserTests.ParsesToMatchingEase`

## Invariants

- A cached string maps to one `Ease` for the life of the process, and that ease is deterministic.
- `Parse` never returns null.

## Edge cases

- `Parse(null)` throws `NullReferenceException` from `Trim`, not `ArgumentNullException`.
- `""` and whitespace-only strings resolve to `Eases.None`.
- Only the first letter is lowered: `"INOUTQUAD"` is unknown and resolves to `None`.
- `"Linear(0, 1)"` throws `FormatException`: the deprecated-prefix check is case-sensitive, so the name becomes the fixed `linear` with arguments. `"Steps(4)"`, `"CubicBezier(…)"` and `"Irregular(5)"` are unknown names and resolve to `None`.
- `"in2)"` has no `(`, so it is an unknown name and resolves to `None`; `"in(2))"` throws because `"2)"` is not a number.
- `"in(1,)"` parses: the empty second token is never read.
- `wiggle` accepts numeric type names that are defined values, so `"wiggle(4, 1)"` is `EaseInOut`.
- `"wiggle(2.7)"` is `Wiggle(2)`; `"wiggle(0.5)"` truncates to 0 and throws `ArgumentOutOfRangeException`.
- Distinct spellings of one ease (`"out(2)"`, `"Out(2)"`, `"out( 2 )"`) are cached as distinct instances.

## Threading and lifetime

- `Parse` may be called from any thread; the cache is a `ConcurrentDictionary`. Two threads parsing one new string at once may both run the factory, and one result wins.
- Cached eases live for the process.

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
- The removed string forms return `None` without upstream's console warning.

## Decisions

- **Ease strings accept PascalCase names.** A leading capital is lowered, so `"InOutQuint"` parses alongside upstream's `"inOutQuint"`. Reason: not recorded; it was flagged as low-confidence when first built and kept.
- **Ease strings take raw and non-numeric arguments.** The factories receive the raw argument text and its comma tokens: `fromPath(...)` gets the whole text between the parentheses, `wiggle` a type name, `slowMo` a flag and `expoScale` a nested ease string; numeric forms are unchanged. Reason: upstream's parser reads only numbers, and path data contains commas.
- **Parsed random eases are seeded.** Parsed `rough` and `wiggle(n, random)` draw from `new Random(1)`, where code defaults to `Random.Shared`. Reason: the parser caches what it returns, so a string must always parse to the same curve.
- **Removed string forms parse to `None`.** `steps(`, `irregular(`, `linear(` and `cubicBezier(` return `Eases.None` instead of throwing like other bad input. Kept from upstream, which removed those forms; no other reason recorded.
- **Missing trailing arguments take the factory defaults.** Reason: `"out"` and `"out(2)"` then mean what `Eases.Out()` and `Eases.Out(2)` do, so a string and the call it names agree.
- **Ease names are registered only while diagnostics are active.** See CORE-EASES.

## Non-goals

- No string form for springs; a `Spring` is passed as an object.
- No nesting beyond `expoScale`'s inner ease.

## Open items

- `"Linear(…)"` throws `FormatException` while `"linear(…)"`, `"Steps(…)"`, `"CubicBezier(…)"` and `"Irregular(…)"` resolve to `None`: the deprecated-prefix check is case-sensitive though names are not.
- `Parse(null)` throws `NullReferenceException` rather than `ArgumentNullException`.
- The cache is unbounded; a caller building ease strings from varying numbers grows it without limit.
- Untested: concurrent use and no-cache-on-throw (003, 004), empty parentheses on a fixed ease (010), ignored extra arguments (016), `fromPath` error propagation (019), factory range errors (028).
