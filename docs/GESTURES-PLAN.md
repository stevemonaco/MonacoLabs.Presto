# Gestures — pinch, zoom and rotate

Phase 35 in [PLAN.md](PLAN.md). Status: **implemented** (`Transformable`, its tests, the docs and the Photo viewer
sample of §5; the Android and Browser real-touch smoke runs are still to do). §7 lists where the build departs from §1–§4. This plan follows the post-Phase-33 gap review. `Draggable` is a
faithful single-pointer port of anime.js `createDraggable`. Nothing in Presto handles a second finger, a trackpad pinch
or a Ctrl+wheel zoom, and zooming about a point needs translation and scale solved together, which one axis at a time
cannot express. Photo viewers, maps, canvases and diagram editors all need that. With Phase 33 a release can coast.

| Phase | Scope | Size |
|---|---|---|
| 35 | `Transformable`: pan, pinch-zoom and rotate on one element, focal-point zoom, rubber-banded scale limits, pan bounds, double-tap, wheel and trackpad zoom, inertia on release; a photo viewer sample | large |

## 0. What Avalonia 12.1.2 offers (verified against the source)

- **`PinchGestureRecognizer`** (`src/Avalonia.Base/Input/GestureRecognizers/PinchGestureRecognizer.cs`) raises
  `InputElement.PinchEvent` with `Scale` (distance ratio since the second contact), `ScaleOrigin` (the midpoint at the
  start, never updated), `Angle` and `AngleDelta` (degrees, positive clockwise), and `PinchEndedEvent` as soon as either
  contact lifts. It reads positions with `e.GetPosition(Target)`, so on a target that it is itself transforming, the
  positions move with the transform. It only accepts `PointerType.Touch` and `Pen`, and only two contacts.
- **Other recognizers:** `ScrollGestureRecognizer` (touch and pen pan with its own inertia, used by `ScrollViewer`),
  `PullGestureRecognizer` and `SwipeGestureRecognizer`. There is no rotate recognizer. `Gestures.DoubleTapped` and
  `Holding` exist as routed events.
- **Trackpad gestures are platform-specific:**
  - **macOS:** only `Avalonia.Native` raises `RawPointerEventType.Magnify`/`Rotate`/`Swipe`
    (`src/Avalonia.Native/TopLevelImpl.cs`). They surface as `InputElement.PointerTouchPadGestureMagnifyEvent`,
    `…RotateEvent` and `…SwipeEvent`, all `PointerDeltaEventArgs`.
  - **Windows:** `Avalonia.Win32` handles `WM_TOUCH` and `WM_MOUSEWHEEL` but no gesture messages, so a precision
    touchpad pinch reaches Avalonia as the Ctrl+wheel Windows synthesizes for it.
  - **Browser:** browsers deliver a trackpad pinch as a `wheel` event with `ctrlKey`. Avalonia's `input.ts` forwards
    the modifiers and calls `preventDefault()`, so the page does not zoom. The host element also sets
    `touch-action: none` (`dom.ts`), so touch pinches reach the app rather than the browser.
  - **X11:** no touchpad gesture handling.

  Ctrl+wheel therefore covers trackpad pinch on Windows and Browser, and is the conventional mouse zoom in image
  viewers, maps and design tools.
- **Touch:** multi-touch arrives as separate `Pointer`s of `PointerType.Touch` through `TouchDevice`, which implicitly
  captures each pointer to its hit element on `TouchBegin`. Android (`AvaloniaView.Input.cs`), iOS (`InputHandler.cs`,
  one id per `UITouch`; indirect iPad pointer touches are skipped) and Browser (`pointerId` per touch) all do this.
