# Fixes — correctness items left open after Phase 33

Phase 34 in [PLAN.md](PLAN.md). Status: **done**. This phase collects what the post-Phase-33 review found still
open. Two items come from the SAMPLES-PLAN §8 findings (rows 32 and 33) and one from PLAN §7. Three are edges that
Phases 31–33 reported but left unfixed, and one is an unexplained observation on the Activity feed. Each item below
was checked against the committed code (`57b56d1`).

| # | Item | Area | Size | Test first |
|---|---|---|---|---|
| 1 | `Presto.Remove` mid-frame corrupts the engine's child list | core | small | yes |
| 2 | `AnimateOnCompositor` ignores `RenderTransformOrigin` | bridge | small | yes (pixel) |
| 3 | `Animate.Layout` re-arms stale after clear-and-restore mid-flight | bridge | tiny | yes |
| 4 | `VelocityTracker` sampling differs from upstream in two edge cases | core + bridge | small | yes |
| 5 | Page-transition takeover across UI thread and compositor | bridge | medium | yes |
| 6 | Activity feed scrolled by the header's height (investigation) | sample / bridge | small | repro only |
| 7 | Scroll anchoring for inserts above a scrolled virtualized list | bridge + sample | medium | repro first |

The items are in that order: highest risk for the least work first, and the two list-scrolling items last because
item 6 may turn out to be item 7.

## 1. `Presto.Remove` mid-frame

**Today.** `Engine.Update` caches `nextTickable = activeTickable._next` before it ticks a child
(`Monaco.Presto/Engine.cs:88`). If that child's callback removes the *next* child, for example
`Presto.Remove(target)` or `Draggable.Stop`/`Reset` from an `OnUpdate`, the removal goes through
`Composer.RemoveTargetsFromRenderable` (`Core/Composer.cs:94-96`), which cancels it and calls
`Helpers.RemoveChild(engine, next)`. That clears `next._prev` and `next._next` (`Core/Helpers.cs:44-52`). The loop then
steps onto the detached `next`, sees it paused, and calls `RemoveChild(this, next)` a second time
(`Engine.cs:100`). With `_prev` and `_next` both null, that call sets `_head = null` and `_tail = null`. From then on
the engine drops **every** remaining child, not just the rest of that frame. The timeline child loops in
`Core/Renderer.cs:271` and `:299` cache their next child the same way.

**Parity.** Upstream has the same shape: `engine.js:70` caches `nextTickable`, and `helpers.js:235` `removeChild`
clears both links unconditionally. The upstream test suites never remove a sibling from inside a callback, so no
ported test covers it.

**Fix.** Make `Helpers.RemoveChild(Clock, Timer)` a no-op for a child that is not linked into the parent (`_prev` is
null and `parent._head != child`). This alone stops the list from being wiped. Then give `Clock` an internal
iteration cursor (`_nextChild`) that `Engine.Update` and the two `Renderer` loops set before ticking a child;
`RemoveChild` advances it past a child that is being removed. There is no deferred unlink and no allocation, and the
loop shape stays the same as upstream, so the ported suites are untouched. Record the deviation in PLAN §7 and remove
the open item there.

**Tests (`EngineTests`, `TimelinesTests`).**
- Three animations A, B and C. A's `OnUpdate` removes B's target at 50 ms. C keeps rendering that frame and the
  next, and new animations created later still tick. This test must fail before the fix.
- The same inside a timeline: a child's callback removes the next child's target.
- A callback that removes *itself* and one that removes the *previous* child behave as today.
- `AllocationTests` budget unchanged.

## 2. `AnimateOnCompositor` and `RenderTransformOrigin`

**Today.** `CompositorAnimation` starts an expression animation on `CenterPoint` whenever a scale or rotation track
is present, with the fixed expression `Vector3(this.Target.Size.X * 0.5, this.Target.Size.Y * 0.5, 0)`
(`Compositor/CompositorAnimation.cs:230`, `:268-272`). The control's `RenderTransformOrigin` is never read, so the
Loaders page's ball (`RenderTransformOrigin="50%,100%"`) squashes about its middle on the render thread and lifts off
the floor. The UI-thread shorthands honor the origin, because Avalonia applies `RenderTransformOrigin` to
`RenderTransform`.

