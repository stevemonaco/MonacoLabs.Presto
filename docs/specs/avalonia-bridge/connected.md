---
id: BRIDGE-CONNECTED
title: Connected animations from XAML
package: MonacoLabs.Presto.Avalonia
namespaces:
  - MonacoLabs.Presto
sources:
  - MonacoLabs.Presto.Avalonia/Connected.cs
types:
  - Connected
tests:
  - ConnectedTests
  - AnimateLayoutAttachedTests
upstream: []
depends:
  - BRIDGE-LAYOUT
  - BRIDGE-THREAD
  - BRIDGE-DECLLAYOUT
  - CORE-BUILDER
  - CORE-DIAGNOSTICS
---

# Connected animations from XAML

## Purpose

Three attached properties give MVVM navigation connected animations with no code-behind: `Key` marks elements in two views as the same thing, `Motion` says how an element arrives, and `Anchor` makes an element enter alongside a keyed flight without flying. Keyed elements record where they are drawn while attached, prepare their key when they leave, and claim it when they arrive. Flights are shared-element layout transitions (BRIDGE-LAYOUT); anchored entrances are compositor animations (BRIDGE-THREAD).

## Requirements

### Arriving

- **BRIDGE-CONNECTED-001** — When an element with a `Key` attaches while another attached element holds the same key, it shall fly from where that element is drawn now into its own slot.
  - Tests: `ConnectedTests.CoexistingViewsFlyFromTheLiveElementAndLeaveNothingPrepared`
- **BRIDGE-CONNECTED-002** — When an element with a `Key` attaches and no other attached element holds the key, it shall fly from the key's prepared entry, consuming it.
  - Tests: `ConnectedTests.AContentSwapFliesTheArrivingElementFromWhereTheLeavingOneWasDrawn`, `ConnectedTests.AContentSwapFliesOutOfAScrolledList`
- **BRIDGE-CONNECTED-003** — When neither exists, or the prepared entry is more than three seconds old, the arriving element shall not animate.
  - Tests: `ConnectedTests.AFirstLoadWithNothingPreparedAnimatesNothing`, `ConnectedTests.APreparedRectIsTakenWithinThreeSecondsAndExpiresAfter`
- **BRIDGE-CONNECTED-004** — A flight shall be a one-shot `PositionAndSize` shared-element transition with the arriving element's `Motion`, or `DefaultMotion` when it has none; the leaving element's `Motion` shall never be used.
  - Tests: `ConnectedTests.TheArrivingElementsMotionSetsTheFlight`, `ConnectedTests.TheLeavingElementsMotionIsIgnoredForTheDefault`, `ConnectedTests.DefaultMotionAppliesWhenNeitherElementSetsOne`
- **BRIDGE-CONNECTED-005** — A flight shall follow BRIDGE-LAYOUT's builder rules, so a `Motion` the layout transition cannot honor on a keyed element throws `NotSupportedException` when `Motion` or `Key` is set, not when the element arrives.
  - Tests: `ConnectedTests.AMotionTheElementsRoleCannotCarryThrowsWhereItIsSet`
- **BRIDGE-CONNECTED-006** — Setting or changing `Key` on an element that is already attached shall re-register it without flying.
  - Tests: `ConnectedTests.ChangingTheKeyOfAnAttachedElementDoesNotFly`
- **BRIDGE-CONNECTED-007** — When a keyed flight takes over an element that has `Animate.Layout`, the flight shall win its move and the layout property shall take over again once it lands (BRIDGE-DECLLAYOUT).
  - Tests: `AnimateLayoutAttachedTests.AConnectedFlightWinsItsMoveAndTheLayoutPropertyResumesAfterIt`

### Leaving

- **BRIDGE-CONNECTED-008** — While an element with a `Key` is attached, it shall record its on-screen layout slot on every layout pass.
  - Tests: `ConnectedTests.AContentSwapFliesOutOfAScrolledList`
- **BRIDGE-CONNECTED-009** — When a keyed element detaches and no other attached element holds its key, it shall prepare the key from its last recorded slot moved by its render-transform translation and its live transition's offset and scale, so leaving mid-flight flies the next arrival from where it was drawn.
  - Tests: `ConnectedTests.AContentSwapFliesTheArrivingElementFromWhereTheLeavingOneWasDrawn`, `ConnectedTests.LeavingMidFlightFliesTheNextArrivalFromWhereTheLeavingElementWasDrawn`
- **BRIDGE-CONNECTED-010** — When another attached element still holds the key, a detaching keyed element shall prepare nothing.
  - Tests: `ConnectedTests.CoexistingViewsFlyFromTheLiveElementAndLeaveNothingPrepared`
