# Motion kit — builder lambdas, state triggers, effects and recipes

Phases 22–25 in [PLAN.md](PLAN.md). Status: **built** (Phases 22–25; see §7 for deviations). This plan follows the post-Phase-21 evaluation, which found
three gaps that matter most in a real app:

- Nothing plays on hover, press, focus, a pseudo-class or a view-model state. The samples wire `PointerEntered` and
  `PointerExited` by hand.
- There is no effect animation: no blur, no drop shadow, no clip or mask reveal, and no 3D tilt.
- Everyday motion has to be written out as a builder every time. The sample has already written its own recipes three
  times: `Motions.cs`, `FormMotions.cs` and `FadeUpExtension.cs`.

It also settles the `Presto.Build()`-versus-lambda question. The builder stays, because it is used as a *value*
throughout the bridge. Lambdas are added as sugar at one-shot call sites.

| Phase | Scope | Size |
|---|---|---|
| 22 | Builder groundwork: `Clone`/`With`, `Action<AnimationBuilder>` overloads | small |
| 23 | State triggers: `Motion.Hover`/`Press`/`Focus`/`States`/`State`, code API, one-shot `Animate.OnTapped`/`OnChanged` | large |
| 24 | Effects: blur, drop shadow, 3D rotation, clip and mask reveals | medium |
| 25 | Recipes and reduced motion: the prebuilt catalogue, XAML markup extensions, motion tokens, reduced-motion flag | medium |

Following the OS "reduce motion" setting was split out of Phase 25 and then dropped: the app reads it and writes the flag (PLAN decision 36).

Phase 22 goes first even though triggers and effects are the headline. It is about a day of work, and both later
phases write their code API in its terms. For example, `card.WhileHover(b => b.Y(-4))` is a Phase 22 overload shape.

## 1. Phase 22 — Builder groundwork

### 1.1 Decisions

1. **`AnimationBuilder` stays the currency.** Every lifecycle surface (`Animate.OnAttached`, `Presence.*`,
   `Connected.Motion`, `Connected.Default*`) and every recipe is an `AnimationBuilder` value, because a lambda cannot be
   any of these:
   - an attached-property value;
   - an `x:Static` or markup-extension result;
   - a dictionary entry.

   `Presto.Build()` stays as the discoverable entry point, beside `CreateTimer` and `CreateTimeline`.
2. **Lambdas are `Action<AnimationBuilder>`.** This matches the existing `Keyframes(Action<KeyframesBuilder>)` and
   `Property(name, Action<TweenKeyframesBuilder>)`. An expression lambda such as `b => b.X(100)` binds to an `Action`,
   and its value is discarded.
3. **No "base recipe plus modifier" overloads.** Recipes are factories (§4.1), so `Recipes.Pop().Delay(…)` already
   modifies a fresh builder. `With` covers shared static builders. One overload per call site is enough.
4. **A shared builder can be varied without mutating it.** Today the options are mutating it for everyone, or dropping
   to `ToParams() with { … }`, which `RecipesPage` does. That cannot add or override a property. It also yields an
   `AnimationParams`, which the builder-only surfaces reject: `Presence.*`, `Animate.OnAttached`, `Connected.Motion`,
   `AnimateLayout`, `AnimateSharedElement` and `AnimateExit`. `With()` is the copy-then-modify path, and `Clone()` is the
   copy it is built on.
   - Once recipes are factories (§4.1), `With` is only needed for builders shared on purpose: an app's `static readonly`
     builders behind `x:Static`, the `Connected` defaults, and a view model's state→builder table.
5. **No `Freeze`.** Mutating a shared builder is sometimes the intent: `RecipesPage`'s slider retunes the recipe for
   later plays. After Phase 25 the library owns only two shared builders, `Connected.DefaultMotion` and
   `DefaultAnchorMotion`. Their getters return a `Clone()` of what the setter stored, so a caller cannot change the
   default by chaining on it. Guarding every setter (plus keyframes and `WhenReduced`) is not worth that. If users report
   a shared builder being mutated under them, `Freeze()` can be added later without breaking anything.

### 1.2 API

```csharp
public sealed class AnimationBuilder
{
    public AnimationBuilder Clone();                               // deep: settings, properties, keyframes
    public AnimationBuilder With(Action<AnimationBuilder> change); // Clone() then change
}

// Core
Presto.Animate(object? targets, Action<AnimationBuilder> build);
Presto.Set(object? targets, Action<AnimationBuilder> build);
Timeline.Add(object? targets, Action<AnimationBuilder> build, TimelinePosition position = default);
Timeline.Add(object? targets, Action<AnimationBuilder> build, Stagger position);
Timeline.Set(object? targets, Action<AnimationBuilder> build, TimelinePosition position = default);

// Bridge: an Action<AnimationBuilder> twin beside every AnimationBuilder overload
control.Animate(b => …);                 PrestoAvalonia.AnimateOnCompositor(targets, b => …);
visual.AnimateOnCompositor(b => …);      PrestoAvalonia.AnimateLayout(control, b => …, mode);
control.AnimateLayout(b => …, mode);     PrestoAvalonia.AnimateSharedElement(source, target, b => …) (+ keyed form);
                                         PrestoAvalonia.AnimateExit(control, b => …);
```

- `Connected.DefaultMotion` and `DefaultAnchorMotion` keep their setters. Their getters return a clone of the stored
  builder, and `Connected` reads the stored builder directly.
- Overload resolution: a lambda never converts to a target-typed `new()`, so the existing
  `[OverloadResolutionPriority(1)]` params overloads keep binding as today.

### 1.3 Tests

**Core (`AnimationBuilderTests`)**
- `Clone` is independent both ways: mutating either builder afterwards leaves the other's `ToParams()` unchanged. This
  includes keyframes and per-property `TweenParams`.