- **Headless:** `HeadlessWindowExtensions` has mouse helpers only ("There are no helper methods for touch or pen
  input"). `TouchDevice`, `IInputRoot` and `RawPointerEventArgs.InputHitTestResult` are `[PrivateApi]` or internal, so
  raw touch cannot be injected. `Pointer(id, PointerType.Touch, isPrimary)` and the `PointerPressedEventArgs`,
  `PointerEventArgs` and `PointerReleasedEventArgs` constructors are public, so tests can raise touch pointer events on
  an element directly.

## 1. Decisions

1. **A new type, `Transformable`, rather than extending `Draggable`.** `Draggable` is a parity port whose ported
   `draggables.test.js` suite pins single-pointer behavior. Its axes are independent, and it has no notion of a scale
   or an angle to zoom about. `Transformable` shares its idioms instead:
   - a params record;
   - an `Animatable` (duration 0) writing the `X`, `Y`, `Scale` and `Rotate` shorthands;
   - `VelocityTracker`s per channel;
   - `Inertia` for release;
   - the same verbs (`Stop`, `Reset`, `Refresh`, `Enable`, `Disable`, `Revert`) and callback names.

   One-finger pan is built in, so `Transformable` also replaces `Draggable` wherever an element pans and zooms.
2. **Raw pointers, not `PinchGestureRecognizer`.** The recognizer reads positions relative to the element being
   transformed, freezes its origin at the start (so a two-finger pan cannot move the focal point), and ends the pinch
   when one finger lifts. `Transformable` tracks up to two pointers itself on its trigger, reading positions in the
   target's **parent** space, which the transform does not move. A third pointer is ignored.
3. **One state, four numbers.** The state is translation `t` (the `X`/`Y` shorthands, in parent units), scale `s` and
   rotation `θ` (degrees, clockwise, as `RotateTransform`). The composer applies scale and rotation about the element's
   `RenderTransformOrigin` `o`, then the translation (RenderTransformComposer remarks). A user `RenderTransform` already
   on the element stays underneath and is not part of the math; the docs say so.
4. **Focal-point zoom and rotate.** Each frame of a two-finger gesture, and each wheel or trackpad step, keeps the
   content point under the focal point `f` fixed. Here `L` is the layout offset, `c = f − L − o`, `k = s′/s` and
   `Δθ = θ′ − θ`. The new translation is `t′ = c − k·Rot(Δθ)·(c − t)`. For two fingers, `f` is the current midpoint,
   `k` the distance ratio since the last move and `Δθ` the angle change. The midpoint's own movement pans, so a
   two-finger drag pans, zooms and rotates at once.
5. **Limits rubber-band, then settle.**
   - **Scale:** beyond `MinScale`/`MaxScale` during a gesture, scale follows the fingers through a friction factor,
     as `Draggable`'s `ContainerFriction` does. On release it springs back to the limit about the last focal point.
   - **Pan:** `PanBounds` gives three modes. `Contain` (the default) keeps the scaled content covering the
     container, or centered when smaller. `Inside` keeps it within the container. `Free` sets no bounds. Bounds are
     computed from the scaled, rotated axis-aligned box.
   - **Container:** `Container` (default: the parent) supplies the box, as `Draggable.Container` does.
6. **Release coasts pan and rotation, not scale.**
   - **Pan:** `Animatable.Fling` with an `Inertia` built from `ReleaseInertia` (a template, as
     `DraggableParams.ReleaseInertia` is). `Min`/`Max` are set from the pan bounds at the scale the release settles
     to, so a coast that runs out of room bounces off the edge through Phase 33's folded spring.
   - **Rotation:** coasts only when `Rotate` is on. With `RotationSnap` (degrees, 0 for none) it lands on a multiple
     through `ModifyTarget`.
   - **Scale:** only settles (decision 5). Zoom momentum is rare in native viewers and makes the focal point drift.
   - **Velocity:** measured with one core `VelocityTracker` per channel, in parent units per ms.
7. **Lifting one finger hands over to pan.** When one of two contacts lifts, the remaining pointer continues as a
   one-finger pan re-based on its current position, so nothing jumps. Its velocity trackers reset their baseline.
   Putting a second finger down mid-pan starts a pinch from the current state.
8. **Double-tap toggles zoom.** `DoubleTapScale` (default 2.5, null to disable) zooms about the tap point from any scale
   at or below 1.01 and resets otherwise. The zoom plays through `Settle`, an `AnimationBuilder` (default:
   `Recipes.Tokens` medium duration and ease) that also drives the spring-back of decision 5 and `ZoomTo`/`Reset`.
   Listens to `Gestures.DoubleTapped`, so mouse double-clicks work too.
9. **Wheel and trackpad zoom.**
   - `WheelZoom` has three values: `None`, `WithControl` (the default) and `Always`. The default leaves a plain wheel
     to an ancestor `ScrollViewer`.
   - Each notch scales by `WheelZoomStep` (default 1.15 per 120 delta units, proportional for smooth trackpad
     deltas) about the cursor, with no rubber band.
   - On macOS, `PointerTouchPadGestureMagnify` (scale `1 + Delta.X`) and `…Rotate` (when `Rotate` is on) are applied
     about the pointer.
   - Handled wheel events are marked handled. Unhandled ones, such as a plain wheel under `WithControl`, pass through.
10. **Panning at rest is opt-in for touch.** When `PanAtRest` is false (the default) and the scale is at `MinScale`, a
    single touch or mouse drag is not captured and not handled, so an ancestor `ScrollViewer`, `Carousel` or
    `Draggable` swipe still works: the photo-gallery convention. Two fingers always engage. Once zoomed in, one finger
    pans.
11. **One owner per element.** Creating a `Transformable` on a target that a live `Draggable` drives, or the reverse,
    throws `InvalidOperationException` naming both. A `Motion` state or recipe animating `Scale`/`Rotate`/`X`/`Y` on
    the same element composes by replace like any other animation, and the docs recommend putting hover motion on an
    inner element instead.
12. **UI thread only.** Direct manipulation needs the latest pointer position every frame, so everything runs through
    the `Animatable` on the UI thread. The compositor is not involved, and Phase 34's compositor `RenderTransformOrigin`
    fix does not affect it.
13. **Reduced motion:**
    - Manipulation is unchanged, because the user drives it.
    - Under `PrestoDefaults.ReducedMotion` the release does not coast: pan and rotation land on their clamped, snapped
      targets at once.
    - `Settle` uses its `WhenReduced` form, which is instant for the default.
    - `ReduceAll` already makes an `Inertia` land in one frame (Phase 33).
14. **Pen counts as touch; mouse is one pointer.** Mouse left-drag pans (subject to decision 10), Ctrl+wheel zooms and
    double-click toggles zoom. Rotation with a mouse happens only through `RotateTo` or the macOS trackpad.

## 2. API

```csharp
public sealed record TransformableParams
{
    public Control? Trigger { get; init; }                 // default: the target
    public Control? Container { get; init; }               // default: the target's parent
    public bool Pan { get; init; } = true;
    public bool Zoom { get; init; } = true;
    public bool Rotate { get; init; }
    public bool PanAtRest { get; init; }
    public PanBounds PanBounds { get; init; } = PanBounds.Contain;
    public double MinScale { get; init; } = 1;
    public double MaxScale { get; init; } = 4;
    public double ScaleFriction { get; init; } = 0.35;     // rubber band beyond the limits
    public double RotationSnap { get; init; }              // degrees; 0 = none
    public double? DoubleTapScale { get; init; } = 2.5;
    public WheelZoom WheelZoom { get; init; } = WheelZoom.WithControl;
    public double WheelZoomStep { get; init; } = 1.15;
    public Inertia? ReleaseInertia { get; init; }          // template; null = the default inertia
    public AnimationBuilder? Settle { get; init; }
    public Action<Transformable>? OnGrab, OnUpdate, OnRelease, OnSettle { get; init; }
}

public enum PanBounds { Contain, Inside, Free }
public enum WheelZoom { None, WithControl, Always }

public sealed class Transformable : IRevertible
{
    public double X { get; }  public double Y { get; }  public double Scale { get; }  public double Rotation { get; }
    public bool IsManipulating { get; }
    public Animatable Animate { get; }
    public Transformable ZoomTo(double scale, Point? focal = null, bool animate = true);
    public Transformable PanTo(double x, double y, bool animate = true);
    public Transformable RotateTo(double degrees, Point? focal = null, bool animate = true);
    public Transformable Reset(bool animate = true);
    public Transformable Stop();  public Transformable Refresh();
    public Transformable Enable();  public Transformable Disable();  public Transformable Revert();
}
```

The entry points are `PrestoAvalonia.CreateTransformable(Control, TransformableParams?)` and the
`control.CreateTransformable(...)` extension, beside `CreateDraggable`. `focal` is in the target's parent space; null
means the container's center.

## 3. Implementation steps

1. **`TransformMath` (internal, pure).** It holds decision 4's focal update, the rubber band, and `PanBounds` for a
   scaled, rotated box. Unit tests only.
2. **`Transformable` pointer core.** Two-pointer tracking in parent space, the pinch/pan state machine with the
   hand-over of decision 7, and `PanAtRest`. Writes go through the `Animatable`; callbacks fire; the ownership check
   against `Draggable` is shared through a small internal registry.
3. **Release:** the scale spring-back, and pan and rotation `Fling` with bounds and snap.
4. **Other inputs:** double-tap, wheel and trackpad, and `ZoomTo`/`PanTo`/`RotateTo`/`Reset` through `Settle`.
5. **Test helper:** a `Touch` helper in the bridge test project that raises `PointerPressed`/`Moved`/`Released`
   for `new Pointer(id, PointerType.Touch, …)` on an element, honoring capture.
6. **Docs:** README section, a cheat-sheet row (no anime.js counterpart; mention Framer's `usePinch`/`useGesture`
   pattern), and a Platforms row per decision 9's findings.
7. **Samples** (§5), then Android and Browser smoke runs with real touch. iOS only compiles here (PLAN Phase 8).

## 4. Tests (`TransformMathTests`, `TransformableTests`, bridge, headless)

- **`TransformMath`:**
  - A zoom about `f` keeps the content point under `f` fixed, for any origin, scale and rotation (property test over
    random states).
  - Zoom then unzoom about the same point returns the start state.
  - `Contain` bounds center content smaller than the container.
  - Rotated bounds use the axis-aligned box.
- **Pinch** (synthetic touch):
  - Spreading two fingers doubles the scale and keeps the midpoint's content point fixed.
  - Moving both fingers together pans.
  - Rotating them rotates only when `Rotate` is on.
  - A third finger changes nothing.
- **Hand-over:** lifting one finger mid-pinch continues as a pan with no jump. A second finger mid-pan starts a pinch
  from the current state.
- **Limits:**
  - Beyond `MaxScale` the scale follows through friction, and the release springs back to `MaxScale` about the last
    focal point.
  - `Contain` pan clamps and the release coasts into the edge and settles on it.
  - `RotationSnap = 90` lands on a multiple.
- **`PanAtRest = false` at scale 1:** a one-finger drag is not handled and does not capture. A parent's
  `PointerMoved` handler sees it.
- **Double-tap and settle:**
  - Double-tap zooms to `DoubleTapScale` about the tap point and toggles back.
  - `Settle` durations are honored, and reduced motion makes it instant.
- **Wheel:**
  - Ctrl+wheel via `MouseWheel` zooms about the cursor.
  - Under `WithControl` a plain wheel is not handled and an ancestor `ScrollViewer` scrolls.
  - `Always` zooms on a plain wheel.
- **Reduced motion:** the release lands on its clamped target in one frame. Manipulation still tracks the fingers.
- **Ownership:** `CreateTransformable` on a target with a live `Draggable` throws, and the reverse throws too. `Revert`
  releases the target so the other can be created.
- **Mouse:** a mouse drag pans once zoomed in, driven through the real `MouseDown`/`MouseMove`/`MouseUp` helpers.
- **Allocation:** a pinch move allocates nothing in the core path beyond Avalonia's event args. `AllocationTests`
  budget, measured with the synthetic touch helper.

## 5. Samples

- **New Showcase page "Photo viewer":**
  - A photo that pinch-zooms, pans with bounds and rotates with a 90° snap.
  - Double-tap toggles zoom and Ctrl+wheel zooms about the cursor.
  - A filmstrip of photos in a `Carousel` underneath: swipes change the photo at rest and pan it when zoomed
    (decision 10).
  - A live readout of `Scale`, `Rotation` and the release velocity, and a Reset button that plays `Settle`.
  - The photos come from the CC0 set Phase 28 adds under `Assets/Photos/`. If Phase 35 lands first, it adds them.
- **Gestures showcase:** a fifth tile is not added. The page already has four tiles at its width, and the viewer needs
  room.
- **Draggable page:** one line of caption pointing to the Photo viewer for multi-touch.

## 6. Open questions (confirm before the phase)

- **Name:** `Transformable` (pan, zoom and rotate) or `Pinchable`. `Transformable` is recommended because it also pans.
- **Scale inertia:** decision 6 leaves it out. Add it, in log-scale space, only if the sample feels dead on release.
- **An ancestor `ScrollViewer`'s `ScrollGestureRecognizer`** may capture the first touch before the second finger
  lands, which cancels the pinch. It needs a real-device check on Android. If it happens, the fix is to capture the first
  touch while any second pointer is down within the recognizer's start distance, or to document
  `ScrollViewer.IsScrollInertiaEnabled`-style opt-outs.
- **A `Transformable` on a `CompositorCanvas` scene element** (a map or canvas). That needs scene-side state rather than
  the shorthands. Out of scope until asked for.
- **Linux trackpad pinch.** X11 raises nothing; decide whether to document it as unsupported or read XInput2 gestures
  (not an Avalonia API today).

## 7. Deviations as built

- **Scale rubber band in log space.** `TransformMath.RubberBand` damps the excess ratio, `max·(s/max)^friction`, rather
  than the linear excess `Draggable`'s `Cushion` uses, so pinching past `MaxScale` and past `MinScale` feel equally stiff.
- **`Inside` with oversized content.** On an axis where the scaled, rotated box is larger than the container, `Inside`
  cannot hold it within, so it falls back to `Contain`'s covering range.
- **Scale spring-back interpolates endpoints.** About a fixed focal point the translation is affine in scale
  (`t′ = c + k·(t − c)`), so easing `X`, `Y` and `Scale` to their ends with the same `Settle` duration and ease keeps the
  focal point still every frame, with no per-frame recompute. The translation's end is clamped into the pan bounds at the
  settled scale. Because both would write `X`/`Y`, the pan does not coast during a spring-back; it only settles.
- **Pan bounds at the landed angle.** Rotation's fling is resolved first, and the pan's `Min`/`Max` come from the
  bounds at the scale and angle the release lands on.
- **A touch during a release grabs**, even at `MinScale` with `PanAtRest` off, so a coast can always be caught.
- **Programmatic moves settle like a release.** `ZoomTo`/`PanTo`/`RotateTo`/`Reset`, double-tap, wheel and trackpad
  drop any gesture in progress without a release. Animated ones ease all four channels to their clamped ends through
  `Settle` and end with `OnSettle`. An animated `RotateTo` interpolates endpoints, so its focal point is exact only at the
  ends. The trackpad rotation is negated on the assumption that `Avalonia.Native` passes AppKit's counter-clockwise
  degrees through; this is unverified on hardware.
- **`ScaleFriction` also rubber-bands the pan.** Past the pan bounds during a gesture, the translation follows through the
  same friction as the scale, rather than stopping at the edge; the release brings it back. Grabbing again inverts the
  band, so a target caught mid spring-back does not jump.
- **`Settle` contributes its duration and ease only.** The builder is resolved with `ToParams()` (so `WhenReduced`
  applies), and its `Duration` and `Ease` drive every channel through `Animatable.Set`; any other setting it carries,
  such as a delay or callbacks, is ignored.
- **Wheel zoom is exponential in the delta.** Avalonia reports a notch as a delta of 1, not 120, and the scale is
  multiplied by `WheelZoomStep^Delta.Y`, which is the same per notch and proportional for fractional trackpad deltas.
  Only the vertical delta zooms. Wheel and magnify steps clamp to the limits at once, with neither rubber band nor
  animation.
- **Mouse and touch do not mix.** A mouse press while a finger holds the target is ignored, and so is a finger while the
  mouse holds it; only the left button grabs.
- **`Pan = false`** stops the pointers' own movement from panning, but a zoom or turn still moves the translation to keep
  its focal point fixed, and the release only settles the pan into its bounds.
- **`Refresh()` during a release** re-aims the pan's coast at the new bounds from its current value and velocity, instead
  of clamping at once.
- **Allocation.** A two-finger move allocates about 550 bytes, not nothing. `Transformable` itself allocates nothing: 400
  bytes are Avalonia's change args for `X`, `Y`, `ScaleX`, `ScaleY` and `Angle`. The rest comes from each
  `Animatable.Set` soft-resetting its animation, which composes the tween's value (a boxed double, and a string for
  `Rotate`'s degrees). Measuring it removed a per-call closure from `Composer.ComposeReplacedTween`.
- **Grabbed pointers skip gesture recognition.** Avalonia's recognizers see handled events too, so a swipe-enabled
  `Carousel` around a zoomed photo stole the pan's capture. `Transformable` calls `PreventGestureRecognition()` on every
  press and move it takes. Avalonia 12.1.2's `Carousel` swipe accepts touch and pen only, so on desktop the Photo viewer
  changes photo through its filmstrip.
- **Linux trackpad pinch** (§6) is documented as unsupported. Ctrl+wheel still zooms on X11.
