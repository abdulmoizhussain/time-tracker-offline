# Project TODOs

This file preserves the original development backlog that was previously kept in `TimeTracker.App\README.md`.

Original author statements and requirement history are maintained separately in [AUTHOR_REQUIREMENTS.md](AUTHOR_REQUIREMENTS.md). Keep implementation status here; preserve the author's wording there.

## Open TODOs

### Follow-ups and verification

- [ ] Select an eligible signing provider and implement/validate release signing after author approval of the plan (AR-040).
- [ ] Verify recovery popup foreground behavior during an actual Windows lock/unlock and startup session (AR-037); offline previews cannot prove Windows foreground focus behavior.
- [ ] Reproduce and resolve typed search failing with longer text in all three fields (AR-026). Installed-build/database-snapshot tests show visible results; exact failing query/expected match and live popup inspection are still needed. The AR-025 cutoff change does not resolve this report.
- [ ] **Author review requested:** seeded timers may start on the previous day when their initial duration exceeds time since midnight; this is explicitly allowed temporarily by AR-022.
- [ ] Verify actual Windows lock/unlock, sign-out/shutdown, hidden startup popup focus, and crash recovery in an interactive session (AR-022).
- [ ] Fix JSON imports discarding Start/End timestamps and assigning entries to the import date; preserve existing large-duration support while defining same-day import handling (AR-020 audit).
- [ ] Define same-day handling for timers running across midnight (including restored timers and row/tray Stop); currently their full duration is assigned to the start date (AR-020 audit).
- [ ] Review historical cross-day/undated entries and reporting/export attribution; do not migrate or modify historical records without an agreed policy (AR-020 audit).
- [ ] Verify sign-in startup, lock/unlock recording while initially hidden, and startup toggles in both native menus in an interactive Windows session.
- [ ] Verify the refreshed UI interactively on mixed-DPI monitors and in Windows high-contrast mode; inspect native tray placement and popup/window focus behavior.

### UI / Menu

1. Move the export/import options to a separate menu bar item under **Import/Export → Import, Export, Import/Export History**. Provide a screen to show the import/export history with details like:
   - Date and time of import/export
   - Number of entries imported/exported
   - First and last entry date for export

2. Add a menu bar with options like **File, Edit, View, Help**:
   - **File**: Import, Export, Exit
   - **Edit**: Clear All Data
   - **View**: Show/Hide Lock/Unlock History
   - **Help**: About

3. Increase the font size everywhere to be more easily visible (text boxes, buttons, duration boxes, time boxes, list view entries, group separators). Consider multi-monitor setups with different resolutions and scaling (e.g. 4K @ 250% and 1080p @ 100%).

4. Move the `Running: 00:45:15` and `No running timer.` indications to the top place, just below the application name box `Offline Time Tracker`. Modify the running text to include the task description, e.g. `Running: 00:45:15 {Description Here}`.

5. Replace the `?` labels on the Start/Stop buttons with play/stop icons or emojis.

### Timer & Entries

7. Add a periodic pop-up reminder to stop the timer if it has been running for a long time, e.g. every 15 minutes. This helps avoid forgotten timers and keeps time-tracking data accurate.

9. [ ] Allow modifying the ongoing timer description from the tray icon mini window. Previous editor reverted; replacement design pending (AR-010).

11. Add a multi-select remove option for entries.

12. While modifying an ended timer, the start and stop boxes should adjust when focus leaves the duration box. Conversely, the duration box should adjust when focus leaves the start/stop time boxes.

13. Format total duration for each day as `3h 30 min` in the group row of each day in the list view.

14. Display `Running: 00:00:35 – {{Description here}}` for the active timer.

15. Provide a default timer text (currently `Untitled task`) that is modifiable from within the app.

### Duration Box Behaviour

16. In both duration boxes, support the following input formats:
    - `33:33` → 33 minutes 33 seconds
    - `33:` → 33 minutes 0 seconds
    - `:33` → 0 minutes 33 seconds
    - `34` → 34 minutes 0 seconds
    - `5.3` / `1.0` → 5.3 / 1.0 hours (decimal hours only when a decimal is present)

17. When moving into any duration box via the Tab key (not mouse click), select all text so it is ready to be removed/cut/copied.

### Data & Pagination

18. Maintain database versioning and show the current database version in the About window.

21. Detect if an instance of the app is already running; if so, activate the existing instance instead of opening a new one.

### Import / Export / Backup

22. Import/Export format:
    ```json
    {
      "TimeEntries": [
        {
          "Description": "",
          "StartDate": "2026-04-30",
          "StartTime": "02:31:00",
          "EndDate": "2026-04-30",
          "EndTime": "03:01:00",
          "Duration": "00:30:00"
        }
      ]
    }
    ```

23. Maintain an import/export history in a separate table recording:
    - For export: first and last entry
    - For import: number of items imported
    - Export items sorted in descending order by `StartDate`

24. Provide backup/restore functionality for the local database.

### Screen Lock / Unlock

25. Maintain a history of Windows lock/unlock events in the database, separately from time entries. Add a future settings option to enable/disable this. Provide a dedicated screen for viewing these events, as this is a distinct feature from normal time tracking.