- `With` leaves the source untouched. A property added or overridden in `With` appears only in the copy, and keeps the
  source's position when it overrides.
- Each lambda overload produces the same params and child layout as the builder overload.

**Bridge (`AnimationBuilderBridgeTests`)**
- Every bridge lambda overload matches its builder twin. Of the call sites above, `AnimateOnCompositor` is the one
  that still throws on an unsupported setting.
- Chaining on `Connected.DefaultMotion`'s getter leaves the default unchanged, and assigning a new default takes
  effect.
- A compile-only test pins that `control.Animate(new() { … })` and `tl.Add(x, new() { … })` still bind to params.

### 1.4 Samples

- `RecipesPage` replaces `Pop.ToParams() with { Delay = … }` with `Pop.With(b => b.Delay(Presto.Stagger(60)))`. The page
  description mentions `With` as the way to vary a shared recipe.
- `TimelineBuilderPage` shows one `Add(target, b => …, position)` beside the `fadeUp` builder form.

## 2. Phase 23 — State triggers

### 2.1 The model

An element can have **motion states**. Each state is a condition plus an `AnimationBuilder` that says where properties
go, and how, while the condition holds. States layer:

- Later-declared states win for the properties they share, as CSS source order does.
- A property no active state names returns to its **rest** value.

This is Framer Motion's `whileHover`/`whileTap`/`whileFocus` plus variants, expressed with Avalonia's own pseudo-classes.

```xml
<Border Classes="card"
        presto:Motion.Hover="{presto:Lift}"
        presto:Motion.Press="{x:Static local:Motions.PressIn}"
        presto:Motion.Focus="{presto:Glow}">
  <presto:Motion.States>
    <presto:MotionState When=":checked" Motion="{x:Static local:Motions.Selected}" />
    <presto:MotionState When="Error" Motion="{x:Static local:Motions.Shake}" Leave="{x:Static local:Motions.Quick}" />
  </presto:Motion.States>
</Border>
```

```csharp
using var hover = card.WhileHover(b => b.Y(-4).Scale(1.02).Duration(180).Ease(Eases.OutQuad));
card.WhilePressed(Recipes.PressIn());
card.While(":checked", b => b.Background(Palette.Aqua));
Motion.SetState(card, FormState.Error);
```

### 2.2 Decisions

1. **One static class, `Motion`, named after the meaning,** like `Presence` and `Connected`. It has these attached
   properties:
   - `Hover`, `Press` and `Focus`: builder-valued sugar;
   - `States`: a `MotionStates` collection of `MotionState { When, Motion, Leave }`;
   - `State`: an `object?` whose `ToString()` tokens match named conditions.
2. **Conditions are a token list on the element itself,** ANDed: `:pseudo-class`, `.class` or a bare `name` matched
   against `Motion.State`. For example, `.selected:pointerover` or `Error`. This is not a selector engine: there are no
   descendant or template parts.
3. **Anything a selector can express goes through a style setter on `Motion.State`.** That is the escape hatch for
   "hover the card, animate the arrow":
   ```xml
   <Style Selector="Border.card:pointerover Path.arrow">
     <Setter Property="presto:Motion.State" Value="hot" />
   </Style>
   ```
   When the selector stops matching, the setter's value goes away, the `hot` state deactivates and the arrow returns.
   Styles can also set `Motion.Hover`, `Press` and `Focus` for every `.card`. Style setters give full Avalonia selector
   power without Presto parsing selectors.
4. **Sugar maps to conditions in a fixed order,** and each sugar property's source is chosen for reliability:

   | Property | Condition | Source |
   |---|---|---|
   | `Hover` | `:pointerover` | `IsPointerOverProperty` |
   | `Focus` | `:focus-visible` | `IsFocusedProperty` plus the `:focus-visible` pseudo-class |
   | `Press` | `:pressed` | synthesised pointer press (below) |

   The sugar states sit before `States` entries in the order Hover < Focus < Press, so press wins over hover for a
   shared property. `:focus-visible` is the default so that a mouse click doesn't play the focus motion. `When=":focus"`
   is available for the other behaviour.
5. **Pressed is synthesised for every element.** Only `Button` and its subclasses set `:pressed`. The tracker sets its
   own pressed flag from `PointerPressed` (left button or touch/pen contact) until `PointerReleased` or
   `PointerCaptureLost`, subscribed with `handledEventsToo` so that a Button that handles its own press still counts.
6. **Other pseudo-classes and classes come from `Classes.CollectionChanged`.** Avalonia keeps pseudo-classes in the same
   `Classes` list with a `:` prefix. Step 1 verifies this on 12.1.2 and pins it with a test, as decision 16 pins the
   compositor internals. Named conditions come from `Motion.State` changes.
7. **Timing:**
   - A property entering a state uses that state's builder: duration, delay, ease, per-property tween params, keyframes
     and loops.
   - A property leaving a state goes to the next winning state's value or to rest. It uses the leaving state's `Leave`
     builder if it has one, otherwise its own timing.
   - `Leave` may only set timing (duration, delay, ease, per-property ease or duration). Giving a property a value there
     throws `NotSupportedException`, as `LayoutMotion` does.
8. **Rest:**
   - For an `AvaloniaProperty`, rest is its base value (everything below `Animation` priority), read when the property
     returns. A binding or style that changed mid-hover is therefore honoured.
   - Once the return completes, the tracker reverts its animation-priority value. Nothing snaps, because the values are
   already equal.
   - For a transform or effect shorthand, rest is the shorthand's identity: 0, 1, or no effect.
   - Step 1 checks that `AvaloniaObject.GetBaseValue` exists on 12.1.2. If it doesn't, the tracker captures rest before
     the first activation and re-reads it on each full return to rest.
9. **Values in a state are absolute.** A relative `"+=4"` would accumulate on every re-entry, so it throws in a state. A
   from-only tween is meaningless as a target, so a `From` without a `To` throws too.
