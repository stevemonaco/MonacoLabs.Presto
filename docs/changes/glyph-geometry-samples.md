# Glyph geometry samples

## Why

`glyph-geometry.md` adds five building blocks: outline pieces, multi-contour morph, warps, shatter and particle text, and raster distortion. Shape-changing letters are rare outside design tools and WebGL demos, so they need samples that show what they are for, not only that they work. The gallery should show these effects in real UI (a clock, a button, a heading), in showcase pieces that go past what the Type lab and Kinetic poem do today, and in a builder page where the parameters can be tried and copied. Depends on `glyph-geometry.md`; implement after it, or after each of its phases.

## What

Five new gallery pages and upgrades to five existing ones. Every page runs under reduced motion in the form its building blocks define, says so in its description, and has a `PrestoSample.Tests` page test.

### New pages

**Glyph lab** (Showcase), the tour of blocks 1 and 2, in four panels:

- *Write-in.* A display word traced outline by outline with `Recipes.WriteIn`, then filled. Replay, a speed slider, and an "all at once / letter by letter" toggle that switches the stagger.
- *Letter morph.* One large glyph cycling A → B → … → Z → 0 → … → 9 on a timer through `MorphGlyphs`. A "Show contours" toggle draws each contour in its own color, with holes dashed and the collapse point of unpaired contours dotted, so the pairing is visible. Font picker (Inter, Archivo, a serif, a script) to show it works on any font.
- *Hole keeping.* `O → 8 → B → 0` side by side with the old single-contour morph, which garbles the counters. This is the "why multi-contour" panel.
- *Shatter.* A word that shatters into vector shards on click and re-forms as the next word (`MorphText(explode: true)` on an outline split), with a shard-count slider and a "reverse" button that reassembles the current word from mid-flight.

**Morph clock** (Showcase), the practical face of `MorphGlyphs`:

- A large `HH:MM:SS` clock whose changing digits morph into the next digit every second; the colon pulses.
- A countdown with a start/stop button that reaches `0:00` and morphs into `DONE`.
- Three buttons whose labels morph into a glyph and back: *Submit → ✓* (driven by the existing submit flow states), *Play ⇄ Pause* glyphs, *Menu → ✕*.
- A row of stat tiles whose numbers morph when "Refresh" changes them, set beside `RollDigits` and `SplitFlap` with the same values, so the three styles of number change can be compared.

**Warp studio** (Builder), a playground for block 3 that prints its own code:

- One headline with a kind picker (all seven `TextWarpKind`s), sliders for `Warp`, `WarpPhase` and `WarpRadius`, a "follow pointer" toggle that drives `WarpOrigin` from the pointer through a motion value, and an "animate" toggle that loops the phase.
- A stagger slider that delays the warp per letter, so a bend or melt can sweep across the word.
- The `CodePanel` prints the `SplitText`, `TextWarp.SetKind` and `AnimationParams` for the current settings.
- Presets: *Flag* (Wave, phase looping), *Rainbow arc* (Bend), *Magnifier* (Bulge on pointer), *Whirlpool* (Twist), *Drip* (Melt with a slow ease), *Star Wars crawl* (Perspective), *Jelly* (pointer-kicked).

**Particle type** (Showcase), block 4's formations:

- Several thousand particles form a word in a `ParticleScene`. Moving the pointer pushes them away and they spring back. Clicking cycles the word, and particles flow between the two point sets, spawning or fading the difference.
- *Edge / Fill* sampling toggle, spacing slider (particle count shown live), and a color picker for gradient by x.
- *Disintegrate* and *Assemble* buttons on a real `TextBlock` caption beneath the stage (the "snap" effect), so the recipe is shown on ordinary UI text, not only on the stage.
- A freeze button, as in Particle field, to show the formation keeps moving on the render thread while the UI thread is blocked.

**Liquid type** (Showcase), block 5's `Distort`:

- A title flowing like water: `Turbulence` with its phase looping, strength on a slider.
- *Goo:* two words drift together inside one `Distort` and fuse where they meet, then pull apart with a neck that thins and breaks, which reads like the Liquid page's metaball button but for text.
- *Glitch:* a heading that glitches for 300 ms on hover, and an "error" banner that glitches in.
- A comparison strip: the same word with a vector `Wave` warp and a raster `Turbulence`, to show when to use each.

