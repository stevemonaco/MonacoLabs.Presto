# Adopting Avalonia 12.2

## Why

The repo now builds against Avalonia 12.2.0 (see the [release post](https://avaloniaui.net/blog/release-12-2) and the [12.2.0 release notes](https://github.com/AvaloniaUI/Avalonia/releases/tag/12.2.0)). The upgrade itself is done: the version moved, the Browser heads moved to `net11.0-browser`, and two behavior changes were fixed where the bridge met them (simulated glyph outlines, BRIDGE-GLYPHS; reorder auto-scroll, BRIDGE-REORDER-046). This proposal is the follow-up: what 12.2 adds that Presto or its samples should use. Composition brushes are large enough for their own proposal, [compositor-brushes.md](compositor-brushes.md).

The largest item is backdrop effects (AvaloniaUI/Avalonia#21826). Today every "glass" or "blurred background" in the repo is faked:

- `Modal` blurs a named `Backdrop` control through its `Visual.Effect` (BRIDGE-MODAL-012, 016, 027).
- Tempo's glass panels are translucent fills over drifting blobs ("Glass needs the moving backdrop", ARCHITECTURE §5).

The other items are smaller: variable-font animation, brush `RelativeTransform`, an opacity-mask rendering fix that changes how Tempo looks, frame-delivery options, and new layout panels.

## What

### 1. Backdrop blur

12.2 adds `Visual.BackdropEffect` (`IEffect?`, styled, `AffectsRender`), plus `BackdropEffectBoundsSource` (`Layout` or `Subtree`) and `BackdropEffectCache` (default, `Retained` or `Volatile`). Any visual can filter what is drawn behind it through the existing `BlurEffect` and `DropShadowEffect`, and XAML takes `BackdropEffect="blur(18)"`. Only Avalonia.Skia implements it, and headless drawing does not.

The composition-side properties are internal and not `Animated`, so the radius cannot animate on the render thread. A UI-thread radius change costs one `ImmutableBlurEffect` allocation and a re-filter of the visual's area. With the `Retained` cache it costs no re-capture, unless the visual itself moves or resizes.

- **A `BackdropBlur` effect shorthand** (BRIDGE-EFFECTS). The key is `"BackdropBlur"` with a typed `.BackdropBlur(radius)` builder method. It drives a bridge-owned `BlurEffect` on `Visual.BackdropEffect`, held at `Animation` priority and mutated in place, as `Blur` drives `Visual.Effect` (BRIDGE-EFFECTS-001, 003, 005): it starts from the user's own backdrop blur and is removed at radius 0. It is independent of the `Effect` shorthands, since it is a different property. `CanAnimateOnCompositor` rejects it.
- **`Modal.BackdropBlur`** (BRIDGE-MODAL): a new `double` property, default 0 (off). While the modal opens, it blurs whatever is behind the scrim by animating the scrim's `BackdropBlur` to that radius, and while it closes, back to 0, both with the existing tokens. Unlike `Backdrop`, it needs no reference to the control behind, and the blurred content is not re-rendered as an effect layer. `Backdrop` stays for apps whose backdrop must blur when the modal is not over it.
- **Gallery Effects page**: a backdrop-blur stage beside the element-blur stage, over moving content, so the two can be compared by eye and timed (BACKLOG [BRIDGE-EFFECTS] blur cost).
- **Tempo glass**: the `.glass` panels and the settings popover get a backdrop blur over the blobs, if task 4 shows the cost is acceptable and the Mica question below is settled. The "Glass needs the moving backdrop" decision is rewritten either way.

### 2. Variable-font animation

12.2 adds `TextElement.FontVariations` (an inherited attached property of type `FontVariationSettings`, in `Avalonia.Media`). `FontVariationSettings` parses `"wght=700, wdth=85"`, and Avalonia's own animator interpolates it, but only when both ends name the same axes. Otherwise it snaps at the midpoint.

- **A `FontVariationSettings` codec** (BRIDGE-CODECS) interpolates per axis. When the two ends name different axes, each missing axis is filled with the font's default from the variation-axis API (#22179). A string end value is parsed with `FontVariationSettings.Parse`. With split text (BRIDGE-TEXT), each piece can stagger its own weight, the variable-font wave that anime.js demos with `font-variation-settings`.
- **Glyph outlines of a variable instance** (BRIDGE-GLYPHS): `GlyphOutlines` reads the font data, which is the default instance. With `FontVariations` set, it must read the outline at the instance's coordinates, so morphs, shards and warps match the text drawn.

### 3. Brush `RelativeTransform`

12.2 adds `IBrush.RelativeTransform`, applied in the unit square of the painted bounds before `Transform` (#21881). The brush codecs carry neither `Transform` nor `RelativeTransform` to the brush they write (BRIDGE-CODECS-018, whose reason is unrecorded). So a gradient rotated with the new property snaps unrotated on its first tweened frame. Both transforms become discrete fields taken from the end value, like kind, spread and units, and BRIDGE-CODECS-018 is replaced. Interpolating them when both ends carry one is left for later.

### 4. Rendering changes to check

- **Opacity under an opacity mask is no longer squared** (#22835). In Skia's default multiply mode, a masked visual at `Opacity` 0.8 used to draw at 0.64. Tempo's blobs sit under a radial `OpacityMask` and rest at 80% (`TempoMotion.BlobGlow`), so they are now brighter at rest. Its backdrop gradient also fades an `OpacityMask`. The gallery's mask recipes (`MaskSweep`, `LineMask`, `LineWipe`, the reveals) and nested masks (#22798 changed their golden image) need the same look. These are visual checks with a retune where the look was tuned against the squared opacity.
- **Windows composition swap chain** (#22115, `Win32PlatformOptions.UseCompositionSwapChain`, opt-in): DWM uses independent flips for lower latency. The gallery Desktop head gets `--swapchain composition|default`, beside `--frame-pacing`, so frame delivery can be compared. The default stays until that comparison is done.
- **iOS at 120 Hz** (#22395, #22412): `iOSPlatformOptions.EnableHighRefreshRate` defaults to on, but an iPhone also needs `CADisableMinimumFrameDurationOnPhone` in `Info.plist`. Both iOS heads get the key. Nothing changes in `AvaloniaFrameSource`: the display link pauses only when nothing requests a frame, which the bridge does every frame while animating. This is unverified until an iOS head runs (BACKLOG [PLATFORM]).

### 5. Layout panels

`FlexPanel` is now part of Avalonia (#20263), and `WrapPanel` gains `Justify`, `Stretch` and `StretchAll` (#20549). Layout transitions (BRIDGE-LAYOUT) and Presence arrivals should work on both, since they read arranged bounds. A bridge test and a gallery demo on the Layout page confirm it. `Reorder` stays single-axis.

### Considered and not proposed

- **The property source generator** (#21797, `[GeneratedStyledProperty]` on C# 13 partial properties) would shorten the controls' hand-written registrations, with no behavior change. A conversion is churn for its own sake. Whether new code should use it is an open question below.
- **The rAF timestamp on the Browser** (#22846) reaches only the `[PrivateApi]` `IRenderTimer`. `RequestAnimationFrame` and `MediaContext` still use their own stopwatch, so `AvaloniaFrameSource` sees no change. The Browser dispatcher's move to `scheduler.postTask` (#22290, #22296) may make wake-ups more prompt, which is worth noting in the Browser check, not designing for.
- **The expression-animation fix** (#22538) touches only compositor expressions that read another animation's property. The bridge's one expression, the `CenterPoint`, reads the visual's size.
- **Geometry hit testing** (#21768) could give drop zones shape-accurate hits, but nobody has asked for that.
- **The Fluent 2 theme** (#21771) would be a gallery restyle, not a Presto change.
- **`InertiaWake`** works around Android's dispatcher running `Input` jobs only when idle. 12.2 changes nothing there that the notes show, so it stays until a device check says otherwise.

## Decisions

- **Backdrop blur is a separate shorthand, not a second mode of `Blur`.** Reason: it is a different property with its own user value, and a visual can carry both, a blurred self over a blurred backdrop. Rejected: a `Blur` argument choosing the property, which would make `Presto.Get(visual, "Blur")` ambiguous.
- **`Modal` keeps `Backdrop` and adds `BackdropBlur`.** Reason: the two blur different things. `Backdrop` blurs one named control wherever it is, while `BackdropBlur` blurs whatever lies behind the scrim. Presence ghosts and other overlay content above the scrim stay sharp under both (BACKLOG [BRIDGE-PRESENCE-EXIT]). Rejected: replacing `Backdrop`, which is shipped API (preview.1).
- **Variable-font axes missing from one end take the font's default.** Reason: Avalonia's midpoint snap makes `wght` alone animating towards `wght, wdth` jump. The font's default is what an axis left unset draws. Rejected: Avalonia's discrete fallback.
- **Brush transforms are discrete, from the end value.** Reason: it is the rule for every other field the codec cannot reconcile, and dropping a transform changes the look on the first frame. Rejected: keeping BRIDGE-CODECS-018 as it is.

## Spec changes

- **BRIDGE-EFFECTS**: requirements added for `BackdropBlur`, mirroring BRIDGE-EFFECTS-001, 003 and 005 on `Visual.BackdropEffect`, plus its independence from the `Effect` shorthands. Its reduced form is the same as `Blur`'s.
- **BRIDGE-MODAL**: requirements added for `BackdropBlur` opening and closing, and its reduced form (it still animates, like BRIDGE-MODAL-023). `BackdropBlurProperty` is a new public member.
- **BRIDGE-CODECS**: `FontVariationSettings` requirements added (per-axis interpolation, missing axes from the font default, string parsing). BRIDGE-CODECS-018 is replaced by "a brush's `Transform` and `RelativeTransform` shall be taken from the end value".
- **BRIDGE-GLYPHS**: a requirement added: with `FontVariations` set, outlines are read at the instance's coordinates.
- **BRIDGE-LAYOUT**: no requirement changes. `FlexPanel` and `WrapPanel` tests are added to existing requirements' `Tests:` lines.
- **ARCHITECTURE §5**: Tempo's glass decision; the gallery's `--swapchain` launch option.

## Tasks

1. **`BackdropBlur` shorthand**, with tests in `EffectTests`: it starts from the user's backdrop effect, is removed at 0, is independent of `Blur`, and is rejected by `CanAnimateOnCompositor`. Typed builder method, `PublicAPI.Unshipped.txt`.
2. **`Modal.BackdropBlur`**, with tests in `ModalTests` for opening, closing, reduced motion, and a value changed while open.
3. **Gallery Effects page** backdrop stage, with a gallery test.
4. **Measure** element blur against backdrop blur on the Desktop and Browser heads: an animated radius, and a static radius over moving content. Record the numbers in BRIDGE-EFFECTS, and update the guide's Platforms note and the backlog item.
5. **Tempo glass** (depends on 4 and on the Mica question): backdrop blur on the panels and popover, ARCHITECTURE §5 rewritten, Tempo tests updated. Visual check on Tempo Desktop.
6. **`FontVariationSettings` codec**, with tests in `CodecTests`: same axes, different axes, string end value, per-piece stagger on split text. Gallery Typography showcase demo, with a variable font added to the gallery assets.
7. **Variable-instance glyph outlines**, with a test in `GlyphOutlineTests`: a `wght` instance's outline covers the ink Avalonia draws.
8. **Brush transforms in the codecs**, with tests in `CodecTests` for a `RelativeTransform` or `Transform` carried from the end value and through a solid-to-gradient run.
9. **Opacity-mask visual checks** on Tempo (blobs at rest and swelling, backdrop gradient on a mode change) and the gallery mask recipes. Retune `BlobGlow` if needed, and record the outcome.
10. **`--swapchain` launch option** on the gallery Desktop head, plus a frame-delivery comparison noted in ARCHITECTURE.
11. **`CADisableMinimumFrameDurationOnPhone`** in both iOS heads' `Info.plist`.
12. **`FlexPanel` and justified `WrapPanel`** layout-transition tests in `LayoutTransitionTests`, and a Layout page demo.

## Open questions

- **Does backdrop blur work under Tempo's Mica window?** An open report on #21826 says the blur does not work in transparent or Mica windows. Tempo asks for Mica, then AcrylicBlur, over a 90% base. Settle it in task 5 by trying it. If the report is right, the choice is backdrop-blurred glass on an opaque base (dropping Mica) or Mica without frosted glass. Recommendation: decide from the screenshots.
- **Is the per-frame cost acceptable for Tempo?** The blobs drift every frame on the compositor, so every panel's retained snapshot is dirty every frame and the whole panel area is re-filtered. Task 4 answers this before task 5 starts.
- **Should variable-font frames be quantized?** `GlyphTypeface` caches one clone per normalized-coordinate step (1/16384), and the cache is unbounded, so a long loop sweeping an axis accumulates clones. Rounding written axis values (for example to whole `wght` units) would bound it, at no visible cost. Recommendation: quantize to the axis unit, and revisit if Avalonia adds the LRU eviction its source comment mentions.
- **Should new library code use the property source generator?** Recommendation: yes for new controls, as an ARCHITECTURE §4 decision, with no bulk conversion.