- **BRIDGE-CONNECTED-011** (inherited) — When a keyed element detaches before it was ever laid out, it shall prepare nothing.
  - Tests: untested
- **BRIDGE-CONNECTED-012** — Clearing `Key` shall unregister the element and stop recording and preparing.
  - Tests: `ConnectedTests.ClearingTheKeyStopsPreparing`

### Anchored entrances

- **BRIDGE-CONNECTED-013** — When a keyed flight starts, every attached element anchored to that key shall play its entrance.
  - Tests: `ConnectedTests.AnAnchoredElementAttachedBeforeItsKeyedElementEntersWithTheFlight`
- **BRIDGE-CONNECTED-014** — When an anchored element attaches while its key's last flight is still in flight, it shall play its entrance.
  - Tests: `ConnectedTests.AnAnchoredElementAttachedAfterItsKeyedElementEntersWithTheFlight`
- **BRIDGE-CONNECTED-015** — When an anchored element attaches with no flight in flight for its key, including a flight that did not move, it shall show normally.
  - Tests: `ConnectedTests.AnAnchoredElementAttachedWithNoLiveFlightShowsNormally`, `ConnectedTests.AnAnchoredElementAttachedAfterAFlightThatDidNotMoveShowsNormally`
- **BRIDGE-CONNECTED-016** — An entrance shall play on the compositor with the anchored element's `Motion`, or `DefaultAnchorMotion` when it has none, reverting any entrance it is already playing.
  - Tests: `ConnectedTests.MotionOnAnAnchoredElementOverridesTheDefaultEntrance`
- **BRIDGE-CONNECTED-017** — A `Motion` on an anchored element that the compositor cannot carry shall throw `NotSupportedException` when `Motion` or `Anchor` is set, not when the entrance plays.
  - Tests: `ConnectedTests.AMotionTheElementsRoleCannotCarryThrowsWhereItIsSet`
- **BRIDGE-CONNECTED-018** — Detaching an anchored element, or clearing its `Anchor`, shall revert its entrance and unregister it.
  - Tests: `ConnectedTests.DetachingAnAnchoredElementRevertsItsEntrance`
- **BRIDGE-CONNECTED-025** — The registry shall hold a key's last flight weakly, so an anchor left under the key does not keep the flight's control alive.
  - Tests: `ConnectedTests.AnAnchorLeftUnderTheKeyDoesNotKeepTheFlownElementAlive`

### Defaults and environment

- **BRIDGE-CONNECTED-019** — `DefaultMotion` and `DefaultAnchorMotion` getters shall return a clone, so chaining on one leaves the default unchanged; their setters shall replace the default for later arrivals.
  - Tests: `ConnectedTests.ChainingOnADefaultLeavesTheDefaultUnchanged`
- **BRIDGE-CONNECTED-020** — All three properties shall work from compiled XAML with bindings and `x:Static` values.
  - Tests: `ConnectedTests.ACompiledXamlViewFliesItsKeyedElementAndEntersItsAnchoredOne`
- **BRIDGE-CONNECTED-021** — In design mode, setting any of the properties shall do nothing.
  - Tests: `ConnectedTests.DoesNothingInDesignMode`
- **BRIDGE-CONNECTED-022** — Flights and entrances shall register with the ambient scope when there is one and carry the diagnostics origin `"Connected"`.
  - Tests: untested

### Defaults

- **BRIDGE-CONNECTED-023** — When `DefaultMotion` is not set, a flight shall last 500 ms with `OutQuint`.
  - Tests: untested
- **BRIDGE-CONNECTED-024** — When `DefaultAnchorMotion` is not set, an entrance shall rise from `Y` 24 and fade in from `Opacity` 0 over 400 ms after a 150 ms delay with `OutQuart`.
  - Tests: untested

## Invariants

- A key's registry entry lists only attached elements and anchors (weak references), and is removed once it lists none.
- At most one entrance per anchored element is live at a time.

## Edge cases

- A `ContentControl` swap detaches the old view before attaching the new one, so the prepared path is taken; a `TransitioningContentControl` attaches the new view first, so the live-element path is taken.
- Two attached elements arriving with one key: each arrival flies from another attached element holding the key, the first one found.
- A recycled list container whose `Key` binding changes re-registers without flying (BRIDGE-CONNECTED-006).

## Threading and lifetime

- UI thread only. Arrival is visual-tree attachment, leaving is detachment; the recorded slot is cleared on detach.
- The static registry keeps the key's last flight weakly (BRIDGE-CONNECTED-025) until the key has no attached element or anchor left; a live flight is kept alive by its control.
- Each keyed element measures itself on every layout pass, one parent walk: fine for a page or a list of dozens, while a virtualizing list of thousands should key only what it needs.

## Reduced motion

