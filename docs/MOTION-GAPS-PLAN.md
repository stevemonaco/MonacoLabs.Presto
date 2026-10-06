# Motion gaps — spring momentum, gradient and shadow tweens, exit animations

The three gaps from the post-Phase-17 review that other UI motion libraries (Framer Motion, GSAP, React Spring, Compose)
cover and Presto does not. Each is its own phase in [PLAN.md](PLAN.md) §5 (Phases 18–20) and ships independently.
Blur and the other effect shorthands are deliberately not here; they are the Prebuilts section of PLAN.md (§8).

## 1. Phase 18 — Velocity-preserving springs on `Animatable` (core)

### Gap

`Spring` is an ease, as in anime.js: a tween eased by a spring always starts at rest. `Animatable.Set` retargets a
property by rewinding its tween, so a spring-followed value changes direction with a dead stop: the momentum it had is
thrown away. Framer Motion, React Spring and Compose carry the current velocity into the new spring, which is what makes a
retargeted spring feel physical. Upstream 4.5.0 has no equivalent (`animatable.js` resets the tween the same way), so this
is a deliberate, opt-in deviation.

### Design

- `AnimatableParams.PreserveVelocity` and `AnimatableProperty.PreserveVelocity` (`bool?`, property wins, default false).
  Opt-in because it changes what a spring-eased animatable does, and because `Draggable` drives its own release springs
  through an `Animatable` and sets their velocity itself.
- Applies to `Set(property, double, …)` when the property's ease is a `Spring` — its own, the animatable's, or the one
  passed to `Set`. Any other ease, and the channel form `Set(property, IReadOnlyList<double>, …)`, keep today's behavior.
- At retarget, per tween:
  1. **Measure** the value's velocity in units per engine-time unit from the running ease: a central difference of the
     ease at the tween's current time, times the tween's distance, over its duration. A spring's own `Ease` is sampled
     through its `PureEase` so the measurement never raises the spring's `OnComplete`. A paused, completed or not-yet-started
     tween has zero velocity.
  2. **Normalize** it into the spring solver's unit — progress per solver second, `v * K * TimeScale / (to - from)` — which
     is exactly the `Spring.Velocity` the solver takes (its `x'(0)`). A retarget to the current value (zero distance)
     cannot carry velocity in an ease-based model and starts at rest.
  3. **Build** a `Spring` with the source spring's mass, stiffness and damping and that velocity. Its settling duration
     depends on the velocity, so each tween gets its own.
- The animation is stretched to the longest settling duration of its tweens (or the `duration` argument, which compresses
  the springs as a `Spring` + duration does today) and each tween eases with `t => spring.Solve(t * span)`, where `span`
  is the longest solver duration: a tween whose spring settles early holds its end value.
- `Spring.Solve` is already internal; nothing new is public on `Spring`.

### Tests (`AnimatablesTests`)

- A retarget mid-flight with `PreserveVelocity` keeps moving the old way for a few frames before turning back, and the
  per-frame step either side of the retarget matches within tolerance.
- Without the flag the first frame after a retarget moves (almost) nothing, which pins today's behavior.
- Retargeting a settled, or never-started, property starts at rest.
- A non-spring ease ignores the flag.
- The property settles exactly on the new target and the animatable's `OnComplete` fires once.

### Sample

The Animatable page gets a momentum stage: two dots follow the pointer's X on the same spring, the top one with
`PreserveVelocity`, so a flick shows the difference side by side.

## 2. Phase 19 — Gradient and box-shadow tweens (core hook + bridge)

### Gap

`BrushCodec` decodes only solid brushes, and nothing decodes `BoxShadows`. So gradient backgrounds (shimmer, aurora,
hover sheen), solid ↔ gradient fades, elevation and glow changes all need hand-written per-frame code.

### Core hook

Two things the current codec contract cannot express:

1. A value whose *shape* differs between its two ends (a 2-stop and a 3-stop gradient, one shadow and two) must be made
   to agree before interpolation. The core only knows how to pad the shorter channel list with zeros, which is wrong for
   colors and offsets.
2. A string end value the core would misparse. `"0 4 16 0 #40000000"` becomes a complex value whose "numbers" include the
   hex digits.

New optional interface in `Monaco.Presto.Targets`:

```csharp
public interface IReconcilingValueCodec : IValueCodec
{
    // Claims a string end value before the core parses it; false leaves it to the core (plain colors, relative values).
    bool TryParse(string text, DecomposedValue into);

    // Makes both ends the same kind and channel count, once both are decomposed and relative values resolved.
    void Reconcile(DecomposedValue from, DecomposedValue to);
}
```

