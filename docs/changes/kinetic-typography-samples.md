# Kinetic typography: samples

## Why

`kinetic-typography-library.md` adds `TextSplitter.SlotOf`, the `FontWeight`/`FontStretch` codecs, `TextPath`, `MorphText`, `SplitFlap` and the `Scatter`/`Gather`/`Fall`/`Wave` recipes. This change shows them off in two showcase pages with the "wow" factor the gallery's typography demos lack. It starts once the library change has landed.

## What

- **Type Lab** (new showcase page, `--page type-lab`), five sections:
  - **Text on a path.** A ribbon of text rides a Bézier whose control points you drag. *Morph* runs `MorphTo` from wave to circle to S-curve with the text riding along, and *Flow* loops `TextPath.Offset`.
  - **Weight wave.** A `FontWeight` 100 ↔ 900 alternating loop staggered across a split headline, with a second row doing the same with `FontStretch`.
  - **Shatter.** Click the word to `MorphText` it into the next word of a list with `explode: true`. A toggle switches to the plain (no burst) form.
  - **Departures.** A `SplitFlap` board, six rows of flight, destination and time, that updates every few seconds.
  - **Elastic letters.** Drag any letter of a word. The neighbors follow along a spring `Trail` that weakens with distance, and on release everything springs home with overshoot.
- **Kinetic poem** (new showcase page, `--page kinetic-poem`). A lyric-video sequence of six phrases on a 120 BPM grid:
  - **Phrase moves.** Words slam in (`PopIn(scale: 2.5)`), a key word stretches by spreading its characters from the center, a line ripples (`Wave`), a line drops away (`Fall`), and a word explodes (`Scatter`).
  - **Beat dot.** A beat dot pulses on each beat.
  - **Controls.** One timeline with a label per phrase, a scrubber, play/pause and reverse.
  - **No re-splits.** The stage sits in a `Viewbox` at a fixed design width, so phrases never re-split and the timeline is built once.
- **A bundled display font.** The sample ships a font family with all nine weights and several widths (OFL, e.g. Inter or Roboto Flex static instances) under `samples/PrestoSample/Assets/Fonts`, used by the weight wave and as the pages' display face.

Nothing in the library changes.

## Decisions

- **Bundle a full static family.** Reason: Avalonia has no variable-font support, so the weight wave steps through static faces. With all nine weights the steps are 100 apart, and a distinctive display face also makes the pages stand out.
- **The poem stage is a fixed-width `Viewbox`.** Reason: re-pointing timeline children after a re-split is a BRIDGE-TEXT non-goal. A fixed design width means one split per phrase and one master timeline.
- **Elastic letters need no library change.** `Draggable.XValue`/`YValue`, `MotionValue.Trail(count, spring)` and `Follow.SetX`/`SetY` compose it. A weakening chain is `Trail` mapped by a gain per hop. Rejected: a `Trail` overload with a gain.
- **The samples pass seeds to the random recipes.** Reason: deterministic `*PageTests` and a replay that looks the same.

## Spec changes

None: sample pages have no specs. The page tests are cited from the library specs' `tests:` lists where they cover a requirement (`TypeLabPageTests` in BRIDGE-TEXTPATH and BRIDGE-SPLITFLAP).

## Tasks

1. The Type Lab page and its five sections, registered in the gallery and `--list-pages`, each with a `CodePanel` snippet, plus the bundled font. Cover it with `TypeLabPageTests`: each section builds; the path text re-places after a morph; shatter ends on the next word; the board settles on its text; a drag moves the neighbors and settles home.
2. The Kinetic poem page with its scrubber and reverse. Cover it with `KineticPoemPageTests`: each label is reached; seeking to the end and reversing leaves every phrase at its start values (the `KeepTime` regression in a real page); reduced motion plays fades only.
3. Visual checks on the Desktop head, `--page type-lab` and `--page kinetic-poem`, with `--motion full` and `--motion reduced`:
   - the text stays glued to the path through a morph and while dragging control points;
   - the weight wave is evenly stepped;
   - the shatter has no flash at the swap;
   - the board's fold reads as 3D;
   - the elastic letters don't jitter;
   - the poem's scrub forwards and backwards.

   The elastic drag and control-point drag need a real mouse, so they also go in the by-hand backlog line.
4. README: `TextPath`, `SplitFlap`, `MorphText` and the new recipes in the feature list.

## Open questions
