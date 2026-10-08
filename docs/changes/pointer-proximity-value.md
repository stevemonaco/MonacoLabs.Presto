# Pointer proximity values and magnetic pull

## Why

Pointer-proximity effects (letters that swell as the pointer nears, buttons that lean towards it, spotlights) are hand-built today. `TypographyPage.LeanWordmark` (`samples/PrestoSample/Showcase/Pages/TypographyPage.axaml.cs`) handles `PointerMoved` on a stage, translates each letter's layout-slot center into the stage, computes a clamped, eased falloff and pushes three `Animatable.Set` calls per letter, with a separate `PointerExited` handler to relax. The pieces involved (measuring against the layout slot so a moved element never feeds back into its own pull, going back to rest when the pointer leaves, scrolling and layout moving the element while the pointer stays still) are easy to get wrong and are repeated on every page that wants them.

`PrestoAvalonia.PointerValue` (BRIDGE-FOLLOW-050) is no help here: it listens on the control itself, so it only sees the pointer while it is *over* the control, and proximity is about the pointer approaching from outside.

This change resolves the backlog item "[BRIDGE-RECIPES] A pointer proximity / magnetic pull prebuilt, replacing the hand-built versions on the Typography and Animatable pages" in `docs/BACKLOG.md`.

## What

Two new motion value sources in `PrestoAvalonia`, tracked over a surface (the control's `TopLevel` by default) rather than over the control:

```csharp
// 1 with the pointer on or inside the control's edge (or at its center), falling linearly to 0 at `radius` DIPs away.
MotionValue PointerProximityValue(Control control, double radius, Visual? over = null, bool fromCenter = false);

// Pointer position minus the control's center along `axis`, in DIPs.
MotionValue PointerOffsetValue(Control control, Orientation axis, Visual? over = null);
```

Both measure against the control's layout slot (`Bounds` translated into the surface), never its render transform, so an element pulled or scaled by the value does not change what it measures. Both go to 0 while the pointer is absent: outside `over`, gone from the window, or a touch contact lifted. A distance curve is a `Map` away (`proximity.Map([0, 1], [0, 1], Eases.InOutQuad)`), and center distance is `Combine(dx, dy, Math.Sqrt(...))`. The values plug into `Follow.*`, `Follow.Bind` and `Spring` like any other source.

On top of them, a `Magnetic` attached-property prebuilt modeled on `Tilt` (BRIDGE-EFFECTS):

```xml
<Button presto:Magnetic.Enabled="True" presto:Magnetic.Strength="0.35" presto:Magnetic.Radius="48" />
```

While enabled and attached, the element is pulled towards the pointer by `offset × Strength × proximity` on each axis. Proximity is measured from its edge over `Radius`, and the pull goes through `Springs.Snappy` into the `"X"`/`"Y"` shorthands as a followed base layer (BRIDGE-FOLLOW-038), so an animation of the same shorthand still layers over it. When the pointer leaves, the element springs back to rest. Under reduced motion nothing moves.

Unchanged: `PointerValue` keeps its over-the-control semantics, and `Tilt` keeps its own tracker.

## Decisions

- **Track over the `TopLevel` by default, with an optional `over` surface.** Reason: proximity only means something while the pointer is outside the control. `over` limits the effect to a stage or card (the Typography wordmark stage). Rejected: listening on the control (BRIDGE-FOLLOW-050's shape), which is blind until the pointer arrives.
- **Listen with a tunnel handler and `handledEventsToo`.** Reason: buttons, text boxes and a capturing `Draggable` mark pointer events handled, and the effect must keep tracking over them.
- **One shared tracker per surface.** All values over one surface share a single `PointerMoved`/`PointerExited`/`PointerReleased` subscription held in a private attached value. Reason: a split wordmark makes one value per letter, and N handlers per move would cost N event dispatches. The tracker goes away when its last value stops.
- **Measure the layout slot, not the rendered position.** Reason: the value usually drives the element's own transform, and measuring the transformed position feeds the pull back into itself (the Typography page's comment records exactly this bug).
- **Re-measure when the control moves under a still pointer**, on the control's `EffectiveViewportChanged` (which fires for ancestor layout and scrolling), using the last pointer position. Reason: a list scrolling under a resting pointer must update proximity.
- **An absent pointer reads 0 on both values.** Reason: 0 is rest for every intended use (no pull, no swell), and returning to rest is the effect's leave behavior. Rejected: holding the last value, which would leave a magnet stuck when the pointer leaves the window.
- **Touch counts only while the contact is down; mouse and pen count while hovering.** Reason: touch has no hover, and a lifted finger would otherwise freeze a pull in place. With several pointers, the one that moved last wins.
- **Edge distance by default, `fromCenter` as an opt-in.** Reason: for a wide button, edge distance makes the whole perimeter equally sensitive. Center distance suits small dots and letters (the wordmark uses it). It is a `bool`, matching `ScrollValue`'s `progress` and `PointerValue`'s `relativeToBounds`, not a new enum.
- **`Magnetic` is an attached-property class like `Tilt`, not an `Interactions` builder.** Reason: `Interactions.*` return `AnimationBuilder`s played once on a trigger, while a magnet is continuous pointer-driven state with an attach lifetime, which is `Tilt`'s shape. Attached properties already work from XAML, so no markup extension is needed.
- **Magnetic owns its spring inside the motion value graph** (`Spring(Springs.Snappy)` before `Follow.X`/`Follow.Y`) rather than through `Follow.Timing`. Reason: a `MotionValue` on a shorthand ignores `Follow.Timing` (BRIDGE-FOLLOW-042), and a spring that keeps velocity on retarget is the right feel for a pointer that never stops moving.
- **The Animatable page stays hand-built.** Reason: `samples/PrestoSample/Pages/` mirrors the anime.js documentation examples, and that page demonstrates `Animatable.Set` itself. Its follower also maps the pointer to stage coordinates and *holds* on leave, which is neither proximity nor this change's absent-reads-0 rule. A `Magnetic` demo goes on the Micro-interactions showcase page instead.
- **Re-measuring is decided by measurement.** Task 1 measures `EffectiveViewportChanged` per letter on the Typography page; if it is too costly, the tracker re-measures all its values on the surface's `LayoutUpdated` instead, and the spec records which was chosen and why.
- **Magnets keep tracking a pointer captured elsewhere.** A `Draggable` drag elsewhere still drives magnets, as designed; relaxing them during another element's drag is not built.
- **`Magnetic` uses a fixed `Springs.Snappy`.** A configurable `Magnetic.Spring` (parsed like `FollowParams.Spring`) goes to the backlog.

## Spec changes

### BRIDGE-FOLLOW (`docs/specs/avalonia-bridge/follow.md`)

Add to Motion value sources (and `sources:` gains the tracker file):

- `PrestoAvalonia.PointerProximityValue` shall hold 1 − clamp(d ÷ `radius`, 0, 1), where d is the distance in DIPs from the pointer to the control's layout bounds (0 inside them), or to their center when `fromCenter` is true, both translated into the tracking surface.
- `PrestoAvalonia.PointerOffsetValue` shall hold the pointer's position minus the center of the control's layout bounds along `axis`, in DIPs, translated into the tracking surface.
- The tracking surface shall be `over` when given, otherwise the control's `TopLevel`.
- A pointer source shall receive pointer moves on its surface through a tunnel handler that also sees handled events, so a child that handles or captures the pointer does not hide it.
- While the pointer is absent (outside the surface, after `PointerExited` from it, or after the contact of a touch pointer is released), a pointer proximity or offset value shall hold 0.
- While a touch pointer's contact is not pressed, it shall not count as present; a mouse or pen pointer shall count as present while it is over the surface.
- When several pointers are present, a pointer proximity or offset value shall follow the pointer that moved last.
- A pointer proximity or offset value shall measure the control's layout bounds and ignore its render transform, so a value driving the control's own transform does not change what it measures.
- When the control's effective viewport changes (an ancestor's layout or scroll offset moved it) while the pointer is present, the value shall be recomputed from the last pointer position.
- All pointer proximity and offset values over one surface shall share one set of event subscriptions, which is removed when the last of them stops.
- A pointer move shall allocate nothing beyond Avalonia's event args, however many values share the surface.
- When `radius` is not positive, `PointerProximityValue` shall throw `ArgumentOutOfRangeException`.
- Extend BRIDGE-FOLLOW-053 to name `PointerProximityValue` and `PointerOffsetValue`. Their lifetime is the control's: a value also stops when its surface leaves the visual tree.

