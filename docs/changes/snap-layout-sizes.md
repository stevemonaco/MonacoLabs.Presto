# Animated sizes move both edges together

## Why

Animating `Width` or `Height` of a centered control makes its edges shake. With layout rounding on, Avalonia rounds the animated size and the centering offset to device pixels separately, so as the size moves one pixel at a time, the left edge and the right edge step on alternate frames. The outline shimmers, and centered content inside it (a label) hops by a pixel. It happens at 100% scaling as well as at fractional scales, where it is most visible.

The library and samples have worked around this four times instead of fixing it once:
- `SubmitButton` gave up an overshooting expand because the label wobbled as the spring settled (BRIDGE-SUBMIT Decisions).
- The Submit Flow sample turns `UseLayoutRounding` off around its collapse and expand (`SubmitFlowPage.Resize`), and moved its face out of the button so the label's centering doesn't depend on the animated width.
- Swipe places its action strip without layout rounding (BRIDGE-SWIPE Decisions).
- MorphGlyphs' pieces skip layout rounding (BRIDGE-RECIPES Decisions).

`SubmitButton` itself never got the sample's fix, so the Micro-interactions "Place order" button still shakes while it collapses and expands. Any app that animates a centered control's `Width` hits the same thing.

## What

When the bridge animates `Width` or `Height` on a `Layoutable` whose `UseLayoutRounding` is true, each frame writes a value that sits a whole number of **two**-device-pixel steps away from the tween's `from` value: `from + round((value − from) · s / 2) · 2 / s`, where `s` is the element's render scaling (1 when it is not in a visual tree). The final frame of the tween writes the exact `to` value.

With the size changing by an even number of device pixels, the centering offset changes by a whole number of device pixels, so both edges move by the same amount on the same frame, and content centered inside moves not at all. Edges stay on device pixels at every frame, so they stay sharp; turning rounding off instead leaves edges blurred mid-motion and the content still stepping.

What does not change: tweens on an element with `UseLayoutRounding="False"` write the raw value (that element asked for sub-pixel layout); every other property, including `Margin`, `Padding` and `ColumnDefinition.Width`, is written as today; `Presto.Get` and callbacks still see the raw interpolated number.

## Decisions

- **Snap to two device pixels, relative to the tween's `from`.** Reason: an even step is what keeps a centered control symmetric; measuring from `from` keeps the first frame where the control already is, whatever its fractional size. Rejected: turning layout rounding off during the tween (blurred edges, content still steps, and the element's own setting has to be saved and restored across cancel, revert and overlapping tweens); snapping to one device pixel (the case that shakes today).
- **On by default, no new API.** Reason: the shake is never wanted, and an element that wants sub-pixel sizes already says so with `UseLayoutRounding="False"`. Rejected: a `PrestoAvalonia` switch, which nobody would know to turn on.
- **`Width` and `Height` only.** Reason: they are the sizes a centered control animates; `MinWidth`/`MaxWidth` and `Margin`/`Padding` are rarely animated on centered content, and a `Thickness` would need a per-side rule. Rejected for now: all `double` layout properties.
- **The scaling is read when the frame is written.** Reason: it is a read from the element's visual root, and a window moved to another monitor changes it mid-tween. Rejected: caching it when the tween resolves (stale after a monitor move).
- **A control not centered moves in two-pixel steps.** Accepted: at the slow ends of an ease the far edge moves two device pixels at a time, which is not visible at animation speeds.
- **`SubmitButton`'s expand stays on `Springs.Gentle`.** The reason in its decision is reworded: the restraint is now a matter of taste, since an overshoot no longer wobbles the label. Rejected: switching back to `Springs.Bouncy` in this change (a design change, separate from the fix).

## Spec changes

**BRIDGE-TARGETS** (`property-resolution.md`)
- Add: "When `Layoutable.WidthProperty` or `Layoutable.HeightProperty` is animated on an element whose `UseLayoutRounding` is true, each frame before the last shall write `from` plus a whole number of two-device-pixel steps at the element's render scaling, rounded to the nearest step; the last frame shall write the exact `to` value."
- Add: "An element whose `UseLayoutRounding` is false shall get the raw interpolated size."
- Decisions: the first five decisions above.

**BRIDGE-SUBMIT** (`progress-and-submit.md`)
- Decisions: reword "The expand does not overshoot" to keep `Springs.Gentle` as a choice of feel and drop the layout-rounding reason, which now cites BRIDGE-TARGETS.

## Tasks

1. **Snap in the double fast path.** In `AvaloniaPropertyAccessor.SetValue` (`Targets/AvaloniaObjectTargetAdapter.cs`), for `WidthProperty`/`HeightProperty` on a `Layoutable` with `UseLayoutRounding`, snap the number against `tween`'s from value at the element's render scaling, and write the exact number once the tween has ended (`_currentTime >= _changeDuration`, as the other accessors test). No allocation per frame.
   - Tests (bridge, headless): a centered control in a wider parent animating `Width` 168 → 44 moves its left and right edges by the same amount on every frame (the difference between its two margins never changes), and ends at exactly 44; the same with an overshooting spring; an element with `UseLayoutRounding="False"` gets the raw values; `Height` snaps the same way; `Opacity` is still clamped and unsnapped. A unit test of the snap function at scalings 1, 1.25 and 1.5 shows each written value is `from` plus an even number of device pixels. `BridgeAllocationTests` covers a `Width` tween allocating nothing per frame.
   - Update BRIDGE-TARGETS.
2. **Drop the sample workaround.** Delete `SubmitFlowPage.Resize` and call `Submit.Animate(Collapse)`/`(Expand)` directly. Keep the face outside the button.
3. **SubmitButton decision.** Reword BRIDGE-SUBMIT's "expand does not overshoot" decision.
4. **Visual check** on the Desktop gallery at 100% and at a fractional Windows scaling (125% or 150%), DevTools screenshots of the edges mid-tween:
   - Micro-interactions "Place order": collapse and expand, both edges glide together and the label holds still.
   - Submit Flow "Send message": the same, now without the rounding workaround.
   - Tempo's start pill (`PillResize`): unchanged or better.

## Open questions

None.