10. **Arrival and recycling snap.** When the element attaches, its active states are applied with `Presto.Set`, not
    animated. A selected row scrolled back into a virtualized list, or a view shown again, must not replay its states.
    Detach cancels and reverts every state animation. The definitions stay.
11. **Composition is replace.** A hover during a `Presence.Enter` takes over the properties both name from where they
    stand, and the entrance keeps the rest. That is the desired look (a card rising in and lifted by the pointer goes
    straight to the lift). The recipes in §4 are designed around it.
12. **One tracker per element,** in a private attached property, as `Presence` has. It resolves the winning value per
    property and plays at most one `Animation` per state transition, covering the properties that changed.
13. **One-shot triggers are separate and stay on `Animate`,** which is the raw-trigger class (decision 17):
    - `Animate.OnTapped` plays a builder on `Tapped` and restarts it on each tap.
    - `Animate.OnChanged` plus `Animate.Watch` plays a builder whenever the watched value changes, but not when it is
      first bound. This covers counters that pulse and badges that bump.

    These play and revert on completion. They are not states.

### 2.3 API

```csharp
public static class Motion
{
    public static readonly AttachedProperty<AnimationBuilder?> HoverProperty;   // Control
    public static readonly AttachedProperty<AnimationBuilder?> PressProperty;
    public static readonly AttachedProperty<AnimationBuilder?> FocusProperty;
    public static readonly AttachedProperty<MotionStates?> StatesProperty;
    public static readonly AttachedProperty<object?> StateProperty;
    // Get/Set pairs for each
}

public sealed class MotionStates : AvaloniaList<MotionState>;      // shared by style setters; definitions only
public sealed class MotionState { public string When { get; init; } public AnimationBuilder? Motion { get; init; } public AnimationBuilder? Leave { get; init; } }

public static class MotionExtensions                               // code API; states added here sit after XAML ones
{
    public static IDisposable WhileHover(this Control control, AnimationBuilder motion);
    public static IDisposable WhileHover(this Control control, Action<AnimationBuilder> motion);
    // WhilePressed, WhileFocused, While(string when, …) likewise
}
```

- A `MotionState` is immutable once added. The collection can be shared between controls through a style, and all
  per-element runtime state lives in the tracker.
- Disposing a handle removes its state. Its properties then return like any other state that stops matching.

### 2.4 Implementation steps

1. **Probe tests.**
   - Pseudo-classes raise `Classes.CollectionChanged`.
   - `GetBaseValue` is available.
   - `:focus-visible` is set on keyboard focus only.
   - `handledEventsToo` sees a Button's own press.

   Record the findings in §6.
2. **`MotionTracker`.**
   - Token set: the synthesised flags, `Classes`, and `Motion.State` tokens.
   - Ordered state list: sugar first, then `States`, then code-added states.
   - The resolved target per property.
   - A diff on every token change, which plays one animation per entering state and one per leaving state for the
     properties that changed.
   - Snap on attach; cancel and revert on detach.
3. **Attached properties and `MotionState`/`MotionStates`,** plus the XAML namespace entry and design-mode guard, as
   `Animate` has.
4. **Code API** (`While*` extensions and handles).
5. **`Animate.OnTapped`, `Animate.OnChanged` and `Animate.Watch`.**
6. **README:** a "State motion" section after Lifecycle, and a cheat-sheet row mapping Framer's `whileHover`/
   `whileTap`/variants to `Motion.*`. PLAN §7 gets decisions for 2.2 items 2, 3, 8, 10 and 11.

### 2.5 Tests (`MotionStateTests`, Avalonia.Headless)

- Hover:
  - Hover in and out eases to the target and back to rest.
  - The return uses `Leave` timing when one is given.
  - Rest follows a local value changed mid-hover.
- Layering:
  - Hover plus press on a shared `Scale` shows press's value.
  - Releasing the press returns to hover's value, not to rest.
  - Leaving both returns to rest.
- Interruption: leaving mid-enter starts the return from the current value, with no jump.
- Pressed:
  - Synthesised on a `Border`.
  - A `Button`'s own handled press still counts.
  - Capture loss ends it.
- Focus: `:focus-visible` from the keyboard plays; a pointer click does not; `When=":focus"` plays on both.
- Classes: `.selected` added and removed from code, and `:checked` on a `ToggleButton`.
- `Motion.State`:
  - Bound enum values match.
  - Multiple tokens work.
  - A style setter on a descendant selector drives a child and releases it when the selector stops matching.
- Shared `MotionStates` set by a style on three controls; each keeps its own runtime state.
- Snap on attach: a recycled container re-attached while `:selected` shows the selected value with no animation
  running.
- Detach reverts every state animation.
- Keyframed and looping states: a looping state loops while active and returns to rest when left.
- Relative or from-only values in a state throw; a `Leave` with a property value throws.
- `OnTapped` restarts on each tap. `OnChanged` does not play on the initial binding and plays on each later change.
- `AllocationTests`: an idle tracker allocates nothing per frame, and a hover in and out allocates only at the
  transition.

### 2.6 Samples

- **New Reference page "States":**
  - real Buttons, a TextBox, a CheckBox list and cards with `Motion.Hover`/`Press`/`Focus`;
  - a `:checked` toggle list;
  - a parent-hover-to-child arrow through a style setter;
  - a `Motion.State`-bound status chip;
  - a live log of state transitions;
  - the page's XAML printed beneath it.

  Tab through it to show that focus-visible and pointer focus differ.
- **`FormStatesPage`** moves from view-model builder lookups (`FormMotions` dictionaries through `Animate.OnAttached`)
  to `Motion.State="{Binding State}"` with a `MotionStates` list in XAML. `FormMotions.cs` shrinks to the builders.
  Update the description.
