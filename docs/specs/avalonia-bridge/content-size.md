---
id: BRIDGE-CONTENTSIZE
title: Content size transitions
package: MonacoLabs.Presto.Avalonia
namespaces:
  - MonacoLabs.Presto
sources:
  - MonacoLabs.Presto.Avalonia/ContentSizeTransition.cs
  - MonacoLabs.Presto.Avalonia/ContentSizeArm.cs
  - MonacoLabs.Presto.Avalonia/Animate.cs
  - MonacoLabs.Presto.Avalonia/PrestoAvalonia.cs
  - MonacoLabs.Presto.Avalonia/AnimateExtensions.cs
types:
  - ContentSizeTransition
tests:
  - ContentSizeTests
  - ContentSizePresenceTests
  - ReferencePagesTests
  - ScopePruningTests
upstream:
  - src/layout/layout.js
depends:
  - BRIDGE-LAYOUT
  - BRIDGE-ATTACH
  - BRIDGE-PRESENCE
  - CORE-ANIMATION
  - CORE-BUILDER
  - CORE-SCOPE
---

# Content size transitions

## Purpose

Eases a host's real layout size whenever its content changes size (Compose's `animateContentSize`), clipping the content meanwhile, on the UI thread. Unlike a scaling layout transition nothing is stretched, so corner radius, shadow and text stay true, at the cost of a layout pass per frame. Available from code (`AnimateContentSize`) and XAML (`Animate.ContentSize`, whose attached property is otherwise BRIDGE-ATTACH's). Works with `Presence` content swaps (BRIDGE-PRESENCE) and takes precedence over layout transitions on and beside the host (BRIDGE-LAYOUT). Also specifies `PrestoAvalonia.AnimateContentSize`, `AnimateExtensions.AnimateContentSize` and `Animate.ContentSize`.

## Requirements

### Resizing

- **BRIDGE-CONTENTSIZE-001** — When the content of a host with a content size transition changes size, the host's layout size shall ease from the old size to the new one and land on it exactly.
  - Tests: `ContentSizeTests.TheHostEasesToItsNewSizeAndLandsOnItExactly`, `ReferencePagesTests.TheFaqCardEasesOpenToItsNaturalHeightWithoutRewrappingTheAnswer`
- **BRIDGE-CONTENTSIZE-002** (inherited) — `AnimateContentSize` shall record the host's size at creation when the host is arranged, and otherwise let the host's first layout pass only record.
  - Tests: untested
- **BRIDGE-CONTENTSIZE-003** — During a resize the host's sole visual child shall be laid out at its final size, held there by an extra right and bottom `Margin` at `Animation` priority that eases to zero on top of the child's own margin, so its text never reflows.
  - Tests: `ContentSizeTests.WrappingTextKeepsItsFinalLayoutAndIsClippedToTheHost`
- **BRIDGE-CONTENTSIZE-004** — The frame rendered right after the content changes size shall still show the old size.
  - Tests: `ContentSizeTests.TheFrameAfterTheContentChangesStillShowsTheOldSize`
- **BRIDGE-CONTENTSIZE-005** — During a resize the child's `Clip` shall be held at `Animation` priority to the host's inner box (its border and padding deflated, its corner radii reduced by them), and the host itself shall not be clipped, so its shadow and rounded corners still draw.
  - Tests: `ContentSizeTests.TheHostKeepsItsShadowAndRoundedCornersWhileItsContentIsClipped`, `ContentSizeTests.WrappingTextKeepsItsFinalLayoutAndIsClippedToTheHost`
- **BRIDGE-CONTENTSIZE-006** — An axis shall ease only when the content changed by more than 1 px along it and the host changed by the same amount within 1 px, so a stretched axis moves in one frame.
  - Tests: untested
- **BRIDGE-CONTENTSIZE-007** — When the content changes size again mid-resize, the new resize shall start from the size drawn now.
  - Tests: `ContentSizeTests.AChangeMidFlightRetargetsFromTheCurrentSize`
- **BRIDGE-CONTENTSIZE-008** (inherited) — When the host does not have exactly one layoutable visual child, the transition shall ease nothing and cancel any resize in flight.
  - Tests: untested
- **BRIDGE-CONTENTSIZE-009** — When the resize completes, the margin and clip holds shall be released.
  - Tests: `ContentSizeTests.TheHostEasesToItsNewSizeAndLandsOnItExactly`
- **BRIDGE-CONTENTSIZE-010** — When the resize's duration collapses to the core's minimum or less, the host shall land at its new size in one frame.
  - Tests: `ContentSizeTests.AReducedResizeIsInstant`
- **BRIDGE-CONTENTSIZE-011** — `Then` shall resolve with the transition when the resize in flight lands, and at once when none has started.
  - Tests: `ContentSizeTests.ThenCompletesWhenTheResizeLands`

### Content swaps

- **BRIDGE-CONTENTSIZE-012** — When the host's `Child` or `Content` is swapped, the host shall resize from the size drawn now to the new content's, while `Presence` ghosts the old content out, clipped to the box the host had at the swap, and enters the new one.
  - Tests: `ContentSizePresenceTests.ASwapGhostsTheOldContentEntersTheNewAndResizesBetween`, `ContentSizePresenceTests.AHostInsideATransitioningContentControlResizesAcrossItsSwap`, `ReferencePagesTests.NextViewSwapsThePanelThroughAGhostAndEndsAtTheNewViewsSize`
- **BRIDGE-CONTENTSIZE-013** — A swap mid-resize shall retarget from the current size and clip its ghost there.
  - Tests: `ContentSizePresenceTests.ASwapMidResizeRetargetsFromTheCurrentSizeAndClipsItsGhostThere`
- **BRIDGE-CONTENTSIZE-014** — A host removed mid-resize shall ghost at its drawn size.
  - Tests: `ContentSizePresenceTests.TheHostRemovedMidResizeGhostsAtItsDrawnSize`

### Precedence over layout transitions

- **BRIDGE-CONTENTSIZE-015** — A layout transition on a host with a content size transition shall ease only its position, whatever its `LayoutAnimateMode`.
  - Tests: `ContentSizePresenceTests.ALayoutTransitionOnAContentSizeHostEasesOnlyItsPosition`
- **BRIDGE-CONTENTSIZE-016** — While a host resizes, and through the layout pass that lays out its last frame, a layout transition on any control in the host's top level that is neither the host, inside it, nor its ancestor shall cancel its move and land in one frame, since the resize already eases it.
  - Tests: `ContentSizePresenceTests.TheRowsBelowAnExpandingRowFollowItAndEndInTheirSlots`, `ContentSizePresenceTests.ARowInAListBelowAnExpandingRowFollowsItInItsSlot`

### Ending

- **BRIDGE-CONTENTSIZE-017** — `Cancel` shall stop the resize, leave the host at its natural size and give the child back its own margin and clip, still armed for later changes.
  - Tests: `ContentSizeTests.CancelOrRevertRestoresTheUsersOwnValues`
- **BRIDGE-CONTENTSIZE-018** — `Revert` shall cancel and stop easing later size changes.
  - Tests: `ContentSizeTests.CancelOrRevertRestoresTheUsersOwnValues`
- **BRIDGE-CONTENTSIZE-031** — Once reverted, the transition shall count as released to the scope it registered with (CORE-SCOPE).
  - Tests: `ScopePruningTests.RevertedBridgeRevertiblesArePrunedFromTheirScope`
- **BRIDGE-CONTENTSIZE-019** (inherited) — Creating a content size transition on a host that already has a live one shall revert the earlier one.
  - Tests: untested
- **BRIDGE-CONTENTSIZE-020** — `AnimateContentSize` shall register the transition with `Engine.Current.CurrentScope` when there is one.
  - Tests: untested

### Motion

- **BRIDGE-CONTENTSIZE-021** — The `(duration, ease)` overloads shall use that duration and ease (the engine default when null) with no delay; the builder overloads shall take the builder's `Duration`, `Delay` and `Ease`.
  - Tests: `ContentSizeTests.TheHostEasesToItsNewSizeAndLandsOnItExactly`
- **BRIDGE-CONTENTSIZE-022** — When the builder animates any property, the call shall throw `NotSupportedException` ("Layout decides the size a content size transition eases to, so give it only timing and an ease."); a setting a layout transition cannot honor throws as in BRIDGE-LAYOUT-040.
  - Tests: `ContentSizeTests.ABuilderAContentSizeTransitionCannotHonorThrowsWhenApplied`

### XAML

- **BRIDGE-CONTENTSIZE-023** — While a control with `Animate.ContentSize` is attached it shall have a content size transition; attaching shall record its size without easing, and detaching shall revert it.
  - Tests: `ContentSizeTests.AttachingRecordsTheSizeAndDetachingReverts`
- **BRIDGE-CONTENTSIZE-024** — Setting or changing `Animate.ContentSize` on an attached control shall revert the live transition and arm a new one recording the current size; setting it to null shall revert and stop listening.
  - Tests: `ContentSizeTests.AttachingRecordsTheSizeAndDetachingReverts`
- **BRIDGE-CONTENTSIZE-025** — An `Animate.ContentSize` builder that the transition cannot honor shall throw `NotSupportedException` when it is set, whether or not the control is attached.
  - Tests: `ContentSizeTests.ABuilderAContentSizeTransitionCannotHonorThrowsWhenApplied`
- **BRIDGE-CONTENTSIZE-026** (inherited) — The attached form shall not register with an ambient scope, and in design mode shall do nothing.
  - Tests: untested

### Reduced motion

- **BRIDGE-CONTENTSIZE-027** — For the `(duration, ease)` overloads, while `ReducedMotion` is set as a resize starts, the resize shall land in one frame.
  - Tests: `ContentSizeTests.ADurationResizeLandsInOneFrameOnlyUnderReducedMotion`, `ReferencePagesTests.UnderReducedMotionNextViewLandsAtTheNewViewsSizeAtOnce`
- **BRIDGE-CONTENTSIZE-028** — For `Animate.ContentSize`, the builder's reduced form shall be read when it arms, and a change of `ReducedMotion` shall re-arm at the control's next layout pass.
  - Tests: `ContentSizeTests.AReducedResizeIsInstant`

### Defaults

- **BRIDGE-CONTENTSIZE-029** (inherited) — When no resize has started, the transition's `Progress` shall be 0.
  - Tests: untested
- **BRIDGE-CONTENTSIZE-030** (inherited) — When no resize has started, the transition's `Completed` shall be true.
  - Tests: untested

## Invariants

- At most one live content size transition per host.
- Outside a resize the child carries none of this transition's margin or clip.
- The host's and its siblings' layout always reflect the eased size; the child's always reflects the final one.

## Edge cases

- A host whose visual child is swapped while no resize is in flight resizes only along axes that follow the content; with one in flight it retargets even when no axis qualifies.
- A `Panel` host with several children never eases.
- Min/max size on the host are not overridden: the margin works inside the host's own constraint.

## Threading and lifetime

- UI thread only. The transition subscribes to the host's `LayoutUpdated` and lives in a weak per-host table; `Revert` unsubscribes. The mirror animates a private object, so it is not paused when the host detaches.
- The host is laid out again every frame of a resize: meant for a panel, not hundreds of rows.

## Reduced motion

- BRIDGE-CONTENTSIZE-027 and -028; each trigger reads the flag when it fires; a content-size move is finite, so the change signal does not replay it (BRIDGE-REDUCED Following a change). `ReduceAll` collapses the mirror's duration, which lands the resize at once (BRIDGE-CONTENTSIZE-010).

## Allocation

- Each frame of a resize pushes a new `Thickness` and a new clip geometry and triggers a layout pass; it is not allocation-free. Passes with no size change allocate nothing.

## Provenance

- Upstream's `layout` module animates real width and height with a 1 px tolerance and overrides min/max; Presto keeps the 1 px tolerance but eases a margin on the child instead of the host's size, does not override min/max, and reacts to every layout pass automatically, because a held host size hid content that grew mid-flight and the margin already works inside the host's own constraint.

## Decisions

- **A content size transition eases the host's real layout size, holding the child with a margin.** On the UI thread, the host's sole child gets an extra right and bottom `Margin` at `Animation` priority (drawn size minus natural size) eased to zero by a mirror animation, so the child keeps its natural constraint and text never reflows; min/max are not overridden. Upstream's real width/height layout animation maps here, and position moves to BRIDGE-LAYOUT. Reason: a scaling FLIP stretches children, radius and shadow, while Compose, Flutter, SwiftUI and upstream's `layout` module animate real size; the margin lets every pass measure the content, so a mid-flight change retargets from the drawn size. Rejected: holding the host's `Width`/`Height` (the spike showed it hides content that grows mid-flight); scaling; overriding min/max as upstream does.
- **Only an axis that follows the content eases, within 1 px.** An axis eases only when the host grew by as much as its content. Reason: a change of a pixel or less is not worth easing, and layout rounding can leave the host's growth a fraction off its content's; a stretched axis then moves in one frame.
- **The margin is written in the `LayoutUpdated` that sees the change.** Reason: the re-layout it triggers then runs before the next render, so that frame still shows the old size.
- **The content is clipped, not the host.** While a resize is in flight the child's `Clip` is held at `Animation` priority to the host's inner box and released with the margin. Reason: the host's shadow and rounded corners still draw outside it.
- **A content size host owns its size.** A layout transition on the host eases only position; while it resizes, layout moves anywhere else in its top level (outside it and not above it) land in one frame; a content swap resizes from the drawn size while `Presence` ghosts the old content clipped to the box at the swap. Reason: the resize already eases whatever it moves and a compositor flight would trail behind it; before this, `ItemLayout` neighbors of an expanding row lagged, and limiting it to the host's parent let a list nested under a sibling (the gallery nav's pages in a section below one that unfolds) fly after the move. Accepted: an unrelated layout move elsewhere in the window during a resize also lands in one frame.
- **Duration overloads land at once under the flag.** See BRIDGE-LAYOUT.
- **The signal is an event on `Engine`, not on `PrestoDefaults`.** See CORE-REDUCED.

## Non-goals

- Easing size along a stretched axis; animating a host with several children; moving the host (use BRIDGE-LAYOUT on it, which then eases only position).

## Open items

- Untested: recording at creation (002), the stretched-axis rule (006), a host without a sole child (008), one live transition per host (019), scope registration (020), the attached form's scope and design mode (026).
