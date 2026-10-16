---
id: BRIDGE-SPLITFLAP
title: Split-flap display
package: MonacoLabs.Presto.Avalonia
namespaces:
  - MonacoLabs.Presto.Controls
sources:
  - MonacoLabs.Presto.Avalonia/Controls/SplitFlap.cs
types:
  - SplitFlap
tests:
  - SplitFlapTests
  - BridgeAllocationTests
  - TypeLabPageTests
upstream: []
depends:
  - BRIDGE-TRANSFORMS
  - BRIDGE-EFFECTS
  - BRIDGE-TOKENS
  - CORE-TIMER
  - CORE-REDUCED
---

# Split-flap display

## Purpose

A row of character cells that flip like a departures board: when `Text` changes, each cell steps forward through `Charset` to its new character, folding its halves in perspective. It is an untemplated control, like `Flip` (BRIDGE-FLIP): the cells are its own children, drawn with its font, `Foreground`, `CellBackground` and `CornerRadius`. The fold is a `Rotate3DTransform` with the 3D shorthands' perspective (BRIDGE-EFFECTS), and one engine timer (CORE-TIMER) drives every cell.

## Requirements

### Cells

- **BRIDGE-SPLITFLAP-001** — The control shall show `Cells` cells, or one per character of `Text` when `Cells` is 0, padding the text with spaces and truncating it.
  - Tests: `SplitFlapTests.TheBoardIsNamedByItsTextAndItsCellsAreRaw`
- **BRIDGE-SPLITFLAP-002** — When `Charset` contains no lowercase letter, the control shall show `Text` upper-cased in the invariant culture; otherwise as it is.
  - Tests: `SplitFlapTests.AnUppercaseCharsetShowsTheTextUpperCased`
- **BRIDGE-SPLITFLAP-003** — Each cell shall be a top half and a bottom half, each clipped to its half of the cell and drawn with the control's font, `Foreground`, `CellBackground` and `CornerRadius` (top corners on the top half, bottom corners on the bottom one), the cells `CellSpacing` apart; every cell shall be as wide as the charset's widest character plus half an em.
  - Tests: manual — the drawn halves need a rendered frame; checked by eye on the Desktop Type lab page's departures board.

### Flipping

- **BRIDGE-SPLITFLAP-004** — When `Text` changes, each cell whose character differs shall step forward through `Charset`, wrapping, one step per `FlipDuration`, cell `i` starting `i × CellStagger` after the change.
  - Tests: `SplitFlapTests.ACellStepsThroughTheCharsetInOrderAndWraps`, `SplitFlapTests.CellsStartTheirStaggerApart`
- **BRIDGE-SPLITFLAP-005** — A cell whose shown or target character is not in `Charset` shall take one step straight to the target.
  - Tests: `SplitFlapTests.ACharacterOutsideTheCharsetTakesOneStep`
- **BRIDGE-SPLITFLAP-006** — A step shall fold the top half of the old character from `RotateX` 0 to −90 over its first half with an in-ease, then unfold the bottom half of the new character from 90 to 0 with an out-ease, both with perspective `Recipes.FlipPerspective`; under them, the top half shows the new character and the bottom half the old one until the step ends.
  - Tests: `SplitFlapTests.ATextChangeMidFlipCarriesOnFromWhatTheCellShows`
- **BRIDGE-SPLITFLAP-007** — A `Text` change mid-flip shall retarget each cell from the character it shows, without restarting its current step or a pending stagger.
  - Tests: `SplitFlapTests.ATextChangeMidFlipCarriesOnFromWhatTheCellShows`

### Lifetime and accessibility

- **BRIDGE-SPLITFLAP-008** — Once every cell rests, the control shall pause its timer, so the engine stops requesting frames.
  - Tests: `SplitFlapTests.ABoardAtRestStopsRequestingFrames`
- **BRIDGE-SPLITFLAP-009** — The control's automation name shall be `Text`, and the cells shall be `AccessibilityView.Raw`.
  - Tests: `SplitFlapTests.TheBoardIsNamedByItsTextAndItsCellsAreRaw`
