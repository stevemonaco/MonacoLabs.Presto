# Cross-platform additions

Phase 26 in [PLAN.md](PLAN.md). Status: **proposed**. This plan collects work that needs platform-specific code or
verification on the heads that are not part of the day-to-day loop (Browser, macOS, Android, iOS). Each item is kept out
of the feature phase that motivated it, so that phase can land on the Desktop head alone.

| Phase | Scope | Size |
|---|---|---|
| 26 | Reduced-motion sources: follow the OS "reduce motion" setting on each platform | medium |

## 1. Phase 26 — Reduced-motion sources

### 1.1 Background

Phase 25 ships reduced motion as a flag the app sets: `Presto.Defaults.ReducedMotion` picks each builder's
`WhenReduced` form, and `Presto.Defaults.ReduceAll` also zeroes every other animation's duration. Nothing reads the OS
setting yet. Avalonia 12.1.2 does not expose one (`IPlatformSettings` has none), so each platform has to be asked
directly.

### 1.2 Decisions

1. **Following the OS is opt-in.** The app's own flag stays authoritative unless the app asks to follow the system:
   ```csharp
   AppBuilder.Configure<App>().UsePresto(followSystemReducedMotion: true);
   // or at any time
   using var follow = PrestoAvalonia.FollowSystemReducedMotion();
   ```
   While following, the source writes `Presto.Defaults.ReducedMotion` at start and on every change it reports. Disposing
   the handle stops following and leaves the flag where it is. An app with its own "Reduce motion: System / On / Off"
   setting follows for System and disposes and sets the flag for On and Off.
2. **A source is a small interface in the bridge:**
   ```csharp
   public interface IReducedMotionSource
   {
       bool IsReduced { get; }
       event EventHandler? Changed;   // raised on the UI thread; a source that cannot observe changes never raises it
   }
   ```
   `FollowSystemReducedMotion(IReducedMotionSource? source = null)` takes an explicit source for a platform the bridge
   does not cover, or for tests.
3. **Built-in sources, picked by `OperatingSystem.Is*()` at runtime.** The bridge stays on plain `net10.0`:

   | Platform | Setting | Read | Change notification |
   |---|---|---|---|
   | Windows | `SPI_GETCLIENTAREAANIMATION` (inverted) | `SystemParametersInfo` through `LibraryImport` | `WM_SETTINGCHANGE` is not surfaced by Avalonia; re-read on window activation |
   | Browser | `prefers-reduced-motion: reduce` | `JSImport` of `globalThis.matchMedia` | the media query list's `change` event through a `JSImport`ed callback |
   | macOS | `NSWorkspace.accessibilityDisplayShouldReduceMotion` | `objc_msgSend` against `libobjc` | `NSWorkspaceAccessibilityDisplayOptionsDidChangeNotification`, or re-read on activation |

4. **Mobile sources need platform target frameworks,** so they are not in the bridge:
   - Android: `Settings.Global.ANIMATOR_DURATION_SCALE` of 0 counts as reduced, read through the activity's `Context`.
   - iOS: `UIAccessibility.IsReduceMotionEnabled`, with `UIAccessibility.Notifications.ObserveReduceMotionStatusDidChange`.

   Each is an `IReducedMotionSource` of about 30 lines. Whether they ship as small packages
   (`Monaco.Presto.AvaloniaBridge.Android`/`.iOS`) or as README snippets for the app's head is the open question in §2.
5. **An unsupported platform follows nothing.** `FollowSystemReducedMotion()` with no source for the current OS leaves
   the flag alone and returns a no-op handle, and the XML docs say which platforms are covered.

### 1.3 Implementation steps

1. `IReducedMotionSource`, `FollowSystemReducedMotion` and the `UsePresto` parameter.
2. Windows source, with the activation re-read.
3. Browser source.
4. macOS source, if a Mac is available to verify it; otherwise deferred with the mobile ones.
5. Mobile sources as packages or snippets, per §2.
6. README: the "Reduced motion" section gains "Following the system", with the platform table.

### 1.4 Tests

- A fake source drives the flag: the initial value is applied, `Changed` updates it, disposing the handle stops
  updates and leaves the last value.
- Without following, a source's `Changed` does not touch the flag.
- Windows: `SystemParametersInfo` returns a value on the test machine (skipped elsewhere); toggling the setting is a
  manual check, recorded in §3.
- Browser, macOS and mobile: manual checks on each head, recorded in §3, as the Phase 8 smoke runs were.
- Sample: the Storefront and Recipe gallery switches gain a "System" position.

## 2. Open questions

- **Mobile sources.** Separate small packages, or README snippets only?
- **Windows change notification.** Re-reading on activation misses a change made while the window is active (rare, since
  the setting lives in the Settings app). A message hook would need Avalonia's Win32 window procedure, which is not public.

## 3. Deviations as built

Filled in as the phase lands.
