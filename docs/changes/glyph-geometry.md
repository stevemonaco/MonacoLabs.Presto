# Glyph geometry

## Why

The kinetic text features treat a character as a rigid box. A split piece can move, rotate, scale, fade, blur, clip and mask, `MorphText` flies matching letters to new slots, and `TextPath` places rigid glyphs along a curve, but no letter ever changes shape. `MorphTo` does change shapes, but it samples one contour into an open polyline (BRIDGE-SHAPES-017, -022), so letters with several contours or holes (`A`, `B`, `8`, `é`) come out garbled.

Changing the shape of a letter is common in design tools (After Effects text animators and warps, Rive Text Modifiers) and in WebGL experiments (three.js text explosions, Blotter.js), and missing from UI animation libraries. anime.js and GSAP can only morph text that was converted to outlines by hand, because DOM text is not a path. Avalonia gives us glyph outlines at runtime (`GlyphRun.BuildGeometry`, `FormattedText.BuildGeometry`), so Presto can offer these effects on any font with no asset preparation. The set of effects is ours: users pick and tune them, they do not write their own deformations.

Prompted by the kinetic typography wave (`MorphText`, `TextPath`, `SplitFlap`) and the discussion that followed it.

## What

Five building blocks, each usable on its own, each with a reduced-motion form.

### 1. Outline pieces

`SplitTextParams.Outlines` (default false) makes each character piece a `Path` whose `Data` is the glyph's outline, filled with the block's `Foreground`, instead of a text run. It requires `Chars`. Each piece's geometry is in piece-local coordinates, laid out on the slot the text run would have had, so layout, `SlotOf`, the stagger helpers and every existing effect keep working. Because each piece is a `Shape` with a `Geometry` `Data`, the existing `"Draw"` property and `MorphTo` apply to it directly.

`PrestoAvalonia.GlyphOutline(TextBlock block, int index)` and `PrestoAvalonia.TextOutline(string text, Typeface typeface, double fontSize)` return outlines for use outside a splitter (a `Path` in XAML, particle sampling).

```csharp
var split = PrestoAvalonia.SplitText(Title, new SplitTextParams { Words = null, Chars = new SplitPieceParams(), Outlines = true });
Presto.Animate(split.Chars, new AnimationParams { ["Draw"] = TweenValue.FromTo("0 0", "0 1"), Delay = Presto.Stagger(60) });
```

`Recipes.WriteIn(TextBlock)` packages the most common use: split into outlines with no fill, trace each letter's contours in reading order, then fade the fill in and unsplit to plain text.

### 2. Multi-contour morph

`MorphTo` learns geometries with several figures. Each side is split into closed contours and classified as outer or hole (by winding and containment). Outer contours are paired with outer contours and holes with holes inside their paired outers, by distance between normalized centroids. A contour with no partner collapses to (or grows from) its partner group's centroid. Each pair is resampled to a common point count, its start point rotated and its direction matched to minimize travel, and the morph renders closed figures with the even-odd fill rule. Single-contour morphs keep today's behavior: an open polyline, sampled as before.

Two recipes build on it:

- `Recipes.MorphGlyphs(TextBlock block, string text)` morphs each character's outline into the character at the same position of `text`, aligned on the end of the string when the text is numeric (so a counter's units column stays put), and grows or collapses extra characters from or into their slot's center. It ends as plain text. This is the digit-clock and label-to-icon morph.
- `MorphText` gains `shape: true`: matched letters fly as today, and unmatched old letters morph into the nearest unmatched new letters instead of scattering and popping.

### 3. Warps

A warp deforms the outline pieces of a split block every frame through one of a fixed set of functions of the point's position in the block:

| `TextWarpKind` | Effect |
|---|---|
| `Wave` | Vertical sine displacement along x, travelling with `WarpPhase` |
| `Bend` | Bends the baseline into an arc whose curvature is the amount |
| `Bulge` | Fisheye magnification within `WarpRadius` of `WarpOrigin` (negative pinches) |
| `Twist` | Rotation proportional to distance from `WarpOrigin` |
| `Melt` | Downward drips, longer at seeded columns, growing with the amount |
| `Perspective` | Keystone: the top edge shrinks or grows relative to the bottom |
| `Jelly` | Each point is pulled back to rest by a spring, kicked by the motion of `WarpOrigin` |

Warps follow the reveal-shorthand pattern (BRIDGE-EFFECTS): `"Warp"` is a shorthand on the split block (the strength, 0 = none) with companion shorthands `"WarpPhase"`, `"WarpOrigin"` (a point in block coordinates) and `"WarpRadius"`. The kind is an attached property, `TextWarp.Kind`. `WarpOrigin` can be driven by a motion value from pointer proximity, so a bulge follows the pointer. Outlines are flattened once at split time with long straight edges subdivided, so a warp curves straight stems smoothly.