- **`ChartMorphPage`** point lift moves to `WhileHover`, replacing the `PointerEntered`/`PointerExited` pair at
  `ChartMorphPage.axaml.cs:46`.
- **`MicroInteractionsPage`:**
  - the like button gets `Motion.Press` plus an `Animate.OnTapped` pop;
  - the tab labels get `Motion.Hover`;
  - the toast pause-on-hover stays hand-wired, because it pauses a timer rather than animating.
- **`CountersPage`:** each tile gets an `Animate.OnChanged` bump watched on its value.
- **Keep hand-wired:**
  - `InterruptionPage` tiles, because comparing composition modes on retarget is the point of that page;
  - `CarouselPage` autoplay hold and `TypographyPage` wordmark lean, which are logic or pointer-following rather than
    states.

## 3. Phase 24 — Effects

### 3.1 Decisions

1. **Effects are shorthands owned by the bridge,** as transforms are. A new `EffectComposer` owns `Visual.Effect` the
   way `RenderTransformComposer` owns `RenderTransform`:
   - It writes one bridge-owned mutable effect instance at `Animation` priority and mutates it per frame. There is no
     per-frame allocation beyond Avalonia's property-changed args, and the `BrushCodec` reuse pattern applies.
   - `Revert` clears it, which restores the user's own `Effect`.
   - When the user has set an effect of the same kind, its values are the starting point.
2. **A visual holds one effect.** Avalonia has no effect group:
   - Animating a blur shorthand and a shadow shorthand on one visual at once throws `InvalidOperationException`, naming
     both.
   - Sequential use switches the kind.
   - The docs say to put the blur on an inner element of a shadowed card.
   - For a `Border`, prefer `BoxShadow` (Phase 19). It is cheaper and not exclusive.
3. **An effect at rest is removed.** Any effect costs an offscreen pass. When blur reaches 0, or shadow opacity reaches
   0 with the animation complete, the composer reverts instead of leaving a zero effect installed.
4. **3D rotation joins the transform shorthands.**
   - `RotateX`, `RotateY` and `Perspective` (the `Depth`) drive one bridge-owned `Rotate3DTransform` in the composer's
     group.
   - It is inserted only when first used, so 2D-only visuals keep today's group and allocations.
   - This is the basis for the tilt and flip recipes. The compositor still has no counterpart, so these throw there as
     today.
5. **Reveals are shorthands over a bridge-owned `Clip` or `OpacityMask`:**
   - `ClipInset` takes four channels (left, top, right, bottom, in DIPs or `%`) and drives a `RectangleGeometry` on
     `Visual.Clip`.
   - `ClipCircle` takes a radius fraction and a centre point, and drives an `EllipseGeometry`.
   - `MaskSweep` takes progress, angle and feather, and drives a `LinearGradientBrush` `OpacityMask` built through the
     Phase 19 gradient codec.
   - As in point 3, each reveal is removed at full visibility.
6. **Out of scope:** shader and pixel transitions (PLAN §8 bullet 2). They need a child composition visual and a shader
   path, and stay a later phase.

### 3.2 API

| Shorthand | Drives | Values |
|---|---|---|
| `Blur` | `BlurEffect.Radius` | DIPs |
| `ShadowX`, `ShadowY`, `ShadowBlur` | `DropShadowEffect.OffsetX/OffsetY/BlurRadius` | DIPs |
| `ShadowColor` | `DropShadowEffect.Color` | colour string, `Color` |
| `ShadowOpacity` | `DropShadowEffect.Opacity` | 0–1 |
| `RotateX`, `RotateY` | `Rotate3DTransform.AngleX/AngleY` | degrees (`turn`/`rad` accepted, as `Rotate`) |
| `Perspective` | `Rotate3DTransform.Depth` | DIPs |
| `ClipInset` | `Visual.Clip` rectangle | 4 channels, DIPs or `%` |
| `ClipCircle` | `Visual.Clip` ellipse | radius fraction, centre `RelativePoint` |
| `MaskSweep` | `Visual.OpacityMask` gradient | progress 0–1, `angle`, `feather` |

The typed builder methods are generated in `PropertyBuilderExtensions` with the usual four overloads: bare, full,
`FunctionValue` and keyframes. `Shadow(x, y, blur, color)` is a convenience that sets all four.

### 3.3 Implementation steps

1. **`EffectComposer`.** Accessors per shorthand in `AvaloniaObjectTargetAdapter`, canonical names beside
   `TransformShorthands`, conflict detection, and the rest-removal rule.
2. **3D rotation** in `RenderTransformComposer`, and `TransformShorthands` entries.
3. **Reveals:** a clip composer and the mask-sweep brush on the gradient codec.
4. **Builder extensions** and README rows. Add a Platforms note: effects render through Skia on every head. Measure the
   cost of Browser blur in the smoke run rather than assume it.
5. **`Presence.Exit` and `AnimateExit`:** effects on the ghost. The ghost is a `Visual`, so `Blur` and `Clip*` work there
   directly. A blur-out exit is the motivating case.

### 3.4 Tests (`EffectTests`, `TransformsTests`)

- Blur:
  - Animates `Radius`.
  - Revert restores the user's own effect.
  - It starts from a user `BlurEffect`'s radius.
  - It is removed at rest.
- Shadow: all five shorthands in one animation share one `DropShadowEffect` instance.
- Blur and shadow together throw. Blur then shadow in sequence switches kinds.
- `RotateX`/`RotateY`/`Perspective`:
  - Compose with the existing X, Y, Scale and Rotate.
  - `Rotate3DTransform` is absent until used.
  - Revert restores the user's `RenderTransform` wrapper as decision 4 does.