### New spec BRIDGE-MAGNETIC (`docs/specs/avalonia-bridge/magnetic.md`), owns `Magnetic`

Listed under Gestures in the bridge index; depends on BRIDGE-FOLLOW, BRIDGE-TRANSFORMS, CORE-MOTIONVALUE, CORE-SPRING, BRIDGE-REDUCED.

- While `Magnetic.Enabled` is true and the control is in the visual tree, the bridge shall move the control's `"X"` and `"Y"` shorthands to `PointerOffsetValue × Strength × PointerProximityValue` (edge distance, `Radius`) on each axis, through a `Springs.Snappy` follower, as a followed base layer (BRIDGE-FOLLOW-038).
- When the pointer becomes absent, the control shall spring back to an offset of 0.
- When `Magnetic.Enabled` becomes false or the control detaches, the bridge shall stop its values and release both shorthands, restoring the control's own `RenderTransform` once nothing else holds it.
- While `PrestoDefaults.ReducedMotion` is set, the control shall stay at rest.
- When `Magnetic.Strength` or `Magnetic.Radius` changes, the new value shall apply from the next pointer move.
- (inherited) In design mode, setting `Magnetic.Enabled` shall do nothing.
- Defaults: `Strength` 0.35, `Radius` 48 DIPs, `Enabled` false.

