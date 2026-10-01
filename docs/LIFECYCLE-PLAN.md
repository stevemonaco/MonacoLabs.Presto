# Lifecycle animations — one model for initial load, enter and exit

Phase 21 in [PLAN.md](PLAN.md). Status: **built**, with the recommendations in §5 taken. §8 records where implementation refined the proposal; where it and the text above differ, §8 is what shipped. §9 (`Presence.Arrive`, Phase 26) is **built** as proposed; §10 (`Presence.Reveal`, Phase 27) is **built**, with the §10.2 decisions taken and refined in §8 items 7 and 8; §10.5 stays open.

## 1. Problem

The bridge has three attach/detach animation hooks. They were built at different times and answer different questions:

| Today | Class | Fires | Reverts |
|---|---|---|---|
| `Animate.OnAttached` | `Animate` (named after the trigger) | every visual attach: first window show, whole page navigated in, an item added later, a cached view re-shown | on every detach |
| `Presence.Exit` | `Presence` (named after the meaning) | a detach where the element leaves *on its own* | — (plays on a ghost) |
| `Connected.Motion` / `Anchor` | `Connected` | keyed flights and entrances alongside them | on detach |

The mismatch shows up in the Presence sample, which pairs `Animate.OnAttached` with `Presence.Exit`:

- **Different rules.** The exit is filtered ("leaves on its own"); the entrance is not. A row that plays its exit only
  when removed on its own also plays its entrance when the whole page is navigated in, when the window first opens and
  when a cached view comes back. There is no way to say "animate rows added later, but not the rows the page loads with",
  or the reverse.
- **Different naming.** One is named after an Avalonia event, the other after an intent, and they live on two classes.
- **Undefined ordering.** On detach, `OnAttached` reverts and `Exit` snapshots from two independent handlers. Which runs
  first depends on attribute order in XAML. So the ghost of a row removed mid-entrance starts either from the reverted
  resting look or from where the entrance had reached.
- **No initial-load hook.** A view's first appearance (rows fading up in a stagger as a page opens) cannot be told apart
  from a single item arriving later. Framer Motion covers this with `initial` and `AnimatePresence initial={false}`,
  and anime.js demos with a stagger on page load.

## 2. The model

Every arrival and departure of an element is classified once, by the same rule, into one of four lifecycle moments.
Three of them have an attached property on `Presence`, and `Presence.Arrive` is the fallback for both arrivals; the
fourth is deliberately not animated.

| Moment | When | Property |
|---|---|---|
| **Initial** | the element appears as part of a view's initial load: the window's first show, a page navigated in, the items a list is realized with during its first layout, or a cached view shown again | `Presence.Initial` (fallback `Presence.Arrive`) |
| **Enter** | the element arrives *on its own* into a view that has already loaded: an item added to a collection, content swapped into a `ContentControl`, a child added to a panel | `Presence.Enter` (fallback `Presence.Arrive`) |
| **Exit** | the element leaves *on its own* while its parent stays: an item removed, content swapped out, a child removed | `Presence.Exit` (exists) |
| *Unload* | the element leaves *with* its view: a page navigated away, a window closed | none; page transitions belong to the navigation host |

### 2.1 The rule, stated once

Let **R** be the root of the subtree being attached or detached, and **P** its parent (`VisualTreeAttachmentEventArgs.AttachmentPoint`).

- An element moves **on its own** when every visual between it and R wraps only it (exactly one visual child). This is
  today's Exit rule, now shared by Enter. The element *is* R, or R is an item container, content presenter or similar
  wrapper around it.
- On **attach**, an element that moves on its own into a P whose ancestors are all loaded (`Control.IsLoaded`) **enters**.
  Otherwise, whether it arrived inside a bigger subtree or P or an ancestor is still in its first layout, it is part of an
  **initial** load.
- On **detach**, an element that moves on its own out of a P that stays attached **exits**. Otherwise it **unloads**,
  and nothing plays.

