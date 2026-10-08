---
id: BRIDGE-FLIP
title: Flip card
package: Monaco.Presto.AvaloniaBridge
namespaces:
  - Monaco.Presto.AvaloniaBridge.Controls
sources:
  - Monaco.Presto.AvaloniaBridge/Controls/Flip.cs
types:
  - Flip
tests:
  - FlipTests
upstream: []
depends:
  - CORE-BUILDER
  - CORE-REDUCED
  - BRIDGE-EFFECTS
  - BRIDGE-RECIPES
  - BRIDGE-TOKENS
---

# Flip card

## Purpose

A control with two faces, `Front` and `Back`, that turns over in perspective to show the back while `IsFlipped` is set. The card and the mirror on its back face are the control's own children (Flip is an untemplated control), so a page's layout and transforms are untouched. The turn is a `RotateY` (or `RotateX`) and `Perspective` animation from BRIDGE-EFFECTS, sharing `FlipIn`/`FlipOut`'s perspective (BRIDGE-RECIPES).

## Requirements

### Structure

- **BRIDGE-FLIP-001** — The control shall stack `Front` and `Back`, each in its own holder, in one card panel that is its only visual and logical child.
  - Tests: `FlipTests.TheFrontShowsBelow90DegreesAndTheBackAbove`
- **BRIDGE-FLIP-002** (inherited) — When `Front` or `Back` changes, the new control shall replace the old one in its face's holder.
  - Tests: untested
- **BRIDGE-FLIP-003** — A new card shall show its front, with its back hidden.
  - Tests: `FlipTests.TheFrontShowsBelow90DegreesAndTheBackAbove`
- **BRIDGE-FLIP-004** — The back face shall be mirrored by `ScaleTransform(-1, 1)` while `Orientation` is horizontal and `ScaleTransform(1, -1)` while vertical, so it reads the right way round once turned.
  - Tests: `FlipTests.TheBackReadsTheRightWayRoundOnceTurned`

### Turning

- **BRIDGE-FLIP-005** — When `IsFlipped` changes, the card shall turn to 180° (flipped) or 0° (not flipped) through `RotateY` while `Orientation` is horizontal, or `RotateX` while vertical, with `Perspective` 800.
  - Tests: `FlipTests.TheFrontShowsBelow90DegreesAndTheBackAbove`, `FlipTests.TheBackReadsTheRightWayRoundOnceTurned`
- **BRIDGE-FLIP-006** — On every rendered frame of a turn, the front shall be visible while the card's angle is at most 90° in magnitude and the back while it is above.
  - Tests: `FlipTests.TheFrontShowsBelow90DegreesAndTheBackAbove`
- **BRIDGE-FLIP-007** — While `Motion` is null, the turn shall last `Recipes.Tokens.Long` and ease with `Recipes.Tokens.Ease`, read at the toggle.
  - Tests: untested
- **BRIDGE-FLIP-008** — When `Motion` is set, the turn shall be a clone of it, so its timing, ease (a spring setting the duration) and other settings apply, and the builder itself is not changed.
  - Tests: `FlipTests.TheFrontShowsBelow90DegreesAndTheBackAbove`
- **BRIDGE-FLIP-009** — When `IsFlipped` is toggled mid-turn, the new turn shall start from the card's current angle.
  - Tests: `FlipTests.TogglingMidFlipTurnsBackFromWhereTheCardStands`
- **BRIDGE-FLIP-010** — When the control leaves the visual tree mid-turn, the turn shall complete at once, landing on its end angle and face.
  - Tests: `FlipTests.LeavingTheTreeMidTurnLandsTheTurn`
- **BRIDGE-FLIP-011** — When `IsFlipped` changes while the control has no visual root, the card shall be set to its end angle and perspective and the faces shall swap at once.
  - Tests: `FlipTests.ACardOutsideATreeTurnsAtOnce`
- **BRIDGE-FLIP-012** (inherited) — When `Orientation` changes, only the back face's mirror shall change; the card's current angle stays on the previous axis until the next toggle.
  - Tests: untested

### Reduced motion

- **BRIDGE-FLIP-013** — While `Presto.Defaults.ReducedMotion` is true at a toggle, the card shall be set to its end angle and the faces shall swap at once.
  - Tests: `FlipTests.ReducedSwapsTheFacesAtOnce`

### Landing

- **BRIDGE-FLIP-014** — When a turn ends, the card shall rest exactly at its end angle with the face for that angle shown.
  - Tests: `FlipTests.TogglingMidFlipTurnsBackFromWhereTheCardStands`

### Defaults

- **BRIDGE-FLIP-015** (inherited) — When `IsFlipped` is not set, the card shall show its front.
  - Tests: `FlipTests.TheFrontShowsBelow90DegreesAndTheBackAbove`
- **BRIDGE-FLIP-016** (inherited) — When `Orientation` is not set, the card shall turn horizontally, about its vertical axis.
  - Tests: untested

## Invariants

- Exactly one face is visible at any time.
- The card's angle rests at 0° or 180° once a turn ends.
- The faces never carry the rotation themselves; it lives on the card panel.

## Edge cases

- A null face leaves its holder empty; the card still turns.
- Setting `IsFlipped` in XAML before the control is in a tree lands it at once, with no turn on first show.
- A `Motion` with its own callbacks or loop is honored as given, since it is cloned whole.

## Threading and lifetime

- UI thread only. The turn runs on `Engine.Current` against the internal card panel; detaching completes it, so no animation outlives the control's visual tree.

## Reduced motion

- Read at each toggle: the card is set to its end angle and the faces swap at once.

## Allocation

- Not on the tick path beyond the turn's own animation; each rendered frame reads the angle back and sets two `IsVisible` values.

## Deviations from anime.js

- No upstream counterpart.

## Decisions

- **Flip is an untemplated control.** `Flip` has `Front`, `Back` and `IsFlipped`; the card that turns 0↔180° (with `Recipes.FlipPerspective`, 800) and the mirror on the back face are its own children, and it is not templated. Reason: the card and the mirror belong to the control rather than to a panel the page owns, so neither a page's layout nor its `RenderTransform` is touched and a face never sees its rotation; the bridge ships no themes. Rejected: attached properties on a page's panel.

## Non-goals

- Templating or theming: the bridge ships no themes (see BRIDGE-SHEET).
- More than two faces, partial turns or a drag-to-turn gesture.

## Open items

- `Motion` is documented as "the turn's timing and ease, the builder's only", but the code clones the whole builder without validating it (unlike `Selection.Motion`, which rejects other settings), so loops, callbacks and extra properties ride along (code wins).
- Changing `Orientation` while flipped (BRIDGE-FLIP-012) leaves the old axis at 180° and the new one at 0°, so the next toggle turns on the new axis with the old rotation still applied; untested.
- The default timing (007) and face replacement (002) have no tests.
