# TimeTracker

A local-first, offline Windows desktop time-tracking application. It runs from the system tray, stores everything in a local SQLite database, and requires no cloud account.

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
- **Fast with large data** — indexed SQLite tables with WAL mode and paged recent-entry loading.
- **Lock/unlock awareness** — optionally records Windows session lock/unlock events to help spot forgotten timers.
- **Flexible duration input** — type `34`, `33:33`, `:33`, `1:02:30`, or `5.3` depending on what is fastest for you.
- **Single instance** — launching the app a second time simply activates the existing window.
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

### Timer
- Start/stop a running timer.
- Live elapsed-time display in the main window title, status bar, taskbar progress, and tray popup.
- Modify the running timer description from the tray popup.
- Default timer description is configurable in-app (default: `Untitled task`).

### Manual Entries
- Add entries by description, date, start/end times, and duration.
- Duration box and start/end time boxes auto-sync on focus loss.
- Edit existing entries inline by selecting them from the list.

### Entry List
- Grouped by date with human-readable headers: *Today*, *Yesterday*, weekday names, or full date.
- Daily total duration shown in each group header.
- Sorted newest-first.
- Start a new timer pre-filled with an existing description.

### System Tray
- Minimize-to-tray on close.
- Tray icon turns red while tracking, green while idle.
- Left-click opens a compact popup with elapsed time and start/stop control.
- Right-click menu for Open / Exit.

### Import / Export
- JSON-based import/export.
- History of every import/export action is recorded and viewable.

### Screen Lock / Unlock History
- Records Windows session lock/unlock events while the app is running.
- Separate screen to review these events.

### Single Instance
- Uses a per-user named mutex and `EventWaitHandle` so a second launch activates the first instance.


---

## Configuration

`appsettings.json` (copied to output):

```json
{
  "Storage": {
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
| `Storage` | `DatabaseDirectory` | Folder for the SQLite file (relative to app base if not rooted) |
| `Storage` | `DatabaseFileName` | SQLite file name |
| `Hotkeys` | `Enabled` | Not yet wired up |
| `Hotkeys` | `StartStop` | Planned global hotkey |
| `Ui` | `ScaleMultiplier` | Font/control scale (clamped 0.75–2.5) |
| `Timer` | `DefaultDescription` | Fallback description for empty timer starts |

### Command-line arguments

| Argument | Description |
|---|---|
| `--db-path <path>` | Use an alternative SQLite database file. Overrides the `Storage` configuration. Useful for isolated UI tests. |

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

The database uses `PRAGMA user_version` for migrations. Current version is **2**.

### Tables

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
- Week-wise pagination is not implemented.
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