```csharp
PrestoAvalonia.SplitText(Hero, new SplitTextParams { Words = null, Chars = new SplitPieceParams(), Outlines = true });
TextWarp.SetKind(Hero, TextWarpKind.Bulge);
Presto.Animate(Hero, new AnimationParams { ["Warp"] = 0.6, ["WarpRadius"] = 80 });
```

### 4. Shatter and particle text

- **Shards.** `GlyphShards.Of(outline, count, seed)` cuts an outline into roughly `count` triangle-based shards (ear clipping with hole bridging, then merging neighbors to the target count). `Recipes.Shatter(TextBlock, …)` replaces the block with its shards on the window's overlay layer, as `Path` controls in the block's foreground, and plays them outward with spin, the vector counterpart of `Scatter()`. The returned timeline reversed is a re-form. `MorphText(explode: true)` uses shards instead of bitmap ghosts when the block is split into outlines.
- **Points.** `GlyphPoints.Sample(outline, spacing, GlyphSampling.Edge | Fill)` returns evenly spaced points along or inside an outline.
- **Formations.** `ParticleScene.AddFormation(points, color, …)` places one particle per point that springs towards its point, can be pushed by a repel point, and can be retargeted to a new point set (word to word), spawning or fading the difference. `Recipes.Disintegrate(TextBlock)` hides the block and blows a formation of its fill points away on a drift with turbulence; `Recipes.Assemble(TextBlock)` is the reverse.

### 5. Raster distortion

A new `Distort` control (a `Decorator`) draws its child through Skia image filters, using the same draw path as `FilteredImage`:

- `Turbulence` (displacement strength) with `TurbulenceScale` and `TurbulencePhase` (animate the phase for flowing, liquid text),
- `Goo` (blur radius followed by an alpha threshold, which fuses nearby shapes like metaballs),
- `Glitch` (RGB channel split plus seeded horizontal slice offsets).

While every effect is identity, `Distort` draws its child live. Otherwise it draws a snapshot of the child taken when the effect leaves identity and retaken when the child invalidates its visual. It works on any content but is aimed at display text.

### What does not change

Existing character pieces, `MorphText` without `shape`, `Scatter()`, single-contour `MorphTo`, `ParticleScene` emitters, `ImageEffect` and the core are untouched. The core gains nothing: everything here is bridge-side because it is about Avalonia geometry and rendering.

## Decisions

- **Outline pieces are `Path` controls inside the existing splitter, not a new text control.** Reason: every split-piece feature (stagger, `SlotOf`, re-split on resize, reveal triggers, effects, `Draw`, `MorphTo`) then works on outlines for free, and warps get per-letter stagger for free. Rejected: an `OutlineText` control drawing the whole string as one geometry, which would need its own copy of all of that.
- **Outlines are for display text.** Pieces draw unhinted geometry, which differs slightly from the `TextBlock`'s rendering at small sizes, so the recipes unsplit to plain text when they finish. Reason: the swap is invisible at display sizes and keeps text selectable and accessible at rest. Rejected: matching hinting, which Avalonia geometry cannot do.
- **Glyphs without an outline stay text pieces.** Color fonts, emoji and bitmap glyphs have no usable outline, so they fall back to a normal character piece that animates rigidly; `MorphGlyphs` crossfades them. Reason: one bad character must not break the effect.
- **Contour pairing is by classification and normalized centroid distance.** Reason: it keeps holes inside letters (the counter of `0` becomes the counter of `8`'s lower bowl), matches what flubber and MorphSVG users fix by hand, and is cheap for glyph-sized contour counts. Rejected: one combined contour (flubber's approach, which drops holes); optimal assignment over shape descriptors (heavier with no visible gain for letters).
- **Single-contour morphs keep the open polyline.** Reason: existing morphs and their tests (BRIDGE-SHAPES-022) must not change, and an open stroke is right for drawn lines.
- **Warps are a closed set of kinds behind shorthands.** Reason: the reveal shorthands (`ClipCircle` plus `ClipCircleCenter`) set the pattern of a scalar shorthand with companions; a closed set lets us tune each kind, test it and give it a reduced form. Rejected: a public `Func<Point, Point>` warp delegate, which users did not ask for and which we could not keep allocation-free or reduced-motion safe.
- **Warps are applied to a flattened copy of the outline.** The outline is flattened once at split time, and each frame maps those points into a new geometry. Reason: warps must bend straight stems, which Bézier control-point warping cannot do. The per-frame geometry is an accepted allocation, as for `MorphTo` (BRIDGE-SHAPES-022).
- **`Jelly` is stateful; the other kinds are pure functions of their shorthands.** Reason: a jelly response needs velocity, but keeping every other kind pure means seek, reverse and timelines work on them exactly.
- **Shards are `Path` controls on the overlay layer, not particles.** Reason: shards need the text's fill and must be seekable and reversible to re-form, which ordinary tweens give and render-thread particles do not (`FromGeometry` is white-only and the scene cannot draw geometry). Rejected: rasterized shard particles.
- **Particle text is a formation in `ParticleScene`.** Reason: thousands of points need the render-thread scene; the Particle field showcase already shows the scene holding thousands of positioned particles. Rejected: one control per point.
- **Raster distortion uses Skia built-in filters, as image effects do.** Reason: BRIDGE-IMAGEFX already decided against SkSL until built-ins prove insufficient; turbulence, displacement, blur and color matrix cover liquid, goo and glitch. `Distort` is a decorator rather than filters on arbitrary visuals because a draw operation cannot render child visuals, so it draws a snapshot while active.
- **Reduced motion.** `WriteIn` and `MorphGlyphs` crossfade; warps hold their shorthands at identity (the text stays straight); `Shatter` and `Disintegrate`/`Assemble` fade; `Distort` stays at identity. Reason: these are large, continuous or explosive motions, which is what reduced motion opts out of.

