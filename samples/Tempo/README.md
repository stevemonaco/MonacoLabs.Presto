# Tempo

A desktop focus timer with a task list (Pomodoro style), built on Presto the way an outside app would be: one `using MonacoLabs.Presto;`, the controls in XAML, and every animation defined once as a builder and assigned from XAML. It runs on [Semi.Avalonia](https://github.com/irihitech/Semi.Avalonia) (Dark) rather than Fluent, so features that lean on Fluent's templates show up. It references only the library, never the gallery.

- **Tasks** (left): add with Enter, reorder by the handle, swipe from the leading edge to complete or the trailing edge to delete (Del also deletes, with an Undo toast), double-click for a details drawer with notes and an estimate. Completed tasks drop into a collapsible Done group.
- **Focus** (right): a countdown ring with split-flap digits, the mode (Focus, Short break, Long break), the active task, and start/pause, skip and hold-to-abandon. Breaks show a breathing guide. A finished session adds a dot to its task.
- **Stats**: today's minutes and sessions, a 7-day bar chart and the streak, from the Focus | Stats switch in the title bar.
- **Settings** (title-bar button or Ctrl+,): durations, sessions before a long break, auto-start breaks, the intro, and motion (System / Full / Reduced).

Space starts and pauses, N focuses the add box.

## Running

```powershell
dotnet run --project samples/Tempo
dotnet run --project samples/Tempo -- --skip-intro     # start on the main view
dotnet test --project tests/Tempo.Tests
```

An unknown argument exits with code 2. In Debug, F12 opens the Avalonia developer tools and the Presto DevTools overlay is attached.

## Data

Tasks, settings, the session log and the window placement are saved to one file, `%LocalAppData%/Tempo/tempo.json`, after every change. A missing file starts with defaults; a corrupt one also starts with defaults and is kept as `tempo.json.bak`. Delete the file to start over.

## Where the motion is

Every animation is a `public static readonly AnimationBuilder` in [Motion/TempoMotion.cs](Motion/TempoMotion.cs), each with its reduced-motion form, and XAML assigns them with `{x:Static m:TempoMotion.…}` (or a `{presto:…}` recipe where one says it exactly). In brief:

| Moment | Where |
|---|---|
| Rows enter, arrive, exit and reflow; reorder lift; selection glide; checkbox and dot pops; strike line | `Views/TasksView.axaml` |
| Ring sweep (`Follow`), split-flap digits, mode label, play-to-pause morph, running breathe, breathing guide | `Views/FocusView.axaml` |
| Mode accents and the backdrop gradient (OKLCH, following the shell's `Motion.State`), list dimming in breaks | `Styles/Tempo.axaml` |
| Bars growing, tilting cards | `Views/StatsView.axaml` |
| Focus ⇄ Stats transition, switch pill, details drawer, Undo toast, settings popover | `Views/ShellView.axaml` |
| The intro and its handoff (the ring and wordmark fly into the app) | `Motion/TempoIntro.cs`, `Views/IntroView.axaml` |

A few calls have no XAML spelling and are made from code-behind: the session-complete burst (`FocusView`), the intro's timeline and particle wordmark (`TempoIntro`), the Undo toast (`ShellView`) and the reduced-motion setting (`App.ApplyMotionSetting`). Wall-clock time drives the timer (`Model/FocusClock.cs` over a `TimeProvider`); Presto only presents it, so the countdown keeps going while the window is minimized.

The design decisions are in [ARCHITECTURE §6](../../docs/ARCHITECTURE.md), and the friction found while building it under "Tempo findings" in [BACKLOG.md](../../docs/BACKLOG.md).