### BRIDGE-RECIPES

No requirement changes. The Typography and Animatable mentions leave the backlog with the item.

## Tasks

1. **Shared pointer tracker and the two sources.** Add an internal per-surface tracker (private attached value, tunnel `handledEventsToo` handlers, last-moved pointer, touch contact state) plus `PointerProximityValue` and `PointerOffsetValue` on `PrestoAvalonia`, registered through `MotionValueSource` for detach/scope lifetime. Tests in `tests/Monaco.Presto.AvaloniaBridge.Tests/PointerProximityValueTests.cs` (headless window, synthetic mouse and touch input):
   - edge and center falloff
   - offsets on both axes
   - 0 outside `over`, after window exit and after touch release
   - tracking over a child that handles the event
   - a render transform on the control not changing the value
   - scrolling the control under a still pointer updating the value
   - last-moved pointer wins
   - one subscription set for many values, removed with the last one
   - detach and scope revert stopping the value
   - `radius ≤ 0` throwing
   - a zero-allocation pointer move, measured as the Transformable gesture tests do
2. **BRIDGE-FOLLOW spec update.** Add the requirements above with their `Tests:` lines and extend 053.
3. **`Magnetic` attached properties.** `Monaco.Presto.AvaloniaBridge/Magnetic.cs`, structured like `Tilt` with the tracker held while attached. Tests in `MagneticTests.cs`:
   - the pull grows as the pointer nears and is capped by `Strength`
   - it springs back on leave
   - disable and detach restore the own `RenderTransform`
   - an `"X"` animation layers over the pull and reverting it shows the pull again
   - reduced motion stays at rest
   - a `Strength` change applies on the next move
   - design mode does nothing
4. **New BRIDGE-MAGNETIC spec** with `Tests:` lines and an index entry; `SpecCoverageTests` green.
5. **Migrate the Typography wordmark.** Replace `LeanWordmark`/`RelaxWordmark` with one center `PointerProximityValue` per letter over `WordmarkStage`, mapped through `Eases.InOutQuad`, springed, and bound to `Follow.Y`/`Follow.Scale` and a color `Map` for `Foreground`. Update the `TypographyRotatorAndWordmark` snippet. Keep `TypographyPageTests` green, adjusting them if they drive the old handlers directly.
6. **Magnetic demo.** Add a magnetic button row to `MicroInteractionsPage` with a snippet, plus a sample test.
7. **Desktop visual check.** On `--page typography` and `--page micro-interactions`, in both `--motion full` and `--motion reduced`:
   - the wordmark swells and relaxes as before
   - magnetic buttons lean and spring back
   - nothing jitters while hovering a pulled button's edge, which would mean feedback

   The DevTools MCP sends no hover, so a real mouse pass is needed; record it under Manual and platform checks if it is not done.
8. **Close.** Delete the backlog line and this file.

## Open questions
