---
id: BRIDGE-TOAST
title: Toast host
package: Monaco.Presto.AvaloniaBridge
namespaces:
  - Monaco.Presto.AvaloniaBridge.Controls
sources:
  - Monaco.Presto.AvaloniaBridge/Controls/ToastHost.cs
  - Monaco.Presto.AvaloniaBridge/Controls/Toast.cs
types:
  - Toast
  - ToastHost
tests:
  - ToastHostTests
  - BuilderRecipePagesTests
  - MicroInteractionsPageTests
upstream: []
depends:
  - BRIDGE-PRESENCE
  - BRIDGE-SWIPE
  - BRIDGE-DECLLAYOUT
  - BRIDGE-HELPERS
  - CORE-TIMER
  - CORE-ANIMATION
  - CORE-REDUCED
---

# Toast host

## Purpose

`ToastHost` is an `ItemsControl` that owns its items and shows a capped stack of toasts: `Show` adds one, which counts down and dismisses itself, holds while hovered or focused, and leaves on a horizontal swipe. The motion is the item template's (`Presence.Arrive`/`Exit`, BRIDGE-PRESENCE) and the host's `Animate.ItemLayout` restack (BRIDGE-DECLLAYOUT); the host adds the countdown, the cap and the swipe (BRIDGE-SWIPE). `Toast` is the handle `Show` returns.

## Requirements

### Showing

- **BRIDGE-TOAST-001** — When `Show(content, duration)` is called, the host shall add `content` as the last item, built by `ItemTemplate`, and return a new `Toast` for it.
  - Tests: `ToastHostTests.ShowPlaysTheEntrance`, `ToastHostTests.DismissRemovesTheToastAndToastFindsItFromInside`
- **BRIDGE-TOAST-002** — When `Show` is called while `max(MaxCount, 1)` toasts are showing, the host shall dismiss the oldest toasts until there is room, before it adds the new one.
  - Tests: `ToastHostTests.PastMaxCountTheOldestLeavesAndTheRestRestack`, `BuilderRecipePagesTests.ToastStackTrimsToFourAndEveryToastExpires`
- **BRIDGE-TOAST-003** (inherited) — The host shall keep Fluent's `ItemsControl` theme through its style key.
  - Tests: untested
- **BRIDGE-TOAST-004** — The host shall set `ToastHost.Toast` on each item container, inherited, so `GetToast` on any element inside the toast returns its `Toast`, and null outside one.
  - Tests: `ToastHostTests.DismissRemovesTheToastAndToastFindsItFromInside`
- **BRIDGE-TOAST-005** — A toast's entrance shall be its template root's `Presence.Arrive`, and the rest of the stack shall restack through the host's `Animate.ItemLayout` when set.
  - Tests: `ToastHostTests.ShowPlaysTheEntrance`, `ToastHostTests.PastMaxCountTheOldestLeavesAndTheRestRestack`

### Countdown

- **BRIDGE-TOAST-006** — When a toast's container first loads, the toast shall start its countdown of `duration` ms; a later reload shall not restart it.
  - Tests: `ToastHostTests.TheCountdownRunsTheBarDownAndDismissesTheToastWithItsExit`
- **BRIDGE-TOAST-007** — When an element inside the container has `ToastHost.Countdown` set, the countdown shall animate the named property or shorthand of the first such element from 1 to 0 linearly over the duration, and dismiss the toast on completion.
  - Tests: `ToastHostTests.TheCountdownRunsTheBarDownAndDismissesTheToastWithItsExit`, `MicroInteractionsPageTests.NotifyAddsAToastThatDismissesItselfWhenItsTimerRunsOut`
- **BRIDGE-TOAST-008** — When no element is marked with `ToastHost.Countdown`, the countdown shall be a plain timer of the duration that dismisses the toast on completion.
  - Tests: untested
- **BRIDGE-TOAST-009** — While the pointer is over the container or focus is within it, the countdown shall pause; it shall resume when both end.
  - Tests: `ToastHostTests.HoverOrFocusHoldsTheCountdownUntilItLeaves`, `MicroInteractionsPageTests.HoveringAToastHoldsItsCountdownUntilThePointerLeaves`
- **BRIDGE-TOAST-010** (inherited) — When the countdown starts while the pointer is already over the container or focus is within it, it shall start paused.
  - Tests: untested

### Dismissing

- **BRIDGE-TOAST-011** — When `Toast.Dismiss()` is called, the toast shall stop listening to its container, cancel its countdown and remove its item from the host at once, so its exit plays on a ghost.
  - Tests: `ToastHostTests.DismissRemovesTheToastAndToastFindsItFromInside`, `BuilderRecipePagesTests.ClosingAToastRemovesItAtOnceAndPlaysItsExitOnAGhost`
