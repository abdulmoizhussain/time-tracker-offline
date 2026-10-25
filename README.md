# TimeTracker

A local-first, offline Windows desktop time-tracking application. It runs from the system tray, stores everything in a local SQLite database, and requires no cloud account.

The author's original requests are preserved in [Author Requirements](docs/AUTHOR_REQUIREMENTS.md), with verbatim statements separated from implementation decisions and assumptions. Contributors and AI agents should consult it when checking feature intent and append new requirements as they are provided.

The proposed approach for separating public and private material is documented in [Repository Privacy Plan](docs/REPOSITORY_PRIVACY_PLAN.md).

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
- The unfinished-task popup opens activated and topmost on unlock/startup, staying above other regular windows until a choice is made. If it was already open when the PC locked again, unlocking brings it forward again.
- **Temporary policy pending author review:** an initial duration may place Start on the previous local day (for example, 2 hours at 01:00). Normal running timers can also cross midnight; reporting currently assigns their full duration to the Start date.
- If a task was still running when you locked the PC, the app shows **Unfinished task** on unlock. A running task restored at app startup also shows the popup, including when launched from Windows startup into the tray. Actions are **Continue**, **Stop at recorded time**, and **Stop now**. Continue includes elapsed time during the interruption; stopping at its recorded time excludes the later gap. Each button has a visible explanation, tooltip, and accessibility help describing which time is counted; the recorded-time hint shows its exact timestamp or explains why it is unavailable. Escape/X acts as Continue. Closing the main window to the tray does not count as exiting the app.
- Recovery timestamps are independent of optional lock/unlock history recording. Lock and full app exit are recorded; Windows shutdown/sign-out recording is best effort. While active, a checkpoint is saved about every 30 seconds. After a crash or power loss, the popup clearly labels that fallback as **last recorded activity**, not an exact shutdown time. Legacy running tasks without a recorded timestamp offer Continue/Stop now; stopping at a recorded time is disabled.
- In any of the three description inputs, release **Down-arrow** while empty to show the **last five entries by Created**, newest first (repeated descriptions are retained). Typed searches show matching descriptions from the first nonblank character, ignoring outer spaces. Keep focus in the input: **Up/Down** changes the highlighted result, **Enter** fills that description only, and **Escape** dismisses the popup. In the timer description, press **Enter** with the popup closed to start when the description has text; if suggestions are open, the first Enter accepts a suggestion and the next starts. **Enter** in Initial duration also starts when the description has text, using the usual duration validation. Holding Enter does not trigger another action. Enter in manual Duration formats its value without adding an entry.


### Modern desktop UI
- Input fields use compact vertical padding and shorter minimum heights across the app, with matching manual-entry/filter action buttons and unchanged readable font sizes.
- A shared light theme uses indigo actions, white surfaces, readable Segoe UI text, consistent controls, and vector Play/Stop icons across the main window, tray popup, and dialogs.
- The timer stays accessible above the scrollable manual-entry area. Both entry methods remain independent; compact or scaled windows allow scrolling instead of shrinking fields.
- Description search and sorting stay visible. **Filters** expands date/status controls; its count shows applied conditions. **Changes pending** identifies edits awaiting **Apply**. Week navigation remains accessible outside the filter scroller.
- The entry table uses single-line timestamps, left-aligned full durations, running row highlights, and a description column with a saved user-selected width. Metadata remains accessible through horizontal scrolling.
- Expand **Stored locally · database details** to select/copy the full path. Remove/Clear confirmations share the theme and default to Cancel.
- The design reference is [UI_PLAN.md](docs/UI_PLAN.md). `tools/UiPreview` renders actual WPF content with isolated sample data for layout review.

### Usage Guide
- Open **Help → Usage...** for duration input examples and instructions for manual entries, timers, tray controls, week navigation, filtering, and sorting.
- The guide is scrollable and resizable, uses the app's UI scaling, and closes with **Close** or Escape.

