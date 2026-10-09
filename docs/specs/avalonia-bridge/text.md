---
id: BRIDGE-TEXT
title: Text splitting
package: Monaco.Presto.AvaloniaBridge
namespaces:
  - Monaco.Presto.AvaloniaBridge.Text
  - Monaco.Presto.AvaloniaBridge
sources:
  - Monaco.Presto.AvaloniaBridge/Text/TextSplitter.cs
  - Monaco.Presto.AvaloniaBridge/Text/SplitTextParams.cs
  - Monaco.Presto.AvaloniaBridge/Text/SplitPieceParams.cs
  - Monaco.Presto.AvaloniaBridge/PrestoAvalonia.cs
types:
  - TextSplitter
  - SplitTextParams
  - SplitPieceParams
tests:
  - TextSplitTests
upstream:
  - src/text/split.js
  - tests/suites/text.test.js
depends:
  - BRIDGE-INIT
  - BRIDGE-TARGETS
  - CORE-TIMER
  - CORE-ANIMATION
  - CORE-KEYFRAMES
  - CORE-TIMELINE
  - CORE-UTILS
  - CORE-SCOPE
  - CORE-EASES
---

# Text splitting

## Purpose

Port of anime.js `splitText`. A `TextSplitter` takes a `TextBlock`'s text apart into line, word and character controls laid out inside the block, re-splits when the block's width changes, and runs registered effects over each split. Per-piece triggers built on the splitter are BRIDGE-TEXTREVEAL. Also specifies `PrestoAvalonia.SplitText`.

## Requirements

### Splitting

- **BRIDGE-TEXT-001** — `SplitText` shall initialize the bridge and, with no parameters, split into word pieces only, without spaces in them.
  - Tests: `TextSplitTests.DefaultsToWordsOnly`
- **BRIDGE-TEXT-002** — When created inside an ambient scope, the splitter shall register with it.
  - Tests: untested
- **BRIDGE-TEXT-003** — The splitter shall split at once when the block has a positive width, and otherwise at its first size change.
  - Tests: `TextSplitTests.EffectRunsOnceAfterTheSplit`
- **BRIDGE-TEXT-004** — The splitter shall break the text into lines by measuring a copy of the block with its font family, size, style, weight, stretch, letter spacing, wrapping and padding at the block's width, trimming trailing whitespace from each line.
  - Tests: `TextSplitTests.SplitsLinesOnly`, `TextSplitTests.SplitsWordsAndLines`
- **BRIDGE-TEXT-005** — `LineHeight` shall be the height of the first measured line, available inside an effect before the pieces are laid out.
  - Tests: `TextSplitTests.MeasuresTheLineHeightBeforeThePiecesAreLaidOut`
- **BRIDGE-TEXT-006** — With `Lines` set, each wrapped line shall become one line piece; with neither words nor characters on, the line piece shall hold the line's text.
  - Tests: `TextSplitTests.SplitsLinesOnly`, `TextSplitTests.SplitsLinesAndChars`
- **BRIDGE-TEXT-007** — With `Words` set, each run of non-whitespace shall become one word piece, with each whitespace run left as an unrecorded text piece between them.
  - Tests: `TextSplitTests.DefaultsToWordsOnly`, `TextSplitTests.SplitsWordsAndLines`
- **BRIDGE-TEXT-008** — With `Chars` set, each grapheme cluster of a word shall become one character piece, nested inside its word piece when words are on too.
  - Tests: `TextSplitTests.SplitsCharsOnly`, `TextSplitTests.SplitsWordsAndChars`, `TextSplitTests.SplitsLinesWordsAndChars`
- **BRIDGE-TEXT-009** — With `IncludeSpaces`, the whitespace pieces shall be dropped and a no-break space appended to every word that is not the last on its line, inside its last character piece when characters are on.
  - Tests: `TextSplitTests.IncludeSpacesKeepsTheFollowingSpaceInsideThePiece`
- **BRIDGE-TEXT-010** — A kind's `Class` shall be added to the style classes of every piece of that kind.
  - Tests: `TextSplitTests.AppliesTheCustomStyleClasses`
- **BRIDGE-TEXT-011** — A kind's `Wrap` shall put each of its pieces inside its own clipping `Border`, while `Lines`/`Words`/`Chars` list the inner pieces.
  - Tests: `TextSplitTests.WrapsEachPieceInAClippingContainer`
- **BRIDGE-TEXT-012** — Every recorded piece shall carry `LineIndexProperty`, the index of the wrapped line it sits on, and its position among its kind's pieces on that line, both set before any effect runs.
  - Tests: `TextSplitTests.EveryPieceKnowsItsLineInsideAnEffect`, `TextSplitTests.StaggerByLineStepsByLineAndByPositionInTheLine`
- **BRIDGE-TEXT-013** (inherited) — While split, the block's `Text` shall be null and its `Inlines` one `InlineUIContainer` whose root keeps the block at the height of its plain text.
  - Tests: `TextSplitTests.SplittingKeepsTheHostTheHeightOfItsPlainText`