### User Documentation

- [ ] Review and implement [Repository Privacy Plan](REPOSITORY_PRIVACY_PLAN.md): select private files, use separate private storage, and assess existing Git history before any cleanup (AR-036).

- [ ] Include open-source licensing documents: choose the project license, add a root `LICENSE` file, and include any required third-party license/attribution notices (AR-035).

26. Create a README for new users covering:
    - What makes this app different from other time tracking apps
    - How to use the app
    - How the duration boxes work in different ways
    - How the start/stop boxes work
    - System tray behaviour (no floating widget)
    - Performance with larger data sets
    - How to import/export data
    - How to backup/restore data
    - How to contribute
    - FAQ / troubleshooting
    - Screenshots of the app in action

## Build
Publish command:

```cmd
dotnet publish .\TimeTracker.App\TimeTracker.App.csproj -c Release -r win-x64 --self-contained -o .\publish\release\
```

## Completed TODOs

- [x] Add a separate Jira upload window with required date ranges, editable saved connection/token settings, script-compatible ticket matching/worklog requests, daily logs beside the database, copyable in-memory failures, and separate upload history without changing `TimeEntries` or adding duplicate checks (AR-048/049).

### Completed work

- [x] Add matching-description suggestions to Edit entry for running/completed entries, including typing search, click/keyboard acceptance without saving, empty-field recent entries, and stale-result protection (AR-047).

- [x] Extend Edit entry with Date/Duration for completed entries; preserve Start clock time when it fits, shift Start backward to end at midnight on overflow, allow up to 24 hours, and require stopping running timers before timing edits. Preserve Created, description-only historical timing, and active queries/manual drafts (AR-046; completes AR-019).

