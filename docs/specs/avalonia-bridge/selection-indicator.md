---
id: BRIDGE-SELECTION
title: Selection indicator
package: MonacoLabs.Presto.Avalonia
namespaces:
  - MonacoLabs.Presto
sources:
  - MonacoLabs.Presto.Avalonia/Selection.cs
  - MonacoLabs.Presto.Avalonia/SelectionIndicatorMode.cs
  - MonacoLabs.Presto.Avalonia/Compositor/LayoutMotion.cs
types:
  - Selection
  - SelectionIndicatorMode
tests:
  - SelectionIndicatorTests
upstream: []
depends:
  - CORE-ANIMATION
  - CORE-BUILDER
  - CORE-SPRING
  - CORE-REDUCED
  - BRIDGE-TOKENS
---

# Selection indicator

## Purpose

One indicator per list, a pill behind the selected item (`Fill`) or a bar along its edge (`Underline`), that glides to each new selection. It is set with attached properties on a `SelectingItemsControl` (`ListBox`, `TabStrip`, a `TabControl`'s headers) or a `TreeView`, and lives inside the list's `ItemsPresenter`, so it scrolls with the items and outlasts their virtualization. The glide is a plain core animation (CORE-ANIMATION) over the indicator's four edges; its timing defaults come from BRIDGE-TOKENS.

## Requirements

### Lifetime

- **BRIDGE-SELECTION-001** — When `Indicator` is set to a template, the list shall build the template once and track its selection with the built control as the indicator.
  - Tests: `SelectionIndicatorTests.FillCoversTheItem`
- **BRIDGE-SELECTION-002** (inherited) — When `Indicator` changes, the list shall cancel the old indicator's glide, remove it from the presenter and build the new template; a null template, or one whose `Build` returns null, shall leave no indicator.
  - Tests: untested
- **BRIDGE-SELECTION-003** (inherited) — While in design mode, setting `Indicator` shall do nothing.
  - Tests: untested
- **BRIDGE-SELECTION-004** — The indicator shall be a visual and logical child of the list's `ItemsPresenter`, inserted behind the items panel, and shall not be hit-testable.
  - Tests: untested
- **BRIDGE-SELECTION-005** — When the list's presenter is replaced or rebuilds its panel and drops the indicator, the indicator shall be re-inserted on the next `LayoutUpdated`.
  - Tests: untested
- **BRIDGE-SELECTION-006** (inherited) — While the list has no presenter, the indicator shall not be placed.
  - Tests: untested
- **BRIDGE-SELECTION-007** (inherited) — When the list leaves the visual tree, it shall cancel the running glide and forget the indicator's placement, so the next placement snaps.
  - Tests: untested

### Placement

- **BRIDGE-SELECTION-008** — The indicator shall be placed on the selected container's rect, measured in the presenter's coordinates from the container's parent, so a render transform on the container does not move it.
  - Tests: `SelectionIndicatorTests.FillCoversTheItem`
- **BRIDGE-SELECTION-009** — On a `SelectingItemsControl`, the selected container shall be `ContainerFromIndex(SelectedIndex)`; on a `TreeView`, it shall be the selected item's `TreeViewItem`'s `PART_LayoutRoot` part, or the `TreeViewItem` itself when it has none.
  - Tests: `SelectionIndicatorTests.CollapsingATreeViewSectionHidesItAndExpandingPlacesItWithoutEasing`
- **BRIDGE-SELECTION-010** — In `Fill` mode, the indicator shall be arranged over the whole selected rect, inset by its own `Margin`.
  - Tests: `SelectionIndicatorTests.FillCoversTheItem`
- **BRIDGE-SELECTION-011** — In `Underline` mode in a horizontal list, the indicator shall be a bar along the item's bottom edge, as wide as the item and as tall as the indicator's desired height.
  - Tests: `SelectionIndicatorTests.AHorizontalListsUnderlineIsABarAlongTheItemsBottomEdge`
- **BRIDGE-SELECTION-012** — In `Underline` mode in a vertical list, the indicator shall be a bar along the item's left edge, as tall as the item and as wide as the indicator's desired width, with its `Margin` insetting it.
  - Tests: `SelectionIndicatorTests.AVerticalListsUnderlineRunsDownTheItemsLeftEdgeInsetByItsMargin`
- **BRIDGE-SELECTION-013** (inherited) — A list shall count as horizontal when its items panel is a `StackPanel`, `VirtualizingStackPanel` or `WrapPanel` with horizontal `Orientation`; every other panel shall count as vertical.
  - Tests: `SelectionIndicatorTests.AHorizontalListsUnderlineIsABarAlongTheItemsBottomEdge`, `SelectionIndicatorTests.AVerticalListsUnderlineRunsDownTheItemsLeftEdgeInsetByItsMargin`
- **BRIDGE-SELECTION-014** — The indicator shall be re-placed on every `LayoutUpdated` of the list, and a placement equal to the current one while the indicator is shown shall do nothing.
  - Tests: `SelectionIndicatorTests.ItSnapsWhenTheSelectedItemMovesOnAResize`

### Hiding

- **BRIDGE-SELECTION-015** — When nothing is selected, the indicator shall be hidden.
  - Tests: `SelectionIndicatorTests.ItHidesWhenNothingIsSelected`
- **BRIDGE-SELECTION-016** — While the selected container is not realized, the indicator shall be hidden.
  - Tests: `SelectionIndicatorTests.ItHidesWhenTheSelectedContainerIsVirtualizedAway`
- **BRIDGE-SELECTION-017** — While the selected container is not effectively visible, or its far (right or bottom) corner lies more than 0.5 DIP outside a clipping ancestor between it and the presenter, the indicator shall be hidden.
  - Tests: `SelectionIndicatorTests.CollapsingATreeViewSectionHidesItAndExpandingPlacesItWithoutEasing`
- **BRIDGE-SELECTION-018** — When the indicator hides, its running glide shall be canceled.
  - Tests: untested
- **BRIDGE-SELECTION-019** (inherited) — When a list's control is not a `SelectingItemsControl` or a `TreeView`, the indicator shall stay hidden.
  - Tests: untested

### Glide and snap

- **BRIDGE-SELECTION-020** — When the list raises `SelectionChanged` as its own source and the indicator is already shown, the indicator shall glide from where it stands to the new item's rect.
  - Tests: `SelectionIndicatorTests.ItGlidesToANewSelectionOverTime`
- **BRIDGE-SELECTION-021** (inherited) — A `SelectionChanged` event bubbling from a nested selecting control shall be ignored.
  - Tests: untested
- **BRIDGE-SELECTION-022** — When the selected rect changes for any reason other than a selection change (a resize, a scroll into view, a `TreeView` section expanding), the indicator shall snap to it without easing, canceling any running glide.
  - Tests: `SelectionIndicatorTests.ItSnapsWhenTheSelectedItemMovesOnAResize`, `SelectionIndicatorTests.ItSnapsOntoASelectionScrolledIntoView`, `SelectionIndicatorTests.CollapsingATreeViewSectionHidesItAndExpandingPlacesItWithoutEasing`
- **BRIDGE-SELECTION-023** (inherited) — When the indicator is hidden at a selection change, the next placement shall snap rather than glide.
  - Tests: untested
- **BRIDGE-SELECTION-024** (inherited) — When a glide starts while another is running, the running glide shall be canceled and the new one shall start from the indicator's current edges.
  - Tests: untested
- **BRIDGE-SELECTION-025** — While `Motion` is null, the glide shall last `Recipes.Tokens.Medium` and ease with `Recipes.Tokens.Ease`, read at the selection change.
  - Tests: `SelectionIndicatorTests.ItGlidesToANewSelectionOverTime`
- **BRIDGE-SELECTION-026** — When `Motion` is set, the glide shall take its `Duration` (or `Presto.Defaults.Duration` when it has none), its `Delay` and its `Ease` (or `Tokens.Ease` when it has none).
  - Tests: untested
- **BRIDGE-SELECTION-027** — When the glide's ease is a spring, the glide shall last the spring's settling duration instead of the motion's duration.
  - Tests: untested
- **BRIDGE-SELECTION-028** — When `Motion` sets anything a layout transition cannot honor (keyframes, loop, alternate, reversed, autoplay, frame rate, playback rate or ease, composition, modifier, id, priority, any callback, a per-target duration or delay, or a property other than an ease-only `X`, `Y` or `Scale`), the selection change shall throw `NotSupportedException`.
  - Tests: untested

### Stretch

- **BRIDGE-SELECTION-029** — While `Stretch` is true and the glide travels along the list's axis, the leading edge's progress shall be `sin(tπ/2)` and the trailing edge's `1 − cos(tπ/2)`, each then put through the glide's ease, so the indicator widens mid-glide and lands on the new item's rect.
  - Tests: `SelectionIndicatorTests.AStretchingIndicatorsLeadingEdgeRunsAheadOfItsTrailingEdge`
- **BRIDGE-SELECTION-030** — The leading edge shall be the edge facing the direction of travel (right or bottom when moving forward, left or top when moving back); the cross-axis edges shall use the plain ease.
  - Tests: `SelectionIndicatorTests.AStretchingIndicatorsLeadingEdgeRunsAheadOfItsTrailingEdge`
- **BRIDGE-SELECTION-031** (inherited) — When the glide's centers coincide along the list's axis, `Stretch` shall have no effect.
  - Tests: untested

### Reduced motion

- **BRIDGE-SELECTION-032** — While `Presto.Defaults.ReducedMotion` is true at a selection change, the indicator shall be placed on the new item at once.
  - Tests: `SelectionIndicatorTests.UnderReducedMotionANewSelectionIsPlacedAtOnce`

### Defaults

- **BRIDGE-SELECTION-033** (inherited) — When `IndicatorMode` is not set, the indicator shall be placed in `Fill` mode.
  - Tests: untested
- **BRIDGE-SELECTION-034** (inherited) — When `Stretch` is not set, every edge of the glide shall use the plain ease.
  - Tests: untested

## Invariants

- At most one indicator per list, and it is never one of the items panel's children.
- After every layout pass the indicator is either hidden or arranged at the selected container's current rect (or the glide's current frame towards it).
- Only a selection change animates; every other change of slot lands at once.