- **BRIDGE-TEXT-014** — While split, the block's automation name shall be its text and every piece shall be hidden from automation (`AccessibilityView.Raw`).
  - Tests: untested
- **BRIDGE-TEXT-015** — While split, the block's `ClipToBounds` shall be false at `Animation` priority, so any local or style value comes back on revert.
  - Tests: `TextSplitTests.TurnsOffTheHostClipWhileSplitAndRestoresItOnRevert`, `TextSplitTests.AHostThatDidNotClipStillDoesNotAfterRevert`
- **BRIDGE-TEXT-016** — Only the block's plain text shall be split; inline formatting is not carried onto the pieces.
  - Tests: untested

### Re-split and effects

- **BRIDGE-TEXT-017** — After the first split, a width change shall restart a 150 ms debounce, and when it completes with a width different from the last split the splitter shall split again: cleanups first, then the effects over the new pieces.
  - Tests: `TextSplitTests.EffectRunsAgainWhenTheWidthChanges`
- **BRIDGE-TEXT-018** — `AddEffect` shall run the effect at once when the pieces exist and again after every split, running the `Action` it returned before the next split and on revert.
  - Tests: `TextSplitTests.EffectRunsOnceAfterTheSplit`, `TextSplitTests.CleanupSeesThePiecesItsEffectWasGiven`
- **BRIDGE-TEXT-019** — The `Timer` overload of `AddEffect` shall hand the returned timer's playback position to the timer the next split creates (`Utils.KeepTime`), and revert the last one with the splitter.
  - Tests: `TextSplitTests.TheAnimationAnEffectReturnsKeepsItsTimeAcrossARespilt`, `TextSplitTests.RevertRevertsTheAnimationAnEffectReturned`
- **BRIDGE-TEXT-020** — `Refresh` shall split again at the current width, running the cleanups and then the effects.
  - Tests: `TextSplitTests.RefreshRunsTheCleanupsAndTheEffectsAgain`
- **BRIDGE-TEXT-021** — `Revert` shall stop responding to resizes, run the cleanups, empty the piece lists, put back the text the block had when the splitter was created, and clear the automation name and the clip override.
  - Tests: `TextSplitTests.RevertRunsTheCleanupsAndStopsRespondingToResizes`, `TextSplitTests.RevertRestoresTheOriginalTextBlock`
- **BRIDGE-TEXT-054** — `SlotOf` shall return the piece's layout box in the block's coordinates, before transforms, for any piece of the current split, valid from the first effect of that split; for a control that is not a piece of this splitter it shall throw `ArgumentException`.
  - Tests: `TextSplitTests.SlotOfGivesEachPiecesLayoutBoxInBlockCoordinates`, `TextSplitTests.SlotOfIsValidInsideTheFirstEffect`, `TextSplitTests.SlotOfIsValidInsideTheFirstEffectOnACenteredBlock`, `TextSplitTests.SlotOfIgnoresRenderTransforms`, `TextSplitTests.SlotOfFollowsAResplit`, `TextSplitTests.SlotOfRejectsAControlThatIsNotAPieceOfThisSplit`

### Scramble

- ~~**BRIDGE-TEXT-022**~~ — Moved to CORE-SCRAMBLE.
- ~~**BRIDGE-TEXT-023**~~ — Moved to CORE-SCRAMBLE.
- ~~**BRIDGE-TEXT-024**~~ — Moved to CORE-SCRAMBLE.
- ~~**BRIDGE-TEXT-025**~~ — Moved to CORE-SCRAMBLE.
- ~~**BRIDGE-TEXT-026**~~ — Moved to CORE-SCRAMBLE.
- ~~**BRIDGE-TEXT-027**~~ — Moved to CORE-SCRAMBLE.
- ~~**BRIDGE-TEXT-028**~~ — Moved to CORE-SCRAMBLE.
- ~~**BRIDGE-TEXT-029**~~ — Moved to CORE-SCRAMBLE.
- ~~**BRIDGE-TEXT-030**~~ — Moved to CORE-SCRAMBLE.
- ~~**BRIDGE-TEXT-031**~~ — Moved to CORE-SCRAMBLE.
- ~~**BRIDGE-TEXT-032**~~ — Moved to CORE-SCRAMBLE.
- ~~**BRIDGE-TEXT-033**~~ — Moved to CORE-SCRAMBLE.
- ~~**BRIDGE-TEXT-034**~~ — Moved to CORE-SCRAMBLE.
- ~~**BRIDGE-TEXT-035**~~ — Moved to CORE-SCRAMBLE.
- ~~**BRIDGE-TEXT-036**~~ — Moved to CORE-SCRAMBLE.
- ~~**BRIDGE-TEXT-037**~~ — Moved to CORE-SCRAMBLE.
- ~~**BRIDGE-TEXT-038**~~ — Moved to CORE-SCRAMBLE.
- ~~**BRIDGE-TEXT-039**~~ — Moved to CORE-SCRAMBLE.

