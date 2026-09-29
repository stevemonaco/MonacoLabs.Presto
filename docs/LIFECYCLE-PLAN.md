# Lifecycle animations — one model for initial load, enter and exit

Phase 21 in [PLAN.md](PLAN.md). Status: **built**, with the recommendations in §5 taken. §8 records where implementation refined the proposal; where it and the text above differ, §8 is what shipped.

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
Three of them have an attached property on `Presence`; the fourth is deliberately not animated.

| Moment | When | Property |
|---|---|---|
| **Initial** | the element appears as part of a view's initial load: the window's first show, a page navigated in, the items a list is realized with during its first layout, or a cached view shown again | `Presence.Initial` |
| **Enter** | the element arrives *on its own* into a view that has already loaded: an item added to a collection, content swapped into a `ContentControl`, a child added to a panel | `Presence.Enter` |
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
- Each is independent: an element with only `Exit` set does nothing on arrival. To animate every arrival alike, set
  Initial and Enter to the same builder.
- `Animate.OnAttached` stays unchanged as the **raw trigger**: every attach, no classification. Its docs point to
  `Presence` for lifecycle work. It remains the right tool for things tied to "while attached" rather than to arrival,
  such as the toast countdown bar.
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

Sample: a new Reference page, **Lifecycle**, beside Presence. It has three views, the third a virtualized `ListBox` of 300 rows that scrolls without a log entry, whose rows carry Initial, Enter and Exit,
plus buttons to navigate, show a view again, add and remove rows. A log is written by each builder's `OnBegin`, so it
shows what `Presence` classified rather than what the button meant. The Presence page and the Toasts builder page moved
from `Animate.OnAttached` to `Presence.Initial`/`Enter`; the toast countdown bar keeps the raw trigger.