- `ClipInset` in `%` follows a resize.
- `ClipCircle` reveal ends with no `Clip` installed.
- `MaskSweep` progress 0 to 1 ends with no mask.
- Exit ghost blur: the ghost's effect animates and the element is untouched.
- A compositor builder naming an effect shorthand throws `NotSupportedException`.
- `AllocationTests`: `Blur` and `ShadowBlur` hold the existing per-frame budget (80 B, the property-changed args).
- Pixel test: a headless render of `Blur` 0 versus 8 differs, and back at 0 matches the original.

### 3.5 Samples

- **New Reference page "Effects":**
  - blur in and out on text and an image;
  - a drop shadow driven by five sliders;
  - `BoxShadow` beside `DropShadowEffect` on the same card;
  - a `RotateY` card flip with `Perspective`;
  - a pointer-driven `RotateX`/`RotateY` tilt;
  - `ClipInset`, `ClipCircle` and `MaskSweep` reveals over a photo;
  - a row removed with a blur-out exit.

  Each stage prints its builder.
- **`ValueTypesPage`:** add one effect card that links to the Effects page. The gradient and elevation cards stay.
- **`GalleryFlipPage`:** the detail pane opens over a blurred grid, with `Blur` on the grid host while the pane is
  open.
- **`SwipeDeckPage`:** the top card gains a small `RotateY` lean with the horizontal drag.

## 4. Phase 25 — Recipes and reduced motion

### 4.1 Decisions

1. **Recipes are factories, not static builders.** `Recipes.FadeUp(distance: 24)` returns a new `AnimationBuilder` on
   every call, so a caller can chain `.Delay(…)` without touching anyone else's copy.
2. **The catalogue is in the bridge** (namespace `Monaco.Presto.AvaloniaBridge.Recipes`), because nearly every recipe
   names shorthands. One class per family, one public type per file:

   | Class | Recipes |
   |---|---|
   | `Entrances` | `Fade`, `FadeUp/Down/Left/Right`, `ScaleIn`, `PopIn`, `BlurIn`, `SlideIn(edge)`, `FlipIn` (RotateX) |
   | `Exits` | the mirror of each entrance |
   | `Attention` | `Shake`, `Pulse`, `Wobble`, `Jelly`, `Heartbeat`, `Tada`, `Bounce` (keyframed, with an `intensity` factor) |
   | `Loops` | `Float`, `Spin`, `Breathe`, `Blink` |
   | `Interactions` | `Lift`, `Grow`, `PressIn`, `Glow` (shadow), `Dim` (for `Motion.*` states) |
   | `Loading` | `Shimmer` (`MaskSweep` loop), `Spinner`, `Dots` (stagger), `Indeterminate` |

   `Recipes` itself is a facade whose static methods forward to the families, so `Recipes.FadeUp()` is all most users
   type.
3. **Entrances use from-only values. Exits and states use to-values.** An entrance written as `.Opacity(from: 0)` lands
   on the element's own resting opacity. `Connected.DefaultAnchorMotion`'s `to: 1` is fixed in this phase for the same
   reason.
4. **Entrances and exits are compositor-safe:** transform, opacity and per-property keyframes only, apart from `BlurIn`,
   `BlurOut` and `FlipIn`, whose XML docs say so. They therefore run through `AnimateOnCompositor` as well as
   `Presence.*` and `Animate.OnAttached`.
5. **Motion tokens move into the library.** The sample's `MotionTokens` record becomes the public `MotionTokens`, with
   `Standard`, `Expressive` and `Reduced` presets. `Recipes.Tokens` supplies every recipe's default duration, ease,
   distance and stagger step. Named arguments override one call's values, and replacing `Recipes.Tokens` restyles the
   app.
6. **Every entry point takes the same optional arguments:** `duration`, `delay`, `ease`, `distance` or `intensity`, and
   `stagger`. `stagger` becomes `Delay = Presto.Stagger(step)`, which works with Presence batches (decision 23).
7. **XAML uses markup extensions,** one per family verb:
   - `{presto:Fade Direction=Up, Distance=24}`, `{presto:Scale From=0.8}`, `{presto:Pop}`, `{presto:Blur Radius=12}`,
     `{presto:Slide Edge=Left}`, `{presto:Shake Intensity=1.5}`, `{presto:Lift}`, `{presto:PressIn}`, `{presto:Glow}`,
     `{presto:Shimmer}`.
   - An extension reads `IProvideValueTarget`. Given `Presence.Exit` as the target property, it builds the exit form.
     Inside a style `Setter`, the target is not the property, so `Out="True"` states it explicitly.
8. **Reduced motion is core-level and opt-in per builder.**
   - `PrestoDefaults.ReducedMotion` is a `bool`. `AnimationBuilder.WhenReduced(Action<AnimationBuilder>)` stores an
     alternative, and `ToParams()` picks it while the flag is set.
   - Every recipe supplies one:
     - entrances and exits become short fades;
     - attention becomes a brief opacity blink or nothing;
     - loops become static;
     - interactions keep colour and shadow but drop travel and scale.
   - Animations without an alternative are unaffected, unless the app also sets `PrestoDefaults.ReduceAll`. That
     clamps durations to 0, which gives instant sets.
   - The app sets the flag, including from the OS "reduce motion" setting if it wants to follow it; Presto ships no
     platform-specific sources.
9. **Stateful recipes are small bridge types, not builders.** Pointer-follow `Tilt` needs the pointer position, so it is
   an attached property `Tilt.Enabled`/`Tilt.Strength` driven by an `Animatable` over `RotateX`/`RotateY`. The
   typewriter and count-up recipes need `SplitText` or `Text`, so they are `Recipes.Typewriter(textBlock, …)` and
   `Recipes.CountUp(textBlock, to, format)`, returning `Animation`.

### 4.2 Implementation steps

1. **Core:** `WhenReduced`, `PrestoDefaults.ReducedMotion`/`ReduceAll`, and the `ToParams` switch. `Clone` copies the
   alternative.