`IsLoaded` is what separates the two arrival kinds. Avalonia sets it only after a control's first layout *and render*
(verified in the 12.1.2 docs), so:

| Case | Classified as | Why |
|---|---|---|
| window first shown | Initial | nothing is loaded yet |
| page navigated in | Initial | the page's rows arrive inside a bigger subtree |
| rows an `ItemsControl` realizes during its first layout | Initial | on their own, but into a panel that is not loaded |
| item added later | Enter | on its own, into a loaded panel |
| cached view shown again | Initial | a detached view is unloaded again (`Unloaded` clears `IsLoaded`) |

Classification happens synchronously at attach, never at `Loaded`: `Loaded` runs after the first render, so an
animation started there would show one frame at rest first.

### 2.2 Ordering and interruption

One tracker per element owns all three properties, so ordering is defined rather than left to XAML attribute order.

- **Detach:** the Exit ghost is snapshotted first, from where the element is drawn (mid-entrance if one is running).
  Only then are the element's own Initial/Enter animations reverted, so the recycled control is left clean.
- **Attach:** an element that re-attaches while its own ghost is still exiting dismisses that ghost at once. This
  happens when a panel child is removed and re-added, or on undo; the ghost stood in for the element. A recycled
  container is a different instance and is not affected.
- **Re-classification:** each attach is classified afresh, so a control can play Initial on one attach and Enter on the next.

### 2.3 Staggering an initial load

Elements that arrive in the same pass under the same loading ancestor form a **batch**. Each element's animation is still
created at its own attach, so the first frame already shows its from-values. Its `(index, total)` for function values and
`Presto.Stagger` comes from its ordinal in the batch. This lets `Delay(Presto.Stagger(40))` ripple rows in as a page
opens, with no code.

`total` is not known while a batch is still arriving, so v1 supports order-based staggers (`From = First`, the
default). Staggers that need the total (`Center`, `Last`, a grid) are an open question (§5.2).

## 3. API

```xml
<DataTemplate x:DataType="local:Task">
    <Border Classes="task"
            presto:Presence.Initial="{x:Static local:Motions.RiseIn}"
            presto:Presence.Enter="{x:Static local:Motions.SlideIn}"
            presto:Presence.Exit="{x:Static local:Motions.SlideOut}" />
</DataTemplate>
```

- `Presence.InitialProperty`, `Presence.EnterProperty`, `Presence.ExitProperty`: all `AnimationBuilder?` on `Control`,
  with the usual `Get`/`Set` accessors. Initial and Enter play on the element itself at UI-thread priority. They revert
  on detach, after any Exit snapshot, and on value change, as `Animate.OnAttached` does today.
- `Presence.ArriveProperty`: the fallback for both arrivals. Initial plays `Initial ?? Arrive` and Enter plays
  `Enter ?? Arrive`, so a set Initial or Enter wins for its own moment.
- Each is independent: an element with only `Exit` set does nothing on arrival. To animate every arrival alike, set
  Arrive.
- `Animate.OnAttached` is the **"while attached" trigger**: every attach, no classification. Use it for loops and
  timers tied to being on screen, such as a skeleton shimmer or the toast countdown bar, and `Presence` for entrances.
- `PrestoAvalonia.AnimateExit` stays the imperative exit. Imperative entrances need nothing new (`control.Animate(...)`).
- Internal: a `PresenceMoment` enum (Initial, Enter, Exit, Unload) and one `Classify` function used by all three, so
  tests can pin the rule directly.

## 4. Migration

