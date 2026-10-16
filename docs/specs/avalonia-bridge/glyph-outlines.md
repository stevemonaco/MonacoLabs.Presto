---
id: BRIDGE-GLYPHS
title: Glyph outlines
package: MonacoLabs.Presto.Avalonia
namespaces:
  - MonacoLabs.Presto
  - MonacoLabs.Presto.Text
sources:
  - MonacoLabs.Presto.Avalonia/Text/GlyphOutlines.cs
  - MonacoLabs.Presto.Avalonia/Text/GlyphContours.cs
  - MonacoLabs.Presto.Avalonia/Text/GlyphShards.cs
  - MonacoLabs.Presto.Avalonia/Text/GlyphPoints.cs
  - MonacoLabs.Presto.Avalonia/Text/GlyphSampling.cs
  - MonacoLabs.Presto.Avalonia/PrestoAvalonia.cs
types:
  - GlyphShards
  - GlyphPoints
  - GlyphSampling
tests:
  - GlyphOutlineTests
  - GlyphShardsTests
  - GlyphPointsTests
upstream: []
depends:
  - BRIDGE-TEXT
  - BRIDGE-SHAPES
  - BRIDGE-CODECS
---

# Glyph outlines

## Purpose

Reads the outlines of text as geometry, so letters can change shape: `PrestoAvalonia.GlyphOutline` gives the outline of one character of a `TextBlock` as its character piece (BRIDGE-TEXT) lays it out, and `PrestoAvalonia.TextOutline` the outline of shaped text, for a `Path` in XAML or for sampling. `GlyphShards` cuts an outline into shards that tile it, for `Recipes.Shatter` (BRIDGE-RECIPES), and `GlyphPoints` samples points along or inside it, for particle text (BRIDGE-PARTICLES). Behind them, an internal contour model flattens an outline into closed figures classified as outer contours or holes, which the outline pieces, multi-contour morphs (BRIDGE-SHAPES) and warps build on. `PrestoAvalonia` is owned by BRIDGE-HELPERS; this spec specifies its `GlyphOutline` and `TextOutline`.

## Requirements

### Outlines

- **BRIDGE-GLYPHS-001** — `GlyphOutline(block, index)` shall return the outline of the block's character at `index`, counted as `TextSplitter.CharsOf` counts the graphemes a character split makes pieces of, in the coordinates of that character's piece, with the nonzero fill rule; while the block is split, it shall read the text from before the split.
  - Tests: `GlyphOutlineTests.EachCharOutlineLandsOnItsPieceInsideItsSlot`, `GlyphOutlineTests.GlyphOutlineCountsCharactersWithoutWhitespace`
- **BRIDGE-GLYPHS-002** — When `index` is not the index of one of the block's characters, `GlyphOutline` shall throw `ArgumentOutOfRangeException`.
  - Tests: `GlyphOutlineTests.GlyphOutlineCountsCharactersWithoutWhitespace`
- **BRIDGE-GLYPHS-003** — `TextOutline(text, typeface, fontSize)` shall return the outline of `text` shaped and broken into lines as a `TextLayout` does, with its origin at the top left of the first line box and the nonzero fill rule.
  - Tests: `GlyphOutlineTests.TextOutlineOriginIsTheTopLeftOfTheFirstLineBox`, `GlyphOutlineTests.TextOutlineStacksLinesDownward`
- **BRIDGE-GLYPHS-004** — A character with no outline (whitespace, a missing glyph, or a glyph of a font carrying color or bitmap tables: `COLR`, `CBDT`, `sbix` or `SVG `) shall add nothing to an outline, so a character made only of such glyphs has an empty outline.
  - Tests: `GlyphOutlineTests.WhitespaceHasAnEmptyOutline`, `GlyphOutlineTests.EmojiHasAnEmptyOutline`
- **BRIDGE-GLYPHS-005** — When the font simulates bold or oblique, the outline shall be emboldened or skewed by -0.3 as Avalonia.Skia draws it.
  - Tests: untested

### Contour model

- **BRIDGE-GLYPHS-006** — Flattening shall turn every figure of an outline into a closed polygon, closing open figures as a fill does, and shall subdivide curves and straight segments alike so that no segment, the closing one included, exceeds a fixed maximum length in DIPs.
  - Tests: `GlyphOutlineTests.FlatteningCapsEverySegmentStraightEdgesIncluded`
