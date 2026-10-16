---
id: BRIDGE-TEXTREVEAL
title: Text reveals and text exits
package: MonacoLabs.Presto.Avalonia
namespaces:
  - MonacoLabs.Presto
sources:
  - MonacoLabs.Presto.Avalonia/TextReveal.cs
  - MonacoLabs.Presto.Avalonia/TextRevealExtension.cs
  - MonacoLabs.Presto.Avalonia/TextPieces.cs
  - MonacoLabs.Presto.Avalonia/Text/PieceTargets.cs
  - MonacoLabs.Presto.Avalonia/Text/PieceGhosts.cs
  - MonacoLabs.Presto.Avalonia/Text/TextSplitter.cs
  - MonacoLabs.Presto.Avalonia/Presence.cs
  - MonacoLabs.Presto.Avalonia/Animate.cs
types:
  - TextReveal
  - TextRevealExtension
  - TextPieces
tests:
  - TextRevealTests
  - PresenceTextExitTests
  - TextSplitTests
  - RecipeMarkupTests
  - TypographyPageTests
  - ScrollStoryPageTests
upstream: []
depends:
  - BRIDGE-TEXT
  - BRIDGE-PRESENCE
  - BRIDGE-ATTACH
  - BRIDGE-MOTION
  - BRIDGE-RECIPES
  - BRIDGE-MARKUP
  - CORE-UTILS
  - CORE-REDUCED
---

# Text reveals and text exits

## Purpose

`TextReveal.Pieces` retargets the triggers on a `TextBlock` onto its split pieces, so any builder (and every recipe) becomes a per-line, per-word or per-character reveal: `Presence`'s arrivals and `Animate.OnAttached`/`OnTapped` split the block with BRIDGE-TEXT's `TextSplitter`, play on the pieces and rejoin the text when done. A leaving or hidden block with `Pieces` exits per piece too. `TextSplitter.StaggerByLine` and `LineHeightOf` give recipes line-aware delays and distances; `TextRevealExtension` is the base of the text recipes' markup extensions (BRIDGE-MARKUP, BRIDGE-RECIPES). Also specifies `TextSplitter.StaggerByLine` and `TextSplitter.LineHeightOf`.

## Requirements

### Retargeting

- **BRIDGE-TEXTREVEAL-001** — While a `TextBlock`'s `Pieces` is not `None`, `Presence`'s arrivals and `Animate.OnAttached` and `OnTapped` shall play their builder on the block's split pieces of that kind and not on the block.
  - Tests: `TextRevealTests.InitialWordsAnimateEveryWordAndNotTheBlockWhichWaitsHiddenForTheSplit`, `TextRevealTests.CharCascadeAnimatesEveryCharacter`, `TextRevealTests.OnTappedRevealsThePiecesAndATapMidRevealRestartsIt`
- **BRIDGE-TEXTREVEAL-002** — `Lines` shall split into line pieces only, `Words` into word pieces only, and `Chars` into character pieces without word pieces.
  - Tests: `TextRevealTests.MaskWrapsEachLineInAClippingBorder`, `TextRevealTests.CharCascadeAnimatesEveryCharacter`
- **BRIDGE-TEXTREVEAL-003** — When `Mask` is true, each animated piece shall sit in its own clipping `Border`.
  - Tests: `TextRevealTests.MaskWrapsEachLineInAClippingBorder`, `TextRevealTests.LineMaskRisesEachLineBehindItsClipToRest`
- **BRIDGE-TEXTREVEAL-004** — From the trigger until the first split's effect runs, the block shall be held at opacity 0 at `Animation` priority, and released when the pieces start playing.
  - Tests: `TextRevealTests.InitialWordsAnimateEveryWordAndNotTheBlockWhichWaitsHiddenForTheSplit`
- **BRIDGE-TEXTREVEAL-005** — The pieces shall be the animation's whole target list, so index-based function values and staggers see each piece's index and the piece count rather than the panel's arrival batch.
  - Tests: untested
- **BRIDGE-TEXTREVEAL-006** — When the block re-splits mid-reveal, the animation shall be rebuilt over the new pieces at the time the old one had reached.
  - Tests: `TextRevealTests.AResizeMidRevealResplitsAndKeepsTheTime`
- **BRIDGE-TEXTREVEAL-036** — When the block's `Text` changes from outside mid-reveal, the reveal shall be rebuilt over the new text's pieces at the time the old one had reached (BRIDGE-TEXT-059), and when it completes the block shall show that text with its binding intact.
  - Tests: `TextRevealTests.ABoundTextChangedMidRevealResplitsKeepingTheTimeAndEndsOnTheNewTextStillBound`
