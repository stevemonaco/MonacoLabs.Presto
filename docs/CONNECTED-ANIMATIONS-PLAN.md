# Monaco.Presto — Connected animations plan

Extends the shared-element transition (`PrestoAvalonia.AnimateSharedElement`, COMPOSITOR-PLAN §4.4) towards what the
Windows Community Toolkit's `ConnectedAnimationService` offers. §1–5 are PLAN.md Phase 16 (done); §6, XAML keys, is
Phase 17. Cross-fading dissimilar elements is deliberately deferred, and is a likely candidate for a future "prebuilts"
layer along with similar effects.

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

- Honored: `Duration`, `Delay` (new for layout transitions: the control holds its inverted place during the delay) and
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
- Builder: duration and delay are honored (held during the delay, moving after it); an arc's halfway point is off the
  straight line between start and end; a value on X/Y/Scale, or an unsupported setting, throws.

Known limit, found while testing: a flying control is still drawn inside its own ancestors, so one flying into a clipped
container (a `ScrollViewer`) is clipped to it until it lands. Lifting it into an overlay layer for the flight, as the
Toolkit does, is future work alongside cross-fade.

## 5. Sample

The Connected navigation showcase page switches to the keyed API (prepare in the gallery, start in the detail view), uses
the builder overload with an arc for the photo, and its gallery becomes a scrolling list of all destinations, which the
scroll fix makes possible.

## 6. XAML keys (Phase 17)

The keyed API still needs code to call prepare before the old view leaves and start once the new one is built, and in
an MVVM app (ViewLocator or DataTemplate navigation, `TransitioningContentControl`, tab switches) that code has no
natural home. XAML keys move it into markup: views declare which elements are "the same thing", and nothing in either
view's code knows a transition happens.

```xml
<!-- Gallery item template -->
<Border Classes="photo" presto:Connected.Key="{Binding Id, StringFormat=photo-{0}}" />

<!-- Detail view -->
<Border Classes="photo hero"
        presto:Connected.Key="{Binding Id, StringFormat=photo-{0}}"
        presto:Connected.Motion="{x:Static local:Motions.Flight}" />
<StackPanel presto:Connected.Anchor="{Binding Id, StringFormat=photo-{0}}">…</StackPanel>
```

### 6.1 Decisions

1. **Three separate attached properties** on a static `Connected` class in the bridge (`Monaco.Presto.AvaloniaBridge`),
   because each answers a different question on a different element and binds differently: `Key` is usually a per-item
   binding, `Motion` a shared recipe that a style setter can apply, and `Anchor` sits on an element with no counterpart
   in the other view. The Toolkit splits it the same way (`Connected.Key`, `Connected.AnchorElement`). One combined
   value (`{local:Connected Key=…, Motion=…}`) was rejected: it loses per-property bindings and style setters and still
   does not cover anchors.

   | Property | Type | Question | Where |
   |---|---|---|---|
   | `Key` | `string?` | Which element is the same thing in both views? | The matching element in both views |
   | `Motion` | `AnimationBuilder?` | How does this element arrive? | A keyed element (its flight) or an anchored one (its entrance) |
   | `Anchor` | `string?` | Which keyed flight does this element arrive with, without flying itself? | Elements around the destination |

2. **The arriving side's `Motion` wins.** An element's `Motion` is used when that element is the one arriving, so on a
   round trip each side's `Motion` shapes the flight into it. There is no fallback to the leaving side's `Motion`: with
   none on the arriving element, `Connected.DefaultMotion` applies.
3. **`Motion` means "how this element arrives"** in both roles. On a keyed element it is a layout-transition builder,
   under the same rules as `AnimateSharedElement(…, builder)` (§3: timing, ease and per-axis eases only). On an anchored
   element it is an entrance played with `AnimateOnCompositor`, so it carries values (`.Y(from: 24, to: 0).Opacity(from: 0, to: 1)`).
4. **Defaults are static and settable.** `Connected.DefaultMotion` (500 ms, `OutQuint`) for flights and
   `Connected.DefaultAnchorMotion` (rise 24 px and fade in over 400 ms, `OutQuart`, 150 ms delay) for anchored entrances.
   They are builders, so every use snapshots them.
5. **Mode is always `PositionAndSize`** for keyed flights, as for `AnimateSharedElement`; no property for it until a sample needs one.

### 6.2 Lifetime

Arrival is **attachment to the visual tree**, as for `Animate.OnAttached`. Setting or changing `Key` on an element that is
already attached re-registers it but is not an arrival, so a recycled list container whose binding changes does not fly.