### Timer
- Start/stop a running timer.
- Live elapsed-time display in the main window title, status bar, taskbar progress, and tray popup.
- Select description text and press Ctrl+C to copy it, or use right-click → **Copy description** for the entire description. Double-clicking description text selects a word; double-click elsewhere on an entry or choose **Edit entry** from its right-click menu to edit its description in a popup. Running timers keep running; Save updates the list, main timer, title, and tray popup. Blank/whitespace-only descriptions disable Save. Cancel/Escape/X discards the draft. The popup description supports matching existing descriptions while typing (from the first nonblank character). Up/Down highlights matches; Enter or click fills the draft without saving; Escape dismisses matches. Down-arrow in an empty description shows the last five entries. Completed entries also expose Date and Duration (Clock/Unit patterns, 1 second to 24 hours for timing changes). Changing Date keeps the original Start clock time; changing Duration recalculates End. If the result would cross midnight, Start shifts backward so End is midnight ending the selected date. Exactly 24 hours runs midnight-to-midnight. Running entries keep Date/Duration disabled until stopped. Description-only saves preserve existing timing, including historical large durations; Created always stays unchanged. Entries with no Start time use midnight when their timing is edited.
- Default timer description is configurable in-app (default: `Untitled task`).

### Manual Entries
- Click **Manual entry** to collapse or expand both manual rows. Collapsing makes more space for the entry list and preserves both drafts. It starts expanded until you save a different toggle state; that state restores on the next launch.
- Manual duration input must be **1 second–24 hours**. Malformed, negative, overflowing, or out-of-range components are rejected with an inline message. Start Date is required; start/end times require 24-hour `HH:mm` or `HH:mm:ss`, with two digits per component, hours 00–23, and minutes/seconds 00–59. `22:45` means 22 hours and 45 minutes; single numbers without a colon are invalid. Both fields show `HH:mm` placeholders and format hints when hovering their labels or inputs. End must follow start on the same day; ambiguous/nonexistent clock-change times are rejected. Stop a running entry before changing its duration/date/times.
- This input limit does not change existing/imported large durations: list cells, elapsed timers, totals, and JSON exports show full hours without wrapping at 24. Import allows durations above 24 hours within the existing integer-seconds storage range and reports skipped invalid/unrepresentable entries.
- Both rows place a compact Start Date field first, followed by a wider Description field, then duration or time inputs.
- **Add by duration**: Description, Start Date, and Duration, with its own Add/Save button.
- Duration examples, the input limit, and the clickable **Duration help** link appear in parentheses beside **By duration**, rather than on a separate line below the fields.
- **Add by start and end time**: Description, Start Date, Start Time, and End Time, with its own + Add with Range/Save Range button. Both manual-entry action buttons align with their input fields and match their height.
- Each row keeps an independent draft. Adding an entry clears only that row. Both rows support description suggestions. Single-click on ordinary entry content, including its description, highlights the entire row in light lavender without automatically selecting text. The highlight remains when switching windows and restores after a list refresh if that entry is still visible; deleting it or filtering/paging it out clears selection. Rows alternate white/light gray within each day group. Selection takes priority over stripes and hover; running rows retain their amber indicator. Description text can still be selected/copied, timer buttons retain their actions, and double-click outside the description opens the description editor without changing either manual-entry draft.
- Duration entries end at the current local clock time on the selected date, with Start that duration earlier, and store both timestamps in UTC. If Start would fall before midnight, Start becomes midnight and End is midnight plus the duration. Exactly 24 hours ends at midnight the following day; longer input is rejected. Created/Modified still reflect when the record was saved. The list groups entries by local date.
- Manual Duration starts empty with a `00:00:00` placeholder; Start Time and End Time start empty with `HH:mm` placeholders. Each row clears its own fields after adding.
- Both duration boxes accept **Clock pattern** (existing bare minutes, decimal hours, `mm:ss`, `hh:mm:ss`) and **Unit pattern** (`1h 2m 3s`, `1h 55m`, `59m 59s`). Unit pattern uses whole numbers in h/m/s order; spaces are optional and letters are case-insensitive. Each number needs a unit. Only single-unit inputs overflow: `90m` → `01:30:00`, `1000s` → `00:16:40`; multi-unit minutes/seconds must be 0–59. `1m 2`, `1h 123`, `-2s`, `2 2m`, `1h 90m`, repeated/reordered units, and decimal units are invalid. Without units, `29` still means 29 minutes.
- Valid duration input normalizes to `hh:mm:ss` on click-away, Tab, or Enter; invalid text remains with an inline error. Enter in Initial duration retains its start-timer action; Enter in manual Duration formats only.

