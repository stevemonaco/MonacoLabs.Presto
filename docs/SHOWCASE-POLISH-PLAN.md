# Showcase polish round (13f)

Follow-up to [SAMPLES-PLAN.md](SAMPLES-PLAN.md) after the first hands-on pass over the showcase. Three pages feel wrong
in use, and the text work stops at the reference page. Sample-only work: the library does **not** change. Any gap the
work hits goes in SAMPLES-PLAN §8 as a new row, exactly like the earlier phases.

Applies to every item: captions are **2–3 sentences** saying what to try and what to notice. Rationale and §8
cross-references stay in code comments and SAMPLES-PLAN, not in the page. (The current captions read like design
notes, which is part of why the interruption page is hard to follow.)

## 1. Title sequence — `TitleSequencePage`

**Problem.** The feature cards rise, then visibly "blink"; the whole sequence feels busy and jerky around that beat.

1. **Diagnose before changing anything.** Using the headless harness (`tests/PrestoSample.Tests/SceneHarness.cs`),
   attach the page and sample each card's `Opacity` and effective Y (render transform) every frame from t=0 through
   the end of the timeline and through one loop restart by the `TransportBar`. Find the discontinuity. Candidates, in
   order of suspicion:
   - the cards start at `Opacity = 1` in XAML (the subtitle starts at 0, the cards do not), so anything that renders
     them before their `FromTo` applies — the first frames, a restart, a seek — shows them and then snaps them out;
   - `Eases.OutBack()` driving `Opacity` as well as `Y`: overshoot past 1 then back, and on the way out a dip;
   - the root `PlaybackEase = InOutSine` compressing the end of the sequence so the cards beat runs at a changing rate;
   - the loop restart (`TransportBar` restarts on `Completed`) jumping everything back while the drift is still visible.
   Write the confirmed cause into the commit summary and, if it is a library behaviour, a §8 row.
2. **Fix the cause**, then calm the beat:
   - cards start hidden in XAML (`Opacity="0"`), like the subtitle;
   - split the card tween: `Y` 28 → 0 with `OutQuart` (or `OutBack(1.2)` at most), `Opacity` 0 → 1 with `OutQuad` over a
     shorter duration (~450 ms) so it is fully opaque before the rise settles; stagger ~120 ms;
   - start the cards once the headline has landed rather than overlapping the subtitle scramble (`"+=200"` after the
     subtitle rather than after the title chars);
   - drop the root `PlaybackEase`. It is what made label seeks land in the wrong place (§8 row 5), so the SEEK buttons
     become honest and the caption about it goes; keep `PlaybackEase` demonstrated elsewhere only if it already is;
   - hold ~800 ms at the end (a trailing `"out"` beat) so a looped restart does not cut straight from the finished frame.
3. **Test**: a `TitleSequencePageTests` that pins the fix — card opacity is monotonic non-decreasing through the cards
   beat, and 0 at t=0 and immediately after a restart.

## 2. Board — `BoardPage`

**Problem.** Dropping a card waits for the throw to fully settle (long) before the lane under it makes room, so the drop
feels dead for a second and then everything shuffles at once.

1. **Live insertion gap.** While a card is lifted, the lane it is aimed at opens a gap at the slot it would land in: the
   cards at and after that slot are laid out one `RowPitch` down, and their `AnimateLayout` plays the push. When the aim
   moves to another slot or lane, the old gap closes and the new one opens. Drive it from `OnSnap` (which already names
   lane + slot) and `OnDrag`; make sure it keeps updating during the release throw so the gap follows a flung card.
   Implement as `Layout(int lane, int? gapAt = null)`; the bin never gets a gap.
2. **Faster settle.** Measure release→`OnSettle` time for a gentle drop and a fling. Retune `ReleaseStiffness` /
   `ReleaseDamping` / `ReleaseMass` / `MinVelocity` so a gentle drop settles in ≲ 350 ms and a fling still reads as
   thrown. Record the before/after numbers in the summary.
3. **Drop** on settle reparents the card into the gap that is already open, so nothing else moves at the drop.
4. Keep every constraint the page already works around (fixed 12-row lanes, revert on grab, axes written directly rather
   than `Reset()` — see the §8 rows cited in code).
5. **Test**: extend `BoardPageTests` — lifting a card and aiming at slot N of another lane moves that lane's cards at
   index ≥ N down one pitch before release; moving the aim away restores them; the drop leaves lanes contiguous.

## 3. Interruption lab — `InterruptionPage` (rebuild)

**Problem.** It is unclear what the page demonstrates: `replace` and `none` draw identical paths, the difference lives
only in a counter, and the travelling-card wobble is explained by a paragraph about the additive pass.

New purpose, one sentence at the top of the page: **"What happens when an animation is interrupted mid-flight."** Two
panels, each answering one question visibly.

### 3.1 Retarget — "three ways to change your mind"