- **BRIDGE-TOAST-012** — When the countdown completes, the toast shall be dismissed with its exit.
  - Tests: `ToastHostTests.TheCountdownRunsTheBarDownAndDismissesTheToastWithItsExit`
- **BRIDGE-TOAST-013** (inherited) — A second `Dismiss()` on a toast already removed shall do nothing.
  - Tests: untested
- **BRIDGE-TOAST-014** — The host shall put `Swipe.Dismiss` and a commit command on each container, so a horizontal swipe either way past half the toast's width slides it out.
  - Tests: `ToastHostTests.ASwipeSlidesTheToastOutAndDismissesItWithoutItsExit`, `MicroInteractionsPageTests.SwipingAToastPastHalfItsWidthSlidesItOutTowardsTheSwipeAndDismissesIt`
- **BRIDGE-TOAST-015** — When a swipe commits, the toast shall set its container's opacity to 0 and dismiss itself, so it leaves without playing its exit from its slot.
  - Tests: `ToastHostTests.ASwipeSlidesTheToastOutAndDismissesItWithoutItsExit`

### Reduced motion

- **BRIDGE-TOAST-016** — While `ReducedMotion` is set when a countdown starts, the marked element shall animate `Opacity` from 1 to 0.35 instead of its named property, which stays at its resting value, and the toast shall still dismiss itself at the end.
  - Tests: `ToastHostTests.UnderReducedMotionTheToastArrivesInPlaceAndTheBarFadesInsteadOfShrinking`, `BuilderRecipePagesTests.ToastCountdownFadesInsteadOfShrinkingUnderReducedMotionAndStillExpires`
- **BRIDGE-TOAST-017** — The entrance, exit and restack shall take their own builders' reduced forms; the host adds none.
  - Tests: `ToastHostTests.UnderReducedMotionTheToastArrivesInPlaceAndTheBarFadesInsteadOfShrinking`

### Defaults

- **BRIDGE-TOAST-018** — When `MaxCount` is not set, it shall be 3.
  - Tests: untested
- **BRIDGE-TOAST-019** — When `Show` is called without a `duration`, the countdown shall last 4000 ms.
  - Tests: untested

## Invariants

- The host's `Items` and its list of live toasts are in the same order and of the same length: item `i` belongs to toast `i`.
- At most `max(MaxCount, 1)` toasts show after any `Show`.

## Edge cases

- `MaxCount` 0 or negative behaves as 1.
- Lowering `MaxCount` does not trim the stack; the next `Show` does.
- `Dismiss()` before the container loads removes the item and no countdown ever starts.
- A duration of 0 dismisses the toast on the first tick after it loads.

## Threading and lifetime

- UI thread only. A `Toast` holds its host, container and countdown; once dismissed it drops its container handlers and is unreferenced by the host. The countdown is a regular engine animation or timer, held by the `Toast`, since `Presto.GetAnimations` omits paused animations (BRIDGE-HELPERS).

## Reduced motion

- Read when each countdown starts: the bar fades its opacity to 35 % instead of moving; the template's builders supply their own reduced forms (BRIDGE-TOAST-016–017). A swipe follows BRIDGE-SWIPE's reduced rule.

## Allocation

- One timer or animation per toast. Not on the tick path otherwise.

## Deviations from anime.js

- No upstream counterpart.

## Decisions

- **`ToastHost` owns its items and trims instead of queueing.** It is an `ItemsControl` filled only through `Show`, which dismisses the oldest toasts past `MaxCount` before adding; the motion is the item template's `Presence` and the host's `Animate.ItemLayout`, and the swipe is `Swipe` on the container. Reason: a cap trimmed inside the collection's own `CollectionChanged` cannot change it there, so the host must add the items itself; no page waits for a toast, so trimming is enough. Rejected: a queue; a host over a view-model collection.
- **`GetAnimations` omitting paused animations is documented, not fixed.** See BRIDGE-HELPERS.

## Non-goals

- No queue: toasts past the cap are trimmed, not deferred.
- No `ItemsSource` support; the host owns its `Items`.
- No animation properties of its own; the motion is the template's.

## Open items

- `Items` changed directly, or `ItemsSource` set, desyncs the host's toast list from its items; nothing guards it, and preparing a container for an item `Show` did not add indexes past the list.
- A re-prepared container (template change) would call `Attach` again and double its handlers; untested.
- Untested: the plain-timer countdown without a marked element, starting paused under an existing hover, a second `Dismiss`, and `MaxCount <= 0`.