2. **Bridge:**
   - `MotionTokens` and presets, and `Recipes.Tokens`.
   - The family classes and the `Recipes` facade.
   - `Tilt`, and the text recipes.
3. **Markup extensions** and the exit-form detection.
4. **README:**
   - a "Recipes" section with a table of every recipe, its arguments and its reduced form;
   - a "Reduced motion" section;
   - PLAN §8 marks the recipe bullets built (masks and reveals are covered by Phase 24).

### 4.3 Tests

**Core**
- `WhenReduced` is picked only while the flag is set.
- `ReduceAll` zeroes durations but keeps end values.
- `Clone` and `With` carry the alternative, independently of the source.

**Bridge (`RecipeTests`)**
- Each recipe:
  - builds on a UI-thread animation;
  - builds on the compositor where decision 4 says it can;
  - ends at the element's resting values (entrances) or at its stated end (exits);
  - has a reduced form with no transform travel.
- Entrance from-only: a card with `Opacity` 0.6 lands on 0.6.
- The markup extension returns the exit form under `Presence.Exit` and the entrance form under `Presence.Enter`.
- `Recipes.Tokens` swap: the next call uses the new tokens, and a builder already made keeps its own values.
- `Tilt`: pointer moves drive `RotateX`/`RotateY` within `Strength`, and leaving springs back.
- `Presto.Defaults.ReducedMotion = true` makes a `Presence.Enter` recipe play its fade.

### 4.4 Samples

- **New Builder page "Recipe gallery":**
  - one tile per recipe, grouped by family, each with a Play button and its one-line call printed beneath;
  - sliders for duration and intensity;
  - a token preset picker;
  - a reduced-motion switch that shows every reduced form;
  - a second tab with the same stage in XAML through the markup extensions.
- **Retire the sample-side recipes:**
  - `Motions.cs`, `FadeUpExtension.cs` and the fades in `LifecyclePage` switch to `Recipes` and
    `{presto:Fade}`.
  - Each of those pages keeps one explicit builder, so the page still teaches the raw form beside the recipe.
  - `MotionTokensPage` becomes a demo of the library tokens and the real reduced-motion flag.
- **Showcase pages** whose fade-ups and pops are incidental (`TitleSequencePage` cards, `ConnectedNavPage` detail
  text, `ToastsPage`) switch to recipes, which shortens their printed code.

## 5. New showcase: "Storefront"

A real-world page that uses Phases 22–25 together. It goes into the Showcase section once Phase 25 lands.

- **Product grid.**
  - Cards enter with `{presto:Fade Direction=Up}` and a stagger over `Presence.Initial`.
  - Each card has `Motion.Hover="{presto:Lift}"`, `Motion.Press="{presto:PressIn}"`, a `Tilt` on the image, and a
    `:focus-visible` glow for keyboard browsing.
- **Filter chips.** A `:checked` state recolours the chip; the grid reflows through `AnimateLayout`.
- **Add to cart.**
  - `Animate.OnTapped` pops the button.
  - The cart badge bumps through `Animate.OnChanged` watched on the count.
  - A thumbnail flies to the cart with `AnimateSharedElement`.
- **Quick view.** A modal opens over a `Blur`red grid with a `ClipCircle` reveal from the clicked card, and closes with
  a blur-out `Presence.Exit`.
- **Loading.** A `Shimmer` skeleton while a Presto timer stands in for the fetch.
- **Reduced motion.** A header switch flips `Presto.Defaults.ReducedMotion` live, so the page shows every recipe's
  reduced form in context.

The page ships with a `PrestoSample.Tests` page test (hover, press, focus and reduced-motion paths through the
headless harness) and a Desktop visual check through DevTools screenshots.

## 6. Open questions (confirm before the relevant phase)

- **Naming.** `Motion` for states and `Recipes` for prebuilts. The alternatives were a Framer-style `While.Hover`, and
  `Interact` for states. `Motion` collides with nothing in Avalonia 12.1.2.
- **Initial states.** They snap on attach (decision 10). Should a `Motion.AnimateInitial` opt-in exist for a page that
  wants its selected card to animate in? *Settled by Phase 23: they snap, and no opt-in was built.*
- **Momentum.** A state spring interrupted mid-flight restarts at rest velocity. Phase 18's velocity measurement is
  `Animatable`-only. Should the tracker reuse it (`MotionState.PreserveVelocity`) or run states on an `Animatable`
  where their properties allow? *Settled by Phase 23: left out; state springs restart at rest velocity.*
- **Compositor states.** `Motion.*` could play transform- and opacity-only states through `AnimateOnCompositor`.
  Input arrives on the UI thread anyway, so the gain is smoothness under load, not responsiveness. Left out unless a
  sample shows the need. *Settled by Phase 23: left out; every state plays on the UI thread.*
- **Press on touch.** Synthesised pressed ends on capture loss. Should a touch that turns into a scroll (a
  `ScrollViewer` taking capture) cancel the press state immediately? That is the proposed default. *Settled by Phase
  23: yes; `PointerCaptureLost` ends the press at once.*
- **Mobile reduced-motion sources.** *Dropped with the rest of the OS sources; the app writes the flag.*
- **Effect conflict.** Throwing on blur plus shadow is strict. The alternative is a documented "last kind wins"; decide
  after the Effects page shows which one users trip over. *Settled by Phase 24: it throws InvalidOperationException
  naming both shorthands (decision 29); one after the other switches the kind.*

## 7. Deviations as built

Filled in as each phase lands, as in the other plans.

**Phase 22**
- `Presto.Animate` and `Presto.Set` had no `[OverloadResolutionPriority(1)]` before this phase (only the bridge params
  overloads did); they gained it so `Presto.Animate(x, new() { … })` keeps binding to params beside the lambda twin.

