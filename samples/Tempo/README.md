# Tempo

A desktop focus timer with a task list (Pomodoro style), built on Presto the way an outside app would be: one `using MonacoLabs.Presto;`, the controls in XAML, and every animation defined once as a builder and assigned from XAML. It runs on [Semi.Avalonia](https://github.com/irihitech/Semi.Avalonia) (Dark) rather than Fluent, so features that lean on Fluent's templates show up. It references only the library, never the gallery.

- **Tasks** (left): add with Enter, reorder by the handle, swipe from the leading edge to complete or the trailing edge to delete (Del also deletes, with an Undo toast), double-click for a details drawer with notes, an estimate and a tag. Each row shows its tag as a colored chip, and the chips above the list (All, Writing, Code, Design, Admin, Personal) filter it; a task added under a filter takes its tag. Completed tasks drop into a collapsible Done group.
- **Focus** (right): a countdown ring whose digits morph into each second (the ring's glow pulses through the last ten), the mode (Focus, Short break, Long break), the active task, and start/pause (a pill reading Start focus, Start break or Resume that shrinks to a circle while running), skip and hold-to-abandon (let go early and it shakes). Breaks show a breathing guide. A finished session adds a dot to its task.
- **Stats**: today's minutes and sessions, the streak, a 7-day bar chart, a 12-week heatmap of focused minutes, a time-by-tag donut for Today, Week or All, and your best day, from the Focus | Stats switch in the title bar.
- **Settings** (title-bar button or Ctrl+,): a glass popover in four sections whose rows arrive in turn: Timer (− / + steppers for the durations and sessions before a long break), Behavior (auto-start breaks, the intro), Motion (System / Full / Reduced, with a dot that bounces under Full and fades under Reduced) and Data (restore sample data, clear all data). Both data buttons open a confirm dialog that grows in a circle from the button over a blurred app; the red button inside must be held for about 600 ms, and Escape cancels until you have pressed it, after which it shakes the dialog instead.

**Compact** (a window narrower than 720 DIP, down to 320): the title bar keeps the wordmark and the settings button, and a bottom bar switches between Tasks, Focus and Stats pages, with a pill gliding to the chosen item. The task list fills its page with the tag chips scrolling sideways, the ring sizes to the width, and Stats stacks its cards in one scrolling column. Task details and settings open as bottom sheets (details at half or full height), and the Undo toast sits above the bar. Widening the window from the Tasks page lands on Focus.

The window asks Windows 11 for Mica (then Acrylic) behind translucent glass cards over a slowly drifting backdrop; elsewhere the painted backdrop looks the same.

Space starts and pauses, N focuses the add box.

## Running

```powershell
dotnet run --project samples/Tempo
dotnet run --project samples/Tempo -- --skip-intro     # start on the main view
dotnet run --project samples/Tempo -- --size 360x780   # the compact layout
dotnet test --project tests/Tempo.Tests
```

`--size WxH` sets the window size in DIPs for that run only: it wins over the saved size and the maximized state, and the saved placement is left as it was. An unknown argument or a malformed size exits with code 2. In Debug, F12 opens the Avalonia developer tools and the Presto DevTools overlay is attached.

## Data

Tasks, settings, the session log and the window placement are saved to one file, `%LocalAppData%/Tempo/tempo.json`, after every change. The first run, with no file yet, is populated with sample data: sixteen tagged tasks and eight weeks of session history, the same every time and dated back from today. A corrupt file starts empty and is kept as `tempo.json.bak`. Settings has **Clear all data** and **Restore sample data**, each behind a hold-to-confirm dialog: clearing scatters the task rows, drops the Stats bars to zero if Stats is showing, then empties everything but the settings and shows the empty list's line; restoring brings the sample back with the rows arriving in turn. Deleting the file also starts over from the sample.

## Where the motion is

Every animation is a `public static readonly AnimationBuilder` in [Motion/TempoMotion.cs](Motion/TempoMotion.cs), each with its reduced-motion form, and XAML assigns them with `{x:Static m:TempoMotion.…}` (or a `{presto:…}` recipe where one says it exactly). In brief:

| Moment | Where |
|---|---|
| Rows enter, arrive, exit and reflow (also when a tag chip filters the list); reorder lift; selection glide; tag-chip indicator glide; checkbox and dot pops; strike line; the add box's plus turning into a check while it has text | `Views/TasksView.axaml` |
| Ring sweep (`Follow`), last-ten-seconds glow pulse (`TempoMotion.FinalPulse`), mode label, play-to-pause morph, start/pause pill resizing to a circle (`Animate.ContentSize`), skip nudge on hover, running breathe, breathing guide | `Views/FocusView.axaml` |
| Changed digits and the start/pause label morphing glyph by glyph (`Recipes.MorphGlyphs`), set at once under reduced motion; hold-to-abandon shaking when let go early (`TempoMotion.AbandonShake`) | `Views/FocusView.axaml.cs` |
| Mode accents, the backdrop gradient and blobs (OKLCH, following the shell's `Motion.State`), glass panels and cards, list dimming in breaks | `Styles/Tempo.axaml` |
| Backdrop blobs drifting on the compositor (`TempoMotion.BlobDrift`), still under reduced motion | `Views/ShellView.axaml`, `Views/ShellView.axaml.cs` |
| Totals flipping (`SplitFlap`), bars growing, tilting cards, the heatmap popping in outward from today, the donut sweeping in and easing between ranges (`Follow.Draw`), the best-day line fading in word by word | `Views/StatsView.axaml` |
| Page transitions (`TempoMotion.PageSwap`), switch pill, details drawer, Undo toast, settings popover opening and closing, settings button's quarter turn on hover and while open; compact: the bottom bar's pill and icon pop (`TempoMotion.CheckPop`), the details and settings bottom sheets | `Views/ShellView.axaml`, `Styles/Tempo.axaml` |
| Confirm dialog: circle reveal from the pressed Data button over a blurred backdrop (`presto:Modal`; reduced, it shows at once and only the scrim fades) and its hold-to-confirm ring | `Views/ShellView.axaml`, `Views/ConfirmDialog.axaml` |
| The dialog shaking on Escape after a press (`TempoMotion.DialogShake`); the clear's row scatter, then the bars collapsing (`TempoMotion.ClearScatter`, `TempoMotion.BarCollapse`), before the empty list's line fades in | `Views/ConfirmDialog.axaml.cs`, `Views/ShellView.axaml.cs` |
| Settings rows arriving in turn (`TempoMotion.SettingsRowArrive`), stepper buttons pressing in, the motion preview dot (`TempoMotion.MotionPreview`) | `Views/SettingsFlyout.axaml`, `Styles/Tempo.axaml` |
| Stepper values morphing (`Recipes.MorphGlyphs`); the preview replaying on a motion change | `Views/Stepper.axaml.cs`, `Views/SettingsFlyout.axaml.cs` |
| The intro and its handoff (the ring and wordmark fly into the app) | `Motion/TempoIntro.cs`, `Views/IntroView.axaml` |

A few calls have no XAML spelling and are made from code-behind: the session-complete burst, the digit and label morphs and the abandon shake (`FocusView`), the intro's timeline and particle wordmark (`TempoIntro`), the Undo toast, the backdrop drift on the compositor, the compact arrangement from the window width and moving the task list, details and settings between the two arrangements, opening settings from its command and the settings button's open class, opening the confirm dialog from its button and the clear's sequence (`ShellView`), the dialog's Escape shake (`ConfirmDialog`), the stepper value morphs (`Stepper`), the motion preview's replay (`SettingsFlyout`), and the reduced-motion setting (`App.ApplyMotionSetting`). Wall-clock time drives the timer (`Model/FocusClock.cs` over a `TimeProvider`); Presto only presents it, so the countdown keeps going while the window is minimized.

The design decisions are in [ARCHITECTURE §6](../../docs/ARCHITECTURE.md), and the friction found while building it under "Tempo findings" in [BACKLOG.md](../../docs/BACKLOG.md).