### Upgrades to existing pages

- **Type lab:** the shatter word gains a "vector shards" toggle (on by default) beside the existing burst toggle, and a "shape morph" toggle that turns on `MorphText(shape: true)`.
- **Kinetic poem:** `BREAK` shatters into vector shards on the beat; a new phrase *melts* off the bottom of the frame with a `Melt` warp; the opening word is written in with `WriteIn`. Stays seekable, since shards and pure warps are ordinary tweens on the timeline.
- **Title sequence:** the title is written in with `WriteIn` and then filled, before the existing sequence continues.
- **Typography:** the `MOTION` proximity word gains a "bulge" mode, a `Bulge` warp whose origin follows the pointer, next to the existing lift-and-brighten mode.
- **Micro-interactions:** a submit button whose label morphs into a ✓ glyph on success and back to the label after a pause.

### Not in scope

A single-line (stroke) font for true handwriting, extruded 3D lettering, and an end-to-end "poster" page combining every block (see Open questions).

## Decisions

- **Two showcase tours plus one builder page, not one page per block.** Glyph lab covers blocks 1 and 2 together because the morph needs outlines to explain it; warps get a Builder page because their value is in the parameters; particles and distortion get showcase pages because they are stage pieces. Reason: matches how the gallery already splits Type lab (tour), Particles (playground) and Particle field (stage).
- **The morph clock is its own page.** Reason: it is the most practical use of the work and should be findable by name. Rejected: a panel inside Glyph lab, where it would read as a demo rather than a pattern.
- **Every new effect also appears on ordinary UI text.** The Disintegrate caption, the submit button, the Typography bulge and the stat tiles show the recipes on plain `TextBlock`s in normal layouts. Reason: it shows these are not stage-only effects.
- **Comparisons are part of the pages.** Hole keeping vs. the old morph, morph vs. roll vs. flap digits, vector vs. raster wave. Reason: they explain why each building block exists and when to choose it.
- **Bundled fonts only.** Archivo is already bundled for the Type lab; add one OFL serif and one OFL script font. Reason: the font picker must look the same on every head and in the page tests.

- **No Kinetic poster page in this change.** Reason: a page chaining every block is best built once the pieces have settled; it goes to the backlog.
- **The contour view is a sample-only toggle.** Reason: nothing yet shows a DevTools overlay is needed; move it there if debugging morphs proves to need it.

## Spec changes

None in `docs/specs/`: the sample is not specified there. The new pages appear in the gallery catalog (`MainViewModel`) with descriptions in the existing style, which states the reduced-motion form.

## Tasks

1. Bundle a serif and a script OFL font in the sample beside Archivo, with their licenses.
2. **Glyph lab** page, its four panels and a `GlyphLabPageTests`: the write-in ends as plain text; the letter cycle advances; shatter ends as the next word; reduced motion crossfades.
3. **Morph clock** page and `MorphClockPageTests`: a tick morphs only the changed digits; the countdown ends as `DONE`; the button label returns after success; reduced motion crossfades.
4. **Warp studio** page in Builder, with presets, printed code and `WarpStudioPageTests`: each preset sets its kind and parameters; the printed code matches the settings; reduced motion leaves the headline straight.
5. **Particle type** page and `ParticleTypePageTests`: the formation reaches its points; clicking retargets; disintegrate hides the caption and assemble restores it; reduced motion fades.
6. **Liquid type** page and `LiquidTypePageTests`: the three effects leave identity and return; reduced motion stays at identity.
7. Upgrades to Type lab, Kinetic poem, Title sequence, Typography and Micro-interactions, extending their existing page tests.
8. Catalog entries and descriptions for the five new pages.
9. Visual check of every new and upgraded page on the Desktop head (`--page <slug>`, `--motion full` and `--motion reduced`), with DevTools screenshots, checking each glyph-geometry block (outline pieces and `WriteIn`, multi-contour morph and `MorphGlyphs`, warps, shards and particle text, `Distort`) at display and body font sizes, recording findings in the specs' Open items and then dropping the `manual` open item in BRIDGE-DISTORT; and one run on the Browser head to check Particle type and Liquid type frame rates there.

## Open questions

None.