**Phase 23**
- Probe findings (pinned by `MotionProbeTests`):
  - Pseudo-classes live in `Classes` with their `:` prefix and raise `Classes.CollectionChanged` (`:pointerover` from a
    headless mouse move, `:checked` on a `ToggleButton`), so one subscription covers classes and pseudo-classes.
  - `GetBaseValue` exists. The untyped `AvaloniaObjectExtensions.GetBaseValue(AvaloniaObject, AvaloniaProperty)` routes
    to the typed call, so no generic helper is needed; it returns the style or local value beneath an Animation-priority
    value and follows a local value set meanwhile. With only the default beneath, it returns `AvaloniaProperty.UnsetValue`,
    so rest is then the property's default (inherited properties aside); decision 8's capture fallback is not needed.
  - `:focus-visible` is set on Tab focus and not on pointer-click focus (only `:focus` is).
  - `AddHandler(PointerPressedEvent, …, handledEventsToo: true)` sees a Button's own press, already `Handled`.
- `MotionStates` is a plain `AvaloniaList<MotionState>` with a public constructor, so XAML can write it. The attached
  property defaults to `null`, so `<presto:Motion.States>` needs an explicit `<presto:MotionStates>` wrapper around its
  `MotionState` children, in element syntax and in a style setter alike.
- Rest (decision 8) is read through the untyped `GetBaseValue`, with the property resolved from its canonical name by
  `AvaloniaObjectTargetAdapter.ResolveProperty` (made internal for this); `UnsetValue` falls back to the metadata
  default. A shorthand's identity comes from a new `RenderTransformComposer.Identity`. A returning property that is
  neither an `AvaloniaProperty` nor a transform shorthand throws `NotSupportedException`.
- `Motion.State` tokens are its `ToString()` split on whitespace and commas, and a bare name is only allowed as the
  leading token of a `When`.
- `:pressed` in any `When` reads the tracker's own flag (a `Pressed` flag on the parsed condition), never `Classes`, so
  a Button's own `:pressed` is not used. The press counts a `LeftButtonPressed` update (touch and pen report as left)
  through Tunnel and Bubble with `handledEventsToo`, and ends on a left release or capture loss. The pointer handlers
  are subscribed only while some state has a `:pressed` condition.
- Focus (decision 4) needed no `IsFocusedProperty` subscription: `:focus` and `:focus-visible` arrive through
  `Classes.CollectionChanged` like every other pseudo-class.
- A keyframed state is expanded per property, so a transition can play just the properties whose winner changed; the
  value a snap or a hand-over to that state aims at is its last keyframe's.
- Code-added states keep the tracker alive when every XAML property is cleared.
- `Animate.OnTapped` is registered on `InputElement`, where `Tapped` lives; `OnChanged` and `Watch` are on `Visual`.
- Chart morph's point lift is a shared `MotionStates` list set with `Motion.SetStates`, not `WhileHover` as §2.6
  planned, because it needs a `Leave` and the `While*` API takes none. The tooltip is not a state (it sets text,
  position and the transform origin), so it moved to one `IsPointerOver` property-changed handler per point that runs
  `ShowTip` inside the page's `Scope`.
- Counters sets `Animate.Watch` from code on each refresh rather than binding it, and Micro-interactions sets the tab
  labels' `Motion.Hover` through a `TabStripItem` style setter.

**Phase 24**
- One scalar cannot carry §3.2's extra reveal parameters, so they are companion shorthands, each with its own builder
  methods, identity and revert:
  - `ClipCircleCenter`, a `RelativePoint` defaulting to 50%, 50%. `ClipCircle` is the radius fraction alone, 1
    reaching the corner farthest from the centre.
  - `MaskSweepAngle`, in degrees with `turn`/`rad` accepted, 0 sweeping left to right.
  - `MaskSweepFeather`, the edge's width as a fraction of the sweep, default 0.2.
- Shadow identities: without a user `DropShadowEffect`, a shadow starts black, at opacity 1, blur 0 and offset 0, so
  animating `ShadowBlur` alone shows it. A user effect of the same kind supplies the starting values (decision 1). A
  shadow is removed at rest only when `ShadowOpacity` ends at 0; a blur is removed on any frame whose radius is 0.
- Conflict detection happens when the adapter resolves the accessor, so it throws from the `Animate`/`Set` call. A
  kind conflicts with the other kind's running animations (`Presto.GetAnimations` per shorthand), or with a claim made
  earlier in the same dispatcher job, which catches a blur and a shadow in one animation before either has rendered. A
  claim lapses with the job, so a `Presto.Get` does not hold one. One after the other switches the kind. `ClipInset`
  and `ClipCircle` conflict the same way, because a visual holds one clip. `MaskSweep` has only one kind, so it cannot
  conflict.
- Exit ghosts: nothing changed in `Presence`. The ghost is an `Image`, so the effect shorthands apply to it directly.
  Its clip box was not inflated for a blur's spread, because the box is the set of ancestor clips that would have cut
  the live element's blur too.
- `MaskSweep` does not go through the Phase 19 gradient codec. `MaskComposer` owns one `LinearGradientBrush` with two
  fixed stops (opaque, then transparent) and moves its start and end points each frame instead. Skia clamps stop
  offsets to 0–1, and the soft edge has to run past both ends of the visual.
- `ClipInset` takes one, two or four values in `Thickness` order, or a `Thickness`. A bare number is DIPs. Each
  channel's unit is the end value's, and a start value written in the other unit is not converted. The clip is laid
  out from `Bounds` and follows a resize while installed. A reveal starts from its identity (fully visible), never
  from the user's own `Clip` or `OpacityMask`.
- `Rotate3DTransform` sits between the scale and the 2D rotate in the group. This is upstream's `validTransforms`
  order reversed, with perspective applied at the rotation's slot because one transform carries the angles and the
  `Depth`. `Perspective`'s identity is 0, Avalonia's default, meaning no perspective.