**Other compositor paths.** `LayoutTransition` anchors its inverse scale on the corner on purpose (FLIP math,
`Compositor/LayoutTransition.cs:271-273`), so it is correct and stays as it is. `PrestoPageTransition` plays through
`PrestoAvalonia.RunOnCompositor` → `CompositorAnimation`, so it inherits this fix.

**Fix.** Build the expression from the origin read when the animation compiles. A relative origin becomes
`Vector3(this.Target.Size.X * ox, this.Target.Size.Y * oy, 0)`, which still follows layout resizes. An absolute
origin becomes a constant `Vector3`. When `RenderTransformOrigin` changes while tracks are held, restart the
expression from a property-changed subscription made while the animation holds the visual. The subscription is
removed on revert.

**Tests (`CompositorAnimationTests`).**
- Pixel: a `ScaleY` 1 → 0.5 on the compositor with origin `50%,100%` keeps the bottom edge; with the default origin it
  keeps the center, as today.
- An absolute origin (`0,0`) scales from the corner.
- Changing the origin mid-animation moves the pivot at the next frame.
- `PageTransitionTests`: a `PageSharedAxis` scale honors a host's origin.

## 3. `Animate.Layout` stale re-arm

**Today.** While a `Connected` or shared-element flight owns the move, `LayoutArm.Follow` awaits the flight's
`Then()`. It then re-arms if `_transition is null && Builder == builder` (`LayoutArm.cs:81-89`). Clearing the
property releases the arm, and setting the *same builder instance* back creates a new arm. The released arm's pending
closure still sees an equal builder, so it arms a transition that no attach or detach handler owns. That transition
replaces the new arm's and is never reverted on detach.

**Fix.** Give `LayoutArm` a generation counter, incremented by `Arm`, `Disarm` and `Release`. `Follow` captures it
and re-arms only if the counter has not moved.

**Tests (`AnimateLayoutAttachedTests`).** Clear and restore the same builder while a flight is pending. After the
flight lands, exactly one transition is live, and a detach reverts it.

## 4. `VelocityTracker` sampling parity

**Today.** Upstream clamps and rounds *each* sample, then keeps the plain maximum of a stack that starts at
`[0, 0, 0]` (`draggable.js:490-491`). Presto keeps the raw samples, picks the one with the largest magnitude, and then
clamps and rounds that one (`VelocityTracker.cs:51-62`, `Draggable.cs:441-442`). The two agree whenever the multiplier
is ≥ 0 and the limits do not change, which is why the parity tests pass. They differ in two cases:
- **A negative `VelocityMultiplier`.** Upstream's maximum includes the zero seeds and prefers the least negative
  sample.
- **`Draggable.Refresh` changing the multiplier or the limits mid-drag.** Upstream keeps the samples it already
  clamped; Presto re-clamps the whole window with the new values.

**Fix.** Keep `VelocityTracker`'s public behavior, which is signed and strongest by magnitude and right for
`Animatable.Fling`. Give `Draggable` upstream's semantics through an internal overload,
`AddDisplacement(distance, time, multiplier, min, max)`. It stores the clamped, rounded sample and reports the plain
maximum, so the clamp applies once per sample at the time it is taken. No allocation.

**Tests (`DraggableTests`, `VelocityTrackerTests`).** Extend `ReproducesTheDraggableSampler` with a negative
multiplier and a mid-sequence limit change against the upstream replica. `VelocityTrackerTests` stays green unchanged.

## 5. Page-transition takeover across threads

**Today.** A canceled `HostRun` is stopped, not reverted: a UI-thread `Animation` is canceled in place and a
`CompositorAnimation` is paused (`PrestoPageTransition.cs:175-181`). The next run starts from where the host stands:
- **Compositor to compositor:** `CompositorScratch.Seed` reads the paused animation's mirror
  (`Compositor/CompositorScratch.cs:34-54`).
- **UI thread to UI thread:** the new run reads the shorthands' current values.