- **Outlines may be read through SkiaSharp.** If task 1 finds no Skia-free way to enumerate a glyph's contours, the glyph module uses `SKFont.GetGlyphPath` and `SKPath` iteration, as image effects already lease Skia. Reason: contours are required and the bridge already depends on Avalonia.Skia.
- **`WriteIn` traces outline fonts; no stroke-font support in v1.** Reason: outline tracing then filling is a recognized style on its own, and a single-line font can come later through `TextOutline` with open contours.
- **No extruded warp kind in v1.** Reason: extrusion needs depth sorting and is closer to 3D than to a warp; `Perspective` covers the flat case.
- **Shards are capped per glyph if the stress bench requires it.** Settled during task 7 against the stress bench; the cap, if any, is recorded in BRIDGE-RECIPES.
- **One change, implemented in phases.** Order: tasks 1–5 (outlines and morph), 6 (warps), 7–8 (shatter and particle text), 9 (`Distort`), each phase leaving the tests green.

## Spec changes

New spec `docs/specs/avalonia-bridge/glyph-outlines.md` (BRIDGE-GLYPHS), types `GlyphShards`, `GlyphPoints`, `GlyphSampling`; depends on BRIDGE-TEXT, BRIDGE-SHAPES, BRIDGE-CODECS. Draft requirements:

- `GlyphOutline` shall return the outline of the block's character at `index` in that character's local coordinates, as laid out in the block, with its fill rule.
- `TextOutline` shall return the outline of `text` shaped with `typeface` at `fontSize`, with its origin at the top left of the first line box.
- A character with no outline (color, emoji or bitmap glyph, or whitespace) shall have an empty outline.
- `GlyphShards.Of` shall return shards whose union covers the outline, each a closed figure, about `count` of them, the same for the same seed.
- `GlyphPoints.Sample` with `Edge` shall return points spaced `spacing` apart along every contour; with `Fill`, points on a `spacing` grid inside the outline, jittered by a seed.

Changes to `docs/specs/avalonia-bridge/text.md` (BRIDGE-TEXT), new requirements:

- With `Outlines` set, each character piece shall be a `Path` whose `Data` is the character's outline and whose `Fill` is the block's `Foreground`, placed on the slot a text piece would have had.
- With `Outlines` set and `Chars` not set, the splitter shall throw `ArgumentException`.
- A character with an empty outline shall be split into a text piece as without `Outlines`.

Changes to `docs/specs/avalonia-bridge/shapes.md` (BRIDGE-SHAPES):

- -017 changed: the pair holds, per contour pair, two polylines with the same point count, spread evenly along their contours.
- -022 changed: when both geometries have one figure, each frame shall write an open polyline as today; otherwise, one closed figure per contour pair with the even-odd fill rule.
- Added: contours shall be classified as outer or hole and paired outer with outer and hole with hole, by distance between centroids normalized to each geometry's bounds.
- Added: a contour without a partner shall morph from or to the centroid of its partner group.
- Added: each pair's second polyline shall start at the point and run in the direction that minimize the summed squared travel.
- Invariant added: both ends of each contour pair have the same number of points.

New spec `docs/specs/avalonia-bridge/text-warp.md` (BRIDGE-WARP), types `TextWarp`, `TextWarpKind`; depends on BRIDGE-TEXT, BRIDGE-GLYPHS, BRIDGE-TARGETS, BRIDGE-EFFECTS, CORE-REDUCED. Draft requirements:

