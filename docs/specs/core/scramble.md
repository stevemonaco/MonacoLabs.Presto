---
id: CORE-SCRAMBLE
title: Text scrambling
package: MonacoLabs.Presto
namespaces:
  - MonacoLabs.Presto
sources:
  - MonacoLabs.Presto/Core/TextScrambler.cs
  - MonacoLabs.Presto/ScrambleTextParams.cs
  - MonacoLabs.Presto/ScrambleFrom.cs
  - MonacoLabs.Presto/Presto.cs
types:
  - ScrambleTextParams
  - ScrambleFrom
tests:
  - ScrambleTextTests
upstream:
  - src/text/scramble.js
  - tests/suites/text.test.js
depends:
  - CORE-ANIMATION
  - CORE-KEYFRAMES
  - CORE-TIMELINE
  - CORE-UTILS
  - CORE-EASES
  - CORE-TARGETS
---

# Text scrambling

## Purpose

Initially ported from anime.js `scrambleText`. `Presto.ScrambleText` is a chained function value for a string property that fills the text with random characters and settles it along a reveal wave. It works on any target with a string `Text` property.

## Requirements

### Scramble

- **CORE-SCRAMBLE-001** — `Presto.ScrambleText` shall return a chained function value that resolves, per target, to a tween from 0 to 1 with a linear ease, the scramble's duration and delay, and a text modifier.
  - Tests: `ScrambleTextTests.IsAFunctionBasedValue`, `ScrambleTextTests.ReturnsATweenWithFromToEaseDurationAndModifier`
- **CORE-SCRAMBLE-002** — The tween's duration shall be `Duration` when positive, and otherwise `(n − 1) × 1000 / RevealRate + SettleDuration` with `n` the longer of the start and end text (the end text when starting blank), plus `RevealDelay`, rounded to a whole time unit and scaled by `Globals.TimeScale`.
  - Tests: untested
- **CORE-SCRAMBLE-003** — `Chars` shall be a set name (`lowercase`, `uppercase`, `numbers`, `symbols`, `braille`, `blocks`, `shades`) when it equals one, and otherwise literal characters with `a-z` ranges expanded and a `-` at either end kept literal.
  - Tests: `ScrambleTextTests.ScramblesWithTheCustomCharacters`
- **CORE-SCRAMBLE-004** — The scramble shall start from the target's `Text` property as `Presto.Get` reads it, or from the text the previous tween of the property ended on when there is one.
  - Tests: `ScrambleTextTests.KeyframesStartFromThePreviousFramesText`
- **CORE-SCRAMBLE-005** — The scramble shall settle on `Text` when set, else on its start text when chained, else on the target's cached original text (CORE-SCRAMBLE-028).
  - Tests: `ScrambleTextTests.SettlesOnTheCustomText`, `ScrambleTextTests.SettlesOnThePreviousScramblesTextWhenChained`, `ScrambleTextTests.ModifierReturnsTheTargetTextAtOne`
- **CORE-SCRAMBLE-006** — At progress 0 of an unchained scramble, `Override` (the default) shall show every non-space character of the start text replaced by one from `OverrideChars` (default `Chars`); an empty `OverrideChars` shall start from no text, and `Override` false shall leave the start text until the reveal reaches each character.
  - Tests: `ScrambleTextTests.DefaultOverrideScramblesTheTextAtTheStart`, `ScrambleTextTests.OverrideFalsePreservesTheOriginalTextAtTheStart`
- **CORE-SCRAMBLE-007** — At progress 1 the modifier shall return the settled text exactly; in between, each character shall show its settled character once the reveal has passed it, a scramble character while inside its settle window, and its start state before.
  - Tests: `ScrambleTextTests.ModifierReturnsScrambledTextInBetween`, `ScrambleTextTests.RevealsMoreCharactersAsItProgresses`, `ScrambleTextTests.AnimateEndsOnTheTargetText`
- **CORE-SCRAMBLE-008** — A position that is a space in the start or the settled text shall stay a space while it scrambles.
  - Tests: `ScrambleTextTests.ModifierPreservesWhitespace`
- **CORE-SCRAMBLE-009** — Called twice with the same progress, the modifier shall return the same string instance.
  - Tests: `ScrambleTextTests.ModifierCachesTheValueOfTheSameInput`
- **CORE-SCRAMBLE-010** — Scramble characters shall be redrawn only when progress crosses a `1 / SettleRate`-second step.
  - Tests: untested
- **CORE-SCRAMBLE-011** — The reveal shall run outwards from `FromIndex` when set, else from the left, the right, the middle or in a seeded random order per `From`, with `Auto` meaning right when the text shrinks and left otherwise; `Reversed` shall invert the order.
  - Tests: untested
- **CORE-SCRAMBLE-012** — When shrinking text reveals from the center or a `FromIndex`, its characters shall settle only after every extra character has gone.
  - Tests: untested