- The compositor rejection needed no new code. `CompositorProperty` already throws `NotSupportedException` for any
  name outside its X/Y/Rotate/Scale/Opacity list.
- Samples: in Card gallery, the veil's alpha went from `E6` to `A6` so that the blur behind it shows. The Swipe deck
  lean is 0.06° of `RotateY` per pixel of drag, clamped to 8°, at `Perspective` 800.
- §3.3 step 4's Browser blur measurement is deferred: the Browser head stays out of the day-to-day loop, so the cost of
  the Effects page's blur stage and Card gallery's blur-behind there is still unmeasured, and the README Platforms note
  says so.

**Phase 25**
- Reduced motion as PLAN decisions 35–36; reduced-form params carry an internal `IsReducedMotion` mark that `ReduceAll`
  skips, and a new `Engine`/`Scope` copies both flags.
- Namespace (decision 2): a namespace `Monaco.Presto.AvaloniaBridge.Recipes` cannot sit beside the type `Recipes` in
  `Monaco.Presto.AvaloniaBridge` (CS0101). `Recipes`, `MotionTokens` and the `Edge` enum live in the root namespace, so
  one `using Monaco.Presto.AvaloniaBridge;` covers `Recipes.SlideIn(Edge.Left)`; the family classes live in
  `Monaco.Presto.AvaloniaBridge.Prebuilt` (folder `Prebuilt/`).
- `MotionTokens` names its eases `Ease` and `EmphasizedEase`, since `Standard` is a preset, and gains `Scale` and
  `BlurRadius` as the travel of the scale, pop and blur recipes. Entrances default to `Medium`, exits to `Short`; pops
  and flips use `EmphasizedEase`.
- `stagger` (decision 6) is a `bool`: it delays each element by `Tokens.StaggerStep` after `delay`
  (`Presto.Stagger(step, new() { Start = delay })`). Another step is `.Delay(Presto.Stagger(…))` chained on.
- Every entrance and exit also fades. `SlideIn`/`SlideOut` travel four times `Distance` by default. The flips take an
  `angle` (default ±90°, not a token) and set `Perspective` 800 as a to-value, which is invisible once the rotation is 0.
- `FlipOut` is UI-thread only like `FlipIn`: the compositor has no `RotateX` or `Perspective`.
- Attention, Loops, Interactions and Loading follow the entrance conventions. Attention recipes are per-property
  keyframes scaled by `intensity`, last `Long`, and end at identity; reduced, each blinks Opacity 1 → 0.6 → 1 over
  `Short`. Loops and Loading iterate over twice `Long` with `InOutSine` (the spins linearly), not the token ease, which
  would stall a loop at each turn. Reduced, loops are static (no properties, one iteration) and loading indicators
  become a slow Opacity pulse, so a wait still shows.
- Interactions are to-values only, since `MotionState` rejects relative and from-only values, and take no `stagger`
  because a state applies to one element. Reduced, `Lift` keeps its shadow, `Glow` and `Dim` are unchanged, `Grow` does
  nothing and `PressIn` dims Opacity to 0.8. `Glow` defaults to `DodgerBlue`; `Indeterminate` takes the track width.
- `MotionTracker` re-reads a state's values whenever the reduced-motion flag differs from the one they were read under,
  so toggling it takes effect at the next transition; a state already held keeps its values until it is left.
- `Shimmer` is a `MaskSweep` loop, not a gradient sweep through the Phase 19 codec (PLAN §8), and `Dots` staggers its
  batch by a sixth of an iteration itself, so it takes no `stagger`.
- `Tilt` drives real `RotateX`/`RotateY` at `Perspective` 800 through an `Animatable` whose angles spring (bounce 0.25,
  500 ms, `PreserveVelocity`), rather than PLAN §8's compositor skew-and-scale approximation, so it is UI thread only. The
  `Animatable` is created on attach and reverted on detach or when `Enabled` goes false; positions are measured against
  the untransformed layout slot. Under `ReducedMotion` pointer moves are ignored, so nothing tilts.
- `Recipes.Typewriter(textBlock, charDelay, duration, delay, ease)` splits into characters only and reverts the split
  when the last one has appeared; reduced, it plays `Fade` on the whole block. `Recipes.CountUp(textBlock, to, format,
  duration, delay, ease)` starts from the number the block shows (or 0), formats in the current culture (default `"N0"`)
  and lasts `Long`; reduced, it is a zero-duration `Text` set through `WhenReduced`. `Typewriter` reads the flag when
  called instead, because its reduced form animates the block rather than the split characters.
- Markup extensions are in the root namespace, beside `Recipes`, so the existing `presto` XAML prefix covers them. They
  derive from an abstract `RecipeExtension` holding `Duration`, `Delay`, `Ease` (an `EaseParser` string), `Stagger` (the
  step in ms, from `Delay`) and `Out` (`bool?`). An explicit `Out=False` forces the entrance form even on
  `Presence.Exit`; with no service provider the entrance form is provided. `{presto:Fade}` gained a public
  `FadeDirection` enum (`None`/`Up`/`Down`/`Left`/`Right`). XAML needs commas between arguments, so decision 7's
  examples now read `{presto:Fade Direction=Up, Distance=24}`.
- Samples: the Recipe gallery is a Builder page after Motion tokens, printing each tile's call through `Snippet.Of`, and
  it puts `Recipes.Tokens` and the flag back when it detaches, as Motion tokens and Storefront do. The sample's
  `Motions.cs`, `FadeUpExtension.cs` and `MotionTokens` were deleted; XAML and Lifecycle each keep one explicit
  `Presto.Build()`. Storefront sits before Stress bench, bumps the cart badge with `{presto:Pop}` through
  `Animate.OnChanged`, and uses `{presto:Scale}` for the grid's filter enter and exit.