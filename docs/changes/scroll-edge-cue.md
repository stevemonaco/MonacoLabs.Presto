# Scroll edge cues

## Why

Nothing in the bridge shows that a scroller has more content past an edge. `PrestoAvalonia.ScrollValue` gives the offset or the progress (BRIDGE-FOLLOW-047), but not the distance left to scroll, and no attached property cues an edge. So Tempo hand-rolls `Views/ScrollEdge.cs` (116 lines): `ShadowTop`/`ShadowBottom` fade a shadow `Border` in by the distance past its edge, from `ScrollChanged`, and `FadeSides` fades a sideways chip row's ends with an opacity mask whose stops it mutates in place. Three shadow pairs (`TasksView` ×2, `StatsView`) and the compact chip row (`Tempo.axaml`) use it. Any app with a clipped list wants the same cue, and the hand-rolled version has subtle points to get right: finding a `ListBox`'s templated scroller rather than one nested inside its items, ramping instead of popping, and invalidating a mask that was mutated in place (the bridge's own `Effects/MaskComposer.cs` notes the renderer misses that).

Prompted by the BRIDGE-SCROLL Tempo finding "Nothing cues that a scroller has more past an edge".

## What

Three additions to BRIDGE-SCROLL:

- **`Scroll.FadeEdges`** (`double`, DIPs, default 0 = off), set on a `ScrollViewer` or on a control that templates one (`ListBox`, `ItemsControl` with a scrolling template). While it is above 0 the bridge sets the control's `OpacityMask` to a linear gradient along the scroll axis. Each end fades from transparent to opaque over `FadeEdges` DIPs (capped at half the control's length, as BRIDGE-MARQUEE-026 does), and the strength of each end follows how far the content goes past it: none at the edge, full once `Scroll.CueRamp` DIPs remain. The axis follows the rule `StickToEnd` and parallax use: vertical, or horizontal when only that axis scrolls.
- **`Scroll.CueFor`** (`Control?`) plus **`Scroll.CueEdge`** (new enum `ScrollEdge`: `Top`, `Bottom`, `Left`, `Right`; default `Top`), set on any element, usually a shadow `Border` laid over the scroller. While `CueFor` is set, the bridge writes the element's `Opacity` as `clamp(distance past CueEdge ÷ CueRamp, 0, 1)` for the scroller that `CueFor` names or templates. The app keeps its own shadow visual, theme and placement; the bridge drives only its opacity.
- **`Scroll.CueRamp`** (`double`, DIPs, default 24), read from the scroller or templating control: how far the content has to go past an edge before its cue is at full strength.
- **`PrestoAvalonia.ScrollRemainingValue(scroller, axis = Vertical)`**: a `MotionValue` holding `max(0, extent − viewport − offset)` on the axis, rounded to whole pixels, so code can drive any other cue (a scale, a blur, an arrow button) through `Follow` sinks.

Cues update inside the scroller's own offset, extent and viewport notifications, through the scroller's shared `ScrollContainer`, so they never trail the content by a frame (as parallax does, BRIDGE-SCROLL-004). They allocate nothing per notification: the mask brush and its four stops are built once when `FadeEdges` turns on and changed in place, and the control is invalidated after each change.

Tempo's `Views/ScrollEdge.cs` is deleted. The shadow pairs become `presto:Scroll.CueFor="{Binding #TaskList}" presto:Scroll.CueEdge="Bottom"`, and the compact chip row's setter becomes `presto:Scroll.FadeEdges` = 24.

What does not change: `ScrollValue` keeps its signature and behavior; no shadow is drawn by the bridge; scroll offsets are never written by a cue.

## Decisions

