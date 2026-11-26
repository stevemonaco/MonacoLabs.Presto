# Brushes on the compositor

## Why

`AnimateOnCompositor` carries only `X`, `Y`, `Rotate`, `Scale*` and `Opacity` (BRIDGE-THREAD-010), because those were the only composition properties a `CompositionVisual` exposes. Every color and gradient animation runs on the UI thread through the brush codecs (BRIDGE-CODECS-005–018), so it stops when the UI thread blocks and costs a UI-thread write per frame even when it is an ambient loop that runs all day. Tempo hit this twice: its backdrop drift moved to the compositor but its colors could not ("X, Y and Scale are all the compositor carries", ARCHITECTURE §5), and `Loading.Shimmer` is documented as UI-thread only.

Avalonia 12.2 adds composition brushes (AvaloniaUI/Avalonia#20181, fixed by #21873): `CompositionSolidColorBrush`, `CompositionLinearGradientBrush`, `CompositionRadialGradientBrush` and `CompositionConicGradientBrush`, with `CompositionGradientStop`, in `Avalonia.Rendering.Composition`. They implement `IBrush`, so one can be assigned to any brush property (`Background`, `Foreground`, `BorderBrush`, `Fill`, `Stroke`), and their properties animate on the render thread through `StartAnimation`. This change teaches the compositor animation to drive them.

## What

`AnimateOnCompositor`, and `Animate` with `Compositor` or `PreferCompositor`, accept a brush property of the target (any `AvaloniaProperty` whose type is `IBrush`, by key or by name, for example `Border.BackgroundProperty` or `"Background"`). The end value is what the UI thread takes today: a color, a color string, a `SolidColorBrush`, or a linear, radial or conic gradient brush. The animation looks the same as the UI-thread one, because the start and end brushes are reconciled by the same codec (stops resampled onto the union of offsets, a solid end shaped as the gradient, a missing brush fading in, kind, spread and units from the end value), and the mirror samples the same interpolation, color space and hue rule included.

On start, on the UI thread, the bridge:

1. Decodes the start brush (the property's current value, or, when another compositor animation drives the property, its mirror's value) and the end value with the brush codec.
2. Creates one composition brush of the kind drawn during the animation, on the compositor of the target's visual, with one `CompositionGradientStop` per reconciled stop, and holds the property at `Animation` priority with it (as the opacity hold does, BRIDGE-THREAD-028).
3. Compiles tracks from the mirror: brush `Opacity`; solid `Color`; linear `StartPoint` and `EndPoint`; radial `Center`, `GradientOrigin`, `RadiusX` and `RadiusY`; conic `Angle` and `Center`; and each stop's `Color` and `Offset`. Geometry and offset tracks compile like transform tracks (two exact key frames for a single plain tween, BRIDGE-THREAD-016, else 100 samples). Color tracks are always sampled.
4. On completion, writes the codec's end brush (a `SolidColorBrush` for a solid end, or an ordinary gradient brush) through the hold and stops the composition brush's animations, so the property reports what is drawn.

Playback control (pause, seek, reverse, speed, restart, complete) works as it does for transform tracks: stop and recompile from the mirror (BRIDGE-THREAD-032–038). Cancel stops the brush's animations where they stand. Revert releases the hold, so the control's own brush shows again. Brush properties join the per-visual composition registry: a later compositor animation on the same brush property takes it over, and animations on different properties run side by side (BRIDGE-THREAD-046–048).

What does not change: UI-thread brush tweens and their codecs; the seven transform and opacity keys; `CompositorScene`, whose `ImmediateDrawingContext` accepts only `IImmutableBrush`; XAML entry points, which stay on the UI thread (BACKLOG [BRIDGE-THREAD]).

## Decisions

- **A brush animation creates its own composition brush.** The bridge never animates a composition brush the app assigned. It decodes it like any `ISolidColorBrush` or `IGradientBrush` and writes a new one. Reason: the brush codecs never modify the brush they start from (BRIDGE-CODECS-007, 016), and an app brush may be shared between controls. Rejected: animating an app-supplied `CompositionBrush` in place.
- **The drawn kind is fixed for the whole run.** A solid-to-gradient run draws a gradient from the first frame; a gradient-to-solid run draws the gradient, its stops easing to the color, then the completion writes the `SolidColorBrush`, as BRIDGE-CODECS-015 does on the UI thread. Reason: a composition brush cannot change kind, and the stop list itself is not animatable, so the stop count is fixed at the reconciled union when the run starts.
- **Colors are always sampled.** Avalonia's composition color interpolator lerps straight-alpha bytes in sRGB, while Presto interpolates with premultiplied alpha and optionally in another color space with a hue rule (CORE-COLORS, `Tints.Recolor`). Reason: a two-key-frame color track would draw a different midpoint than the same animation on the UI thread. 100 segments keep the difference below a byte.
- **Completion replaces the composition brush, and a detached target stops ticking.** Composition brushes are activated when created and never deactivated, so a running animation keeps the compositor clock busy even when nothing displays the brush. On completion, cancel, revert and detach, the bridge stops every animation it started on the brush and its stops. On attach, it recompiles from the mirror (BRIDGE-THREAD-030). Reason: a looping recolor on a page that was navigated away would otherwise keep the render loop awake. This differs from transform tracks, whose finished key frames stay on the visual (BRIDGE-THREAD invariants), because a visual's tracks end with it and a brush's do not.
- **The brush belongs to the visual's compositor.** It is created from `ElementComposition.GetElementVisual(target).Compositor`; a target attached under another compositor gets a new brush on recompile. Reason: since #21873 a brush drawn on a foreign compositor throws `InvalidOperationException`.
- **`OpacityMask` stays UI-thread.** It is rejected with a reason from `CanAnimateOnCompositor`, and `AnimateOnCompositor` throws for it. Reason: Avalonia converts an opacity mask to an immutable brush (`BrushExtensions.ToImmutable`), which a composition brush is not; the spike (task 1) confirms this. So `Loading.Shimmer` and the mask recipes stay on the UI thread.
- **Undecodable brushes and brush transforms stay out.** An image, visual or drawing brush fails `CanAnimateOnCompositor` with the property as the reason, as an undecodable value does on the UI thread. `Transform` and `RelativeTransform` follow the codec (BRIDGE-CODECS-018). Reason: Avalonia has no composition tile brush, and brush transforms are not `Animated` in the composition schema.

## Spec changes

**BRIDGE-THREAD** (`animation-thread.md`):

- BRIDGE-THREAD-010, changed: adds "and a brush property of the target (an `AvaloniaProperty` of type `IBrush`, by key or name) to the composition brush the animation creates for it".
- BRIDGE-THREAD-011, changed: the message lists "X, Y, Rotate, Scale, ScaleX, ScaleY, Opacity and brush properties".
- BRIDGE-THREAD-006, changed: an undecodable start or end brush, and `OpacityMask`, are reasons.
- BRIDGE-THREAD-015, changed: a brush property starts from the property's current value, or from the mirror of the compositor animation that drives it.
- Added: the composition brush is of the drawn kind, with one stop per reconciled stop, and it is held at `Animation` priority for the run.
- Added: which composition properties a brush track drives, per kind (the list in step 3 of What).
- Added: a color track is sampled at 100 segments per iteration even when it is a single plain tween.
- Added: on completion, the codec's end brush is written through the hold and the composition brush's animations stop.
- Added: on detach, the brush's animations stop. On attach, they recompile from the mirror, on the attaching visual's compositor.
- BRIDGE-THREAD-043, changed: cancel also stops the brush's animations where they stand and releases the brush hold.
- BRIDGE-THREAD-044, changed: revert also releases the brush hold, so the control's own brush is drawn.
- Decisions: "Not CSS … Skew, colors and other properties are UI-thread only" is reworded to name brushes as carried, and the decisions above are added.

**BRIDGE-CODECS** (`value-codecs.md`): no requirement changes. A note records that the compositor's mirror samples these codecs.

**BRIDGE-RECIPES-MEDIA** (`recipes-media.md`): `Tints.Recolor` notes that it runs on the compositor when its property is a brush property.

**GUIDE.md**: the Render thread section adds brushes to what the compositor carries, with the `OpacityMask` and `CompositorScene` exceptions.

## Tasks

1. **Spike, kept as pinning tests** (`CompositionBrushSpikeTests`, headless with Skia and the frozen `CompositorClock`). A composition brush assigned to `Border.Background`, `TextBlock.Foreground`, `Border.BorderBrush` and `Shape.Fill` draws. A `Color` key frame animation moves the pixels. `StopAnimation` leaves them where they stand. A brush assigned to `OpacityMask` throws. A brush from another compositor throws. A `BrushTransition` on the property does not replace a brush held at `Animation` priority. A forever animation on a detached brush still holds a compositor clock item.
2. **Compile.** Brush keys resolve in the compiler and the mirror's scratch gets brush slots that resolve through the brush codecs. Tests in `CompositorCompileTests`: a solid recolor compiles to one sampled `Color` track; a gradient-to-gradient run with different stops compiles one stop per union offset; each gradient kind compiles its geometry tracks; a single plain angle tween compiles to two exact key frames; a gradient-to-solid run draws a gradient; `OpacityMask` and an image brush are rejected with their reasons.
3. **Run.** The hold, completion, detach and attach, and compositor affinity. Tests in `CompositorAnimationTests`: a recolor draws the mid color at half time; completion leaves a `SolidColorBrush` on the property; a revert shows the control's own brush; a detached target's brush stops ticking and resumes from the mirror when it attaches again.
4. **Playback and registry.** Pause, seek and reverse recompile brush tracks; a second compositor animation on `Background` takes the brush over; `Background` and `X` run side by side; `Presto.Remove(control, "Background")` cancels only the brush track. Tests in `CompositorPlaybackTests` and `CompositorRegistryTests`.
5. **Thread choice.** `CanAnimateOnCompositor` and `PreferCompositor` accept brush properties and fall back for `OpacityMask` and undecodable brushes. Tests in `AnimationThreadTests`.
6. **Gallery.** The Compositor page gets a brush lane: a solid recolor, a conic angle sweep and a radial center drift, side by side with the same animations on the UI thread, under the page's "Freeze UI thread for 2 s" button. Gallery test for the lane. Visual check on the Desktop sample with the UI thread blocked.
7. **Docs.** GUIDE Render thread section, specs as above, CHANGELOG.

## Open questions

- **What does reading the property mid-run return?** `Presto.Get(control, "Background")` or `GetValue` would return the composition brush, whose client-side values are the ones last set (the start), not what is drawn. Should `Presto.Get` read the mirror instead? Recommendation: return the mirror's value, so a read reports what is drawn.
- **Should a compositor form of shimmer exist?** `Loading.Shimmer` sweeps an `OpacityMask`, which the compositor cannot carry. A `Background` highlight sweep could run there, but it looks different (it paints over the content instead of revealing it). Recommendation: leave `Shimmer` as it is, and add a compositor-capable recipe only if a sample asks for one.
- **Tempo has no clean use yet.** Its backdrop gradient is an `OpacityMask` and its accents follow `Motion.State` on the UI thread. Recommendation: dogfood through the gallery lane only, since the backdrop-effects work left Tempo's glass without a backdrop blur (ARCHITECTURE §5, "Glass needs the moving backdrop, and no backdrop blur").
