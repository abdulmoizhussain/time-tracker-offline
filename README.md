# TimeTracker

A local-first, offline Windows desktop time-tracking application. It runs from the system tray, stores everything in a local SQLite database, and requires no cloud account.

The author's original requests are preserved in [Author Requirements](docs/AUTHOR_REQUIREMENTS.md), with verbatim statements separated from implementation decisions and assumptions. Contributors and AI agents should consult it when checking feature intent and append new requirements as they are provided.

---

## Table of Contents

1. [What Makes This App Different](#what-makes-this-app-different)
2. [Project Structure](#project-structure)
3. [Technology Stack](#technology-stack)
4. [Architecture](#architecture)
5. [Features](#features)
6. [Configuration](#configuration)
7. [Build & Run](#build--run)
8. [Testing](#testing)
9. [Publish](#publish)
10. [Database Schema & Versioning](#database-schema--versioning)
11. [Import / Export Format](#import--export-format)
12. [Known Limitations & TODOs](#known-limitations--todos)
13. [Contributing](#contributing)
14. [Troubleshooting](#troubleshooting)

---

## What Makes This App Different

- **Offline by default** — all data lives in a local SQLite file.
- **No floating widget** — minimize to the system tray and forget about it.
- **Week-based browsing** — SQLite with WAL mode; the list queries one Monday–Sunday week at a time across the full database.
- **Lock/unlock awareness** — optionally records Windows session lock/unlock events to help spot forgotten timers.
- **Flexible duration input** — type `34`, `33:33`, `:33`, `1:02:30`, or `5.3` depending on what is fastest for you.
- **Instance policy** — one instance per executable directory, always. A copy in another directory can run alongside an existing copy only when its own `appsettings.json` has `"AllowMultipleInstances": true` (default: `false`). Reopening the same copy activates its window. Applies within the current Windows user/session.
- **Tested** — unit tests for parsing and data access, plus UI smoke tests that drive the real WPF app.

---

## Project Structure

```
TimeTracker/
├── README.md                 # This file
├── AGENTS.md                 # Guidelines for AI agents working on the codebase
├── TimeTracker.sln           # Visual Studio solution
├── TimeTracker.App/          # WPF entry point & UI
├── TimeTracker.Core/         # Models, contracts, configuration
├── TimeTracker.Data/         # SQLite repositories
├── TimeTracker.Tests/        # Unit / integration tests (xUnit)
├── TimeTracker.UITests/      # End-to-end UI tests (xUnit + UI Automation)
├── tools/IconGenerator/      # Console app to generate the app icon
├── publish/release/          # Self-contained published output
├── docs/                     # Additional documentation
│   ├── TODO.md               # Development backlog
│   ├── AUTHOR_REQUIREMENTS.md # Original author statements and requirement history
│   ├── TESTING.md            # How to run and extend tests
│   ├── UI_AUTOMATION.md      # Automation IDs for UI tests
│   ├── TROUBLESHOOTING.md    # Common issues and fixes
│   ├── CHANGELOG.md          # Recent changes
│   └── SPEC.md               # Full rewrite specification
└── appsettings.json          # Runtime configuration
```

### Project Responsibilities

| Project | References | Responsibility |
|---|---|---|
| `TimeTracker.Core` | — | Domain models, repository interfaces, `AppRuntimeOptions` |
| `TimeTracker.Data` | `Core` | SQLite connection factory + `TimeEntryRepository` |
| `TimeTracker.App` | `Core`, `Data` | WPF UI, view-models, converters, tray icon, import/export |
| `TimeTracker.Tests` | `Core`, `Data` | Unit and repository integration tests |
| `TimeTracker.UITests` | — (launches App) | End-to-end smoke tests against the compiled EXE |
| `tools/IconGenerator` | — | Generates multi-resolution `TimeTracker.ico` |

---

## Technology Stack

- **.NET 8** (`net8.0-windows` for the WPF app and UI tests)
- **WPF** + **Windows Forms** (`NotifyIcon`)
- **SQLite** via `Microsoft.Data.Sqlite`
- **Dapper** for lightweight SQL mapping
- **CommunityToolkit.Mvvm** for MVVM source generators
- **Microsoft.Extensions.Hosting** for dependency injection
- **xUnit** for testing
- **System.Windows.Automation** for UI tests (built into .NET Windows Desktop)

---

## Architecture

The app follows a layered MVVM architecture:

```
┌─────────────────────────────────────┐
│ TimeTracker.App (WPF UI)            │
│  - Windows / Views                   │
│  - ViewModels                        │
│  - Converters / Helpers              │
└──────────────┬──────────────────────┘
               │ uses
┌──────────────▼──────────────────────┐
│ TimeTracker.Data                    │
│  - SqliteConnectionFactory           │
│  - TimeEntryRepository               │
└──────────────┬──────────────────────┘
               │ implements
┌──────────────▼──────────────────────┐
│ TimeTracker.Core                    │
│  - ITimeEntryRepository              │
│  - IScreenSessionEventRepository     │
│  - Models                            │
│  - AppRuntimeOptions                 │
└─────────────────────────────────────┘
```

- `App.xaml.cs` bootstraps the host, loads `appsettings.json`, applies UI scaling, enforces single-instance behaviour, and shows `MainWindow`.
- `MainViewModel` owns the timer loop, manual-entry commands, import/export logic, and description suggestions.
- `TrayPopupViewModel` provides a compact view-model for the tray popup window.

---

## Features

### Initial timer duration, unfinished-task recovery, and keyboard suggestions

- Before starting a timer, optionally enter **Initial duration** beside its description. It accepts the manual duration formats, from zero to 23:59:59; blank starts from zero and shows a `00:00:00` hint. Start is moved backward by that duration, and elapsed time continues from it. Created records the actual addition moment. The initial-duration draft clears after starting.
- **Temporary policy pending author review:** an initial duration may place Start on the previous local day (for example, 2 hours at 01:00). Normal running timers can also cross midnight; reporting currently assigns their full duration to the Start date.
- If a task was still running when you locked the PC, the app shows **Unfinished task** on unlock. A running task restored at app startup also shows the popup, including when launched from Windows startup into the tray. Actions are **Continue**, **Stop at recorded time**, and **Stop now**. Continue includes elapsed time during the interruption; stopping at its recorded time excludes the later gap. Each button has a visible explanation, tooltip, and accessibility help describing which time is counted; the recorded-time hint shows its exact timestamp or explains why it is unavailable. Escape/X acts as Continue. Closing the main window to the tray does not count as exiting the app.
- Recovery timestamps are independent of optional lock/unlock history recording. Lock and full app exit are recorded; Windows shutdown/sign-out recording is best effort. While active, a checkpoint is saved about every 30 seconds. After a crash or power loss, the popup clearly labels that fallback as **last recorded activity**, not an exact shutdown time. Legacy running tasks without a recorded timestamp offer Continue/Stop now; stopping at a recorded time is disabled.
- In any of the three description inputs, release **Down-arrow** while empty to show the **last five entries by Created**, newest first (repeated descriptions are retained). Typed searches show matching descriptions from the first nonblank character, ignoring outer spaces. Keep focus in the input: **Up/Down** changes the highlighted result, **Enter** fills that description only, and **Escape** dismisses the popup. Enter does not add an entry or start a timer.


### Modern desktop UI
- Input fields use compact vertical padding and shorter minimum heights across the app, with matching manual-entry/filter action buttons and unchanged readable font sizes.
- A shared light theme uses indigo actions, white surfaces, readable Segoe UI text, consistent controls, and vector Play/Stop icons across the main window, tray popup, and dialogs.
- The timer stays accessible above the scrollable manual-entry area. Both entry methods remain independent; compact or scaled windows allow scrolling instead of shrinking fields.
- Description search and sorting stay visible. **Filters** expands date/status controls; its count shows applied conditions. **Changes pending** identifies edits awaiting **Apply**. Week navigation remains accessible outside the filter scroller.
- The entry table uses single-line timestamps, left-aligned full durations, running row highlights, and a description column that grows with the window. Metadata remains accessible through horizontal scrolling.
- Expand **Stored locally · database details** to select/copy the full path. Remove/Clear confirmations share the theme and default to Cancel.
- The design reference is [UI_PLAN.md](docs/UI_PLAN.md). `tools/UiPreview` renders actual WPF content with isolated sample data for layout review.

### Usage Guide
- Open **Help → Usage...** for duration input examples and instructions for manual entries, timers, tray controls, week navigation, filtering, and sorting.
- The guide is scrollable and resizable, uses the app's UI scaling, and closes with **Close** or Escape.

### Timer
- Start/stop a running timer.
- Live elapsed-time display in the main window title, status bar, taskbar progress, and tray popup.
- Select description text and press Ctrl+C to copy it, or use right-click → **Copy description** for the entire description. Double-clicking description text selects a word; double-click elsewhere on an entry or choose **Edit entry** from its right-click menu to edit its description in a popup. Running timers keep running; Save updates the list, main timer, title, and tray popup. Blank/whitespace-only descriptions disable Save. Cancel/Escape/X discards the draft. Only Description and Modified change; dates/duration and Created are preserved.
- Default timer description is configurable in-app (default: `Untitled task`).

### Manual Entries
- Click **Manual entry** to collapse or expand both manual rows. Collapsing makes more space for the entry list and preserves both drafts. The section starts expanded.
- Manual duration input must fit within **00:00:00–23:59:59 on the selected date** (maximum duration 23:59:59). Malformed, negative, overflowing, or out-of-range components are rejected with an inline message. Start Date is required; start/end times use 24-hour `H:mm` or `HH:mm:ss` with hours 0–23 and minutes/seconds 0–59. End must follow start on the same day; ambiguous/nonexistent clock-change times are rejected. Stop a running entry before changing its duration/date/times.
- This input limit does not change existing/imported large durations: list cells, elapsed timers, totals, and JSON exports show full hours without wrapping at 24. Import allows durations above 24 hours within the existing integer-seconds storage range and reports skipped invalid/unrepresentable entries.
- Both rows place a compact Start Date field first, followed by a wider Description field, then duration or time inputs.
- **Add by duration**: Description, Start Date, and Duration, with its own Add/Save button.
- Duration examples, the input limit, and the clickable **Duration help** link appear in parentheses beside **By duration**, rather than on a separate line below the fields.
- **Add by start and end time**: Description, Start Date, Start Time, and End Time, with its own + Add with Range/Save Range button. Both manual-entry action buttons align with their input fields and match their height.
- Each row keeps an independent draft. Adding an entry clears only that row. Both rows support description suggestions. Single-click selects an entry; double-click outside the description opens the description editor without changing either manual-entry draft.
- Duration entries use the current local clock time on the selected date and store both Start and End in UTC. If that duration would extend past 23:59:59, Start shifts backward so End is 23:59:59. Created/Modified still reflect when the record was saved. The list groups entries by local date.
- Duration box and start/end time boxes auto-sync on focus loss.

### Entry List
- Header column separators are darker and thicker. Entry values are left-aligned except centered ID and Timer cells.
- Applied filters and sort selections restore on the next launch. Pending selections are preserved separately when closing/exiting, so they still require Apply. Preferences are saved beside the database as `<database path>.view.json`; Reset persists the defaults. A missing/corrupt preferences file falls back to defaults.
- Running entries are highlighted with an amber background and border, semibold text, and an “In progress” tooltip when visible in the current week/filter. The highlight is removed when the timer stops.
- Grouped by date with human-readable headers: *Today*, *Yesterday*, weekday names, or full date.
- Daily completed duration shown at the left of each group header, immediately beside Today/Yesterday/the date label.
- Sorted newest-first.
- **Created** and **Modified** columns show each entry's timestamps in local time (scroll horizontally to see all columns).
- Start a new timer pre-filled with an existing description.
- Right-click an entry for **Edit entry**, **Remove entry** (with confirmation) and, if running, **Stop running entry**. Actions target the clicked row, even if another row is selected. Saving re-applies the current list filters/sort/week; an edited entry can disappear if its new description no longer matches. Duration/start-date editing in the popup is deferred.
- The row's timer button shows **▶** for completed entries and **■** for running entries. Clicking **■** stops that entry and refreshes timer indicators and the current filtered week.
- **Older week / Newer week** navigation includes a week date range and page count. The default first page is the current week (or the latest week if future-dated entries exist); empty intervening weeks remain navigable.
- Filter by description text, inclusive local From/To dates, and All/Running/Completed status. Click **Apply** to reload the newest matching week; **Reset** clears filters and restores default sorting. Blank dates impose no date limit. Description searches treat `%` and `_` literally.
- Sort by Start, Description, Duration, Created, or Modified, ascending or descending. Daily groups stay newest-first; the chosen sort applies **within each day**. Equal values use entry ID as a stable tie-breaker. Filters and sorting persist while navigating or refreshing after changes.
- Daily and weekly totals show completed duration for the filtered entries. The footer also shows this week's entry count and the total matching count across the database.

### System Tray
- Minimize-to-tray on close.
- **File → Exit completely (including tray)** shuts down the whole app and removes its tray icon; the menu tooltip explains the difference from closing the main window with X.
- The app icon uses a fully opaque indigo background filling the entire icon canvas, including its corners, and a large white stopwatch for visibility in light and dark Windows themes.
- Tray icon turns bright red while tracking, lime green while idle; the circle fills the available Windows tray icon canvas without outer padding.
- Left-click opens a compact popup with elapsed time and start/stop control.
- The popup opens beside the clicked tray area on that monitor, stays within its work area, and **Open App** restores the main window.
- Right-click menu for Open / Launch at startup / Exit.
- Toggle **File → Launch at startup** or the same checked option in the tray right-click menu. Both reflect the current user's Windows Run registration; enabling launches this copy quietly in the tray after sign-in, while normal launches open the main window. Lock/unlock recording continues in the background when enabled in configuration.
- Registration is opt-in and uses only the `TimeTracker` value under `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`; no administrator access is normally required. Disabling removes only that value. Keep the executable in a stable built/published location; if moving it, disable registration first and enable again from the new location. Windows Startup Apps/Task Manager or organization policy can independently disable startup execution, even while the Run value remains registered.

### Import / Export
- Export first offers an optional local date range (inclusive). Leave the option off for all completed entries, or leave an individual boundary blank for an open-ended range. It uses Start date, falling back to End/Created, independently of list filters and pagination. Running entries are excluded. Only then choose the destination directory/filename in the save-file dialog. Exports are not capped to 10,000 records.
- JSON-based import/export.
- History of every import/export action is recorded and viewable.

### Screen Lock / Unlock History
- Records Windows session lock/unlock events while the app is running.
- Separate screen to review these events.

### Single Instance
- Uses a per-user named mutex and `EventWaitHandle` so a second launch activates the first instance.


---

By default, the database is `%LOCALAPPDATA%\TimeTracker\Data\timetracker.db`. Timer preferences (`user-settings.json`) and the error log live in `%LOCALAPPDATA%\TimeTracker`; list preferences stay beside the database. Deleting or replacing the installation directory therefore leaves these files intact.

On first launch with the new storage behavior, an existing database in the old installation-relative folder is copied using SQLite's backup API, including WAL data, with its list preferences. The original remains untouched. An existing destination is never replaced or merged. A migration error stops startup rather than silently opening an empty database. Close the old app before upgrading so it cannot continue logging to the old database after the snapshot.

Copies in different installation directories share this per-user database by default. Use distinct absolute `DatabaseDirectory` values or explicit portable mode if copies need separate logs. `--db-path` still selects an explicit database file (relative paths resolve beside the executable) and bypasses migration. `appsettings.json` remains installation configuration; the app does not write to it. This separation protects against deleting the installation, but it is not a backup against disk failure or deleting AppData.

## Configuration

`appsettings.json` (copied to output):

```json
{
  "AllowMultipleInstances": false,
  "Storage": {
    "PortableMode": false,
    "DatabaseDirectory": "Data",
    "DatabaseFileName": "timetracker.db"
  },
  "Hotkeys": {
    "Enabled": false,
    "StartStop": "Ctrl+Shift+T"
  },
  "Ui": {
    "ScaleMultiplier": 1.0
  },
  "Timer": {
    "DefaultDescription": "Untitled task"
  }
}
```

| Section | Setting | Description |
|---|---|---|
| Root | `AllowMultipleInstances` | Allow this copy alongside another directory; never permits duplicates from the same directory. Default `false`. |
| `Storage` | `PortableMode` | Default `false`: writable data lives in `%LOCALAPPDATA%\TimeTracker`. Set `true` only for an intentional portable copy that stores data beside its EXE. |
| `Storage` | `DatabaseDirectory` | SQLite folder, relative to the data root unless absolute; default `Data`. Environment variables are expanded. |
| `Storage` | `DatabaseFileName` | SQLite file name |
| `Hotkeys` | `Enabled` | Not yet wired up |
| `Hotkeys` | `StartStop` | Planned global hotkey |
| `Ui` | `ScaleMultiplier` | Font/control scale (clamped 0.75–2.5) |
| `Timer` | `DefaultDescription` | Fallback description for empty timer starts |

### Command-line arguments

| Argument | Description |
|---|---|
| `--db-path <path>` | Use an alternative SQLite database file. Overrides the `Storage` configuration. Useful for isolated UI tests. |
| `--startup` | Start quietly in the tray without showing the main window. Used by Windows startup registration; a duplicate startup launch does not activate the existing instance. |

---

## Build & Run

Requires the .NET 8 SDK or a later SDK that supports `net8.0`.

```cmd
dotnet build TimeTracker.sln
```

Run the app:

```cmd
dotnet run --project .\TimeTracker.App\TimeTracker.App.csproj
```

Run with an isolated database:

```cmd
dotnet run --project .\TimeTracker.App\TimeTracker.App.csproj -- --db-path "C:\Temp\TimeTracker-dev.db"
```

Verified build environment:
- SDKs: `9.0.315`, `10.0.401`
- Runtimes: .NET 8.0.31, 9.0.20, 10.0.12

---

## Testing

Run unit / integration tests:

```cmd
dotnet test TimeTracker.Tests -c Debug
```

Run UI smoke tests (requires an interactive Windows session):

```cmd
dotnet test TimeTracker.UITests -c Debug
```

Run everything:

```cmd
dotnet test TimeTracker.sln -c Debug
```

See [`docs/TESTING.md`](docs/TESTING.md) for detailed instructions on writing and running tests, and [`docs/UI_AUTOMATION.md`](docs/UI_AUTOMATION.md) for the list of automation IDs.

---

## Publish

Self-contained release for Windows x64:

```cmd
dotnet publish .\TimeTracker.App\TimeTracker.App.csproj -c Release -r win-x64 --self-contained -o .\publish\release\
```


---

## Database Schema & Versioning

The database uses `PRAGMA user_version` for migrations. Current version is **3**.

### Tables

`TimerInterruptions` (schema v3) stores each running entry's latest recovery reason and UTC timestamp, with a cascading reference to `TimeEntries`. Saving a checkpoint does not change the entry's Created/Modified timestamps.

#### `TimeEntries`
| Column | Type | Notes |
|---|---|---|
| `Id` | INTEGER PK AI | |
| `Description` | TEXT NOT NULL | |
| `StartUtc` | TEXT NULL | ISO-8601 UTC |
| `EndUtc` | TEXT NULL | ISO-8601 UTC |
| `DurationSeconds` | INTEGER NOT NULL | |
| `IsRunning` | INTEGER NOT NULL | Boolean flag |
| `CreatedUtc` | TEXT NOT NULL | |
| `UpdatedUtc` | TEXT NOT NULL | |

#### `ScreenSessionEvents`
| Column | Type | Notes |
|---|---|---|
| `Id` | INTEGER PK AI | |
| `EventType` | TEXT NOT NULL | e.g. `Lock`, `Unlock` |
| `OccurredUtc` | TEXT NOT NULL | |
| `CreatedUtc` | TEXT NOT NULL | |

#### `ImportExportHistory`
| Column | Type | Notes |
|---|---|---|
| `Id` | INTEGER PK AI | |
| `ActionType` | TEXT NOT NULL | `Import` or `Export` |
| `ItemCount` | INTEGER NOT NULL | |
| `FirstEntryDescription` | TEXT NULL | |
| `FirstEntryStartUtc` | TEXT NULL | |
| `LastEntryDescription` | TEXT NULL | |
| `LastEntryStartUtc` | TEXT NULL | |
| `FilePath` | TEXT NULL | |
| `Notes` | TEXT NULL | |
| `OccurredUtc` | TEXT NOT NULL | |
| `CreatedUtc` | TEXT NOT NULL | |

---

## Import / Export Format

```json
{
  "TimeEntries": [
    {
      "Description": "Sample task",
      "StartDate": "2026-04-30",
      "StartTime": "02:31:00",
      "EndDate": "2026-04-30",
      "EndTime": "03:01:00",
      "Duration": "00:30:00"
    }
  ]
}
```

Exports are sorted descending by `StartDate`.

---

## Known Limitations & TODOs

A detailed backlog is maintained in [`docs/TODO.md`](docs/TODO.md). High-level gaps include:

- Global hotkeys are configured but not implemented.
- The About window shows the database version as `Unknown`.
- No automated backup/restore workflow yet.
- UI tests cover only basic launch/start/stop/add scenarios; more control IDs are needed to automate the list view, tray menu, and dialogs.

---

## Contributing

1. Fork or clone the repository.
2. Open `TimeTracker.sln` in Visual Studio 2022+ or use `dotnet build`.
3. Follow the existing code style:
   - `ImplicitUsings` and `Nullable` enabled.
   - MVVM pattern with source-generated commands/properties.
   - Async repository methods with `CancellationToken` support.
4. Add or update documentation in `docs/` for non-trivial features.
5. Add or update tests for changed behaviour.
6. Ensure the solution builds and tests pass before submitting changes.

---

## Troubleshooting

See [`docs/TROUBLESHOOTING.md`](docs/TROUBLESHOOTING.md) for common issues such as startup crashes, resource load failures, build hangs, and UI test timeouts.

---

*Built with .NET 8, WPF, and SQLite.*


Stopping a running entry clears the timer description for the next task, including stops from the entry list or unfinished-task popup. Start/Stop buttons align with the timer input row.