Two cases were left open (Phase 31 report):
- **UI thread → compositor.** `Seed` picks up the UI-thread shorthand values correctly, but the canceled
  run's `RenderTransform` stays applied until the old run is reverted at completion (`_previous`, `:131-133`). The
  composition `Translation` therefore adds on top of it, and the page is drawn offset twice.
- **Compositor → UI thread.** The UI-thread animation reads the shorthands, which are at rest, so it starts from
  rest while the paused composition `Translation` still applies until the end.

**Fix.** In `HostRun`'s constructor, when the previous run used the other thread:
- **UI thread → compositor:** revert the previous UI-thread animation immediately after `RunOnCompositor` has seeded
  its scratch. Both changes land in the same compositor commit, so nothing is drawn between them.
- **Compositor → UI thread:** read the paused animation's `ScratchOf(host)`, write those values to the host with
  `Presto.Set` (X, Y, Scale, Rotate, Opacity), revert the compositor animation, and only then start the UI-thread
  animation, which reads them as its starting values.

In both cases the previous run is then dropped rather than kept as `_previous`.

**Tests (`PageTransitionTests`).** Switch between a blur transition (UI thread) and a slide (compositor) mid-flight in
both directions. The host's drawn position and opacity are continuous across the switch, within one frame's travel,
and after completion nothing is left animated on either thread.

## 6. Activity feed offset (investigation)

**Observed once** on the Desktop sample. After two live inserts at the top, a DevTools click on the top row's
Dismiss left `PART_ScrollViewer.Offset` at `0, 39`, which is exactly the day header's height, so the "new" pill
showed. Two later attempts, one with `Animate.ItemLayout` off and one with it back on, stayed at `0, 0`.

**Hypotheses,** in the order to test them:
1. **Selection.** A pointer press inside a `ListBoxItem` selects the row, and `ListBox.AutoScrollToSelectedItem`
   (true by default) scrolls to it when inserts or the removal shift `SelectedIndex`.
2. **Focus.** The clicked row's `Button` holds focus. When its container is removed, focus moves to a neighbor,
   and `BringIntoView` or `VirtualizingStackPanel`'s focused-element handling moves the offset.
3. **Avalonia's anchoring** (item 7) engaging on a pass where the offset was already past its edge tolerance.
4. The `ItemLayout` FLIP measuring a container during the change. This is unlikely, since the transition writes only
   the composition `Translation`, never layout.

**Step.** A headless test in `PrestoSample.Tests` builds the page, runs two timer inserts, selects or focuses the top
row, dismisses it and settles, with `ItemLayout` on and off. The repro decides the outcome:
- **Hypothesis 1 or 2:** fix it in the sample (a feed is not a selection list, so
  `AutoScrollToSelectedItem="False"`, or non-focusable rows) and document the trap beside `Animate.ItemLayout`.
- **Hypothesis 3:** fold it into item 7.
- **No repro:** record it in §7 and close it.

## 7. Scroll anchoring for inserts above

**Today.** Row 33 measured `VirtualizingStackPanel` moving the row being read by about 77 px when one item was
inserted above a list scrolled to 3000. The feed therefore holds live events behind the pill while scrolled down.
Row 33 said the panel keeps no anchor. That is not quite right for 12.1.2:
- `ScrollContentPresenter` implements `IScrollAnchorProvider` and adjusts the offset when the chosen anchor's position
  changes (`ArrangeWithAnchoring`, `Presenters/ScrollContentPresenter.cs:438-480`). This only happens once the offset
  is past its edge tolerance.
- `VirtualizingStackPanel` registers every visible realized container as an anchor candidate
  (`VirtualizingStackPanel.cs:278`).