- **BRIDGE-TEXTREVEAL-007** — When the reveal completes, the split shall be reverted so the block shows its plain text again.
  - Tests: `TextRevealTests.ACompletedRevealPutsTheTextBack`, `TextRevealTests.LineMaskRisesEachLineBehindItsClipToRest`, `ScrollStoryPageTests.AChapterHeadingMasksInByLineAsItsChapterEntersAndIsRejoinedAfter`
- **BRIDGE-TEXTREVEAL-008** — A looping reveal shall stay split.
  - Tests: `TextRevealTests.ALoopingRevealStaysSplit`
- **BRIDGE-TEXTREVEAL-009** — When the builder resolves to its `WhenReduced` form, the trigger shall play it on the unsplit block.
  - Tests: `TextRevealTests.ReducedTextRevealsFadeTheBlockUnsplit`
- **BRIDGE-TEXTREVEAL-010** — `Motion` states and `Animate.OnChanged` shall always animate the block itself.
  - Tests: `TextRevealTests.AHoverStateStillAnimatesTheBlock`
- **BRIDGE-TEXTREVEAL-011** — When the trigger fires again mid-reveal, the running reveal's split shall be reverted and a new one started from the beginning.
  - Tests: `TextRevealTests.OnTappedRevealsThePiecesAndATapMidRevealRestartsIt`
- **BRIDGE-TEXTREVEAL-012** — When the block detaches mid-reveal, the reveal shall be canceled and the split reverted; detaching before the split shall release the opacity hold.
  - Tests: `TextRevealTests.DetachingRevertsTheSplit`, `TextRevealTests.DetachingBeforeTheSplitReleasesTheHold`

### Line helpers

- **BRIDGE-TEXTREVEAL-013** — `StaggerByLine` shall return a function value giving a `Control` target `start + lineIndex × lineStep + indexInLine × pieceStep`, and any other target `start` (0 when unset).
  - Tests: `TextSplitTests.StaggerByLineStepsByLineAndByPositionInTheLine`, `TextSplitTests.StaggerByLineStartOffsetsEveryDelay`, `TextRevealTests.WordFadeStartsEachLineAfterEveryWordOfTheLineBefore`
- **BRIDGE-TEXTREVEAL-014** — `LineHeightOf` shall return the current `LineHeight` of the splitter that made the piece, following a refresh; for an exit ghost, the line height recorded on it; otherwise 0.
  - Tests: `TextSplitTests.LineHeightOfReadsTheOwningSplitAndFollowsARefresh`, `PresenceTextExitTests.LineHeightOfAGhostIsTheBlocksLineHeight`

### Exits of a leaving block

- **BRIDGE-TEXTREVEAL-015** — When a `TextBlock` with `Pieces` leaves and its exit is not a reduced form, `Presence` shall exit it as one ghost per piece in one frame canvas, the ghosts being the exit's targets in reading order.
  - Tests: `PresenceTextExitTests.AnIndexStaggerStartsTheGhostsInReadingOrder`, `PresenceTextExitTests.TextExitRecipesAnimateEveryGhostToItsEndAndDismissTheHost`, `TypographyPageTests.TheRotatorDropsTheOldWordAsCharGhostsThenRisesTheNewOneWithoutFlashing`
- **BRIDGE-TEXTREVEAL-016** — Each ghost shall stand at its piece's layout offset in the block.
  - Tests: `PresenceTextExitTests.EachGhostStandsInItsPiecesSlot`, `PresenceTextExitTests.EachGhostStandsInItsPiecesSlotOnACenteredBlock`
- **BRIDGE-TEXTREVEAL-017** — Each ghost shall carry its piece's line index, index in line and the block's line height, so `StaggerByLine` and `LineHeightOf` work on ghosts.
  - Tests: `PresenceTextExitTests.StaggerByLineStartsEveryGhostOfALineAfterEveryGhostOfTheLineBefore`, `PresenceTextExitTests.CharCascadeOutStartsEachLinesGhostsAfterTheLineBefore`, `PresenceTextExitTests.LineHeightOfAGhostIsTheBlocksLineHeight`
- **BRIDGE-TEXTREVEAL-018** — When `Mask` is true, each ghost shall be clipped to its piece's box.
  - Tests: `PresenceTextExitTests.AMaskedLineGhostIsClippedToItsLineBox`
- **BRIDGE-TEXTREVEAL-019** — A block that is not split shall be split for the snapshot, outside any ambient scope, its pieces laid out by the split (BRIDGE-TEXT-054) at the block's last width, and reverted before the exit plays; a block still split by an arrival shall be snapshotted from its live pieces, their shorthand transforms moving onto the ghosts.
  - Tests: `PresenceTextExitTests.LeavingMidRevealGhostsTheLivePiecesAsDrawn`
