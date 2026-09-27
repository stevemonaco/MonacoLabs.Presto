# Monaco.Presto — Connected animations plan

Extends the shared-element transition (`PrestoAvalonia.AnimateSharedElement`, COMPOSITOR-PLAN §4.4) towards what the
Windows Community Toolkit's `ConnectedAnimationService` offers. PLAN.md Phase 16. XAML keys (`Connected.Key`) and
cross-fading dissimilar elements are deliberately deferred; cross-fade and similar effects are likely candidates for a
future "prebuilts" layer.

## 1. Scrolled sources and targets (fix)

`LayoutTransition.Measure` takes scroll offsets back out so that scrolling is never mistaken for a layout move. That is
right for `AnimateLayout`, but a shared element measured out of a scrolled list and played into a view that is not
scrolled (or the reverse) lands off by the difference in their offsets.

- A shared-element source is measured **on screen**: where it is drawn, scroll included.
- The transition converts that on-screen rect into the target's layout frame at the target's first layout pass, by
  adding the target's own accumulated scroll offset. Later passes are frame-to-frame as before, so scrolling an armed
  control is still not a move.

## 2. Keyed shared elements

Today both controls must be in hand at once, while the source is still on screen. Framework-driven navigation (a view
model changes, a DataTemplate builds the next view, the old view is gone first) never has both.

```csharp
PrestoAvalonia.PrepareSharedElement("photo", thumbnail);           // old view, before it leaves; returns false if not on screen
PrestoAvalonia.AnimateSharedElement("photo", hero, 600, Eases.OutQuint);   // new view, whenever it is built
PrestoAvalonia.AnimateSharedElement("photo", hero, builder);
```

- `Prepare` stores only the on-screen rect (no control reference, so nothing is kept alive), replacing any earlier one
  under the key.
- The keyed `AnimateSharedElement` consumes the entry and returns the `LayoutTransition`, or `null` when nothing is
  prepared under the key, like the Toolkit's `GetAnimation(key)?.TryStart(...)`.
- An entry expires three seconds after it was prepared, as the Toolkit's does, so a navigation that never completed
  cannot fly a later, unrelated element from a stale place.
- Both controls must share a window (true of every shared-element move already).

## 3. Builder overloads and arc flights

`AnimateLayout(control, builder)` and `AnimateSharedElement(source | key, target, builder)` take an `AnimationBuilder`
for the motion; the layout still decides where the control goes.

- Honoured: `Duration`, `Delay` (new for layout transitions: the control holds its inverted place during the delay) and
  `Ease`.
- `X`, `Y` and `Scale` may carry **only an ease** (`.X(ease: Eases.OutSine).Y(ease: Eases.InSine)`): a per-axis ease
  shapes the flight, and different X and Y eases make it an **arc**. Giving any of them a value throws, since the
  layout owns the values.
- Anything else (other properties, loops, callbacks, function-valued duration or delay, …) throws
  `NotSupportedException` naming it, as the compositor builder terminal does. `Then()` covers completion.

Implementation: a translation with one ease stays the exact single segment it is today; with differing X and Y eases it
is sampled into linear key frames (100 segments, as the compositor compiler samples anything inexact). The mirror
animation carries the same per-axis eases, so an interruption mid-arc measures where the control is really drawn.

## 4. Tests (bridge, `LayoutTransitionTests`)

- A shared element from a scrolled list into an unscrolled panel starts where the source is drawn; and back.
- `PrepareSharedElement` then a keyed start after the source is removed from the tree flies from where it was.
- A keyed start with nothing prepared returns `null` and moves nothing; a second keyed start after one consumed the
  entry returns `null`. Expiry is not tested, since it needs three seconds of wall time.
- Builder: duration and delay are honoured (held during the delay, moving after it); an arc's halfway point is off the
  straight line between start and end; a value on X/Y/Scale, or an unsupported setting, throws.

Known limit, found while testing: a flying control is still drawn inside its own ancestors, so one flying into a clipped
container (a `ScrollViewer`) is clipped to it until it lands. Lifting it into an overlay layer for the flight, as the
Toolkit does, is future work alongside cross-fade.

## 5. Sample

The Connected navigation showcase page switches to the keyed API (prepare in the gallery, start in the detail view), uses
the builder overload with an arc for the photo, and its gallery becomes a scrolling list of all destinations, which the
scroll fix makes possible.