The shift survives this because an insert before the realized range sets `_startUUnstable`
(`Utils/RealizedStackElements.cs:170-175`). The panel then re-estimates where its first element starts and
re-realizes from that estimate. The anchor element can be recycled, which unregisters it (`:1127`), and the estimate
also moves the extent (row 33's 447 px).

**Decision.** Ship a small bridge helper rather than wait for Avalonia. Phase 32 made list motion first-class, and a
live list that cannot insert while the user reads is a common need. Proposed surface: `Presence.ScrollAnchor` (bool)
on an `ItemsControl`, beside the list rules `Presence` already owns. How it works:
- **On a collection change** that lands before the first visible index (read through `ItemChanges`), record the first
  fully visible item and its offset from the viewport top.
- **After the layout pass,** find that item's container again with `ContainerFromItem`, which works even if it was
  recycled and re-realized, and set `ScrollViewer.Offset` so the item returns to the same place.
- **A second pass** corrects the result if the panel re-estimates again after the offset write.

How it cooperates with the rest of Presto:
- **`Animate.ItemLayout`:** the compensating pass changed the offset, so `ItemChanges.Scrolled` suppresses easing,
  which is Phase 32's rule. Rows stay still.
- **`Presence.Reveal`:** re-realized rows keep their `RevealOnce` memory, which is keyed by item. A row newly
  realized by the correction is a scroll realization, and it reveals as scrolling would.
- **`Presence.Enter`:** an inserted item that lands out of view is not realized, so nothing plays.

**Step 1 (repro first).** Port row 33's measurement into a bridge test: a list scrolled to 3000 with uneven rows and
one insert at index 1. Assert the first visible item's viewport Y before and after, once without the helper (this
pins the Avalonia behavior) and once with it. If Avalonia's own anchoring turns out to hold with some panel setting,
document that setting instead of building the helper.

**Tests.**
- Inserts and removals above, below and inside the viewport.
- A batch insert.
- A reset, which does not anchor.
- Offset 0, which does not anchor, so the list keeps showing new items at the top.
- With `ItemLayout` on, no container eases on the compensating pass.
- With `RevealOnce`, no replay.

**Sample.** The Activity feed turns anchoring on. Live events insert immediately, even while scrolled down, and the
pill becomes a count of "N new above" that scrolls to the top. At the top, events still enter through
`Presence.Enter`. Update row 33's outcome, the feed's caption and `LIFECYCLE-PLAN.md` §10 where they describe the hold.

## 8. Samples

- **Loaders:** the compositor ball flattens onto the floor (item 2).
- **Activity feed:** anchored live inserts and the pill as a count (item 7), plus whatever item 6 decides.
- **Page transitions page:** a transition picker change mid-flight shows the cross-thread takeover (item 5).
- No sample changes for items 1, 3 and 4. They are covered by tests only.

## 9. Open questions (confirm before the phase)

- Item 7's home: `Presence.ScrollAnchor` versus `Animate.ScrollAnchor` or a separate `ScrollAnchor` class. It is not
  an animation, but it exists for animated lists and reads `Presence`'s list-change tracking.
- Item 4: if a negative multiplier has no real use, documenting the difference instead is an option. The mid-drag
  `Refresh` case argues for the fix anyway.

## 10. Deviations as built

- **Item 1.** Built as planned: guard and cursor both ship (PLAN §7 decision 38). The cursor also carries a
  direction flag (`_nextChildIsPrev`), because a timeline running backwards walks its children through `_prev`. A
  child callback that re-renders its own timeline (a `Seek` on the parent) runs the child loop re-entrantly on the same
  cursor, so the outer loop ends that frame where the inner one did instead of carrying on with stale times.
- **Item 3.** Built as planned, except that only `Disarm` bumps the generation: `Arm` and `Release` both go through it.
  The generation check replaces the old `_transition is null && Builder == builder` test, which it implies. The test
  detaches in the gap before the restored arm's next layout pass, which is where the released arm's orphan showed, and
  then reattaches to check that one transition is live and a detach reverts it.
- **Item 2.** Built as planned. The origin subscription is also dropped on cancel, which stops the `CenterPoint`
  expression as a revert does. The absolute origin stays an expression with constant components, so start, restart and
  stop all go through the same `CenterPoint` animation. The `PageSharedAxis` test uses `PageAxis.Z` with a top-left
  origin and checks that the incoming page covers its top-left corner before its bottom-right one.