- **The bridge drives an app-supplied shadow's opacity rather than drawing one.** Reason: a shadow's color, height, margins and corner treatment are theme work (Tempo's differ inside the Done expander), and an opacity on the app's element keeps all of that in XAML styles. Rejected: `Scroll.EdgeShadows="True"` adorning bridge-drawn shadows (would need a styling surface of its own); a templated `ScrollEdgeHost` control (forces a wrapper on every list).
- **Cues follow scroll position, not time, so reduced motion leaves them on.** Reason: they are a static reading of where the content is, and they ease only as fast as the user scrolls, like a scrollbar thumb. Rejected: suppressing them under `ReducedMotion` (removes information, not motion).
- **The mask fade is named `FadeEdges`, as on `Marquee`.** Reason: same meaning (a ramp length in DIPs at both ends of the axis, 0 = off), so one name to learn. The only difference is that the scroller's ends fade only while content lies past them.
- **One mask axis, picked as `StickToEnd` and parallax pick it.** Reason: a control has one `OpacityMask`, and a single linear gradient cannot fade two axes; the "vertical, or horizontal when only that scrolls" rule is already in BRIDGE-SCROLL-003 and -058. Rejected: a radial or composed mask for both axes (no use case; a two-axis scroller can take shadow cues on all four edges).
- **The mask lives on the scroller or templating control, and replaces any `OpacityMask` there.** Reason: on the control it stays put while the content scrolls, and its relative stops change only with the control's length or `FadeEdges`. Accepted: a visible scrollbar inside the control fades with it, so the mask suits scrollers whose bars are hidden, as Tempo's chip row's are.
- **`CueFor` names a scroller or a control that templates one, and only that scroller counts.** Reason: binding `#TaskList` (a `ListBox`) is how XAML reaches it; the templated scroller is the `ScrollViewer` whose `TemplatedParent` is that control, found when its template is applied, so a scroller inside an item template is never picked. Same rule for `FadeEdges` on a templating control.
- **The cue writes `Opacity` with `SetCurrentValue` and clears it when `CueFor` is cleared or the element detaches.** Reason: the shadow's XAML or style value is its look before the first scroll reading; `SetCurrentValue` overrides it without losing the binding priority, as scroll offset writes do. Rejected: a Presto tween per notification (the value already moves only with the scroll; easing it would lag the content).
- **`CueRamp` is a separate length from `FadeEdges`.** Reason: one is how far the content must go past the edge, the other how wide the fade is drawn; Tempo uses 24 for both, which become the defaults, but a wide fade on short content wants a short ramp. Rejected: one length for both.
- **`ScrollRemainingValue` is a new method, not a third mode on `ScrollValue`.** Reason: `ScrollValue`'s `bool progress` cannot take a third mode without a breaking change or an awkward pair of flags. Rejected: a `ScrollMeasure` enum overload (more surface for one case); leaving remaining distance out (code cues would recompute it from extent, viewport and offset by hand).
- **In design mode the cues do nothing.** Reason: same as `Speed` and `StickToEnd` (BRIDGE-SCROLL-065), no container or handler is created in the previewer; the shadow shows at its XAML opacity.

## Spec changes

**BRIDGE-SCROLL** (`scroll.md`)
- `types`: add `ScrollEdge`. `sources`: add the cue source file(s). `tests`: add `ScrollEdgeCueTests`.
- Purpose: add edge cues to the list of what the spec covers.
- New section "Edge cues", requirements added:
  - "While `FadeEdges` is above 0 on a `ScrollViewer` or a control templating one, the bridge shall set the control's `OpacityMask` to a linear gradient along the scroll axis (vertical, or horizontal when only that scrolls) whose ends ramp from transparent to opaque over `FadeEdges` DIPs, capped at half the control's length on that axis."
  - "Each end's transparency shall be `clamp(distance past that end ÷ CueRamp, 0, 1)`, so an end with nothing past it is fully opaque."
  - "While `CueFor` is set on an element, the bridge shall write the element's `Opacity` with `SetCurrentValue` as `clamp(distance past CueEdge ÷ CueRamp, 0, 1)` of the scroller `CueFor` names."
  - "The scroller of a control that is not a `ScrollViewer` shall be the `ScrollViewer` whose `TemplatedParent` is that control, found once its template is applied; scrollers nested in its content shall be ignored."
  - "Cues shall update inside the scroller's offset, extent and viewport notifications, never trailing the content by a frame."
  - "When `FadeEdges` returns to 0 the bridge shall clear the mask; when `CueFor` is cleared or the element detaches it shall clear the written `Opacity`, and release the scroller's container."
  - "A cue shall apply the same while `ReducedMotion` is set."
  - "`FadeEdges`, `CueFor`, `CueEdge` and `CueRamp` shall be settable from XAML and from styles."
  - "`PrestoAvalonia.ScrollRemainingValue` shall hold `max(0, extent − viewport − offset)` on the axis, rounded to whole pixels, refreshing when the extent or viewport changes, and stop as BRIDGE-FOLLOW-053 describes."
- Defaults: "When not set, `FadeEdges` is 0, `CueEdge` is `Top`, `CueRamp` is 24 and `ScrollRemainingValue`'s axis is vertical."
- BRIDGE-SCROLL-065: add `FadeEdges` and `CueFor` to the properties that do nothing in design mode.
- Edge cases: "A `FadeEdges` mask replaces any `OpacityMask` on the control and fades a visible scrollbar with the content." "Content no longer than the viewport cues nothing."
- Reduced motion: "Edge cues follow the scroll position and apply unchanged."
- Allocation: "An edge-cue update allocates nothing per notification (`BridgeAllocationTests`)."
- Decisions: the decisions above.

**BRIDGE-FOLLOW** (`follow.md`)
- Purpose: add `PrestoAvalonia.ScrollRemainingValue` to the specified members, citing BRIDGE-SCROLL for its requirement (or own the requirement here beside BRIDGE-FOLLOW-047; settle during task 1).

**BRIDGE-SCROLLOBSERVER** (`scroll-observer.md`)
- Purpose: add edge cues to the users of the shared `ScrollContainer`.

## Tasks

1. **Remaining value.** Add `ScrollRemainingValue` beside `ScrollValue`, fed from `ScrollContainer.AddValue` with a remaining mode.
   - Tests (bridge, headless): the value holds the distance left on both axes, refreshes when the extent grows and is 0 for content shorter than the viewport; it stops when the scroller detaches.
2. **Shadow cues.** `Scroll.CueFor`, `Scroll.CueEdge`, `Scroll.CueRamp` and the `ScrollEdge` enum, registered on the scroller's `ScrollContainer`.
   - Tests: a `Bottom` cue over a `ScrollViewer` is at opacity 1 at the top, ramps to 0 over the last 24 DIPs and is 0 at the end, read in the offset notification with no frame; a `Top` cue mirrors it; `Left`/`Right` on a horizontal scroller; `CueFor` bound to a `ListBox` follows its templated scroller and ignores a `ScrollViewer` inside an item; a `CueRamp` on the `ListBox` changes the ramp; clearing `CueFor` restores the XAML opacity; detaching releases the container; under `ReducedMotion` the cue still follows; set from a style in XAML.
3. **Mask fade.** `Scroll.FadeEdges` on a scroller or templating control.
   - Tests: the mask runs along the vertical axis, or horizontal when only that scrolls; the ends' alphas follow the distance past them; the ramp is capped at half the length; returning to 0 clears the mask; a mutated mask invalidates the control; `BridgeAllocationTests` covers a scroll with a shadow cue and a mask fade allocating nothing per notification.
   - Update BRIDGE-SCROLL, BRIDGE-FOLLOW and BRIDGE-SCROLLOBSERVER.
4. **Tempo.** Delete `Views/ScrollEdge.cs`; move the three shadow pairs in `TasksView.axaml` and `StatsView.axaml` to `presto:Scroll.CueFor`/`CueEdge`, and the compact chip-row setter in `Styles/Tempo.axaml` to `presto:Scroll.FadeEdges`. Remove the backlog finding.
   - Tests: `Tempo.Tests` still pass; add one that the task list's bottom shadow shows while rows remain below.
5. **Gallery.** Add the cues to the Scroll reference page (a list with top/bottom shadows and a faded horizontal chip row), with a snippet.
6. **Visual check** on Desktop: Tempo's task list, Done list and Stats shadows fade in and out with the scroll, with no pop at either end; the compact chip row (`--size 360x780`) fades only the end with more chips; the gallery page the same. DevTools screenshots at the top, middle and end of each.

## Open questions

- Should `ScrollRemainingValue`'s requirement live in BRIDGE-FOLLOW beside `ScrollValue` (BRIDGE-FOLLOW-047) or in BRIDGE-SCROLL with the cues? Either works; decide in task 1 so ownership is single.
