# Project TODOs

This file preserves the original development backlog that was previously kept in `TimeTracker.App\README.md`.

Original author statements and requirement history are maintained separately in [AUTHOR_REQUIREMENTS.md](AUTHOR_REQUIREMENTS.md). Keep implementation status here; preserve the author's wording there.

## Completed Recently

- [x] Add description-only Edit entry popup via double-click and row context menu for running/completed tasks; disable Save for blank drafts, preserve timing, refresh active queries and running displays (AR-019).
- [ ] Extend the Edit entry popup to support duration/start date after the author specifies the workflow (AR-019).

- [x] Move duration hints and the help link into parentheses beside the By duration heading, removing the standalone hint line (AR-018).

- [x] Make File Exit explicitly say it closes the entire app, including the tray, with explanatory tooltip/accessibility text (AR-017).

- [x] Rename the range action to + Add with Range, align both manual actions with their fields, and move daily completed totals beside date labels (AR-016).

- [x] Reduce shared input-field vertical padding/heights, including date/dropdown inputs and aligned manual/filter actions (AR-014).

- [x] Strengthen header separators; left-align entry values except ID/Timer; persist query selections across launches; offer optional inclusive export dates before choosing the output file (AR-013).
- [x] Add synchronized File/tray Launch at startup toggles using the current user's Run value; startup launches remain hidden in the tray with session recording active, normal launches restore the app (AR-015).
- [ ] Verify sign-in startup, lock/unlock recording while initially hidden, and startup toggles in both native menus in an interactive Windows session.

- [x] Implement the approved UI refresh plan: shared light theme, compact timer/manual rows, expandable filters, readable entry table, running indicators, tray/secondary-window styling, and matching confirmations (AR-012; `UI_PLAN.md`).
- [ ] Verify the refreshed UI interactively on mixed-DPI monitors and in Windows high-contrast mode; inspect native tray placement and popup/window focus behavior.

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

## UI / Menu

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

6. Maximize the size of the tray icon and make both red and green colors very bright/prominent.

## Timer & Entries

7. Add a periodic pop-up reminder to stop the timer if it has been running for a long time, e.g. every 15 minutes. This helps avoid forgotten timers and keeps time-tracking data accurate.

8. [x] Allow modifying the description after starting the timer through the list's Edit entry popup (AR-019), replacing the reverted editors (AR-010).

9. [ ] Allow modifying the ongoing timer description from the tray icon mini window. Previous editor reverted; replacement design pending (AR-010).

10. Add a right-click context menu to copy the description to the clipboard.

11. Add a multi-select remove option for entries.

12. While modifying an ended timer, the start and stop boxes should adjust when focus leaves the duration box. Conversely, the duration box should adjust when focus leaves the start/stop time boxes.

13. Format total duration for each day as `3h 30 min` in the group row of each day in the list view.

14. Display `Running: 00:00:35 – {{Description here}}` for the active timer.

15. Provide a default timer text (currently `Untitled task`) that is modifiable from within the app.

## Duration Box Behaviour

16. In both duration boxes, support the following input formats:
    - `33:33` → 33 minutes 33 seconds
    - `33:` → 33 minutes 0 seconds
    - `:33` → 0 minutes 33 seconds
    - `34` → 34 minutes 0 seconds
    - `5.3` / `1.0` → 5.3 / 1.0 hours (decimal hours only when a decimal is present)

17. When moving into any duration box via the Tab key (not mouse click), select all text so it is ready to be removed/cut/copied.

## Data & Pagination

18. Maintain database versioning and show the current database version in the About window.

19. [x] Implement Monday–Sunday week pagination with Older/Newer navigation, including empty weeks and access beyond the former 500-entry limit.

20. [x] Show filtered daily totals in group headers and the displayed week's total in the pagination summary.

21. Detect if an instance of the app is already running; if so, activate the existing instance instead of opening a new one.

## Import / Export / Backup

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

## Screen Lock / Unlock

25. Maintain a history of Windows lock/unlock events in the database, separately from time entries. Add a future settings option to enable/disable this. Provide a dedicated screen for viewing these events, as this is a distinct feature from normal time tracking.

## User Documentation

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