## Edge cases

- A `VirtualizingStackPanel` keeps a `ListBox`'s selected container realized as its tab-once element, so scrolling the selection away hides the indicator only once that element moves elsewhere.
- A `TreeView`'s selected row is resolved once per selection change and reused for later layouts until the next one.
- `Underline` thickness is the indicator's `DesiredSize` measured at infinity, so a margin on the thickness axis adds to it.
- The clip check looks only at the container's far corner, so a container clipped at its near (left or top) edge still shows the indicator.
- A placement made while handling `LayoutUpdated` lands in the following layout pass.

## Threading and lifetime

- UI thread only. The tracker hooks the list's `SelectionChanged`, `LayoutUpdated` and `DetachedFromVisualTree` and unhooks them when `Indicator` changes; the glide runs on `Engine.Current`.

## Reduced motion

- Read at each selection change: the indicator lands on the new item at once. Snaps are unaffected (they never animate).

## Allocation

- Not on the tick path beyond the glide's own animation; a placement allocates nothing on the steady `LayoutUpdated` path except the `TreeView` part lookup after a selection change.

## Deviations from anime.js

- No upstream counterpart.

## Decisions

- **One indicator per list, in the items presenter.** `Selection.Indicator` builds one indicator, inserted behind the panel as a child of the list's `ItemsPresenter` through `UnsafeAccessor` and re-inserted when the panel rebuilds, placed from the selected container's slot on every `LayoutUpdated`; only a selection change glides, any other slot change snaps, and it hides while the container is unrealized, not effectively visible or clipped by an ancestor. Reason: it scrolls with the items and outlives recycled containers; the panel's children belong to the item generator by index, and child visuals or adorners would draw over the items. Rejected: a `Connected` key on a per-item element shown only when selected, which needs a per-item hide, pays an element per item and loses its source on recycle.
- **`Stretch` follows Material's elastic tab indicator.** The leading edge runs ahead and the trailing edge follows on the elastic interpolator. Reason: not recorded. Rejected: the earlier two-step reach-and-contract stretch.

## Non-goals

- A per-item `Connected` element shown only while selected (see Decisions: one indicator per list, in the items presenter).
- Squash or differing per-edge durations (the Liquid sample's pill stays sample code).
- Selection inside lists that are neither `SelectingItemsControl` nor `TreeView`.

## Open items

- The `Motion` doc says the default glide is `MotionTokens.Medium` on `MotionTokens.Ease`; the code reads the current `Recipes.Tokens`, so a replaced token set changes it (the code wins).
- `Motion` accepts ease-only `X`/`Y`/`Scale` properties through the shared layout-motion parser but the glide ignores them; nothing documents or tests that.
- An unsupported `Motion` throws `NotSupportedException` from inside the `SelectionChanged` handler rather than when `Motion` is set; untested.
- The design hides the indicator when the container is not wholly inside a clipping ancestor; the code checks only the far corner, with a 0.5 DIP tolerance (BRIDGE-SELECTION-017).
- Re-templating the indicator (BRIDGE-SELECTION-002), presenter re-insertion (005), detach (007) and nested-list event filtering (021) have no tests.