- Three side-by-side panes, **replace / none / blend**, each with its own puck. Clicking any pane sends all three pucks
  to the same relative point (mirrored into each pane), so the comparison is always like-for-like.
- **Motion trails**: each pane draws a fading trail of the puck's recent positions (sampled from one `Presto.CreateTimer`
  `OnUpdate`, drawn as small ellipses or a polyline with decaying opacity). Blend's rounder, additive path becomes
  visible instead of described.
- **Burst** button: a scripted sequence of 5 targets ~160 ms apart (seeded), so the effect is reproducible without
  spamming the mouse. **Cancel newest** button: cancels each pane's most recent animation — under `none` the puck is
  visibly yanked toward the older targets, because those animations were still running underneath; under `replace` it
  just stops. This is what makes `none` legible. **Reset** as today (`Presto.Remove` + zero).
- Under each pane: the live animation count (the existing pruned-list approach, §8 row 19) in one short line.

### 3.2 Hover — "interrupt from where you are"

- A row of five tiles that lift and grow on pointer enter and settle back on leave (~500 ms, `OutQuint`).
- A toggle: **retarget** (the default — `To`-only values, so an interrupted hover continues from the current value) vs
  **restart** (`FromTo` from the resting/hovered values, what naive code does). Sweeping the pointer quickly across the
  row makes the restart mode visibly jump while the retarget mode stays continuous.
- Remove the travelling card, its wobble and the `BlendWobble` toggle. §8 row 22 stays as a historical finding; add a
  one-line note to it that the page no longer demonstrates it.

**Test**: rewrite `InterruptionPageTests` for the new surface — burst creates the expected live counts per mode
(replace ends with 1, none with 5 until they complete), cancel-newest under `none` moves the puck away from the last
target within a few frames and under `replace` leaves it where it is, and a hover leave mid-enter under retarget
starts from the current scale (no jump between consecutive frames) while restart jumps.

Update SAMPLES-PLAN §4.8 to describe the rebuilt page.

## 4. New showcase page — Typography, `TypographyPage`

*Showpiece. Everything the text module can do, composed.* Tenth showcase page, registered in `MainViewModel` after
Interruption lab (before Stress bench if that exists). Four panels on one scrolling page; each panel has a
`Replay` button, and all motion is built inside `Scope` / split effects so detach reverts everything.

1. **Masked line reveal + reflow.** A two-to-three sentence paragraph split into `Lines` (`Wrap = true`, so each line is
   clipped) and `Words`. Words rise from 100% of the line height with a per-line stagger (`Stagger` over lines, a small
   inner stagger over words), `OutExpo`. A **width slider** changes the paragraph's `Width`; the split re-runs and
   `Utils.KeepTime` hands the playhead to the rebuilt animation, so dragging the slider mid-reveal re-wraps without
   restarting — the finding §8 row 4 describes, shown working.
2. **Word rotator.** A headline "Animate _____" whose last word cycles through ~5 words (`timelines`, `text`, `shapes`,
   `layouts`, `anything`) on a loop: the outgoing word's chars drop and fade with a staggered `InBack`, the incoming
   word's chars rise with `OutBack` from `StaggerFrom.First`, and the slot's width animates between word widths so the
   headline does not jump. Built as a timeline per word change (re-split after the text swap).
3. **Pointer-reactive wordmark.** A large word ("MOTION") split into chars; moving the pointer over the panel lifts and
   scales each char by its distance from the pointer (falloff over ~160 px) and tints it along `Palette.AccentBrushRamp`;
   leaving relaxes every char back with a spring ease. Use `CreateAnimatable` per char (or retargeting `Animate` with
   the default replace) so rapid movement retargets continuously.
4. **Decode terminal.** A three-to-four line "terminal": each line types in with a blinking caret (`ScrambleText` with a
   `Cursor`, `RevealRate`), a status line cycles messages with `ScrambleText` between them (`Chars = "blocks"`), and a
   percentage counter counts up via `TextModifier` + `Utils.RoundPad`. Loops with a pause.

Keep the page to the showcase visual language (`Styles/Showcase.axaml`, `Palette`). Any text-API friction found (e.g.
chars not addressable per word, split timing, `Foreground` interpolation on chars) becomes a §8 row, not a library change.

**Test**: `TypographyPageTests` smoke test — attach, tick ~3 s of frames, change the width slider mid-reveal and assert
the line pieces were rebuilt and the animation carried on (no exception, progress not reset to 0), detach and assert the
scope reverted.

Add the page to SAMPLES-PLAN §4 (as 4.10) and to the §5 checklist line for this round.

## 5. Verification

- `dotnet build Monaco.Presto.slnx` and `dotnet test --project tests/PrestoSample.Tests` green (plus the core suite).
- Visual pass on the Desktop sample for all four pages (DevTools screenshots where a still frame can show it).
- SAMPLES-PLAN §5 gets a `13f — Polish round` checklist line pointing at this file, ticked when done.
