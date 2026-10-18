---
id: BRIDGE-ACCORDION
title: Accordion and collapsible pane
package: MonacoLabs.Presto.Avalonia
namespaces:
  - MonacoLabs.Presto
sources:
  - MonacoLabs.Presto.Avalonia/Accordion.cs
  - MonacoLabs.Presto.Avalonia/Pane.cs
  - MonacoLabs.Presto.Avalonia/Values/GridLengthCodec.cs
types:
  - Accordion
  - Pane
tests:
  - AccordionTests
  - CollapsiblePaneTests
  - CodecTests
  - NavMotionTests
  - NavPaneTests
upstream: []
depends:
  - BRIDGE-CONTENTSIZE
  - BRIDGE-ATTACH
  - BRIDGE-PRESENCE
  - BRIDGE-TRANSFORMS
  - CORE-SPRING
  - BRIDGE-TOKENS
  - BRIDGE-CODECS
  - BRIDGE-HELPERS
  - CORE-ANIMATION
  - CORE-REDUCED
---

# Accordion and collapsible pane

## Purpose

`Accordion` is an attached form that gives an `Expander` or a `TreeViewItem` accordion motion: the item's size eases through `Animate.ContentSize` (BRIDGE-CONTENTSIZE), its body fades out before it folds and replays its entrances on each open (BRIDGE-PRESENCE), a chevron springs round, and a group keeps one expander open. `Pane` collapses and expands a `Grid` column through the pixel-only `GridLength` codec, restoring the width a splitter left and clipping the content below its minimum rather than reflowing it.

## Requirements

### Accordion body

- **BRIDGE-ACCORDION-001** — When `Accordion.Enabled` becomes true on an item outside design mode, the item shall get `Animate.ContentSize` with `Tokens.Short` and `Tokens.Ease` (duration 0 under reduced motion), so its height eases as it expands and collapses.
  - Tests: `AccordionTests.TheBodyHeightEasesOpen`, `NavMotionTests.CollapsingASectionShrinksItsBodyToZeroAndTheNextHeaderEndsBelowIt`
- **BRIDGE-ACCORDION-002** — When enabled, the item's body part (an `Expander`'s `ExpanderContent`, otherwise `PART_ItemsPresenter`, found among its own template's elements) shall get a `Presence.Exit` fading `Opacity` to 0 over `Tokens.Short` with `Tokens.Ease`, so collapsing fades the body in place before it folds.
  - Tests: `AccordionTests.CollapsingFadesTheBodyInPlaceThenFoldsIt`, `NavMotionTests.CollapsingFadesThePagesInPlaceAndThenHidesThem`
- **BRIDGE-ACCORDION-003** — When enabled, the body part shall get `Presence.EnterOnShow`, so the entrances inside it replay each time it shows.
  - Tests: `AccordionTests.TheBodysChildrenEnterEachTimeItShows`, `NavMotionTests.ExpandingRestoresTheBodyAndStaggersItsPagesBackIn`
- **BRIDGE-ACCORDION-004** (inherited) — When the item's template, or its `ExpanderHeader` toggle's template, is applied again, the accordion shall release the parts it marked and find them again in the new template.
  - Tests: `AccordionTests.ANewTemplateGetsItsPartsMarkedAndTheOldOnesReleased`, `AccordionTests.ANewHeaderTemplateGetsItsChevronMarked`
- **BRIDGE-ACCORDION-005** (inherited) — When `Accordion.Enabled` becomes false, the accordion shall clear the item's `Animate.ContentSize`, the body's `Presence.Exit` and `EnterOnShow`, the chevron mark, the chevron's transition lift and the chevron path's render transform, and stop listening for template changes.
  - Tests: `AccordionTests.DisablingReleasesEverythingItSet`
- **BRIDGE-ACCORDION-006** (inherited) — While in design mode, setting `Enabled` or `Chevron` shall do nothing.
  - Tests: `AccordionTests.DoesNothingInDesignMode`

### Chevron

- **BRIDGE-ACCORDION-007** — When enabled on an item whose `ExpanderHeader` toggle holds `ExpandCollapseChevronBorder` and `ExpandCollapseChevron` (Fluent), the accordion shall set `Chevron = 180` on the border and give the path an identity `TransformOperations` render transform, so Fluent's own flip of its `RotateTransform` does not apply; otherwise, when the item's or the toggle's template holds an element named `PART_PathIcon` (Semi), it shall set `Chevron = 180` on it. A part that already has a `Chevron` value shall keep it.
  - Tests: `AccordionTests.TheChevronSpringsRoundInPlaceOfFluentsFlip`, `AccordionTests.TheChevronIsFoundOnASemiShapedTemplate`, `AccordionTests.AnAppSetChevronIsKept`