**Leaving.** A spike (headless Avalonia 12.1.2) showed that by the time `DetachedFromVisualTree` is raised, the leaving
view's root is already cut from its parent, so a detaching element cannot be measured in the window any more
(`TopLevel.GetTopLevel` is null and `TranslatePoint` to the window fails). It also showed that a `ContentControl` swap
detaches the old view synchronously **before** the new one attaches. So:

- A keyed element records its on-screen layout slot (the scroll-inclusive measurement of §1) on every layout pass while
  it is attached.
- On detach it prepares its key from that slot plus what is still readable off the element: its render-transform
  translation and any live layout transition's offset and scale (so leaving mid-flight flies from where it was drawn).
  It skips preparing when another attached element still holds the key: that element has already flown (coexisting
  views), and an entry left behind would fly the next unrelated arrival.
- The cost is one parent walk per keyed element per layout pass. That is fine for a page or a list of dozens; note it in
  the XML docs, and say that a virtualizing list of thousands should key only what it needs.

**Arriving.** On attach, a keyed element claims its key, in this order:

1. **Another attached element with the same key** (views that coexist, such as `TransitioningContentControl`, which
   attaches the new view before it removes the old): fly from where that element is drawn right now.
2. **A prepared entry** (views that swap): fly from it, consuming it. The three-second expiry of §2 applies.
3. Neither: no flight. A first load, or a list scrolling items into view, animates nothing.

The flight is `AnimateSharedElement` with the prepared rect or the live element as source, and the arriving element's
`Motion` or `Connected.DefaultMotion`.

**Anchors.** An anchored element enters when, and only when, its key's flight is live:

- When a keyed element starts a flight, every *currently attached* element anchored to that key plays its entrance.
- When an anchored element attaches while its key's last flight is still in progress, it plays its entrance too. This
  covers both attach orders within one swap without timing heuristics, and lets late-built content join a flight.
- An anchored element that attaches with no live flight for its key shows normally.
- Entrances run inside the element's own lifetime: detaching an anchored element reverts its entrance.

**Other rules.**

- A registry maps each key to its attached keyed elements (weak references) and to its last flight. Clearing `Key`
  (`null`) unregisters and stops recording; clearing `Anchor` likewise.
- Design mode (`Design.IsDesignMode`) does nothing, as `Animate.OnAttached` does.
- Every flight and entrance registers with the ambient scope (`Engine.Current.CurrentScope`) when there is one, like any
  other animation.

### 6.3 Tests (bridge, `ConnectedTests`)

- A `ContentControl` swapping a view with a keyed element for one with the same key flies the arriving element from where
  the leaving one was drawn (pixels, as `LayoutTransitionTests` does), including from a scrolled list.
- Coexisting views (the new view attached before the old is removed) fly from the live element.
- First load, and an arrival whose key nothing prepared, animate nothing; a prepared entry is consumed by one arrival.
- Changing `Key` on an attached element does not fly; clearing it stops recording and preparing.
- Leaving mid-flight flies the next arrival from where the leaving element was drawn.
- `Motion` on the arriving element sets the flight (a very long duration holds it near its source where the default
  would have moved it); the leaving element's `Motion` is ignored; `Connected.DefaultMotion` applies when neither is set.
- An anchored element attached in the same swap enters (starts transparent and offset, ends at rest) whether it attaches
  before or after its keyed element; one attached with no live flight does not animate.
- A compiled XAML view using all three properties with bindings and `x:Static` works end to end.
- Design mode is a no-op.

### 6.4 Sample

A new Showcase page, **Connected XAML**, with no animation code at all:

- A page view model with `CurrentPage`; gallery and detail view models; a `ContentControl` whose DataTemplates pick the
  view. Clicking a destination (a command) and Back only change `CurrentPage`.
- The same destinations and scenery as Connected navigation. Keys are bound per item (`photo-{Id}`, `title-{Id}`), the
  hero's `Motion` is an `x:Static` arc recipe, and the detail's text column is anchored to the photo key.
- A switch that swaps the `ContentControl` for a `TransitioningContentControl` (cross-fade page transition), exercising
  the coexisting-views path.
- The page prints its own XAML (as the Builder pages print their code), pinned by a test against the `.axaml` file.
- Sample tests: open and back fly the photo (pixels), anchored text enters on open, and the transitioning-host variant flies too.

### 6.5 Docs

README: a "From XAML" subsection under the connected-animation text with the example above and the three-property
table. PLAN.md: tick Phase 17, and record decision 2 (arriving `Motion` wins) under §7 Decided.
