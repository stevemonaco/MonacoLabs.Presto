---
id: CORE-CUE
title: Cues
package: MonacoLabs.Presto
namespaces:
  - MonacoLabs.Presto
sources:
  - MonacoLabs.Presto/Cue.cs
  - MonacoLabs.Presto/CueOfT.cs
  - MonacoLabs.Presto/ICueSource.cs
types:
  - Cue
tests:
  - CueTests
  - AllocationTests
upstream: []
depends: []
---

# Cues

## Purpose

`Cue` and `Cue<T>` are raise-only signals a view model exposes as properties (`public Cue<TaskItem> TaskDeleted { get; } = new();`, then `TaskDeleted.Raise(task)`), so a view can react to a one-off moment through a binding instead of a CLR event subscribed by name. Both implement `IObservable<…>`. An internal non-generic interface lets the bridge subscribe to either with one object-valued sink; the bridge's cue triggers are specified in BRIDGE-ATTACH.

## Requirements

### Raising

- **CORE-CUE-001** — `Raise` shall notify the subscribers present when it starts, in subscription order, passing its value (`null` for `Cue`).
  - Tests: `CueTests.RaiseNotifiesSubscribersInSubscriptionOrder`, `CueTests.ValueTypeCueDeliversTypedValuesAndAnObjectSinkGetsTheSameValue`
- **CORE-CUE-002** — A subscription shall end when its `IDisposable` is disposed, including from inside a raise, and later raises shall not reach it.
  - Tests: `CueTests.DisposingInsideARaiseEndsTheSubscriptionForLaterRaisesOnly`, `CueTests.CuesWorkAsObservablesAndNeverCompleteOrFail`
- **CORE-CUE-003** — A cue shall keep no value: a subscriber added after a raise shall not receive it.
  - Tests: `CueTests.ASubscriberAddedAfterARaiseReceivesNothing`
- **CORE-CUE-004** — `Raise` with no subscribers shall do nothing and allocate nothing, and `Raise` with one typed subscriber shall allocate nothing, without boxing a value-type payload.
  - Tests: `AllocationTests.RaisingACueWithNoneOrOneSubscriberAllocatesNothing`

### Observable

- **CORE-CUE-005** — `Cue` shall implement `IObservable<object?>` and `Cue<T>` shall implement `IObservable<T>`; neither shall call `OnError` or `OnCompleted`.
  - Tests: `CueTests.CuesWorkAsObservablesAndNeverCompleteOrFail`

## Invariants

- A cue stores only its subscriber list, never a raised value.

## Edge cases

- A subscription disposed during a raise, by itself or by an earlier subscriber, still receives that raise, since the raise notifies the subscribers present when it started.
- A subscriber added during a raise is not notified by that raise.
- Disposing a subscription twice does nothing.
- One observer subscribed twice is notified twice, and each subscription ends separately.
- An exception thrown by a subscriber propagates out of `Raise`, and the later subscribers of that raise are not notified.
- `Subscribe(null)` throws `ArgumentNullException`.

## Threading and lifetime

- Single-threaded like the rest of the core, with no locks: raise, subscribe and dispose on one thread.
- A subscription holds its observer strongly until it is disposed; the cue does not end subscriptions itself.

## Reduced motion

- Not affected: a cue raises signals, not motion.

## Allocation

- Subscribers are kept in a copy-on-write array: subscribing and disposing allocate a new array, and `Raise` iterates the current one, so a raise allocates nothing for typed subscribers. The bridge's object-valued sink boxes a value-type payload once per raise.

## Provenance

- No upstream counterpart: anime.js has no view-model signals.

## Decisions

- **A cue is a core object exposed as a property, not a CLR event bound by name.** Reason: a binding already reaches a view-model property in compiled XAML, while subscribing to an event by name needs reflection, which is trim- and AOT-hostile (CORE-TARGETS), and a misspelled name only fails at run time. The core is the home because a core-only view model must be able to raise one. Rejected: an event-name attached property (reflection); a `bool` or counter property the view model toggles (it has to be reset, and two raises in one frame collapse into one); a command the view sets on the view model (it inverts ownership and is still code).
- **A cue holds no value and replays nothing.** Reason: it marks a moment, and a view that attaches later must not react to a moment that ended before it existed. Rejected: a "last value" replay like `BehaviorSubject`.
- **Subscribe while attached, not while the property is set.** A view subscribes to a cue only while its element is attached (BRIDGE-ATTACH). Reason: a view model often outlives its view, so a subscription made when the property is set would keep a detached view alive through the cue. Rejected: weak subscriptions, which add a weak-reference type to the core for what attachment already bounds.
- **One internal hook for both cue types.** `Cue` and `Cue<T>` implement an internal interface whose `Subscribe` takes an object-valued sink, exposed to the bridge through `InternalsVisibleTo`. Reason: the bridge accepts any cue with one type check and no reflection, and a value-type `Cue<T>` is not an `IObservable<object?>`. Rejected: a public non-generic base, a public type only the bridge would read.
- **A raise notifies a snapshot.** Reason: copy-on-write makes the snapshot free, so a raise allocates nothing, and subscribing or disposing from a handler cannot disturb the iteration. Rejected: checking each subscriber is still present before notifying it, which costs a search per subscriber for a case no known caller has.

## Non-goals

- Thread safety.
- Replay, a current value, completion or errors.
- Weak subscriptions.

## Open items

- None.