- **BRIDGE-ACCORDION-008** — When an element with `Chevron` set is attached to a visual tree, it shall link to the first `Expander` or `TreeViewItem` up its templated-parent chain, at any depth of nested templates, and set its `Rotate` at once to `Chevron` degrees if that item is expanded, 0 otherwise.
  - Tests: `AccordionTests.TheChevronSpringsRoundInPlaceOfFluentsFlip`, `NavMotionTests.TheChevronLandsOnItsRotation`
- **BRIDGE-ACCORDION-009** — When the linked item's `IsExpanded` changes, the chevron shall animate `Rotate` to `Chevron` degrees (expanded) or 0 (collapsed) with `Springs.Snappy`.
  - Tests: `AccordionTests.TheChevronSpringsRoundInPlaceOfFluentsFlip`
- **BRIDGE-ACCORDION-010** (inherited) — When the chevron is detached, or `Chevron` is set to null, the link shall be dropped; when `Chevron` changes to another value while attached, the chevron shall relink and land on the new angle at once.
  - Tests: `AccordionTests.AChevronSetWhileAttachedLandsAtOnceAndRelinksOnAChange`, `AccordionTests.AChevronSetToNullNoLongerFollowsItsItem`, `AccordionTests.ADetachedChevronIsUnlinkedAndLandsOnItsAngleWhenReattached`
- **BRIDGE-ACCORDION-011** (inherited) — A chevron with no `Expander` or `TreeViewItem` in its templated-parent chain shall not move.
  - Tests: `AccordionTests.AChevronOutsideAnItemsTemplateDoesNotMove`
- **BRIDGE-ACCORDION-031** — While a chevron is linked, its `Transitions` shall be lifted: set at `Animation` priority to its base list without the `RenderTransform` transitions, re-read from the base at each turn; the lift shall be disposed when the chevron unlinks.
  - Tests: `AccordionTests.ALinkedChevronLiftsItsRenderTransformTransitionsAndUnlinkRestoresThem`

### Group

- **BRIDGE-ACCORDION-012** — When an `Expander` expands, every other `Expander` among the logical descendants of its nearest logical ancestor with `Accordion.Group` true, whose own nearest group is that same ancestor, shall collapse.
  - Tests: `AccordionTests.ExpandingOneCollapsesItsSibling`
- **BRIDGE-ACCORDION-013** — The group shall apply to `Expander`s only, whether or not they have `Enabled`; `TreeViewItem`s are not single-opened.
  - Tests: `AccordionTests.TheGroupCollapsesExpandersWithoutEnabled`, `AccordionTests.TheGroupDoesNotSingleOpenTreeViewItems`

### Accordion reduced motion

- **BRIDGE-ACCORDION-014** — While `ReducedMotion` is set, the item shall open and close within a frame: the fold has duration 0, the body's fade lands at once, and the chevron jumps to its angle.
  - Tests: `AccordionTests.UnderReducedMotionItOpensAndClosesAtOnce`, `NavMotionTests.UnderReducedMotionCollapseAndExpandLandWithinAFrame`

### GridLength codec

- **BRIDGE-ACCORDION-015** — A pixel `GridLength` shall tween as its pixel count, so a number, a relative value or a pixel `GridLength` animates `ColumnDefinition.Width` or `RowDefinition.Height` in one `Animate` call.
  - Tests: `CodecTests.PixelGridLengthsInterpolateFromALengthOrANumber`, `CodecTests.AColumnWidthAndARowHeightAnimateInOneCall`
- **BRIDGE-ACCORDION-016** — When either end of a `GridLength` tween is a star or auto length, the codec shall throw `ArgumentException`.
  - Tests: `CodecTests.AStarOrAutoLengthAtEitherEndThrows`
- **BRIDGE-ACCORDION-017** — When a tweened `GridLength` value falls below 0, such as a spring's overshoot, the codec shall write 0.
  - Tests: `CodecTests.AGridLengthOvershootingZeroWritesZero`

### Pane