- `ValueParser.Decompose` asks a reconciling codec first for a non-relative string.
- `Animation` calls `Reconcile` just before `Homogenize`/`PadShorterValue`, which then have nothing left to do.
- Discrete fields (gradient kind, spread method, units, inset) ride as channels and are read by `Encode` from the tween's
  `_toNumbers`, the way `RelativePointCodec` takes the end value's unit.

### Brush codec (`BrushCodec`, extended)

A gradient decomposes as a complex value:

| Index | Channel |
|---|---|
| 0 | kind: 1 linear, 2 radial, 3 conic (discrete; 0 on an end value means "a solid brush", see below) |
| 1 | spread method (discrete) |
| 2 | unit bit mask of the relative points/scalars (discrete) |
| 3 | opacity |
| 4–9 | geometry: linear `Start.X, Start.Y, End.X, End.Y, 0, 0`; radial `Center.X, Center.Y, Origin.X, Origin.Y, RadiusX, RadiusY`; conic `Center.X, Center.Y, Angle, 0, 0, 0` |
| 10… | one `(offset, r, g, b, a)` per gradient stop, sorted by offset |

A solid brush still decodes as a color, and a missing brush as the number 0. `Reconcile`:

- **solid ↔ gradient:** the solid end takes the gradient's shape, every stop in its color (a missing brush uses the
  gradient's own stop colors at alpha 0, so it fades in without a dark fringe). A gradient → solid tween marks its end
  kind 0, and `Encode` writes a `SolidColorBrush` on the final frame so the property ends on the brush type it was aimed at.
- **gradient ↔ gradient:** stops are resampled onto the union of both ends' offsets, so a 2-stop → 3-stop tween
  interpolates color at each offset instead of padding. A kind mismatch snaps the geometry to the end value's (geometry
  is not convertible between kinds) while stops still interpolate. A unit mismatch lerps the raw numbers in the end value's
  units, as `RelativePointCodec` does.

`Encode` keeps one gradient brush per tween and rewrites its stops in place (the solid path already does this), because
the brush being animated from is often immutable or shared. A brush's `Transform` is not carried. Strings are not parsed
as gradients (Avalonia's `Brush.Parse` reads colors only), so an end value is a brush instance: `TweenValue.Of(brush)`,
a resource or the builder's typed overloads.

### Box-shadow codec (`BoxShadowsCodec`, new, registered for `BoxShadows`)

- Each shadow is `(inset, offsetX, offsetY, blur, spread, r, g, b, a)`; an empty `BoxShadows` is a complex value with no
  channels.
- `TryParse` claims strings through `BoxShadows.Parse` (`"0 4 16 0 #40000000"`, comma-separated lists, `inset`).
- `Reconcile` pads the shorter list as CSS does, with a zero shadow, but in the counterpart's color at alpha 0 so
  that growing or dropping a shadow fades instead of flashing black. Inset is discrete and taken from the end value.

### Builder

`Background(IBrush)`/`Foreground(IBrush)` already take a gradient. `PropertyBuilderExtensions` gains the `BoxShadow`
shorthand set (`TweenValue` string, tween params, keyframes) plus a typed `BoxShadow(BoxShadows)`.

### Tests (`CodecTests`, bridge)

Linear stop colors and points at 50 %; 2 → 3 stop resampling; solid → gradient and gradient → solid (ends as a
`SolidColorBrush`); null → gradient fades in via alpha; radial and conic geometry; the kind mismatch snap; one brush
instance reused per tween; shadow string parse, single shadow interpolation, 1 → 2 shadow padding, inset from the end
value, empty → shadow.

### Sample

The Value types page gets two looping rows in its existing style: a 2-stop → 3-stop gradient that also turns, and an
elevation card whose shadow lifts with it.

## 3. Phase 20 — Exit animations (`Presence`, bridge)

### Gap

An element removed from the tree disappears in the same frame, so nothing can animate its exit. That covers a list item
removed from its collection, a toast dismissed, or the old content of a `ContentControl`. PLAN §7 decision 15 kept
enter/leave out of layout transitions because it "needs a presenter hook, not a layout hook". Framer Motion's
`AnimatePresence` covers it by keeping the element mounted until its exit finishes. Avalonia cannot keep a removed
control. An item container can be recycled, and a panel child can be re-added, the moment it leaves.

### Design: animate a ghost

On removal the element is snapshotted into a bitmap. The snapshot is shown in the window's `OverlayLayer`, exactly
where the element was drawn, and the exit animation plays on the snapshot. The real control is untouched and free to be
recycled. This was verified before planning: at `DetachedFromVisualTree` the control still renders into a
`RenderTargetBitmap` at its own origin, and the event's `AttachmentPoint` is the still-attached parent the removed
subtree was cut from.

- **`Presence.Exit`** (attached, `AnimationBuilder?`, on `Control`): played on a ghost of the element each time it leaves
  the visual tree *on its own*. That means `AttachmentPoint` is still attached, and every visual between the element and
  the removed subtree's root has exactly one visual child. The second condition is what separates an item removed from a
  list, whose container wraps only it, from a whole page navigated away, which takes many elements with it and should
  not leave a ghost of each. Set it on the item's template root or on its container (through an
  `ItemContainerTheme`/style).