- **BRIDGE-GLYPHS-007** (inherited) — The maximum flattened segment length shall be 3 DIPs.
  - Tests: `GlyphOutlineTests.FlatteningCapsEverySegmentStraightEdgesIncluded`
- **BRIDGE-GLYPHS-008** — Each flattened figure shall be classified: a figure inside no other is outer; a figure inside others takes the kind opposite to the smallest figure containing it when their windings differ, and that figure's kind when they match.
  - Tests: `GlyphOutlineTests.OHasAnOuterContourAndAHole`, `GlyphOutlineTests.BHasOneOuterContourAndTwoHoles`, `GlyphOutlineTests.ClassifiesHolesWhicheverWayTheOuterContourWinds`
- ~~**BRIDGE-GLYPHS-009**~~ — Removed: nothing converted flattened figures back to a geometry; warps build their own polygons as they map each point.

### Shards

- **BRIDGE-GLYPHS-010** — `GlyphShards.Of(outline, count, seed)` shall return shards in the outline's coordinates, each one or more closed figures with the nonzero fill rule, each of positive area, whose areas sum to the area of the outline's flattened figures (outer figures less their holes).
  - Tests: `GlyphShardsTests.ShardAreasSumToTheOutlineArea`, `GlyphShardsTests.EveryShardIsClosed`
- **BRIDGE-GLYPHS-011** — It shall triangulate each outer figure by ear clipping, with its holes bridged into it, then merge triangles sharing an edge, the region of smallest area with a neighbor first, into a neighbor drawn from `Utils.CreateSeededRandom(seed)`, until `count` regions remain or no region has a neighbor; so it returns `count` shards for an outline of one part with at least `count` triangles, one region per part at least, and the same shards for the same seed.
  - Tests: `GlyphShardsTests.TheCountIsHonoredExceptThatSeparatePartsNeverMerge`, `GlyphShardsTests.TheSameSeedGivesTheSameShardsAndAnotherSeedOthers`
- **BRIDGE-GLYPHS-012** — When `count` is less than 1, `Of` shall throw `ArgumentOutOfRangeException`, and when Avalonia.Skia does not draw the outline, `ArgumentException`.
  - Tests: `GlyphShardsTests.TheCountIsHonoredExceptThatSeparatePartsNeverMerge`

### Points

- **BRIDGE-GLYPHS-013** — `GlyphPoints.Sample(outline, spacing, Edge)` shall return, for each flattened figure in path order, `round(length / spacing)` points (at least one) spread evenly by distance along it from its first point.
  - Tests: `GlyphPointsTests.EdgePointsAreSpacedAlongEveryContour`
- **BRIDGE-GLYPHS-014** — With `Fill`, it shall return one point per cell of a `spacing` grid laid from the top left of the figures' bounds, at the cell's center moved by up to a quarter of `spacing` on each axis by `Utils.CreateSeededRandom(seed)`, kept only when inside the flattened figures by the even-odd rule; so holes stay empty and the same seed gives the same points.
  - Tests: `GlyphPointsTests.FillPointsAreInsideTheOutlineAndNoneInsideHoles`, `GlyphPointsTests.TheSameSeedGivesTheSamePointsAndAnotherSeedOthers`
- **BRIDGE-GLYPHS-015** — With both flags it shall return the edge points, then the fill points; when `spacing` is not positive it shall throw `ArgumentOutOfRangeException`, and when Avalonia.Skia does not draw the outline, `ArgumentException`.
  - Tests: `GlyphPointsTests.TheSameSeedGivesTheSamePointsAndAnotherSeedOthers`

## Invariants

- A flattened figure has at least three points, and its last point joins its first.
- Outline coordinates are DIPs at the requested font size; nothing is scaled by the render scaling.

## Edge cases

- Empty text and whitespace give an empty geometry, whose bounds are empty.
- A grapheme of several glyphs (a base letter and a combining mark) gives one outline holding every glyph's figures.
- Glyphs of a fallback font used for characters the typeface lacks are read from that fallback font.
- Outer figures that overlap (common in variable fonts) are cut each on its own, so their shards overlap where they do; a degenerate ring with no ear is clipped anyway, so cutting always ends.
- A font collection's stream holds every face; the face whose family name matches the typeface is read, else the first.

## Threading and lifetime