- [x] Require HH:mm or HH:mm:ss for manual Start/End times, interpret two parts as hours/minutes, reject colonless/partial input, and show HH:mm placeholders plus label/input hover hints (AR-045).
- [x] Add labeled Clock/Unit duration patterns in both duration inputs with focus-loss/Enter normalization, strict unit validation and help; use empty manual time placeholders, end duration entries at the selected date's current clock time with midnight fallback and a 24-hour cap; place weekly total beside Entries and compact week buttons/horizontal scrollbars (AR-044).
- [x] Add full-row single-click lavender selection, including clicks in descriptions, retained across focus changes and refreshes when visible; add white/light-gray zebra stripes per day while preserving running indicators and existing row actions (AR-043).
- [x] Replace the app icon with a clear white stopwatch dial inspired by the old app, retaining the opaque edge-to-edge indigo background and all nine sizes; preserve the tray icon (AR-041).
- [x] Start the timer on Enter in the timer description or Initial duration with a nonblank description; preserve suggestion-first Enter and duration validation (AR-042).
- [x] Document Authenticode release-signing options, prerequisites, publish/sign/verify sequence, and Apex One limitations (AR-040); see RELEASE_SIGNING_PLAN.md.
- [x] Comment out Launch at startup menu controls and registry access in all builds while investigating Apex One detections; retain existing `--startup` launch handling (AR-039). Earlier AR-015 registration implementation is superseded.
- [x] Persist column order/widths and Manual entry state; automatically save filter drafts/toggles alongside applied queries. Debounce view changes for three seconds and write in a background task, flushing on close/exit/lock (AR-037/AR-038).
- [x] Align row text, including ID, with column headers; center timer buttons without shifting columns for the row status border or grouping indent (AR-037).
- [x] Activate unfinished-task recovery popups as topmost at startup/unlock and raise an already open popup again on unlock (AR-037).
- [x] Maximize the tray status circle within Windows' icon canvas, removing outer padding and the dark outline (AR-034).
- [x] Store writable data in LocalAppData by default, support explicit portable mode, and safely copy legacy databases/preferences without overwriting or deleting data; restore the authorized 22-entry snapshot to the new default location (AR-033).
- [x] Remove all icon transparency, including corners, and verify the rebuilt EXE embeds the opaque artwork (AR-032).
- [x] Maximize taskbar icon artwork: remove outer padding and enlarge the stopwatch within the badge (AR-031).
- [x] Give the app icon an indigo badge and white stopwatch for light/dark Windows contrast (AR-030).
- [x] Show 00:00:00 as the empty Initial duration field hint (AR-029).
- [x] Always prevent duplicate executable-directory launches; permit other directories only with the launching copy's AllowMultipleInstances setting, default false (AR-027).
- [x] Clear the timer description on successful stop and align Start/Stop with the input row (AR-028).
- [x] Remove the two-character typed-search cutoff and ignore outer query spaces; verify automatic visible results and retained input focus in all three fields after manual-section toggling (AR-025).
- [x] Make Manual entry collapsible without losing drafts; keep its heading accessible while forms scroll and reduce top padding above Entries (AR-024).
- [x] Explain every unfinished-task popup action beside its button, with matching tooltips/accessibility help and fallback/unavailable timestamp guidance (AR-023).
- [x] Add unfinished-task recovery on unlock and app startup, with Continue / Stop at recorded time / Stop now, persistent interruption timestamps, and explicitly labeled activity fallback after abrupt exits (AR-022).
- [x] Add optional Initial duration to Start Timer, preserving Created and ticking from the supplied duration (AR-022).
- [x] Show last five entries on empty-description Down-arrow key-up, with Up/Down navigation, Enter selection, and Escape dismissal for all three description inputs (AR-022).
- [x] Verify and regression-test that Created records the actual addition moment for duration entries, independently of selected dates and shifted Start/End (AR-021).
- [x] Select/copy row descriptions, fix suggestion click/focus and stale-search races in all three description inputs, compact cards/date/header spacing, single-line timestamps, and remove the row's visible In progress label (AR-020).
- [x] Place manual duration entries at the current clock time on the selected date, shift Start back to end at 23:59:59 on overflow, and guard the retained duration-edit path against crossing dates (AR-020).
- [x] Add description-only Edit entry popup via double-click and row context menu for running/completed tasks; disable Save for blank drafts, preserve timing, refresh active queries and running displays (AR-019).
- [x] Move duration hints and the help link into parentheses beside the By duration heading, removing the standalone hint line (AR-018).
- [x] Make File Exit explicitly say it closes the entire app, including the tray, with explanatory tooltip/accessibility text (AR-017).
- [x] Rename the range action to + Add with Range, align both manual actions with their fields, and move daily completed totals beside date labels (AR-016).
- [x] Reduce shared input-field vertical padding/heights, including date/dropdown inputs and aligned manual/filter actions (AR-014).
- [x] Strengthen header separators; left-align entry values except ID/Timer; persist query selections across launches; offer optional inclusive export dates before choosing the output file (AR-013).
- [x] Add synchronized File/tray Launch at startup toggles using the current user's Run value; startup launches remain hidden in the tray with session recording active, normal launches restore the app (AR-015).
- [x] Implement the approved UI refresh plan: shared light theme, compact timer/manual rows, expandable filters, readable entry table, running indicators, tray/secondary-window styling, and matching confirmations (AR-012; `UI_PLAN.md`).
- [x] Add row context menus for confirmed removal and conditional stopping; show/perform Stop on running-row buttons (AR-010).
- [x] Remove the running-description editors and single/double-click form population pending the author's replacement design (AR-010).
- [x] Create the author requirements record with original conversation quotes, separate implementation assumptions, and ongoing maintenance guidance (AR-009).
- [x] Add strict manual duration/time/date validation with inline errors, reject overflow safely, and display/export large existing durations in full hours without capping calculations.
- [x] Move Start Date to the far left of both manual-entry rows with a narrower date column and wider Description in the middle.
- [x] Highlight in-progress entries in the list using the running state, with a tooltip and accessibility status.
- [x] Add Help → Usage with duration formats and guides for manual entry, timers, tray controls, filtering, sorting, and week navigation.
- [x] Add Monday–Sunday pagination across all entries, with description/date/status filters and persistent sorting within daily groups (items 19 and 20).
- [x] Show filtered daily and weekly completed-duration totals, week ranges, and matching-entry counts.
- [x] Split duration and start/end manual entry into independent rows, each with Description and Start Date; honor the selected date for duration entries.
- [x] Show Created and Modified timestamps in the entry list in local time.
- [x] Position the tray popup near the clicked tray area with monitor/DPI-aware placement.
- [x] Fix the tray popup's Open App action to restore the main window.
- [x] Initially add explicit running-description editors with Save/Enter (items 8 and 9); subsequently reverted per AR-010, with replacement design pending.
- [x] Fix startup `StackOverflowException` caused by the `OnStartup` handler sharing the protected method name.
- [x] Fix icon/XAML resource load failure by loading the icon from disk at runtime.
- [x] Fix `ListViewItem` style/resource lookup error.
- [x] Fix tray-popup double-close crash.
- [x] Add global exception logging in `App.xaml.cs`.
- [x] Rename the EXE output to `TimeTracker.exe` (project remains `TimeTracker.App`; assembly name is `TimeTracker`).
- [x] Add `--db-path` CLI argument for isolated test databases.
- [x] Add automation IDs to key `MainWindow` controls for UI testing.
- [x] Create `TimeTracker.Tests` (xUnit) with `DurationParser` and repository tests.
- [x] Create `TimeTracker.UITests` (xUnit + Windows UI Automation) with main-window smoke tests.
- [x] Include both test projects in `TimeTracker.sln`.

### UI / Menu

6. [x] Maximize the size of the tray icon and make both red and green colors very bright/prominent. Completed in AR-034.

### Timer & Entries

8. [x] Allow modifying the description after starting the timer through the list's Edit entry popup (AR-019), replacing the reverted editors (AR-010).

10. [x] Add a right-click context menu to copy the description to the clipboard, plus selectable row text (AR-020).

### Data & Pagination

19. [x] Implement Monday–Sunday week pagination with Older/Newer navigation, including empty weeks and access beyond the former 500-entry limit.

20. [x] Show filtered daily totals in group headers and the displayed week's total in the pagination summary.
