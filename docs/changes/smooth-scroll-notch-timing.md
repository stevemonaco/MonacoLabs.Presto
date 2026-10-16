# Smooth scroll notch timing

## Why

The frame-pacing measurement found a glitch in `Scroll.Smooth` at each wheel notch during a long scroll, with pacing on or off. The frame where the notch lands moves too little: 1.2 or 2.0 px where its neighbours move about 6.5 px. Sometimes the frame after it moves about twice as far (12.3 px). On a long scroll this shows as a small hitch every notch, which is likely part of the unevenness reported on long scrolls through the Gallery's code sections.

The short frame comes from where the ease's clock starts. A notch is handled between frames. `HandleWheel` takes `_from` from the current offset, which is the position rendered at the last frame (time F). It then restarts the ease with `ResetTime()`, which starts it at `Engine.Now`, the notch's own time w, somewhere after F. The next frame at F′ advances the ease by only F′ − w instead of a whole frame, so the offset holds still for w − F. That lost time ranges from nothing to almost a frame, depending on where the notch lands between frames.

The double frame is not explained yet. Starting the ease from F doesn't account for it. It may be a frame callback delayed by handling the wheel event, which the pacer then counts as two intervals (BRIDGE-INIT pacing).

## What

While an ease is running, a notch restarts it from the last frame's time instead of from the notch's time. The new ease starts at the offset rendered at that frame and at that frame's time, so the next frame advances it by a whole interval and the motion carries on without a stall.

A notch at rest (no ease running) still starts at `Engine.Now`. Its first frame covers only the time since the notch, which is the real delay before anything could move.

What does not change:
- the 300 ms `OutQuad` ease and how notches accumulate into one target (BRIDGE-SCROLL-012/-013);
- the end-of-range and cancellation rules (-016/-017);
- the core timing model, `Timer.ResetTime()` and every other caller.

Restarting `OutQuad` on each notch still changes the speed a little at every notch, a sawtooth of about ±7% at a steady 25 notches per second. That is a separate easing question, outside this change.

## Decisions

- **A notch mid-ease rebases to the last frame time.** Reason: `_from` is the offset rendered at that frame, so its time is the matching origin, and the first frame then gets a full interval. Rejected:
  - Keeping `Engine.Now` and lowering `_from` by the expected movement (this guesses the speed);
  - Seeking the ease forward by w − F (this renders between frames);
  - Making `AvaloniaFrameSource.Now` return the last frame time between frames (every input handler would read one frozen time, which breaks velocity tracking of pointer samples).
- **A notch at rest keeps `Engine.Now`.** Reason: the last frame can be arbitrarily old when the engine was idle, and backdating to it would jump the first frame ahead.
- **The core gains an internal `Timer.ResetTime(double now)`, and the public `ResetTime()` calls it with `Engine.Now`.** The bridge passes the engine's last frame time (`Engine._currentTime`, set in `Update`). Reason: there is no public API change, and the bridge already sees core internals. Rejected: a public `Engine.FrameTime` (no other caller needs it yet).

## Spec changes

**BRIDGE-SCROLL** (`scroll.md`):
- **Change -013:** The smooth scroll shall ease from the current offset to the target over 300 ms (× `Globals.TimeScale`) on `OutQuad` with `SetCurrentValue`, landing exactly. A notch at rest shall time the ease from the notch, with no head start. A notch during a running ease shall time it from the last frame, so the next frame advances it by a whole frame.
- **Decisions:** in "Smoothed wheel is opt-in and Win32 only", add the rebasing rule and its reason after the head-start sentence. Also add the rejected alternatives above.
- **Edge cases:** record what task 3 finds about the double frame. If it is a late callback, say that a frame delayed by input is paced as two intervals and moves twice as far.

**CORE-TIMER:** no requirement change. `ResetTime(double now)` is internal, and `ResetTime()` behaves as before.

## Tasks

1. **Core.** Add the internal `Timer.ResetTime(double now)`, and route `ResetTime()` through it.
   - The existing timer tests must stay green; no new core test is needed for a pure refactor.
2. **Smoother.** In `HandleWheel`, when `_to` was set before the notch, call `_ease.Restart().ResetTime(engine._currentTime)`. Otherwise keep `ResetTime()`.
   - Tests in `ScrollSmoothTests`:
     - **Mid-ease notch:** notch, advance 64 ms in 16 ms frames, then let 10 ms pass with no frame, notch again, and deliver one frame 16 ms after the last. The offset is `from + (to − from) × OutQuad(16 / 300)`, where `from` is the offset after the 64 ms. The test fails on today's code, which advances only 6 ms.
     - `ManualFrameSource` can't let time pass without a frame. Give the test a small private `IFrameSource` with a settable `Now`, installed on `Presto.Engine.FrameSource`, so the core's public API is unchanged.
     - **Notch at rest:** `ANotchEasesFromItsOwnTimeWithoutAHeadStart` still holds.
   - Run the bridge suite.
3. **Measure.** Repeat the frame-pacing probe on the 180 Hz monitor with `--frame-pacing on`, before and after: a steady smooth-wheel scroll on the `scroll-story` page, with displacement for the notch frame and the two after it.
   - Build the Desktop head into a scratch folder, launch it, move its window with `SetWindowPos`, and post `WM_MOUSEWHEEL` (delta −120, about every 40 ms) to its window handle so the cursor never moves. A temporary probe logs each notch's time, each frame's wall time and paced time, and each `Offset` change; remove it afterwards.
   - Done when the notch frame is within ±15% of its neighbours' mean, at p5/p95.
   - For each remaining double frame, log whether the pacer stepped two intervals and how late the callback was. If it was a late callback, record it in the BRIDGE-SCROLL edge cases. If not, report the cause and leave the double frame open in the backlog.
4. **Visual check (the maintainer's, after the workflow).** On the Desktop Gallery, do a long, steady smooth-wheel scroll through a page with code sections, and confirm there is no hitch at each notch.

## Open questions

None.