- **`PrestoAvalonia.AnimateExit(Control, AnimationBuilder)`**: the imperative form, for code that removes an attached
  control itself. It ghosts the control where it is drawn now, and the caller removes it in the same frame. It returns the
  ghost's `Animation`, or null when the control is not on screen.
- **Placement:** the ghost's slot comes from the removed subtree's root. That root's `Bounds.Position` is in its former
  parent's coordinates, and the parent is still attached, so it translates to the window; the offsets between the element
  and the root are summed on the way up. The ghost is clipped to the intersection of its former ancestors' `ClipToBounds`
  boxes, so an item leaving a scrolled list does not draw over the list's edges. It copies the element's
  `RenderTransformOrigin` so a scale-out shrinks about the same point, and it is not hit-testable.
- **Snapshot:** a `RenderTargetBitmap` at the window's `RenderScaling`, so it is crisp on high-DPI displays. It is a still
  frame: content animating inside the element freezes for the exit.
  - *Styles* (found on the Desktop head, missed by tests using local values): Avalonia strips a removed branch's styles
    and dynamic resources before any detach event fires, container level included, and a compositor snapshot is
    impossible because the composition visual is gone too. So the branch's logical root, whose `Parent` is already null,
    is lent to the nearest logically attached visual ancestor for the render (`ISetLogicalParent`), with its
    `DataContext` pinned so no binding re-evaluates, then released.
  - *Transforms:* `RenderTargetBitmap` bakes the element's render transform in and crops to its untransformed box. A
    transform held by the shorthands is suspended by emptying the composer's group for the render, which changes no
    property and so does not trip its re-assert, and moves onto the ghost as a `MatrixTransform`. Any other render
    transform stays baked in.
- **Lifetime:** the ghost leaves the overlay when its animation completes or is canceled/reverted (`OnPause` with
  `Canceled`), so a scope reverting on page unload also clears ghosts. Timer callbacks are chained, not replaced.
- **What a builder may animate:** the ghost is an `Image`, so an exit animates the transform shorthands and `Opacity`
  (and anything else an `Image` has). A property only the original control has, such as `Background`, throws as it
  would on any other target. This is documented on `Exit`.
- **Enter** needs nothing new: `Animate.OnAttached` already plays on every attach. The Presence sample pairs the two, and
  `AnimateLayout` on the siblings closes the gap the ghost leaves.

### Tests (`PresenceTests`, bridge, pixel-based like `ConnectedTests`)

- Removing a panel child leaves a ghost drawn in its slot, which fades out and is gone after the exit's duration.
- An `ItemsControl` item removed from its `ObservableCollection` ghosts (the container wraps only it).
- Replacing a `ContentControl`'s content ghosts the old content while the new one is laid out.
- Removing the whole view the element lives in (a multi-child parent) leaves no ghost.
- Closing the window leaves no ghost (`AttachmentPoint` is null).
- A ghost from a scrolled `ScrollViewer` is clipped to the viewport.
- `AnimateExit` ghosts an attached control and returns null for a detached one.
- Canceling or reverting the exit animation removes the ghost; the scope of the caller reverts it.

### Sample

A new *Presence* reference page:
- a to-do list bound to an `ObservableCollection`, whose items enter with `Animate.OnAttached`, leave with
  `Presence.Exit`, and close the gap with `AnimateLayout`. The list sets `ClipToBounds="False"`, because Fluent clips
  `ItemsControl` and would cut off a row sliding up from below the shrunken list;
- a `ContentControl` whose content crossfades between two views.

The Builder section already has a toast stack that collapses each toast's height before removing it, a layout-driven
exit that stays as it is.

## 4. Order and verification

18 → 19 → 20; each lands green on the full suite (`dotnet test --solution Monaco.Presto.slnx`), with its sample checked
on the Desktop head. PLAN.md §7 records the decisions each phase fixes (opt-in momentum, the reconciling codec contract,
the ghost model and its "leaves on its own" rule).
