# Drop targets for Draggable

## Why

A `Draggable` knows where it is but not what it is over. Every drag-onto-something UI (kanban lanes, file-to-folder, a delete bin, a shelf) has to hit test zones by hand, track enter/leave to highlight them, decide the drop on settle, and then do the measure → reparent → `Reset` dance to land the card in its new container. `BoardPage.axaml.cs` is about 80 lines of exactly that (`Zone`, `Aim`, `Land`, the `"target"` class toggling). `Reorder` (BRIDGE-REORDER) covers moving within one list only.

This proposal is self-contained: no other change depends on it, and it can be dropped from scope without affecting them.

## What

Controls become drop zones through a new static class `Drop` with attached properties, usable from XAML or code:

```xml
<Border presto:Drop.Group="lanes" />
<Border presto:Drop.Group="lanes" presto:Drop.IsEnabled="{Binding CanAcceptCards}" />
```

A draggable opts in by naming the group:

```csharp
PrestoAvalonia.CreateDraggable(card, new DraggableParams
{
    DropGroup = "lanes",
    DropHit = DropHit.Overlap,
    CanDrop = (drag, zone) => zone != Bin || card.Tag is not Locked,
    OnZoneChange = drag => Highlight(drag.Zone),
    OnDrop = drag => drag.Reparent((Panel)drag.Zone!, index: Slot(drag)),
    DropMiss = DropMiss.ReturnHome,
});
```

While a drag or its release is in flight, the draggable hit tests the attached, visible, enabled zones of its group in its `TopLevel` on every frame it moves, exposes the zone it is over as `Draggable.Zone`, raises `OnZoneChange` when that changes, and sets the `:dragover` pseudo-class on the current zone. When the release settles over an accepting zone, it raises `OnDrop`; when it settles over no zone (or a rejecting one) it raises `OnDropMiss` and, with `DropMiss.ReturnHome`, springs back to where it was grabbed.

`Draggable.Reparent(Panel panel, int? index = null)` packages the landing: it measures the target where the drag left it drawn, moves it into `panel` (at `index`, else appended), resets the axes, and plays a position-only shared-element flight from the measured rect to its new layout slot, so the card settles from the release into place.

What does not change: draggables without a `DropGroup` behave exactly as today; the drag, snap, release, inertia and Escape behavior is untouched; slot/index computation inside a zone remains the caller's (Reorder owns in-list ordering).

## Decisions

- **Zones are attached properties on a new `Drop` class, matched by a string group.** Reason: zones are usually declared in XAML next to their content, while draggables are created in code (BRIDGE-DRAGGABLE non-goal); a string key lets the two meet without holding references. Rejected: `DraggableParams.Zones` as a control list (stale when zones are templated or recycled); a zone-side `Accepts` type filter (`CanDrop` on the draggable is enough and has the dragged item in hand).
- **Zones are found per `TopLevel` through a registry the attached property maintains on attach/detach.** Reason: hit testing stays a walk over a short list with no visual-tree search per frame. Rejected: scanning the tree on grab (misses zones that appear mid-drag, costs per grab).
- **Hit testing defaults to the target's center; `Pointer` and `Overlap` are opt-in.** `Center`: the drawn target's center lies inside the zone's bounds. `Pointer`: the grabbing pointer lies inside. `Overlap`: the zone with the largest intersection with the drawn target's bounds, any intersection counting. Ties go to the zone registered last (topmost by convention). Reason: center is what users expect for cards; pointer suits large or offset-grabbed targets; overlap removes dead gaps between adjacent zones (Board's column spacing). Rejected: nearest-zone by distance (Board's current rule), which always picks something and so cannot express "dropped nowhere".
- **The hover tracks the drawn target through the release throw, and the drop resolves when the release settles.** Reason: a flung card drops where it visibly comes to rest, as Board does today with `OnUpdate`/`OnSettle`. Rejected: resolving at pointer release from the projected destination, which disagrees with what the user sees when bounds or friction cushion the throw.
- **The draggable does not reparent on its own; `Reparent` is an explicit helper called from `OnDrop`.** Reason: what a drop means (move, copy, delete, update a view model whose template then moves the item) is the app's; but the measure-before-reparent ordering is subtle enough to package once.
- **Escape is a miss.** With `CancelOnEscape`, the drag already springs home (BRIDGE-DRAGGABLE-050); it clears the zone and raises `OnDropMiss`, never `OnDrop`.
- **No reduced form of its own.** Hover and drop are not motion; `ReturnHome` and `Reparent`'s flight are ordinary engine animations and collapse under `ReduceAll` like any other (CORE-REDUCED).

## Spec changes

**BRIDGE-DRAGGABLE** (`docs/specs/avalonia-bridge/draggable.md`): add `Drop`, `DropHit`, `DropMiss` to `types`; add `Drop.cs` and the zone registry to `sources`; strike the "A XAML surface" non-goal's absolute wording to "draggables are created in code; drop zones are declared through `Drop` attached properties". New subsection **Drop zones**:

- When `Drop.Group` is set on a control that is attached to a `TopLevel`, the control shall be a drop zone of that group in that `TopLevel` until the property is cleared or the control detaches.
- When `Drop.IsEnabled` is false, or the zone is not effectively visible, the draggable shall not consider it.
- While a draggable with a `DropGroup` is dragging or its release is in flight, it shall hit test its group's zones on every frame its target moves, using `DropHit`.
- When `DropHit` is `Center`, the hit zone shall be one whose bounds contain the drawn target's center; when `Pointer`, one whose bounds contain the grabbing pointer (the zone held at release is kept through the throw); when `Overlap`, the zone with the largest non-empty intersection with the drawn target's bounds.
- When more than one zone hits, the draggable shall choose the one registered last.
- When the hit zone changes, the draggable shall set `Zone` to it (or null), move the `:dragover` pseudo-class from the old zone to the new one, and raise `OnZoneChange` once.
- When `CanDrop` returns false for a hit zone, that zone shall not become `Zone`.
- When the release settles with `Zone` set, the draggable shall raise `OnDrop` once, after `OnSettle`, then clear `Zone` and the pseudo-class.
- When the release settles with no `Zone`, or Escape cancels the drag, the draggable shall raise `OnDropMiss` once, clear `Zone` and the pseudo-class, and with `DropMiss.ReturnHome` spring the target back to its value at the grab with the release spring.
- When a zone detaches or is disabled while it is `Zone`, the draggable shall clear `Zone` and raise `OnZoneChange`.
- When `Stop`, `Reset`, `Disable` or `Revert` is called while `Zone` is set, the draggable shall clear `Zone` and the pseudo-class without raising `OnDrop` or `OnDropMiss`.
- When `Reparent(panel, index)` is called, the draggable shall measure the target's drawn bounds, move it from its parent panel into `panel` at `index` (appended when null), reset its axes to 0 without `OnUpdate`, and play a position-only shared-element flight from the measured bounds over the release settling duration with `ReleaseEase`.
- When `Reparent` is called on a target whose parent is not a `Panel`, it shall throw `InvalidOperationException`.
- Hit testing shall allocate nothing per frame.
- Defaults: `DropHit` is `Center`; `DropMiss` is `Stay`; `Drop.IsEnabled` is true.

Decisions above move into the Decisions section.

## Tasks

1. `Drop` attached properties (`Group`, `IsEnabled`) and the per-`TopLevel` zone registry with attach/detach maintenance. Tests: `DropZoneTests.AZoneRegistersOnAttachAndLeavesOnDetach`, `…ADisabledOrHiddenZoneIsIgnored`.
2. `DraggableParams.DropGroup`, `DropHit`, `CanDrop`, `OnZoneChange`; per-frame hit test through drag and release; `Zone` and `:dragover`. Tests: `DropZoneTests.CenterHitsTheZoneUnderTheTargetsCenter`, `…PointerHitsTheZoneUnderThePointer`, `…OverlapPicksTheLargestIntersection`, `…TheLastRegisteredZoneWinsATie`, `…ZoneChangesRaiseOnceAndMoveThePseudoClass`, `…ARejectedZoneNeverBecomesZone`, `…AFlungCardTracksZonesThroughTheThrow`, `AllocationTests.DropHitTestingAllocatesNothingPerFrame`.
3. `OnDrop`, `OnDropMiss`, `DropMiss.ReturnHome`, Escape as a miss, zone detach and `Stop`/`Reset`/`Disable`/`Revert` clearing. Tests: `DropZoneTests.SettlingOverAZoneDropsAfterOnSettle`, `…SettlingOverNothingMissesAndReturnsHome`, `…EscapeIsAMiss`, `…AZoneDetachingMidDragClearsZone`, `…RevertClearsZoneWithoutDropping`.
4. `Draggable.Reparent`. Tests: `DropZoneTests.ReparentMovesIntoThePanelAndFliesFromWhereTheDragLeftIt`, `…ReparentOnANonPanelParentThrows`.
5. Update the BRIDGE-DRAGGABLE spec (requirements, `Tests:` lines, Decisions, types, non-goal wording).
6. Migrate `BoardPage` to `Drop.Group`/`DropHit.Overlap`/`OnZoneChange`/`OnDrop`/`Reparent`, replacing `Zone`, the `"target"` class and the snippet's manual landing; keep `Slot` and the gap reflow. Update `BoardPage`'s sample test.
7. Visual check on the Desktop sample (`--page board`): drag between lanes, fling across a lane gap, drop in the bin, Escape mid-drag, in both `--motion full` and `reduced`.

## Open questions

- Should `OnDrop` fire at release (with the projected destination) when the release is long, so a view model can update early? Current answer: no, settle only.
- Should zones also get a routed event (`Drop.DragEnter`/`DragLeave`/`Dropped`) so zone-side code can react without the draggable's callbacks? Leaning no for v1: the pseudo-class covers styling.
- Is `Reparent` worth owning here, or should it be a general `PrestoAvalonia` helper (it also fits `AfterFirstArrange`-style utilities in BRIDGE-HELPERS)?
- Name clash: `Drop` vs Avalonia's `DragDrop`. Alternative: `DropZone`.