| Where | Today | After |
|---|---|---|
| `Pages/PresencePage` rows | `Animate.OnAttached` + `Presence.Exit` | `Presence.Initial` (staggered rise as the page opens) + `Enter` + `Exit` |
| `Pages/PresencePage` swap views | `Animate.OnAttached` + `Presence.Exit` | `Presence.Enter` + `Exit` (the first view gets `Initial`) |
| `Builder/ToastsPage` toast | `Animate.OnAttached` SlideIn; exit collapses by hand | `Presence.Initial`/`Enter` SlideIn; the height collapse stays (it is a layout exit, not a ghost) |
| `Builder/ToastsPage` countdown | `Animate.OnAttached` | unchanged (raw trigger) |
| `Builder/XamlPage`, `FormStatesPage` | `Animate.OnAttached` | unchanged; XamlPage gains a short "OnAttached vs Presence" note |
| README | "Exit animations" section | "Lifecycle animations": the table in §2, the rule, the XAML above |
| PLAN.md §7 decision 17 | "one attached property, other triggers stay open" | amended: the lifecycle triggers live on `Presence` |

No public API is removed, so nothing breaks.

## 5. Open questions (resolved: every recommendation taken)

1. **Name of the initial-load moment.** Recommended: `Initial` (Framer's term, reads as "first appearance"). Rejected:
   `Load`, because Avalonia's `Loaded` also fires for items added later, which is exactly the case this must *not* cover.
   Alternative: `Appear`.
2. **Stagger totals.** Ship order-based staggers only (recommended), or defer every animation in a batch to the first
   engine tick so `total` is known. That tick runs before layout in Avalonia's frame, so rows realized during layout
   would render one frame at rest unless their from-values are written at attach first; a spike would decide.
3. **Virtualized lists.** An item scrolled into view is realized on its own into a loaded panel, so by the rule it
   *enters*. Recommended: accept it and document it, since Framer behaves the same. Alternatively, add
   `Presence.IsInitialScope` or treat a virtualizing panel's realizations as Initial. That is heuristic, because the
   panel does not say why it realized a container.
4. **Unload.** Kept as a non-goal (§2 table). Confirm that page transitions stay with `TransitioningContentControl` /
   `PageTransition` rather than a `Presence.Unload` ghost of a whole view.
5. **Compositor playback.** Initial and Enter run on the UI thread. Offer `Presence.OnCompositor` so they run through
   `AnimateOnCompositor` as `Connected` anchors do? Recommended: later, not in this phase.

## 6. Verification before building

The model rests on two Avalonia behaviours that need a spike, as the Phase 20 ghost rules did:

- `IsLoaded` is false on an `ItemsControl`'s items panel while that control's first layout realizes its containers.
  It is true for a container realized later, and false again for a cached view re-attached after an `Unloaded`.
- Elements attached during a layout pass render their from-values on that pass's frame when the animation is created at
  attach. In other words, no rest-frame flash for Initial.

## 7. Tests (`PresenceTests`, bridge)

- `Classify` for every row of the §2.1 table, plus: content swap (the new view root enters, its content is initial);
  panel child added later (enter); a subtree of two keyed elements added at once (initial for both, since neither
  arrives on its own); window close (unload).
- Initial plays on first show and not when an item is added later; Enter the reverse.
- A three-item `AddRange` and a first-load list each stagger by arrival order.
- Removing an element mid-entrance: the ghost starts from the drawn, mid-entrance look, and the control is reverted.
- A panel child removed and re-added while exiting: the ghost is dismissed and the Enter plays.
- `Animate.OnAttached` behaviour is unchanged (existing `AnimateAttachedTests` stay green).
- Sample: the Presence page opens with a staggered Initial, adds and removes with Enter/Exit, and swaps views,
  all checked on the Desktop head.

## 8. As built

The spike (§6) confirmed both behaviours: `IsLoaded` is false across a view's first layout and after an `Unloaded`, and an
animation created at attach writes its from-values before the first frame. Implementation then refined the rule in four places.

1. **Arrivals stop at panels.** An `ItemsControl` realizing its first item holds one child for a moment, so the
   "wraps only it" test walked straight past a panel. It could then misclassify a view's only row, or split one list
   into several batches. An arrival now walks up past non-panel single-child ancestors that have not loaded. It stops at
   the first panel, multi-child ancestor or loaded ancestor: loaded means Enter, anything else Initial. The separate "on
   its own" test for arrivals turned out to be redundant with this and is gone. It remains for departures.
2. **Exit needs a loaded parent.** A view shown again re-creates its item containers before it has loaded, which ghosted
   every row. It was caught on the Desktop head in the Lifecycle sample. A departure now exits only from a loaded parent,
   the mirror of the arrival rule.
3. **Batches are keyed by panel.** Keying by the ancestor that decided the classification split a list once its item
   containers had loaded, since each container then decided for its own content. The key is the nearest `Panel` ancestor.
4. **`Stagger` follows a growing group.** A stagger sized its cache by the first group it met, as upstream's closure does,
   so a builder's shared stagger read past its cache once a batch grew and gave every later element a zero delay. It now
   rebuilds when the target count changes (a pinned deviation, `StaggerTests.AStaggerReusedOnALargerGroupStaggersEveryTarget`).

5. **Lists decide for their items** (a follow-up, resolving §5.3 differently from its recommendation). A `ListBox` spike
   showed a virtualizing panel *detaching* a row's content when it recycles the container on scroll. Every row scrolled out
   therefore exited on a ghost, and every row scrolled in entered. For an element that is the content of an item container,
   the container or a part of its template, `Presence` now watches the list's `Items` for changes:
   - it **enters** only if its item was just added;
   - it **exits** only if its item was just removed or the list was reset;
   - scroll realization and recycling are new internal moments, `Realize` and `Recycle`, which play nothing.

   The owner is found from the items panel, because a container being recycled has already lost the logical parent
   `ItemsControlFromItemContainer` reads. A list is watched from the first lifecycle arrival in it. It answers only after
   that pass has rendered, so the arrivals of that first pass fall back to the general rule. Covered by
   `ScrollingAVirtualizedListPlaysNothingAndLeavesNoGhosts`, `AVirtualizedListStillEntersAddedItemsAndExitsRemovedOnes`
   and `ClearingAListExitsEveryRow`.
   Three further fixes surfaced only on the Desktop head, where layout runs on the next render tick rather than inside
   the dispatcher job that made the change (headless tests cannot reproduce that ordering):
   - The "just changed" window, and a stagger batch, now end after the next layout pass (`LayoutUpdated`, then a posted
     job) instead of at `DispatcherPriority.Loaded`. After a click, `Loaded` ran before the layout pass that realized the
     added row, so the row was classified as scroll realization.
   - A virtualizing panel moves a removed item's container before the item's content detaches, so the content of a
     container in a `VirtualizingPanel` records where it is drawn after each layout pass, and its ghost uses that slot.
   - The ghost is clipped only by what is outside the item container (the recycled container clipped it to itself), and
     while it is snapshotted, a recycled row inherits from where it was drawn rather than from the ancestor lending it
     styles.

6. **`Presence.Arrive`** (Phase 26, §9) built as proposed. The tracker plays `Initial ?? Arrive` on an initial load and
   `Enter ?? Arrive` on an enter, and `HandleChanged` keeps it alive while any of the four properties is set. The
   sample moved its entrances to `Arrive`: XamlPage, the Recipe gallery's Fade and Pop, the Toasts toast and the
   Lifecycle view panels. The XamlPage star, Shake, the toast countdown and the storefront shimmer stay on `OnAttached`.

7. **Reveal snaps on jumps and keeps arrival order** (Phase 27, §10.2.4 and 5). The item-change watcher also records the
   list's `ScrollViewer` offset (the nearest one above the items panel) each time a layout pass ends. A row realized in a
   pass whose offset has moved more than the viewport on either axis since then is marked seen and not revealed, with
   `RevealOnce` false too. There is no setting. Covered by `AJumpOfMoreThanAViewportSnapsTheRowsItRealizes`.
   Batch ordinals keep realization order, which starts next to the rows already on screen and ends
   at the edge scrolled in from, so a ripple follows the scroll: top-down when scrolling down
   (`RowsRevealedInOnePassStaggerDownFromTheRowsAlreadyShown`), bottom-up when scrolling up. Sorting from the leading edge
   instead would mean re-creating the batch's animations once the pass ends, since each writes its from-values at attach
   with the index it has then, and the ripple would run against the scroll.

8. **The seen memory lives on the item-change watcher** (Phase 27, §10.2.1 and 2). It is a set of items beside
   `Added` and `Removed`, sharing the one `CollectionChanged` subscription. Every list-row arrival marks its item seen,
   whether or not a builder played, and this starts with the list's first arrival, before the watcher answers (§8.5), so
   the rows of a first load count. A removed or replaced item is forgotten, a moved one is kept, and a reset clears the
   set. A recycled container first attaches its template built for no item, which classifies as `Realize`; a row
   without a `DataContext` is neither revealed nor remembered, so these placeholders take no ordinal. `Arrive` is not a
   fallback for `Realize`: it stays "every classified arrival" of §9, and a reveal is opted into only through `Reveal`.
   Tests read the set through the internal `Presence.Seen(ItemsControl)`. Covered by
   `TheRowsOfTheFirstLoadAndAnAddedItemCountAsSeen`, `ScrollingBackOverSeenRowsRevealsThemOnlyWithoutRevealOnce`,
   `AnItemRemovedAndAddedBackIsRevealedAgain`, `AResetForgetsTheSeenRows` and
   `TheSeenMemoryHoldsNoItemAfterItLeavesTheList`. The sample is the Showcase **Activity feed**
   ([SAMPLES-PLAN.md](SAMPLES-PLAN.md) §4.11), whose overlay switches Reveal between off, once and every time.

Sample: a new Reference page, **Lifecycle**, beside Presence. It has three views, the third a virtualized `ListBox` of 300 rows that scrolls without a log entry, whose rows carry Initial, Enter and Exit,
plus buttons to navigate, show a view again, add and remove rows. A log is written by each builder's `OnBegin`, so it
shows what `Presence` classified rather than what the button meant. The Presence page moved from `Animate.OnAttached`
to `Presence.Initial`/`Enter`.

## 9. `Presence.Arrive` (Phase 26)

### 9.1 Problem

Animating every arrival alike means setting `Initial` and `Enter` to the same builder (§3). That duplication is why the
sample still reaches for `Animate.OnAttached` to play entrances: `Builder/XamlPage`'s cards and staggered list, and the
Fade and Pop swatches on `Builder/RecipeGalleryPage`. `OnAttached` is the wrong tool there. It plays on every attach,
including a virtualizing panel's recycling, which `Presence` deliberately ignores (§8.5).

### 9.2 Decisions

1. **`Presence.Arrive` is a fourth attached property**, `AnimationBuilder?` on `Control`, not a setter that writes
   `Initial` and `Enter`. Only a real property works from style setters and bindings, and it leaves `Get`/`Set` on the
   other two meaning what they say.
2. **It is the fallback for both arrival moments.** The tracker's builder for Initial is `Initial ?? Arrive`, and for
   Enter it is `Enter ?? Arrive`. A set `Initial` or `Enter` wins for its own moment, so "rise on page load, slide when
   added" is `Arrive` plus one override. `Realize` and `Recycle` still play nothing, and `Exit` is unaffected.
3. **Change and revert behave like `Initial`/`Enter` as built.** A new value applies from the next arrival. The tracker
   lives while any of the four properties is set (`HandleChanged` gains `Arrive`), and clearing the last one releases it
   and reverts. Detach reverts after any Exit snapshot, and initial-load batching and stagger ordinals apply unchanged.
4. **`Animate.OnAttached` becomes the "while attached" trigger.** Its doc comment, the `Presence` class remarks and the
   README's "From XAML" and "Lifecycle animations" sections say to use it for loops and timers tied to being on screen,
   such as a skeleton shimmer or a countdown, and to use `Presence` for entrances. No API change.

### 9.3 Migration

| Where | Today | After |
|---|---|---|
| `Builder/XamlPage` cards and list | `Animate.OnAttached` | `Presence.Arrive`; the page teaches the three XAML spellings on `Arrive`, and its "OnAttached vs Presence" note becomes the §9.2.4 wording |
| `Builder/XamlPage` binding panel (Float/Shake) | `Animate.OnAttached="{Binding Emphasis}"` | unchanged: a looping emphasis is "while attached" |
| `Builder/RecipeGalleryPage` Fade and Pop swatches | `Animate.OnAttached` | `Presence.Arrive`; Shake stays on `OnAttached` |
| `Builder/ToastsPage` toast | `Presence.Initial` + `Enter` with one recipe | `Presence.Arrive` |
| `Builder/ToastsPage` countdown, `Showcase/StorefrontPage` shimmer | `Animate.OnAttached` | unchanged |
| `Pages/LifecyclePage` view panels (`SetInitial`/`SetEnter` with one `arrive` builder) | both | `Presence.SetArrive`; the rows keep distinct Initial and Enter, since they demonstrate the classification |

Each page that prints its own code has its printed snippet updated to match. `BuilderPagesTests`, which finds the
`OnAttached` elements, follows the move.

### 9.4 Tests (`PresenceLifecycleTests`, bridge)

- `Arrive` alone plays on an initial load and on an enter.
- A set `Initial` wins over `Arrive` on an initial load while `Arrive` still plays on an enter, and the mirror case for `Enter`.
- `Arrive` alone plays nothing on scroll realization or recycling in a virtualized list.
- An initial-load batch using `Arrive` staggers by arrival order.
- An element with only `Arrive` set keeps its tracker, clearing it mid-animation reverts, and detach reverts after an Exit snapshot.
- Existing `PresenceTests`, `PresenceLifecycleTests` and `AnimateAttachedTests` stay green.
- Sample: XamlPage, Recipe gallery and Toasts checked on the Desktop head (Replay, add and remove).

## 10. `Presence.Reveal` (Phase 27)

### 10.1 Problem

A virtualized list has a third kind of arrival: a row scrolled into view. Presence classifies it as the internal
`Realize` moment and plays nothing (§8.5), which is right for entrances but leaves no way to reveal rows as they scroll
in. `Animate.OnAttached` plays there, but it also plays on the first load and on adds, gets no stagger ordinal and
replays on every recycle. `ScrollObserver` watches a `ScrollViewer`, not recycled rows.

### 10.2 Decisions (taken; see §8 items 7 and 8)

1. **`Presence.Reveal` is an `AnimationBuilder?` that plays on `Realize` only.** It never plays on Initial, Enter or
   Recycle, and it has no effect outside an item container of a list, where there is no `Realize`. A non-virtualized
   list inside a `ScrollViewer` never realizes on scroll, so scroll reveals there stay with `ScrollObserver`.
2. **Once per item by default.** `Presence.RevealOnce` (`bool`, default `true`) makes a row reveal only the first time its
   item is realized. The memory is kept per list and keyed by item, not container, since containers are recycled. An item
   counts as seen once it has been shown by any moment, Initial and Enter included. The memory drops an item when it
   leaves `Items` and is cleared on reset, reusing the item-change watcher from §8.5. With `RevealOnce` false, every
   realization reveals.
3. **Direction follows from once.** In the default mode, scrolling back up reveals nothing, because those rows were seen.
   A row is revealed from above only after a jump past it (for example, dragging the scrollbar to the end and scrolling
   back). v1 plays the same builder from either edge. Mirroring by scroll direction is left open (§10.5).
4. **Jumps and flings snap.** When the list's `ScrollViewer` has moved more than one viewport since the previous layout
   pass, the rows realized in that pass are marked seen and not animated. A fling or a scrollbar jump then lands at rest
   instead of fading in a screen of rows at once. It's a fixed rule with no public setting in v1.
5. **Rows realized in one layout pass share a batch**, as an initial load does (§2.3, §8.3), so
   `Delay(Presto.Stagger(30))` ripples the rows a scroll brings in. Ordinals follow realization order, and the spike
   below checks that order runs outward from the edge scrolled in from.

### 10.3 Spike before building

- What order `VirtualizingStackPanel` realizes rows in when scrolling down and up. If it is not edge-outward, either
  sort a batch's ordinals by distance from the leading edge before its animations start, or accept arrival order.
- Whether an animation created at realization shows its from-values on the first frame, as Initial does (§6). A
  realization happens inside a layout pass driven by scrolling, not by a first layout.
- How far `ScrollViewer.Offset` moves between passes on a mouse-wheel notch, a touchpad fling and a scrollbar drag,
  to confirm "one viewport" separates them.

### 10.4 Tests (`PresenceLifecycleTests`, bridge)

- Scrolling a virtualized list reveals newly realized rows, and not the rows realized on first load.
- Scrolling back over seen rows reveals nothing with `RevealOnce`, and does reveal them with `RevealOnce` false.
- An item added and then scrolled to is not revealed again (Enter marked it seen). An item removed and re-added is.
- A reset clears the memory.
- A scroll of more than one viewport in one pass snaps its rows.
- Rows realized in one pass stagger by ordinal.
- Recycle and detach revert a running reveal.
- The seen memory holds no item after it leaves `Items`.

### 10.5 Open

- **Mirroring by direction.** Should a row revealed while scrolling up start from above? That needs either a direction
  the builder can read (a function value, or a `Presence.RevealFromAbove` companion) or a mirrored copy of the builder.
  Decide after the Activity feed shows whether it is noticeable. With the feed built: in the default once mode a row is
  revealed while scrolling up only after a jump passed over it, since a slow scroll up meets rows already seen, so the
  case is rare; the "Every time" setting makes it constant. Whether the upward rise reads as wrong there is left to a
  Desktop hand check of the feed, not yet done.
- **Inserts above a scrolled list.** The Activity feed no longer holds live events back while scrolled down: with
  `Presence.ScrollAnchor` (Phase 34) it inserts them at once. An event inserted above the rows in view is not realized,
  so `Enter` neither plays nor marks it seen, and it is revealed when scrolled to, like any unseen row. They reveal a
  scroll step at a time as of PLAN Phase 39 (§10.7).

### 10.6 Spike

Headless `ListBox` of 200 rows, 300 high (about 40 per row), measured before `reveal-core` was built on it.

- **Realization order.** Scrolling down realizes rows top-down and scrolling up realizes them bottom-up: each pass starts
  next to the rows already on screen and ends at the edge scrolled in from. Arrival order is kept (§8.7).
- **First frame.** An animation created at realization writes its from-values before the first frame, as Initial does.
  `RowsRevealedInOnePassStaggerDownFromTheRowsAlreadyShown` asserts the last row of a pass, still in its delay, is at 0.

- **Offset per pass.** A wheel notch moves 50 in one pass (a sixth of this viewport). Setting `Offset` to 3000 lands in
  one pass, and it realizes and recycles rows in between as the panel re-estimates. Each pointer move on the scrollbar
  thumb is one pass, at about 31 of offset per pixel dragged (5 px moved 150 to 170), so a drag faster than about 10 px
  per frame covers more than a viewport. A touchpad fling cannot be driven headless and is left for the Desktop head.
  One viewport therefore separates a wheel notch and a slow drag from jumps and fast drags, and a fast drag snapping
  is what §10.2.4 intends.

### 10.7 Phase 39 (as built)

Each bug was pinned by a failing headless test before its fix, measured on the Activity feed (viewport 690, rows 68
apart, extent about 140 000) and in `ScrollAnchorTests`. The tests pass now and stay as regressions.

**Bug A: new rows do not reveal one scroll step at a time.**
- **Input.** `EventsInsertedAboveRevealOnlyInThePassThatBringsThemIntoView`: three live events at 300 or 3000, then single
  wheel notches up (15 or 60 frames apart) or a 1 px-per-move thumb drag. The live timer keeps inserting every 4 s.
  Fails at 3000 by wheel and by drag, and at 300 with 60 frames between notches.
- **Cause.** Without an insert mid-scroll each notch reveals one new row, but `Tracker.HandleAttached` adds to `Seen` on
  every attach, including rows realized only in the insert pass that the anchor correction scrolls away before anything
  is drawn. A row re-attached in that pass loses its running reveal and, now seen, does not start another.
- **Fix.** A row realized for a reveal is not marked seen: its list holds it (`ItemChanges.Held`) until a
  layout pass ends with its container intersecting the viewport. The held rows that pass shows then reveal as one batch
  in realization order. A pass whose anchor correction scrolled is skipped, a jump marks the held rows seen, and a
  detach drops them. A second path lost reveals too: on a plain list a notch reveals a row, and the insert landing in the
  same frame re-realizes it, reverting the reveal. So a recycle while the list is anchoring forgets a row whose reveal
  has not finished. `PresenceLifecycleTests.RowsInsertedAboveAScrolledListRevealInTheWheelNotchThatBringsThemIntoView`
  (1000) pins that path, and the `CacheLength` cases pin rows realized out of view.

**Bug B: scrolling to the top lands on an old row.**
- **Input.** `ScrollingBackToTheTopLandsThereWithTheNewestEventShown`, "before the anchor clears": from 300 with three
  inserts, wheel up to 83, insert one event and run one layout pass, then scroll to 0 by 10 notches or a 100 px drag
  sent before the dispatcher runs its Background jobs, and run the next pass. The headless input methods run queued jobs
  first, so the test calls `IHeadlessWindow` directly. `AWheelNotchBeforeTheAnchorClearsIsNotUndone` does the same on
  the 30/90 rows.
- **Cause.** `ItemChanges._anchor` lives until `AfterLayout`'s Background post, and `OnLayoutUpdated`
  corrects on every pass while it is set. A scroll in that window is undone. On a desktop, jobs below `Input` wait while
  input is pending, so a continuous wheel or drag keeps the anchor alive for the passes that follow.
- **Fix (deviates from FIXES-PLAN §7).** `ItemChanges` keeps the offset the anchor expects: the offset at
  the change, then the offset each correction leaves. A pass that ends at any other offset was scrolled by the reader,
  so it drops the anchor instead of correcting. The anchor still waits for the Background clear otherwise, since the
  panel's shift can show one pass after the insert. `AScrollToTheTopBeforeTheAnchorClearsStaysThereWithTheAddedRowShown`
  pins it on a plain `ListBox`.

**Desktop check.** DevTools can send clicks and keys but not wheel or drag input, so the Desktop feed (viewport 465) was
driven by scrollbar line and page buttons and `PageUp`/`PageDown`, with live events landing throughout. Scrolled
to 1 400 with three to six events above, each step up revealed only the new rows it brought in (Reveal 24 → 25 → 26
→ 27, then +7 staggered for a page) and ended at `0, 0` with the newest row and the day header shown and the pill
cleared; an event landing at 0 kept the offset there. The pill click snapped to the top with Reveal unchanged
(§10.2.4). Wheel notches, touchpad scrolling and thumb drags are covered by the headless tests only.

**Open: manual wheel/drag check on Desktop.** Neither bug reproduced from DevTools-set offsets, and Bug B's cause
(Background jobs starved while wheel or drag input is pending) is inferred from dispatcher priorities, not observed. A
manual pass with a mouse wheel, touchpad and thumb drag on the Desktop Activity feed is still needed to confirm both fixes.