- **BRIDGE-TEXTREVEAL-020** — When a level has more than 256 pieces, the ghosts shall coarsen from characters to words to lines, and to one whole-element ghost when every level is over the cap.
  - Tests: `PresenceTextExitTests.OverTheCapGhostsCoarsenFromCharsToWordsToLinesToOneWholeGhost`
- **BRIDGE-TEXTREVEAL-021** — The ghosts' shared host shall be dismissed exactly once: when the last ghost's tween ends, at once when the exit is canceled, and inside the call for an instant exit.
  - Tests: `PresenceTextExitTests.TheHostIsDismissedWhenTheLastGhostEnds`, `PresenceTextExitTests.ReduceAllLeavesNoGhost`
- **BRIDGE-TEXTREVEAL-022** — When the exit is a reduced form, the block shall leave as one whole-element ghost.
  - Tests: `PresenceTextExitTests.ReducedTextExitRecipesFadeOneWholeGhostOverShort`

### Exits of a hidden block

- **BRIDGE-TEXTREVEAL-023** — When a block with `Pieces` is hidden through `IsVisible` under `HideMode` `Hold`, the exit shall play on the block's live split pieces with no ghost and no opacity hold, be rebuilt on each re-split, and leave the block whole once hidden.
  - Tests: `PresenceTextExitTests.AHeldHidePlaysOnTheLiveCharsAndHidesWholeOnceItEnds`
- **BRIDGE-TEXTREVEAL-024** — When the block is shown again mid held hide, the piece exit shall play back from where it stands.
  - Tests: `PresenceTextExitTests.ShownAgainMidHeldHideThePieceExitPlaysBack`
- **BRIDGE-TEXTREVEAL-025** — Under `HideMode` `Ghost`, the block shall collapse at once and exit through per-piece ghosts in one host.
  - Tests: `PresenceTextExitTests.AGhostHideCollapsesAtOnceAndGhostsPerPieceInOneHost`
- **BRIDGE-TEXTREVEAL-026** — When the block detaches mid held hide, the hold shall be released and the split reverted.
  - Tests: `PresenceTextExitTests.DetachingMidHeldHideReleasesTheHoldAndRevertsTheSplit`
- **BRIDGE-TEXTREVEAL-027** — Under `ReduceAll` a hide shall be instant and leave no split; a reduced held hide shall play on the unsplit block.
  - Tests: `PresenceTextExitTests.UnderReduceAllAHideIsInstantAndLeavesNoSplit`, `PresenceTextExitTests.AReducedHeldHidePlaysOnTheUnsplitBlock`
- **BRIDGE-TEXTREVEAL-028** (inherited) — When a held hide starts while an arrival's split is still in place, that split shall be reverted before the exit splits the block again.
  - Tests: untested

### Markup

- **BRIDGE-TEXTREVEAL-029** — A `TextRevealExtension` provided to a property of a `TextBlock` in XAML, `Presence.Exit` included, shall set the block's `Pieces` to its recipe's kind and, for a masked recipe, `Mask` to true, each only when not already set.
  - Tests: `RecipeMarkupTests.TextRevealExtensionsConfigureTheBlockUnlessAlreadySet`
- **BRIDGE-TEXTREVEAL-030** (inherited) — In a style `Setter` the extension shall configure nothing, so the block has to set `Pieces` itself.
  - Tests: untested
- **BRIDGE-TEXTREVEAL-031** — `LineStep` and `PieceStep` shall be passed to the recipe, which uses its own defaults when they are unset; under `Presence.Exit` the extension shall provide the recipe's exit form.
  - Tests: `RecipeMarkupTests.TextRevealExtensionsProvideTheirRecipe`, `RecipeMarkupTests.TextRevealsAreTheirExitFormOnExitAndTheirEntranceOnEnter`

### Defaults

- **BRIDGE-TEXTREVEAL-032** (inherited) — When `Pieces` is not set, it shall be `None`, so the triggers animate the block itself.
  - Tests: untested
- **BRIDGE-TEXTREVEAL-033** (inherited) — When `Mask` is not set, it shall be false, so pieces are not clipped.
  - Tests: untested
- **BRIDGE-TEXTREVEAL-034** (inherited) — When a `TextRevealExtension` subclass does not pass `mask`, the extension shall not configure `Mask`.
  - Tests: untested
- **BRIDGE-TEXTREVEAL-035** (inherited) — When `StaggerByLine`'s `pieceStep` is not set, it shall be 0, so every piece of a line gets the same delay.
  - Tests: untested

## Invariants