### Defaults

- **BRIDGE-TEXT-040** — When `SplitTextParams.Words` is not set, the splitter shall split words, so parameters that set only `Lines` or `Chars` still give word pieces, as anime.js does.
  - Tests: `TextSplitTests.SplitsWordsAndLines`, `TextSplitTests.SplitsWordsAndChars`
- **BRIDGE-TEXT-041** — When `SplitTextParams.Lines` or `Chars` is not set, the splitter shall make no pieces of that kind, as anime.js does.
  - Tests: `TextSplitTests.SplitsWordsAndChars`
- **BRIDGE-TEXT-042** — When `IncludeSpaces` is not set, it shall be false, as anime.js does.
  - Tests: `TextSplitTests.SplitsWordsAndChars`
- **BRIDGE-TEXT-043** — When a kind's `Class` is not set, its pieces shall get no added style class, as anime.js does.
  - Tests: untested
- **BRIDGE-TEXT-044** — When a kind's `Wrap` is not set, its pieces shall not be wrapped in a clipping `Border`, as anime.js does.
  - Tests: untested
- ~~**BRIDGE-TEXT-045**~~ — Moved to CORE-SCRAMBLE.
- ~~**BRIDGE-TEXT-046**~~ — Moved to CORE-SCRAMBLE.
- ~~**BRIDGE-TEXT-047**~~ — Moved to CORE-SCRAMBLE.
- ~~**BRIDGE-TEXT-048**~~ — Moved to CORE-SCRAMBLE.
- ~~**BRIDGE-TEXT-049**~~ — Moved to CORE-SCRAMBLE.
- ~~**BRIDGE-TEXT-050**~~ — Moved to CORE-SCRAMBLE.
- ~~**BRIDGE-TEXT-051**~~ — Moved to CORE-SCRAMBLE.
- ~~**BRIDGE-TEXT-052**~~ — Moved to CORE-SCRAMBLE.
- ~~**BRIDGE-TEXT-053**~~ — Moved to CORE-SCRAMBLE.

## Invariants

- A block is split by at most one live splitter at a time as far as `TextReveal` lookups are concerned; each piece records the splitter that made it.
- Piece lists are empty whenever the block is not split.

## Edge cases

- With `Lines`, `Words` and `Chars` all null the block is still re-laid out as one plain row per line, with no recorded pieces.
- `Refresh` after `Revert` splits again but no longer follows resizes.
- Text changed on the block after the splitter was created is overwritten by the original on revert.

## Threading and lifetime

- UI thread only. The resize debounce is an engine timer; the splitter listens to the block's `SizeChanged` until reverted.
- Splitters and their debounce timer belong to the ambient scope when one is active.

## Reduced motion

- Not affected: splitting is not motion. The animations built on them state their own reduced forms (BRIDGE-TEXTREVEAL, BRIDGE-RECIPES).

## Allocation

- Not on the tick path.

## Deviations from anime.js

- Pieces are `Control`s inside the block's `InlineUIContainer`, not DOM spans; the piece tree comes from `SplitPieceParams` instead of HTML templates, and upstream's `clone`, `debug`, function templates and `wrap` modes other than clip are not ported.
- Accessibility uses the block's automation name instead of a visually hidden copy.
- The host's clip is turned off while split, because a `TextBlock` clips to its bounds by default and would cut off pieces transformed past the line box, where upstream's spans overflow freely.
- Upstream wraps every effect in `keepTime`; here only the `Timer` overload is, since an `Action` effect has no time to keep.
- Lines are measured on a styled copy of the block rather than by reading rendered word positions, so no font-ready wait exists.

## Decisions

- **Line-aware stagger and line height are public helpers.** `TextSplitter.StaggerByLine` returns a function value stepping by line index and by position in the line, and `LineHeightOf` reads the owning split's `LineHeight` (0 for a non-piece). Reason: recipes must say "from one line down" before pieces have bounds, which the Typography sample page had computed by hand.
- **`SlotOf` instead of a `Measure` API.** `MorphText` captures the old slots, sets the new text, splits it, and FLIPs each new piece from its partner's old slot. Nothing has to be measured offscreen. Rejected: `TextSplitter.Measure(block, text)`, a second layout path that would have to match the real one.
- **The split lays its pieces out itself.** Effects run before the host's next layout pass, so the split measures and arranges its piece tree at the block's padding and text alignment, where the host's arrange puts it. Reason: `SlotOf` and exit ghosts need bounds from the first effect, without forcing a layout pass on the host from inside its own `SizeChanged`.

## Non-goals

- Rich text or inline formatting; `TextBox` or other text controls.
- Timeline children that re-point to new pieces after a re-split; effects rebuild instead.

## Open items

- The 150 ms resize debounce is not scaled by `Globals.TimeScale`, unlike every other bridge timer, so it misbehaves in seconds mode.
- Untested: scope registration (002), automation (014), plain-text only (016).