### Entry List
- Completed weekly time sits beside Entries in the same row. Week navigation buttons and horizontal scrollbars use compact heights; vertical scrollbar width is unchanged.
- Header column separators are darker and thicker. Entry values, including ID, align with header text; only Timer buttons are centered.
- Applied filters and sort selections restore on the next launch. Pending selections are preserved separately, so they still require Apply. Column order, column widths, the Filters toggle, and the Manual entry toggle also restore per database. Changes save in a background task after three seconds without further view changes; every column resize restarts that delay. Closing, exiting, or locking flushes pending settings. Resizing the window does not override chosen column widths. Preferences are saved beside the database as `<database path>.view.json`; Reset persists the defaults. A missing/corrupt preferences file falls back to defaults.
- Running entries are highlighted with an amber background and border, semibold text, and an “In progress” tooltip when visible in the current week/filter. The highlight is removed when the timer stops.
- Grouped by date with human-readable headers: *Today*, *Yesterday*, weekday names, or full date.
- Daily completed duration shown at the left of each group header, immediately beside Today/Yesterday/the date label.
- Sorted newest-first.
- **Created** and **Modified** columns show each entry's timestamps in local time (scroll horizontally to see all columns).
- Start a new timer pre-filled with an existing description.
- Right-click an entry for **Edit entry**, **Remove entry** (with confirmation) and, if running, **Stop running entry**. Actions target the clicked row, even if another row is selected. Saving re-applies the current list filters/sort/week; an edited entry can disappear if its new description no longer matches. Completed entries allow Date and Duration edits; running entries allow Description only until stopped. A date edit can move the entry out of the displayed week.
- The row's timer button shows **▶** for completed entries and **■** for running entries. Clicking **■** stops that entry and refreshes timer indicators and the current filtered week.
- **Older week / Newer week** navigation includes a week date range and page count. The default first page is the current week (or the latest week if future-dated entries exist); empty intervening weeks remain navigable.
- Row values align with their column headers, including ID; row timer buttons are centered in their columns.
- Filter by description text, inclusive local From/To dates, and All/Running/Completed status. Click **Apply** to reload the newest matching week; **Reset** clears filters and restores default sorting. Blank dates impose no date limit. Description searches treat `%` and `_` literally.
- Sort by Start, Description, Duration, Created, or Modified, ascending or descending. Daily groups stay newest-first; the chosen sort applies **within each day**. Equal values use entry ID as a stable tie-breaker. Filters and sorting persist while navigating or refreshing after changes.
- Daily and weekly totals show completed duration for the filtered entries. The footer also shows this week's entry count and the total matching count across the database.

### System Tray
- Minimize-to-tray on close.
- **File → Exit completely (including tray)** shuts down the whole app and removes its tray icon; the menu tooltip explains the difference from closing the main window with X.
- The app icon uses an edge-to-edge opaque indigo background with a large white stopwatch face, dark rim, hour marks, and contrasting hands, inspired by the old app. All nine icon sizes (16–256 pixels) have no outer padding.
- Tray icon turns bright red while tracking, lime green while idle; the circle fills the available Windows tray icon canvas without outer padding.
- Left-click opens a compact popup with elapsed time and start/stop control.
- The popup opens beside the clicked tray area on that monitor, stays within its work area, and **Open App** restores the main window.
- Right-click menu for Open / Exit.
- **Launch at startup is disabled (AR-039):** its File/tray controls and registry access are commented out and excluded from all builds while investigating Apex One detections. Existing Windows startup registrations are not removed automatically. The `--startup` argument remains supported for existing registrations or manual launches; lock/unlock recording is unchanged.