- **BRIDGE-ACCORDION-018** — `Pane.Collapse(column, content)` shall ease the column's `Width` to 0 over `Tokens.Short` with `Tokens.Ease`, mark it collapsed, and return the animation.
  - Tests: `CollapsiblePaneTests.CollapsingEasesToZeroAndExpandingRestoresTheDraggedWidth`
- **BRIDGE-ACCORDION-019** — `Pane.Expand(column, content)` shall ease the column's `Width` back to its recorded expanded width over `Tokens.Medium` with `Tokens.Ease`, mark it expanded, and return the animation.
  - Tests: `CollapsiblePaneTests.CollapsingEasesToZeroAndExpandingRestoresTheDraggedWidth`, `NavPaneTests.TheSplitterIsClampedAndItsWidthIsRestoredAfterCollapsing`
- **BRIDGE-ACCORDION-020** — `Pane.Toggle` shall expand a column `IsCollapsed` reports collapsed and collapse it otherwise.
  - Tests: `CollapsiblePaneTests.MinWidthIsLiftedWhileTheWidthMovesAndRestoredOnceExpanded`
- **BRIDGE-ACCORDION-021** — `Pane.IsCollapsed` shall report whether the last `Pane` call on the column collapsed it, and false for a column never passed to `Pane`.
  - Tests: `CollapsiblePaneTests.CollapsingEasesToZeroAndExpandingRestoresTheDraggedWidth`
- **BRIDGE-ACCORDION-022** — On its first call for a column, `Pane` shall remember the column's `MinWidth`; on any call made while the column is expanded and no `Pane` animation on it is in flight (neither completed nor canceled), it shall record the column's `ActualWidth` as the width to expand back to, whatever its `MinWidth`.
  - Tests: `CollapsiblePaneTests.CollapsingEasesToZeroAndExpandingRestoresTheDraggedWidth`, `CollapsiblePaneTests.AColumnWithoutAMinWidthExpandsBackToItsWidth`
- **BRIDGE-ACCORDION-023** — Each `Pane` call shall set the column's `MinWidth` to 0 for the run, and when an expand completes it shall restore the remembered `MinWidth`; a completed collapse leaves it at 0.
  - Tests: `CollapsiblePaneTests.MinWidthIsLiftedWhileTheWidthMovesAndRestoredOnceExpanded`
- **BRIDGE-ACCORDION-024** — On each frame of a `Pane` animation, the content's right margin shall be set to `min(0, width − remembered MinWidth)`, keeping its other margins, so below its minimum it keeps its width and is clipped by its cell rather than reflowed.
  - Tests: `CollapsiblePaneTests.BelowItsMinimumTheContentKeepsItsWidthAndIsClipped`, `NavPaneTests.TheSidebarClipsRatherThanReflowsBelowItsMinimumWidth`
- **BRIDGE-ACCORDION-025** — When a `Pane` animation completes, the column's `Width` shall be set as a local pixel value and the animation reverted, so a `GridSplitter`'s later writes take effect.
  - Tests: `CollapsiblePaneTests.CollapsingEasesToZeroAndExpandingRestoresTheDraggedWidth`
- **BRIDGE-ACCORDION-026** — While `ReducedMotion` is set, a `Pane` call shall land the width at once (duration 0) and still run its completion, restoring `MinWidth` after an expand.
  - Tests: `CollapsiblePaneTests.UnderReducedMotionTheWidthJumps`, `NavPaneTests.UnderReducedMotionTheCollapseLandsOnTheFirstFrame`
- **BRIDGE-ACCORDION-027** — A `Pane` call on a column whose `Width` is a star or auto length shall throw `ArgumentException` from the `GridLength` codec.
  - Tests: `CollapsiblePaneTests.AStarOrAutoColumnThrows`

### Defaults

- **BRIDGE-ACCORDION-028** (inherited) — When `Accordion.Enabled` is not set, the item shall get no accordion motion: no `Animate.ContentSize`, body marks or chevron.
  - Tests: `AccordionTests.WithoutEnabledTheItemGetsNoAccordionMotion`
- **BRIDGE-ACCORDION-029** (inherited) — When `Accordion.Group` is not set on any logical ancestor, expanding an `Expander` shall collapse no other `Expander`.
  - Tests: `AccordionTests.WithoutAGroupExpandingCollapsesNoOtherExpander`
- **BRIDGE-ACCORDION-030** (inherited) — When `Accordion.Chevron` is not set on an element, the element shall not be linked to an item and shall not rotate.
  - Tests: `AccordionTests.WithoutChevronAnElementInTheHeaderDoesNotRotate`