- **Item 4.** Built as planned. `DraggableParams` is an init-only record read into a readonly field, so `Refresh`
  cannot change `MaxVelocity` mid-drag, and the mid-sequence limit change is covered in `VelocityTrackerTests` only.
  The `DraggableTests` case covers a negative `VelocityMultiplier` instead: the zero seeds hold the velocity at 0 until
  three samples have been taken.
- **Item 5.** Built as planned, with two additions. The compositor-to-UI hand-over also writes `ScaleX` and `ScaleY`,
  since the scratch carries them. The values are copied out of the scratch before the compositor animation is
  reverted, because the revert winds the mirror back to its start. The `Presto.Set` animation belongs to the new run,
  which reverts it on release, so a property the UI-thread run does not animate (such as `X` under a blur-only exit)
  is not left behind. The Page transitions page gains a "Blur slide (UI thread)" pair and a "Next, again mid-flight on
  the other thread" button. The button swaps to the other thread's transition for a second `Next` 200 ms into the
  first one, then restores the picked transition.
- **Item 6.** Not reproduced; closed (PLAN §7 decision 39). `DismissingTheTopRowAfterLiveInsertsLeavesTheFeedAtTheTop`
  keeps the offset at `0, 0` with the row selected across the inserts, `ItemLayout` on and off, ruling out hypotheses
  1, 2 and 4, and hypothesis 3 cannot engage at offset 0; the tests stay as regressions.
- **Item 7 (bridge).** `Presence.ScrollAnchor` as planned, with these differences:
  - **Where it lives.** The logic lives in `ItemChanges`, which already owns the collection and layout handlers.
  - **Avalonia's own anchoring** is not reliable, so the helper ships. In `ScrollAnchorTests`, with rows of 30 and 90 and
    one insert at index 1, it holds at offsets 3000 and 3010. At 3100 the reading row moves by 30. At 2990 it is recycled
    out of view. `CacheLength = 2` fails the same way at other offsets (3010 moves by 30), so no panel setting fixes it.
    The tests run at 2990 and 3100.
  - **Unrealized anchor.** After the panel re-estimates, the anchor is often not realized at all (row 43 gave way to
    rows 36–38), so `ContainerFromItem` alone is not enough. `Correct` first calls `ItemsControl.ScrollIntoView`, which
    runs nested layout passes that a guard keeps from correcting, and then makes the exact correction. That is one of
    the two corrections allowed per change.
  - **Recording the anchor.** The change is recorded in `CollectionChanged`, which runs before the panel's
    `PostCollectionChanged`. At that point containers still hold their old indices and slots while `Items` is already
    new, so the anchor item is the container's `DataContext` rather than `ItemFromContainer`. A list whose items are
    their own containers therefore does not anchor.
  - **What counts as above.** A change is above the anchor when its start index is before it. An insert at the anchor's
    own index also counts. Removing the anchor row itself does not, so the rows below close the gap as they would
    without the anchor.
  - **ItemLayout.** `ItemChanges.Anchoring` is set from the change until the pass that realizes it has finished.
    `LayoutTransition` also moves in one frame when it is set, not only when `Scrolled` is set without `Pending`, so
    both the insert pass and the compensating pass ease nothing.
  - **Enter.** An insert into the partly shown band above the anchor row is in view, so it enters.
  - **Phase 39.** The anchor also ends when a pass finishes at an offset other than the one the change or the last
    correction left, since the reader scrolled, rather than correcting that scroll away (PLAN §7 decision 40,
    LIFECYCLE-PLAN §10.7).
- **Item 7 (sample).** Built as planned. The view model inserts every live event at once and counts one in `Unseen`
  when the feed is off the top, and reaching the top clears the count (the held list and `Release` are gone). The pill
  reads "N new above". `ScrolledDownLiveEventsInsertAtOnceWithoutMovingTheRowsAndAPillCountsThem` replaces the hold
  test: two events at offset 3000 keep the first visible row's viewport Y (it moves by 158 px without the anchor), the
  pill reads "2 new above", and clicking it lands at the top with both new rows realized. Item 6 needed no sample
  change, so `AutoScrollToSelectedItem` and the rows' focusability stay as they were.
