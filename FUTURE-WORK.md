# Future Work

Code-review findings from the MonitorSwitcher review (uncommitted changes, 2026-08-11). The feature
works end-to-end; these are edge cases and hygiene items to address before release.

## Warnings

- **MonitorManager.cs:155 — invalid monitor values silently fall back to primary.**
  `target = target ?? displays.Find(d => d.IsPrimary)` returns success for any non-empty name that
  matches no enumerated display, so `DisplayMonitorConVar.SetString`'s `ConCommandException` path is
  dead. A typo (`display_monitor DISPLAY9`) or a saved value for a since-unplugged monitor silently
  relocates to primary, and the next `SaveArchiveConVars` rewrites the saved setting to primary.
  Fix: only fall back to primary for null/empty input (reset/default); return an "unknown display"
  error for non-empty non-matching names. In `ApplyPendingStartupMonitor`, catch that error and
  explicitly fall back to primary with a warning so a disconnected saved monitor still degrades.

- **DisplayMonitorConVar.cs:20 — convar is never unregistered on `OnDestroy`.**
  Under ScriptEngine hot reload the old assembly's convar instance stays rooted in `Console`'s
  private `allConVars`/`archiveConVars`; the reloaded plugin's `Register` bails at
  `FindConVar(Name) != null`, so the new assembly's `SetString`/`GetString` never take effect.
  Fix: unregister in `OnDestroy` by reflecting into `allConVars`/`archiveConVars` and removing
  `Instance` (guard for null `Console.instance`).
  Uninstall note: removing the mod leaves a `display_monitor ...;` line in config.cfg that logs
  "not a recognized ConCommand or ConVar" once per boot until the next clean quit rewrites the file.

## Suggestions

- **DisplayMonitorConVar.cs:59 — deferral gap.** A `SetString` arriving between
  `OnMainMenuControllerInitialized` and `RoR2Application.loadFinished` is queued to
  `PendingStartupMonitor` and dropped (the consumer already ran). Low reachability (users cannot
  open settings during load). Fix: also consume the pending value at the `loadFinished` transition.

- **MonitorManager.cs:240 — repeated Win32 queries.** `GetString` → `GetCurrentDisplayDeviceName` →
  `GetGameWindowHandle` constructs a fresh `Process` and re-runs `MainWindowHandle` /
  `MonitorFromWindow` / `GetMonitorInfo` on every convar read (panel enable, each carousel
  submission, `SaveArchiveConVars` on close/quit). Cache the session-stable window handle / monitor
  name, refreshed by `TryMoveToMonitor`.

- **MonitorSwitcherPlugin.cs:25 — duplicated hook subscribe/unsubscribe lists.** The five `+=`
  registrations in `Awake` are mirrored by the `-=` list in `OnDestroy`; drift between the two
  silently stacks hooks under hot reload. Drive both from one list of (event, handler) pairs.

- **MonitorManager.cs:57 — dead field.** `DisplayInfo.FullName` is written but never read.

## UI follow-up (DONE)

- ~~Display Monitor row renders behind the Fullscreen row (overlapping, un-clickable).~~ Root cause: the
  injector parented the cloned row into the Resolution *row* (`Option, Resolution`) instead of the
  list (`VerticalLayout`), so the clone overlapped. Fixed: clone the Resolution row, parent it to the
  `VerticalLayout` list, and `SetAsLastSibling()` so it flows to the bottom (below HUD Scale).
- ~~Convert it from the checkbox-style carousel to a dropdown-style selection.~~ Now clones the
  Resolution row, strips its `ResolutionControl`, keeps one `MPDropdown`, and binds a custom
  `MonitorDropdownControl : BaseSettingsControl` (dropdown + vanilla confirmation dialog).

## Settings-panel structure (learned)

The in-game Video settings panel (`SettingsSubPanel, Video`) is a header navigator whose content is a
`VerticalLayout` list (has `VerticalLayoutGroup` + `ContentSizeFitter`). Each row is a child of that
list: `Option, Resolution` (label `Text, Name` + `CarouselRect` holding the control), or
`SettingsEntryButton, ...` for carousel rows. `ResolutionControl`'s parent is the row, its parent is
the list. There is no generic dropdown row in the Video tab other than Resolution's two `MPDropdown`s.