- A flight follows its builder's reduced form, read when the flight is created (BRIDGE-LAYOUT-042); an entrance follows its builder's reduced form on the compositor (BRIDGE-THREAD-051).

## Allocation

- Not on the tick path. Each layout pass of a keyed element walks its parents and stores a `Rect`; each arrival allocates a transition.

## Provenance

- No upstream counterpart: the model is the Windows Community Toolkit's `Connected.Key` and `Connected.AnchorElement`, which splits the feature into the same separate properties.

## Decisions

- **Three separate attached properties.** `Key` (which element is the same thing in both views), `Motion` (how this element arrives) and `Anchor` (arrive with a keyed flight without flying) are separate attached properties on a static class. Reason: each answers a different question on a different element and binds differently (`Key` per item, `Motion` a shared recipe through style setters, `Anchor` on an element with no counterpart); the Windows Community Toolkit splits it the same way. Rejected: one combined value (`{local:Connected Key=…, Motion=…}`), which loses per-property bindings and style setters and still does not cover anchors.
- **The arriving side's `Motion` wins.** A flight uses the arriving element's `Motion`, else `DefaultMotion`, never the leaving element's. Reason: on a round trip each side's `Motion` shapes the flight into it. Rejected: falling back to the leaving element's `Motion`.
- **`Motion` means "how this element arrives" in both roles.** On a keyed element it is a layout-transition builder under the shared-element builder rules (timing, ease and per-axis eases only); on an anchored element it is a compositor entrance, so it carries values. Reason: one meaning for the property in both roles.
- **Defaults are static, settable builders handed out as copies.** `DefaultMotion` (500 ms, `OutQuint`) and `DefaultAnchorMotion` (rise 24 px and fade in over 400 ms after 150 ms, `OutQuart`) are snapshotted on every use, and their getters return a clone so chaining on one leaves the default unchanged. Reason: not recorded for the values; the clone stands in for freezing builders (see CORE-BUILDER).
- **The anchor entrance is from-only.** `DefaultAnchorMotion` states only start values (earlier from/to values; replaced when entrances became from-only). See BRIDGE-RECIPES.
- **Keyed flights are always `PositionAndSize`.** There is no property for the mode. Reason: no sample needs another mode yet.
- **Arrival is attachment; a key change is not an arrival.** A keyed element arrives when it attaches to the visual tree, as `Animate.OnAttached` does; setting or changing `Key` while attached re-registers it without flying. Reason: a recycled list container whose binding changes must not fly.
- **Leaving elements record their slot every layout pass and prepare on detach.** The slot is moved by the render-transform translation and the live transition's offset and scale, and nothing is prepared while another attached element holds the key; the per-pass parent walk is documented, with the advice that a virtualizing list of thousands keys only what it needs. Reason: a spike showed a detaching element can no longer be measured in the window, and a `ContentControl` swap detaches the old view before the new one attaches; reading the live translation lets a mid-flight leaver fly from where it was drawn, and skipping when another holder exists avoids an entry that would fly the next unrelated arrival.
- **Claim order: live element, then prepared entry, then nothing.** Reason: covers both coexisting (`TransitioningContentControl`) and swapping navigation, while a first load or a list scrolling items into view animates nothing.
- **Anchors enter only while their key's flight is live.** A starting flight plays the entrance of every attached anchor on its key, and an anchor attaching during its key's flight enters too; otherwise it shows normally, and detaching reverts its entrance. Reason: covers both attach orders within one swap without timing heuristics, and lets late-built content join a flight.
- **Design mode does nothing, and flights register with the ambient scope.** Reason: consistency with `Animate.OnAttached` and other animations.
- **`Motion` is validated where it is set, for the role the element has.** Setting `Motion`, `Key` or `Anchor` reads the motion under the layout-transition rules on a keyed element and the compositor's on an anchored one, and throws `NotSupportedException` there; a `Motion` on an element with neither role is not read. Reason: an exception from the attach handler points nowhere near the mistake; this matches `Animate.Layout`.
- **The registry holds the last flight weakly.** Reason: a strong reference kept the flight's control alive for as long as an anchor stayed registered under the key, while a flight that is still live is already kept by its control.
- **Shared-element sources are measured on screen.** See BRIDGE-LAYOUT.
- **Keyed shared elements store a rect, return null when unprepared, and expire.** See BRIDGE-LAYOUT.
- **A one-shot flight or code transition wins its move.** See BRIDGE-DECLLAYOUT.

## Non-goals

- A mode property for flights (always `PositionAndSize`), cross-fading dissimilar elements, and lifting a flight above clipping ancestors.

## Open items

- Untested: a keyed element detaching before its first layout pass (011), scope registration and the diagnostics origin (022).