- A split `TextBlock` shall resolve the shorthands `Warp`, `WarpPhase`, `WarpOrigin` and `WarpRadius`, with identities 0, 0, the block's center and 100.
- While `Warp` is not 0, each frame shall map every flattened outline point of every outline piece, in block coordinates, through the function of `TextWarp.Kind`, and write the result as the piece's `Data`.
- One requirement per kind giving its function (Wave, Bend, Bulge, Twist, Melt, Perspective) and, for `Jelly`, its spring and how `WarpOrigin` motion excites it.
- Flattening shall subdivide curves and straight segments so no segment exceeds a fixed length in DIPs.
- When `Warp` returns to 0 the pieces shall get back their unwarped outlines; a revert shall do the same and forget the warp state.
- While `ReducedMotion` is set, the warp shorthands shall stay at their identities.
- A warp frame shall not allocate except for the new geometries.

Changes to `docs/specs/avalonia-bridge/recipes.md` (BRIDGE-RECIPES), new requirements for `WriteIn`, `MorphGlyphs`, `MorphText(shape: true)`, `Shatter`, `Disintegrate` and `Assemble`: what each splits, plays and ends as, its defaults (tokens), re-entry mid-animation (complete the running one first, as BRIDGE-RECIPES-079), and its reduced form.

Changes to `docs/specs/avalonia-bridge/particles.md` (BRIDGE-PARTICLES), types adding `ParticleFormation`: `AddFormation` places one particle per point; particles spring to their points; a repel point pushes particles within a radius; `Retarget(points)` pairs particles to new points by nearest neighbor, spawning or fading the difference; the formation counts against `MaxLive`.

New spec `docs/specs/avalonia-bridge/distort.md` (BRIDGE-DISTORT), type `Distort`; depends on BRIDGE-IMAGEFX, CORE-REDUCED. Draft requirements: identity draws the child live; leaving identity takes a snapshot at the render scaling; a child visual invalidation retakes it; one requirement each for turbulence, goo and glitch; the property defaults; reduced motion holds identity.

## Tasks

1. **Spike: reading contours.** Find the way to get a glyph's outline as contours (figures with their points and curves). Candidates: `GlyphRun.BuildGeometry` per glyph plus a public way to enumerate its figures; or `SKFont.GetGlyphPath` with an `SKTypeface` matching Avalonia's glyph typeface and `SKPath` iteration. Also confirm how `ContourLength`, `TryGetPointAtDistance` and dashed strokes behave on multi-figure geometry. Record the outcome as a Decision in BRIDGE-GLYPHS.
2. **Outline extraction.** `GlyphOutline`, `TextOutline`, an internal contour model (figures, flattening with subdivision, signed area, containment). Tests: outlines land inside the measured slot; `O` has two contours, one a hole; whitespace and emoji are empty; flattening caps segment length.
3. **Outline pieces.** `SplitTextParams.Outlines` in `TextSplitter`. Tests: pieces are `Path`s on the same slots as text pieces; re-split on resize keeps outlines; emoji falls back to a text piece; `Draw` traces a piece's outline.
4. **Multi-contour `MorphTo`.** Classification, pairing, collapse, start rotation, closed figures with even-odd. Tests: `O` to `8` keeps a hole throughout; `i` to `l` collapses the dot; single-contour morphs are unchanged (existing `ShapesTests` pass untouched); start rotation reduces travel.
5. **Recipes `WriteIn` and `MorphGlyphs`, and `MorphText(shape: true)`.** Tests: each ends as plain text; numeric alignment keeps units in place; extra digits grow from slot centers; re-entry completes first; reduced forms crossfade.
6. **Warps.** `TextWarp`, `TextWarpKind`, the four shorthands and seven kinds. Tests: one per kind at known points; identity leaves outlines untouched; revert restores; seek and reverse of a pure kind reproduce the same frame; `Jelly` settles; reduced motion holds identity; allocation test within budget.
7. **Shards.** Triangulation with hole bridging and merging, `GlyphShards`, `Recipes.Shatter`, shards in `MorphText(explode: true)` on outline splits. Tests: shard area sums to the outline area; same seed gives same shards; reversed timeline ends with shards on their slots; ghosts removed on completion.
8. **Particle text.** `GlyphPoints`, `ParticleScene.AddFormation`, `ParticleFormation.Retarget`, `Recipes.Disintegrate` and `Assemble`. Tests: sampling spacing; formation converges to its points; retarget spawns and fades the count difference; `MaxLive` caps; reduced forms fade.
9. **`Distort`.** Snapshot lifecycle and the three effects on the image-effect draw path. Tests: identity draws the child with no snapshot; a child change retakes the snapshot; each effect changes pixels in a rendered frame; reduced motion stays identity.
10. Write the new specs, update BRIDGE-TEXT, -SHAPES, -RECIPES and -PARTICLES with `Tests:` lines, add the new specs to the bridge index, and let `SpecCoverageTests` pass.
11. Visual check on the Desktop head of each block, full and reduced motion, at display and body font sizes (the samples come from `glyph-geometry-samples.md`).

## Open questions

None.
