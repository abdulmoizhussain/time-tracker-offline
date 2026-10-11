# Project TODOs

This file preserves the original development backlog that was previously kept in `TimeTracker.App\README.md`.

## Completed Recently

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

8. Allow modifying the description after starting the timer.

9. Allow modifying the ongoing timer from the tray icon mini window.

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

19. Implement pagination for large numbers of entries. Each page should show entries from Monday to Sunday. If the current date is Monday, the first page contains only that day; older entries go to the next page, which shows the complete previous week.

20. After week-wise pagination, show the total duration tracked for each day in the list view and the total duration for the current week in the group row of each week.

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