## Invariants

- An enabled accordion item has exactly one set of marked parts, those of its current templates.
- A chevron is linked to at most one item at a time.
- An unlinked chevron has no lifted `Transitions` value.
- `Pane` keeps one state per column, held weakly; a column's `MinWidth` is 0 exactly while a `Pane` animation runs or after a collapse.

## Edge cases

- `Enabled` on a `TemplatedControl` other than `Expander` or `TreeViewItem` gets `Animate.ContentSize` and the `PART_ItemsPresenter` marks, but no chevron link.
- Disabling `Enabled` clears an `Animate.ContentSize` the user set on the item.
- An item without the Fluent or Semi parts gets no automatic chevron; `Chevron` can still be set on a template element.
- `Pane.Toggle` mid-run reverses from the current width; the expanded width is not re-recorded, since a `Pane` animation is in flight.

## Threading and lifetime

- UI thread only. `Accordion`'s class handlers are registered when the type initializes, by any use of its properties. Chevron links unsubscribe on detach. `Pane` state lives in a `ConditionalWeakTable` keyed by the column; the animation lives until completion or takeover.

## Reduced motion

- Accordion: fold, fade and chevron land within a frame (BRIDGE-ACCORDION-014). Pane: the width jumps and the completion still runs (BRIDGE-ACCORDION-026). The codec is unaffected.

## Allocation

- The codec decodes and encodes without allocating beyond the boxed `GridLength`. `Pane`'s per-frame margin write allocates a `Thickness` value only. Accordion allocates on enable, template apply and each chevron spring.

## Provenance

- No upstream counterpart.

## Decisions

- **Accordion is an attached form, not a style.** `Accordion.Enabled`, `Group` and `Chevron` attach to an `Expander` or `TreeViewItem`: `Animate.ContentSize` goes on the item itself, the body fades under a held hide before folding and replays its entrances through `EnterOnShow`, the chevron's path gets a `TransformOperations` render transform and springs, and `Group`'s single-open covers `Expander`s only. Reason: the bridge ships no themes; the item's template root never hides; Avalonia's `TransformAnimator` skips `TransformOperations`, so Fluent's own 62 ms flip does not add to the spring; the nav needs no single-open. Rejected: an `Expander` style or a templated control.
- **The `GridLength` codec is pixel-only.** A pixel `GridLength` tweens as its pixel count, a value below 0 (a spring's overshoot) writes 0, and a star or auto length at either end throws `ArgumentException`. Reason: a column jumping between star and pixels mid-tween would reflow its whole grid. Rejected: switching discretely between star or auto and pixels.
- **`Pane` works on `Grid` columns only and ends on a local width.** `Pane.Collapse`, `Expand` and `Toggle` keep per-column state in a weak table, lift `MinWidth` for the run, clip the content below its minimum through a negative right margin, and end on a local pixel width with the animation reverted; the width to expand back to is recorded whenever the column is expanded and its last `Pane` animation is no longer in flight. Reason: a `SplitView` already animates its own pane; clipping avoids reflowing the content below its minimum; a held animation value would mask a `GridSplitter`'s writes; the run's own state, unlike `MinWidth`, marks rest for a column without a minimum.
- **Known theme chevrons are found by name, and others are marked with `Chevron`.** Fluent's (`ExpandCollapseChevronBorder` in the header's template) and Semi's (`PART_PathIcon` in the item's or the header's template) are looked up in that order, and a part that already has a `Chevron` value keeps it. Reason: Fluent and Semi are the two themes the samples run on, and a name lookup per template apply is cheap. Rejected: a heuristic such as "the first `PathIcon` in the header", which would match icons in user header content.
- **A linked chevron lifts its `RenderTransform` transitions.** The mechanism of BRIDGE-POPOVER-008: `Transitions` is set at `Animation` priority to the base list without `RenderTransform` entries, re-read from the base before each turn so a re-applied theme is followed, and disposed on unlink. Reason: the spring writes `RenderTransform` every frame, and a theme transition on it (Semi's 250 ms one) eases each write, double-easing the chevron. Rejected: lifting transitions on every property Presto animates on any control, a cross-cutting change to BRIDGE-TARGETS with a cost on every write.

## Non-goals

- No style or templated control for the accordion; the bridge ships no themes.
- `Group` does not cover `TreeViewItem`.
- `Pane` does not drive `SplitView`, which animates its own pane.
- Star and auto lengths are not interpolated.

## Open items

None.