- **BRIDGE-SPLITFLAP-010** — When the control leaves the visual tree, it shall stop and show the targets; a `Text` change while it has no visual root shall show them at once.
  - Tests: `SplitFlapTests.LeavingTheTreeShowsTheTargets`, `SplitFlapTests.AnUppercaseCharsetShowsTheTextUpperCased`
- **BRIDGE-SPLITFLAP-011** — While the engine is hidden, the cells shall not advance (the engine's own pause).
  - Tests: untested

### Reduced motion

- **BRIDGE-SPLITFLAP-012** — While `ReducedMotion` is set at a `Text` change, changed cells shall show their targets at once and fade in from 0 over `Recipes.Tokens.Short`.
  - Tests: `SplitFlapTests.ReducedShowsTheTargetsAtOnceAndFadesTheChangedCells`

### Defaults

- **BRIDGE-SPLITFLAP-013** — `Charset` shall default to a space, `A`–`Z`, `0`–`9` and `.:-/`, in that order; `FlipDuration` to 60 ms; `CellStagger` to 50 ms (`MotionTokens.Standard.StaggerStep`); `CellSpacing` to 2; `Cells` to 0.
  - Tests: `SplitFlapTests.ACellStepsThroughTheCharsetInOrderAndWraps`

### Allocation

- **BRIDGE-SPLITFLAP-014** — A tick shall allocate nothing beyond the property changes it writes.
  - Tests: `BridgeAllocationTests.AFlippingSplitFlapTicksWithinBudget`

## Invariants

- At rest, both halves of a cell show its target and neither flap is visible.
- At most one flap of a cell is visible at a time.
- The timer runs only while some cell is waiting or flipping.

## Edge cases

- A `FlipDuration` of 0 or less lands each cell on its target when its stagger elapses.
- A cell whose pending target is changed back to what it shows rests when its stagger elapses, without a full turn of the charset.
- A duplicate character in `Charset` steps from its first occurrence.
- Cells are UTF-16 characters, not graphemes: a surrogate pair takes two cells.
- Growing `Cells` (or `Text`'s length while `Cells` is 0) adds cells showing a space; shrinking it removes the last cells.

## Threading and lifetime

- UI thread only. The timer belongs to `Engine.Current` at the first animated change and is paused, not canceled, at rest and on detach, so it is reused; it is not tracked by `WhenDetached`, since the control lands itself on detach.

## Reduced motion

- The fade uses `Recipes.Tokens.Ease`. A change outside a visual tree lands without a fade.

## Allocation

- Every per-cell visual, transform and text is built when cells are added or a change arrives; a tick reads a precomputed charset index and writes flap angles, visibility and, at a step's end, cached one-character strings.

## Provenance

- No upstream counterpart.

## Decisions

- **`SplitFlap` is engine-driven and UI-thread only.** One engine timer advances every cell, so mid-flip retargeting is a state change, not a rebuild. `RotateX` needs the bridge's `Rotate3DTransform`, which the compositor lacks (BRIDGE-RECIPES-008).
- **`SplitFlap` upper-cases when its charset has no lowercase.** Reason: that is what a real board does, and lowercase input would otherwise flip in one step.
- **The flaps own their transforms.** Each flap carries its own `Rotate3DTransform` with depth `Recipes.FlipPerspective` instead of going through the `RotateX`/`Perspective` shorthands. Reason: the flaps are the control's private visuals that nothing else animates, so composing is not needed, and a direct angle write keeps the tick within one property change per cell.
- **A control, not a recipe.** Reason: it owns per-cell state and draws tiles a `TextBlock` cannot (from the kinetic typography proposal's "Recipes vs. not").

## Non-goals

- Templating or theming: the bridge ships no themes (see BRIDGE-SHEET).
- Flipping backwards, sound, or per-cell charsets.
- Grapheme cells.

## Open items

- The drawn halves (003) are checked only by eye; the engine-hidden pause (011) has no test of its own.