### Import / Export
- Export first offers an optional local date range (inclusive). Leave the option off for all completed entries, or leave an individual boundary blank for an open-ended range. It uses Start date, falling back to End/Created, independently of list filters and pagination. Running entries are excluded. Only then choose the destination directory/filename in the save-file dialog. Exports are not capped to 10,000 records.
- JSON-based import/export.
- History of every import/export action is recorded and viewable.

### Upload to Jira
- Need a token? The **Create a Jira API token** link beside the token field opens [Atlassian's API token page](https://id.atlassian.com/manage-profile/security/api-tokens) in your default browser. Sign in with the Jira email you enter in TimeTracker, choose **Create API token** (without scopes), and copy the token into the field before saving. This uploader uses organization-domain URLs; scoped tokens require a different API URL, as explained in [Atlassian's token guidance](https://support.atlassian.com/atlassian-account/docs/manage-api-tokens-for-your-atlassian-account/).
- Open **Import/Export → Upload to Jira...**. Enter your Jira organization domain (for example `company.atlassian.net`), account email, and API token. **Save connection settings** stores them in the same database; the token is encrypted with Windows protection for the current Windows user. To replace an expired/revoked token, enter the new token in Connection settings and save. A blank token field keeps the saved token. Moving the database to a different Windows user/computer may require entering a replacement token.
- Choose **both From and To dates** each time you open the window, then **Review entries** to see the count and total duration. Dates are inclusive local calendar dates, using Start, then End/Created when Start is absent. Current list filters and pagination do not affect the upload. Running entries are excluded. Changing dates requires another review.
- **Upload to Jira** creates worklogs directly through the [Jira Cloud REST v3 worklog API](https://developer.atlassian.com/cloud/jira/platform/rest/v3/api-group-issue-worklogs/), without Node.js or an intermediate export file. As in the supplied script, it searches the description from left to right using `([A-Za-z]+-\d+)` and uses only the first matching ticket ID, even when several IDs occur. The full description becomes the comment, and the start timestamp is sent in UTC. Entries below one minute or without a ticket ID appear as failures. Requests run sequentially with a 500 ms delay, retain normal HTTPS certificate validation, and are never automatically retried.
- There is **no duplicate detection or worklog updating**: uploading the same entries again creates additional worklogs. Earlier uploads through the standalone script are not included in this app's history.
- **Upload history** is stored in a separate `JiraUploadHistory` table: account/domain, requested range, upload time, status, successful/failed/not-attempted counts, and the latest successfully uploaded entry date. Partial success does not mean the whole range was uploaded. History survives time-entry deletion; the uploader never changes `TimeEntries`. Incomplete batches remain visible if completion could not be recorded.
- **Stop after current entry** lets the current request finish and lists the remaining entries as not uploaded. Failed/not-attempted entries remain in memory until the next upload or the window closes. Use **Copy failed entries**, or select text directly, to copy their descriptions, local dates/times, durations, and reasons for manual work logging. Network timeouts can have an unknown outcome; the failure text reminds you to check Jira before retrying.
- Daily logs are written **directly in the database directory**, as `yyyy-MM-dd.log`, using `yyyy-MM-dd HH:mm:ss.fff worker [info|warn|error]: message`, including processing and failure summaries. Tokens and authorization values are excluded. The window shows the directory path. These files include entry descriptions and are retained until you remove them.

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

For the proposed Authenticode workflow, see [Release signing plan](docs/RELEASE_SIGNING_PLAN.md). Signing is not yet configured.

Self-contained release for Windows x64:

```cmd
dotnet publish .\TimeTracker.App\TimeTracker.App.csproj -c Release -r win-x64 --self-contained -o .\publish\release\
```


---

## Database Schema & Versioning

The database uses `PRAGMA user_version` for migrations. Current version is **4**.

### Tables

`TimerInterruptions` (schema v3) stores each running entry's latest recovery reason and UTC timestamp, with a cascading reference to `TimeEntries`. Saving a checkpoint does not change the entry's Created/Modified timestamps.

Schema v4 adds `JiraSettings` (one saved domain/email/encrypted token) and `JiraUploadHistory` (batch summaries and UTC timestamps), without altering `TimeEntries` or adding upload flags to entries.

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