- A block with `Pieces` set is never visible split before its reveal starts: it is either held at opacity 0 or unsplit.
- At most one per-piece animation exists per trigger; it is replaced, not added to, on re-split.
- An exit's ghosts share one overlay host, removed once.

## Edge cases

- A block never laid out keeps its hold until it is, or until it detaches (012).
- More than 256 pieces fall back level by level (020); a block too long for lines ghosts whole.
- `Pieces` on a block whose trigger runs a reduced form never splits it, so no hold flashes.

## Threading and lifetime

- UI thread only. The per-trigger handle is an internal `IRevertible` held by `Presence`'s tracker or `Animate`'s trigger; re-trigger, detach and recycle revert it, which releases the hold and reverts the split.
- The exit-time split is created with the ambient scope cleared, so it never belongs to the scope the exit started in.

## Reduced motion

- A builder's `WhenReduced` form plays on the unsplit block (009); a reduced exit plays on one whole ghost (022); a reduced held hide plays on the unsplit block (027). The text recipes' reduced forms are `Opacity` fades over `MotionTokens.Short` (BRIDGE-RECIPES).

## Allocation

- Not on the tick path: splitting and ghosting allocate at the trigger. The per-piece animation ticks like any animation over its targets.

## Provenance

- No upstream counterpart: anime.js leaves per-piece animation to user code over `splitText`.

## Decisions

- **Piece retargeting makes every builder a text reveal; `Motion` states are excluded.** `Pieces` (`None`, `Lines`, `Words`, `Chars`) makes `Presence`'s arrivals, `Animate.OnAttached` and `OnTapped` play their builder on the block's split pieces. Reason: the recipe catalog needs no text variants; a `Motion` state's rest is defined per property on the element, and pieces come and go on re-split.
- **The block waits hidden until the split.** From the trigger until the split's first effect runs, the block is held at `Opacity` 0 at `Animation` priority; the reveal is built inside `TextSplitter.AddEffect`, so a re-split rebuilds it over the new pieces with `Utils.KeepTime`. Reason: a split needs a measured width, but the triggers fire at attach.
- **A non-looping reveal reverts its split when it completes.** A looping one keeps it, as `Typewriter` does. Reason: it returns a single text run to layout, accessibility and hit testing.
- **Masks reuse the splitter's own clip wrappers.** `Mask` splits with `SplitPieceParams.Wrap` on the chosen pieces, so a masked line rises from one line height on wrapped lines, while a per-line wipe uses `ClipInset` on unwrapped lines. Reason: the clipping `Border` wrapper already exists, and the host is already unclipped while split.
- **Reduced text reveals never split, and one attribute configures the block.** The text recipes' `WhenReduced` forms fade the whole block and retargeting is skipped for reduced params; each markup extension sets the `Pieces` and `Mask` its recipe needs unless already set. Reason: one attribute on a `TextBlock` should be enough, and reduced motion must not split.
- **The piece trigger handle is an `IRevertible`, not an `Animation`.** `Presence`'s tracker and `Animate`'s trigger store it, so re-trigger, detach and recycle revert the split and release the hold. Reason: the animation is rebuilt on every re-split.
- **A retargeted arrival does not join its panel's stagger batch.** Reason: its pieces are its own targets, so function values and staggers see each piece's index and the piece count.
- **A leaving block exits as one ghost per piece.** The detached block is split for the call, laid out in its last bounds and reverted; the ghosts carry line metadata and sit in reading order; above 256 pieces a level coarsens chars → words → lines → one whole ghost; the text recipes gain to-value `*Out` forms, reduced to a `Tokens.Short` fade of one whole ghost. Reason: index and line staggers and `LineHeightOf` then work on ghosts, the cap bounds the cost, and splitting the detached block avoids a styled clone. Rejected: a styled clone.
- **One overlay host per text exit.** The ghosts share one host, dismissed with every bitmap exactly once: on completion, on cancel, or inside the call for an instant exit. Reason: not recorded.
- **Hiding split text follows `HideMode`.** Under `Hold` the exit plays on the live pieces with no ghost and no opacity hold, and a re-show reverses it; under `Ghost` it goes through per-piece ghosts; a reduced held hide plays on the unsplit block. Reason: consistency with how `Hold` hides and re-shows any element (BRIDGE-PRESENCE).
- **Line-aware stagger and line height are public helpers.** See BRIDGE-TEXT.
- **`Typewriter` stays the code-first form.** See BRIDGE-RECIPES.

## Non-goals

- Retargeting `Animate.OnChanged` or `Motion` states.
- Rebuilding `Recipes.Typewriter` on this machinery; it stays the code-first form.

## Open items

- Untested: piece-level indexing for staggers (005), reverting an arrival's split before a held hide (028), the style-`Setter` case (030).
