# Tempo polish and showcase pass

## Why

Feedback on the first build of Tempo (`samples/Tempo/`, landed from `tempo-sample.md`): the functionality is solid, but as a showcase it is too plain in places. The two main cards are opaque slabs, the buttons barely change shape, the split-flap clock is less striking than a morph, the settings flyout is a bare form with no motion, and a fresh install opens on an empty app with zero stats, so the motion that depends on data (lists, bars, dots) is not visible at first.

## What

### Ambient backdrop and translucent cards

- **The backdrop moves.** Behind everything, three or four large, soft blobs in the current mode's hue drift slowly on long alternating loops (20–40 s). They are compositor animations, so they cost no UI-thread work. The blobs recolor with the mode through the existing OKLCH accent states. Reduced motion holds them still.
- **The cards are glass.** The Today and Focus cards get a translucent surface (about 55–65% opacity), a 1 px top-lit border gradient, and a soft shadow, so the drifting backdrop shows through them. On Windows 11 the window also asks for `Mica`, then `AcrylicBlur`, via `TransparencyLevelHint`, falling back to the painted backdrop elsewhere.

### A morphing clock

- **The digits morph.** The countdown is a `TextBlock` whose glyphs morph into the next value each second (`Recipes.MorphGlyphs`, as on the gallery's Morph clock page), replacing the `SplitFlap`. The Stats totals keep `SplitFlap`, so both are shown.
- **The last ten seconds** pulse the ring's glow once per second (`Attention`-style), and the final second's morph lands on the mode change.

### Buttons that morph

- **Start/pause becomes a shape-shifting pill.** Idle, it is a pill reading "Start focus" (or "Start break"). Running, it shrinks to a circle with the morphing pause icon. The size change uses `Animate.ContentSize`, the label morphs with `MorphGlyphs`, and the corner radius follows. Paused, it reads "Resume".
- **The skip icon** nudges forward on hover (`Motion.Hover` with a short X keyframe).
- **The add-task "+" turns into a check** while the box has text, and rotates back when it empties. This is a path morph state on the icon, using the existing `IconToPause` pattern.
- **Hold-to-abandon** shakes and resets if it is released early (`{presto:Shake}`).
- **The settings gear** spins 90° on hover and stays rotated while settings are open.

### Sample data

- **First run is populated.** When no `tempo.json` exists, Tempo seeds about 16 tasks, built from a fixed seed so every install looks the same, with dates relative to today:
  - varied titles, notes on a few, estimates of 1–6, and some partly done
  - four already in Done
  - a **tag** on each (Writing, Code, Design, Admin, Personal), shown as a colored chip
- **The history is populated too.** 8 weeks of session history: weekdays heavier, weekends lighter, a few gaps, and a current streak of about 9 days, so every Stats view has shape.
- **Tags filter the list.** A row of tag chips above the list filters it. The rows that stay reflow into place (`Animate.ItemLayout`), the others exit and re-arrive with `Presence`, and a `Selection` indicator glides between chips.

### Richer stats

- **12-week heatmap** (GitHub-style grid of days, shaded by focused minutes), revealed in a diagonal stagger from today outward (`Presence.Arrive` with `Stagger` from the last cell).
- **Time by tag.** A donut whose segments draw on (`Draw`) and morph between the Today / Week / All ranges, chosen by a segmented switch.
- **Best day** callout with a `TextReveal` line ("Best day: Tuesday, 3h 20m").

### Settings: a staggered popover panel, plus a confirm dialog

- **The flyout becomes a designed panel.** It is a glass card with four sections (Timer, Behavior, Motion, Data). Each section and row arrives in a stagger, as on the gallery's Popups page (`Presence.Arrive` with `Stagger` inside a `Popover.Open` flyout). Leaving reverses it.
- **Steppers replace `NumericUpDown`.** Each duration is − / value / + with the value morphing (`MorphGlyphs`) and the buttons pressing in.
- **The motion choice shows its effect.** A small preview dot beside the System / Full / Reduced switch bounces under Full and fades under Reduced.
- **Data section.** "Restore sample data" and "Clear all data…". Both open a `Modal` confirm dialog that reveals in a circle from the button pressed (`Modal.Open(from)`), with the backdrop blurred. The destructive button is red and must be held for about 600 ms (the hold-to-confirm `ProgressRing` around it), and the dialog shakes on Escape after a press. Confirming clear plays a staggered `Scatter` exit over the task rows, collapses the bars to zero, then shows the empty state's `TextReveal` line. Restore rebuilds the seed and plays the normal arrival stagger.

### Fix already applied

Stats → Focus flashed the title-bar dot, hid the backdrop for a moment and jerked the Focus card. Cause: the Focus ring kept the intro's `Connected.Key="ring"`, so every rebuild of the Focus view flew the ring from its last slot (fighting the page slide) and replayed every `handoff` anchor's fade. Fix: the key is bound to `FocusViewModel.RingKey`, which `MainViewModel` clears on the first page swap. The BACKLOG finding and the ARCHITECTURE §6 decision are updated.

## Decisions

- **Settings stays a popover, not a page.** Reason: it is about ten controls, used rarely and briefly. A page would be mostly empty, would swap the Focus view out (and with it the running ring), and would make the Focus | Stats switch mix content with configuration. A popover keeps the timer in view while durations change, and it is where the Popups-style stagger reads best. The confirm dialog goes in a `Modal`, which gives the circle reveal and backdrop blur a page could not. Rejected: a third page in the switch; a right-edge `Drawer` (the task details already use one, and two drawers on one edge confuse).
- **Seeding is first-run only, plus an explicit restore.** Reason: a showcase must look alive on first launch, but a user who clears it must stay cleared. Restore makes the demo state one click away. Rejected: a `--demo` flag (nobody finds it); always seeding on an empty list (fights "Clear all").
- **Sample data is deterministic and relative to today.** A fixed seed builds the same tasks and the same shape of history, dated back from the current day. Reason: screenshots and tests stay stable, and the streak is always current.
- **Tags are added to the model.** `TaskItem` gains an optional `Tag`, and the tags are a fixed set of five with colors from the palette. Reason: filtering and the donut need a category, and a fixed set avoids a tag editor. Rejected: free-form tags (needs editing UI and color assignment).
- **The clock morphs; the totals flip.** Reason: the morph is the more striking effect for the hero number, and keeping `SplitFlap` on the totals keeps both visible. `MorphGlyphs` is code-only, so it joins the explicit code-only exceptions, triggered from `RemainingText` changes in `FocusView`, and is logged as a finding.
- **Glass needs something behind it.** Translucent cards come together with the moving backdrop. Reason: translucency over a flat fill is invisible. The backdrop is compositor-driven so it never competes with the UI thread; under reduced motion it holds still.

## Spec changes

None; sample-only, as before. On landing, the decisions above join the "Tempo" subsection of ARCHITECTURE §6, and new friction goes to the BACKLOG "Tempo findings".

## Tasks

1. **Ambient backdrop and glass cards.** Add the blob layer with loops, mode recolor and the reduced hold; translucent card styles; the Mica/Acrylic hint with fallback. *Check (Desktop):* backdrop visible through both cards in each mode; frame rate steady with DevTools FPS.
2. **Sample data and tags.** Add `Tag` to `TaskItem` and the JSON context; build `SampleData` (fixed seed, relative dates); seed on first run; add `RestoreSampleData` and `ClearAll` to `MainViewModel`. *Tests:* `SampleDataTests` (deterministic, streak ends today, 8 weeks of history, a range of estimates and tags), `MainViewModelTests` (first run seeds, an existing file does not, clear empties tasks and the log and saves, restore re-seeds).
3. **Tag chips and filtering.** Chip row with selection indicator; filtering with reflow and presence. *Tests:* filtering keeps order and selection; *Check (Desktop):* chip glide and list reflow.
4. **Morphing clock and last-ten-seconds pulse.** Replace the `SplitFlap` digits with a glyph-morphing `TextBlock`; add the pulse. *Tests:* the digits' text tracks `RemainingText` in both motion modes; *Check (Desktop):* a minute of countdown with no glyph jitter.
5. **Button morphs.** The start/pause pill ↔ circle with its label morph; skip nudge; add-box icon morph; abandon early-release shake; gear spin. *Tests:* the pill's label for idle, running, paused and break; *Check (Desktop):* each button in both motion modes.
6. **Richer stats.** Heatmap with its diagonal reveal, the tag donut with range switch and segment morphs, the best-day line. *Tests:* `StatsViewModelTests` (heatmap buckets, tag minutes per range, best day). *Check (Desktop):* Focus → Stats replays the reveals; range switching morphs the donut.
7. **Settings panel.** Sectioned glass panel, staggered arrival and exit, steppers with morphing values, motion preview dot. *Check (Desktop):* open and close the panel at both motion settings.
8. **Confirm dialog and clear/restore.** `Modal` confirm with circle reveal and blur, hold-to-confirm, the clear choreography (scatter, bars to zero, empty-state line), restore with the arrival stagger. *Tests:* `SettingsTests` (confirming clear empties and saves; cancel changes nothing; restore re-seeds). *Check (Desktop):* both flows at both motion settings.
9. **Headless smoke and findings.** Extend `MainWindowTests` with filter, clear and restore in both motion modes. Log new friction under "Tempo findings" in BACKLOG; update the README motion table and ARCHITECTURE §6. *Check:* `dotnet test --solution MonacoLabs.Presto.slnx` green.

## Open questions

- **Window material on Semi.** Mica and Acrylic need the window's `Background` to be transparent, and Semi paints `WindowDefaultBackground`. Tempo's window style overrides it to transparent and paints the backdrop itself. Confirm that the painted fallback looks the same where the hint is refused.
- **`MorphGlyphs` once a second for 25 minutes.** Confirm it does not allocate per frame beyond its morph, or accumulate state across calls (the gallery's clock morphs as often, but not for as long).