- UI thread, as text layout is. The SkiaSharp typeface read for each Avalonia glyph typeface is cached for as long as that glyph typeface lives.

## Reduced motion

- Not affected: outlines are geometry, not motion.

## Allocation

- Not on the tick path. Reading an outline shapes the text and allocates its geometry; flattening allocates each figure's point array once, for later per-frame use without further allocation.

## Provenance

- No upstream counterpart: DOM text has no outline, so anime.js morphs only paths converted by hand.

## Decisions

- **Outlines are read through SkiaSharp.** The spike found no Skia-free way to enumerate a glyph's figures: `Geometry` exposes no figures, and `GlyphRun.BuildGeometry` and `FormattedText.BuildGeometry` return an opaque platform geometry. The public `IPlatformTypeface.TryGetStream` gives the font data, from which an `SKTypeface` is made; `SKFont.GetGlyphPath` gives each glyph's path, placed at the glyph positions of a `TextLayout`, and `SKPath` iteration gives the figures. Reason: contours are required and the bridge already depends on Avalonia.Skia, whose image effects lease Skia too. Rejected: matching an `SKTypeface` by family name through Skia's font manager, which misses fonts embedded as Avalonia resources.
- **The public outline keeps its curves; the contour model flattens.** `GlyphOutline` and `TextOutline` return the font's quadratic and cubic segments with the font's nonzero fill rule, and flattened figures render nonzero too. Reason: a `Path` showing an outline should be as smooth as text, while morphs and warps need points; one fill rule at rest and in motion keeps overlapping contours (variable fonts keep them) filled, where even-odd drew their overlap as a hole mid-animation.
- **Outlines are for display text.** Outlines are unhinted geometry, which differs slightly from a `TextBlock`'s rendering at small sizes, so features that show outlines return to plain text when they finish. Reason: the swap is invisible at display sizes and keeps text selectable and accessible at rest. Rejected: matching hinting, which Avalonia geometry cannot do.
- **Glyphs without an outline are empty.** A color, emoji or bitmap glyph is judged by its font carrying color or bitmap tables, and a missing glyph (glyph 0) by its index, and both add nothing to an outline; features built on outlines fall back to a text piece for a character whose outline is empty. Reason: one bad character must not break an effect, and a color font's monochrome fallback outline is not what the text shows. Rejected: parsing `COLR` per glyph, which would keep a color font's plain glyphs but is not worth it for display text.
- **Holes are classified by containment, then winding.** Reason: containment alone misreads fonts whose contours overlap with the same winding (common in variable fonts), and winding alone depends on the font's convention (TrueType outer contours run clockwise, PostScript ones counter-clockwise).
- **Shards are triangles merged smallest first, as plain geometries.** Ear clipping with hole bridging handles any outline the contour model reads, and merging the smallest region first keeps shards of similar size out of the slivers a curve's fan of ears leaves; the points flattening adds along straight edges are dropped first, as they only add slivers. A shard is the boundary of its triangles (edges whose reverse it does not hold), so it draws as one figure without seams. `Of` returns `IReadOnlyList<Geometry>` and a shard pivots about its `Bounds` center. Reason: a caller places and spins a shard with what a `Geometry` already exposes. Rejected: a shard type carrying its centroid, a second public type for one point; Voronoi or random-line cuts, which need polygon clipping against curved outlines and holes.
- **Fill points are a jittered grid.** Reason: a grid gives the even density particles need to tile the letters, and a jitter of at most a quarter cell hides the grid's rows once the particles move while keeping every point in its own cell. Rejected: Poisson-disk sampling, which costs more for no visible difference at particle sizes. Edge points are spread evenly over each figure rather than exactly `spacing` apart, so a closed figure has no short last gap.
- **Straight segments are subdivided too.** Reason: a warp maps points, so a long straight stem with only its two ends would stay straight; subdividing at load keeps every later frame allocation-free apart from its output geometry.

## Non-goals

- Stroke (single-line) fonts and open contours.
- Hinted outlines matching small text pixel for pixel.

## Open items

- Bold and oblique simulation (BRIDGE-GLYPHS-005) and the missing-glyph case of BRIDGE-GLYPHS-004 have no test. The emoji test does reach the color-font path: on the Windows headless font set the emoji shapes to a real glyph of Segoe UI Emoji, which the test checks is not glyph 0.