- **CORE-SCRAMBLE-013** — `Perturbation` shall offset each character's window start and end randomly by up to that fraction of the settle window.
  - Tests: untested
- **CORE-SCRAMBLE-014** — `Cursor` characters shall run ahead of the reveal, its last character leading.
  - Tests: untested
- **CORE-SCRAMBLE-015** — With `Seed` set, the scramble shall be reproducible; without it, it shall draw from the shared generator.
  - Tests: untested
- **CORE-SCRAMBLE-016** — During `Delay` the target shall show its unchanged start text, and during `RevealDelay` it shall scramble without revealing any character.
  - Tests: untested
- **CORE-SCRAMBLE-017** — `Ease`, resolved per target, shall spread the reveal along its curve while the tween itself stays linear.
  - Tests: untested
- **CORE-SCRAMBLE-018** — Reverting a scramble shall restore the text the target had before it.
  - Tests: `ScrambleTextTests.RevertRestoresTheOriginalText`
- **CORE-SCRAMBLE-028** — When an unchained scramble starts, it shall cache the target's current text as its original unless that text is the last one a scramble wrote to the target, so a scramble interrupting a running one still settles on the real text while a text the app set since is honored.
  - Tests: `ScrambleTextTests.AnUnchainedScrambleSettlesOnTextTheAppChangedSince`, `ScrambleTextTests.AScrambleInterruptingARunningOneSettlesOnTheOriginalText`

### Defaults

- **CORE-SCRAMBLE-019** — When `ScrambleTextParams.Chars` is not set, the scramble shall draw from `a-zA-Z0-9!%#_`.
  - Tests: untested
- **CORE-SCRAMBLE-020** — When `From` is not set, it shall be `Auto`, and when `Reversed` is not set, the order shall not be inverted.
  - Tests: untested
- **CORE-SCRAMBLE-021** — When `Perturbation` is not set, it shall be 0, so no window is offset.
  - Tests: untested
- **CORE-SCRAMBLE-022** — When `RevealRate` is not set, the reveal shall advance 60 characters per second.
  - Tests: untested
- **CORE-SCRAMBLE-023** — When `SettleRate` is not set, scramble characters shall be redrawn 30 times per second.
  - Tests: untested
- **CORE-SCRAMBLE-024** — When `SettleDuration` is not set, each character's settle window shall last 300 ms, scaled by `Globals.TimeScale`.
  - Tests: untested
- **CORE-SCRAMBLE-025** — When `Delay` or `RevealDelay` is not set, it shall be 0.
  - Tests: untested
- **CORE-SCRAMBLE-026** — When `Ease` is not set, the reveal shall be spread linearly.
  - Tests: untested
- **CORE-SCRAMBLE-027** — When `Cursor` is not set, no cursor characters shall run ahead of the reveal.
  - Tests: untested

## Invariants

- A scramble's modifier returns exactly the settled text at progress 1 and never a string longer than the longer of its two texts.

## Edge cases

- `ScrambleText` reads `"Text"` whatever property the animation writes.

## Threading and lifetime

- Runs on the engine's thread like any tween; the per-target original and last-write cache holds targets weakly.

## Reduced motion

- Not affected: scrambling is not motion.

## Allocation

- A scramble frame builds one string per distinct progress value; that is by design.

## Provenance

- Scramble's function-based `chars`, `text`, `duration` and `delay` and its `onChange` callback are not ported.
- The original-text cache refreshes where upstream's never does (see "The original-text cache refreshes unless the text is the scrambler's own").

## Decisions

- **ScrambleText is a core value function.** `Presto.ScrambleText` scrambles any target's string `Text` property. Reason: it is pure, built only from core types, and upstream's `scrambleText` is not DOM-bound; only `splitText` needs a UI framework. Rejected: keeping a forwarder in the bridge (BRIDGE-TEXT), which would give one feature two entry points.
- **Unset options reveal plainly; the rates are upstream's.** Without `From`, `Reversed`, `Perturbation`, delays, `Ease` or `Cursor`, the reveal runs in order, linearly and at once (CORE-SCRAMBLE-020, 021, 025 to 027). Reason: each option then adds one effect to a plain reveal. The character set, 60 characters per second, 30 redraws per second and the 300 ms settle window (CORE-SCRAMBLE-019, 022 to 024) are kept from upstream; no other reason recorded.
- **The original-text cache refreshes unless the text is the scrambler's own.** Deviates from upstream's `WeakMap`, which keeps a target's first text forever (CORE-SCRAMBLE-028). Reason: the cache exists so an interrupting scramble settles on the real text, which the own-output test keeps, while an app's later text change is honored. Rejected: always the current text (an interrupting scramble settles on garbage), keeping upstream (stale text after any app change).

## Non-goals

- Rich text or inline formatting.

## Open items

- Untested: the scramble duration formula (002), redraw stepping (010), reveal order and `From` (011, 012), `Perturbation` (013), `Cursor` (014), `Seed` (015), delays (016), `Ease` (017).
