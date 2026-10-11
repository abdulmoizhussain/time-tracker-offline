# TimeTracker — Rewrite Specification

This document captures everything needed to rebuild the TimeTracker application from scratch using the same technologies and behaviour. It describes the currently implemented system only; future enhancements are tracked separately in `TODO.md`.

---

## 1. Purpose & Scope

TimeTracker is a single-user, offline-first Windows desktop time-tracking application. All data is stored locally in SQLite. The app runs from the system tray, supports live timers, manual time entries, import/export, and records Windows lock/unlock session events.

This specification covers:

- Exact technology choices and package versions
- Project and solution layout
- Architecture and MVVM patterns
- Data model, schema, and repository behaviour
- Application startup, single-instance enforcement, and DI
- Every implemented feature and its flow
- UI layout and interaction rules
- Configuration file format
- Build and publish commands
- Asset generation (app icon)
- Edge cases and behavioural notes

---

## 2. Technology Stack

Use the following stack for the rewrite. Versions should match unless a newer patch is unavoidable.

| Layer | Technology | Version / Notes |
|---|---|---|
| Language | C# 12 | ImplicitUsings enabled, Nullable enabled |
| UI Framework | WPF | `UseWPF=true` |
| System Tray | Windows Forms | `UseWindowsForms=true`, used only for `NotifyIcon` |
| Target Framework | .NET 8 | `net8.0-windows` for the app; `net8.0` for class libraries |
| MVVM | CommunityToolkit.Mvvm | `8.4.2` |
| DI / Hosting | Microsoft.Extensions.Hosting | `10.0.7` |
| Configuration | Microsoft.Extensions.Configuration.Json | `10.0.7` |
| Configuration Binder | Microsoft.Extensions.Configuration.Binder | `10.0.7` |
| Database | SQLite | `Microsoft.Data.Sqlite` `10.0.7` |
| ORM / Micro-ORM | Dapper | `2.1.72` |
| Drawing | System.Drawing.Common | `10.0.0` |

### Solution Layout

```
TimeTracker/
├── TimeTracker.sln
├── TimeTracker.App/              # WPF executable
│   ├── Assets/
│   │   └── TimeTracker.ico
│   ├── Converters/
│   ├── Helpers/
│   ├── ViewModels/
│   ├── appsettings.json
│   ├── App.xaml / App.xaml.cs
│   ├── MainWindow.xaml / .xaml.cs
│   ├── AboutWindow.xaml / .xaml.cs
│   ├── DefaultTimerTextWindow.xaml / .xaml.cs
│   ├── ImportExportHistoryWindow.xaml / .xaml.cs
│   ├── ScreenEventsWindow.xaml / .xaml.cs
│   ├── TrayPopupWindow.xaml / .xaml.cs
│   └── TimeTracker.App.csproj
├── TimeTracker.Core/             # Models, contracts, options
│   ├── Configuration/
│   │   └── AppRuntimeOptions.cs
│   ├── Contracts/
│   │   ├── ITimeEntryRepository.cs
│   │   └── IScreenSessionEventRepository.cs
│   └── Models/
│       ├── TimeEntry.cs
│       ├── ScreenSessionEvent.cs
│       └── ImportExportHistory.cs
├── TimeTracker.Data/             # SQLite implementation
│   ├── SqliteConnectionFactory.cs
│   └── TimeEntryRepository.cs
└── tools/
    └── IconGenerator/
        ├── Program.cs
        └── IconGenerator.csproj
```

### Assembly & EXE Names

- Project name: `TimeTracker.App`
- Assembly name: `TimeTracker`
- Output EXE: `TimeTracker.exe`
- Application icon: `Assets/TimeTracker.ico`
- Application manifest: `app.manifest`

---

## 3. Architecture & Design Patterns

### MVVM

- Views are WPF `Window` / `UserControl` classes with minimal code-behind.
- ViewModels use `CommunityToolkit.Mvvm`:
  - `[ObservableProperty]` for bindable fields.
  - `[RelayCommand]` for commands.
- ViewModels are registered as singletons in DI and assigned to `Window.DataContext` during construction.

### Layered Dependency Flow

```
TimeTracker.App (UI + ViewModels)
        ↓
TimeTracker.Data (SqliteConnectionFactory, TimeEntryRepository)
        ↓
TimeTracker.Core (Models + Contracts + Options)
```

- `TimeTracker.Core` has **no** external package references.
- `TimeTracker.Data` references `TimeTracker.Core` only.
- `TimeTracker.App` references both `Core` and `Data`.

### Repository Pattern

- A single concrete repository, `TimeEntryRepository`, implements both `ITimeEntryRepository` and `IScreenSessionEventRepository`.
- It is registered twice in DI:
  - `services.AddSingleton<TimeEntryRepository>()`
  - `services.AddSingleton<ITimeEntryRepository>(...)`
  - `services.AddSingleton<IScreenSessionEventRepository>(...)`
- All repository methods are async and accept `CancellationToken`.
- All database dates are stored as UTC (`DateTime` with `DateTimeKind.Utc` or converted before persistence).

### Configuration

- `appsettings.json` is bundled and copied to the output directory (`PreserveNewest`).
- It is bound to `AppRuntimeOptions` at startup.
- User-specific default timer description is stored separately in `%LocalAppData%\TimeTracker\user-settings.json`.

---

## 4. Data Model & Storage

### Database Connection

Use a `SqliteConnectionFactory` that builds a connection string from a file path:

```csharp
public sealed class SqliteConnectionFactory
{
    public string DatabasePath { get; }
    public string ConnectionString { get; }

    public SqliteConnectionFactory(string databasePath)
    {
        DatabasePath = databasePath;
        ConnectionString = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared
        }.ToString();
    }

    public SqliteConnection CreateConnection() => new(ConnectionString);
}
```

Connection string flags: `ReadWriteCreate`, `Shared` cache.

### Pragmas Applied on Initialize

```sql
PRAGMA journal_mode=WAL;
PRAGMA synchronous=NORMAL;
PRAGMA foreign_keys=ON;
```

### Schema Versioning

Use `PRAGMA user_version` for migrations. Current version is **2**.

On `InitializeAsync`:
1. Create the database directory if it does not exist.
2. Open the connection and apply pragmas.
3. Read `user_version`.
4. If version < 1, create schema v1 and set version to 1.
5. If version < 2, create schema v2 and set version to 2.

### Schema V1

```sql
CREATE TABLE IF NOT EXISTS TimeEntries (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    Description TEXT NOT NULL,
    StartUtc TEXT NULL,
    EndUtc TEXT NULL,
    DurationSeconds INTEGER NOT NULL,
    IsRunning INTEGER NOT NULL,
    CreatedUtc TEXT NOT NULL,
    UpdatedUtc TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS ScreenSessionEvents (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    EventType TEXT NOT NULL,
    OccurredUtc TEXT NOT NULL,
    CreatedUtc TEXT NOT NULL
);

CREATE INDEX IF NOT EXISTS IX_TimeEntries_CreatedUtc ON TimeEntries(CreatedUtc DESC);
CREATE INDEX IF NOT EXISTS IX_TimeEntries_IsRunning ON TimeEntries(IsRunning);
CREATE INDEX IF NOT EXISTS IX_TimeEntries_StartUtc_EndUtc ON TimeEntries(StartUtc, EndUtc);
CREATE INDEX IF NOT EXISTS IX_ScreenSessionEvents_OccurredUtc ON ScreenSessionEvents(OccurredUtc DESC);
```

### Schema V2

```sql
CREATE TABLE IF NOT EXISTS ImportExportHistory (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    ActionType TEXT NOT NULL,
    ItemCount INTEGER NOT NULL,
    FirstEntryDescription TEXT NULL,
    FirstEntryStartUtc TEXT NULL,
    LastEntryDescription TEXT NULL,
    LastEntryStartUtc TEXT NULL,
    FilePath TEXT NULL,
    Notes TEXT NULL,
    OccurredUtc TEXT NOT NULL,
    CreatedUtc TEXT NOT NULL
);

CREATE INDEX IF NOT EXISTS IX_ImportExportHistory_OccurredUtc ON ImportExportHistory(OccurredUtc DESC);
```


### Domain Models

#### TimeEntry

```csharp
public sealed class TimeEntry
{
    public long Id { get; set; }
    public string Description { get; set; } = string.Empty;
    public DateTime? StartUtc { get; set; }
    public DateTime? EndUtc { get; set; }
    public int DurationSeconds { get; set; }
    public bool IsRunning { get; set; }
    public DateTime CreatedUtc { get; set; }
    public DateTime UpdatedUtc { get; set; }
}
```

#### ScreenSessionEvent

```csharp
public sealed class ScreenSessionEvent
{
    public long Id { get; set; }
    public string EventType { get; set; } = string.Empty;
    public DateTime OccurredUtc { get; set; }
    public DateTime CreatedUtc { get; set; }
}
```

#### ImportExportHistory

```csharp
public sealed class ImportExportHistory
{
    public long Id { get; set; }
    public string ActionType { get; set; } = string.Empty;
    public int ItemCount { get; set; }
    public string? FirstEntryDescription { get; set; }
    public DateTime? FirstEntryStartUtc { get; set; }
    public string? LastEntryDescription { get; set; }
    public DateTime? LastEntryStartUtc { get; set; }
    public string? FilePath { get; set; }
    public string? Notes { get; set; }
    public DateTime OccurredUtc { get; set; }
    public DateTime CreatedUtc { get; set; }
}
```

### Repository Contracts

```csharp
public interface ITimeEntryRepository
{
    Task InitializeAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TimeEntry>> GetAllEntriesAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TimeEntry>> GetRecentEntriesAsync(int take = 200, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<string>> SearchDescriptionsAsync(string query, int take = 8, CancellationToken cancellationToken = default);
    Task<TimeEntry?> GetRunningEntryAsync(CancellationToken cancellationToken = default);
    Task<long> AddDurationEntryAsync(string description, TimeSpan duration, CancellationToken cancellationToken = default);
    Task<long> AddManualRangeEntryAsync(string description, DateTime startLocal, DateTime endLocal, CancellationToken cancellationToken = default);
    Task<long> StartTimerAsync(string description, DateTime startUtc, CancellationToken cancellationToken = default);
    Task StopTimerAsync(long id, DateTime endUtc, CancellationToken cancellationToken = default);
    Task UpdateEntryAsync(long id, string description, DateTime? startUtc, DateTime? endUtc, int durationSeconds, CancellationToken cancellationToken = default);
    Task DeleteEntryAsync(long id, CancellationToken cancellationToken = default);
    Task ClearAllDataAsync(CancellationToken cancellationToken = default);
    Task AddImportExportHistoryAsync(ImportExportHistory history, CancellationToken cancellationToken = default);
    Task<ImportExportHistory?> GetLatestImportExportHistoryAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ImportExportHistory>> GetRecentImportExportHistoryAsync(int take = 200, CancellationToken cancellationToken = default);
    Task<int> GetDatabaseVersionAsync(CancellationToken cancellationToken = default);
}

public interface IScreenSessionEventRepository
{
    Task<IReadOnlyList<ScreenSessionEvent>> GetRecentScreenSessionEventsAsync(int take = 500, CancellationToken cancellationToken = default);
    Task AddScreenSessionEventAsync(string eventType, DateTime occurredUtc, CancellationToken cancellationToken = default);
}
```


### Repository Behaviours

- **StartTimerAsync**: inserts a new row with `IsRunning = 1`, `DurationSeconds = 0`, `StartUtc = now`, `EndUtc = null`.
- **StopTimerAsync**: updates the running row with `EndUtc = now`, computes `DurationSeconds` from start/end, sets `IsRunning = 0`.
- **GetRunningEntryAsync**: returns the single row where `IsRunning = 1`, or null.
- **AddDurationEntryAsync**: inserts a completed entry for "today" using only a duration. Start and end are computed from the duration ending at the current local time.
- **AddManualRangeEntryAsync**: inserts a completed entry with explicit local start/end. It is converted to UTC before storage; `DurationSeconds` is computed from the local range.
- **UpdateEntryAsync**: full replacement update of description, start, end, and duration.
- **DeleteEntryAsync**: deletes by `Id`.
- **ClearAllDataAsync**: deletes all rows from `TimeEntries`, `ScreenSessionEvents`, and `ImportExportHistory`.
- **SearchDescriptionsAsync**: case-insensitive `LIKE` search on `Description`, distinct, limited by `take`.
- **GetRecentEntriesAsync**: returns newest `take` entries ordered by `CreatedUtc DESC`.
- **GetRecentImportExportHistoryAsync**: returns newest `take` rows ordered by `OccurredUtc DESC`.
- **GetDatabaseVersionAsync**: returns `PRAGMA user_version`.

All Dapper calls use `CommandDefinition` with the supplied `CancellationToken`.


---

## 5. Application Startup, Single Instance & DI

### Startup Sequence (`App.OnStartup`)

1. Call `base.OnStartup(e)`.
2. Attempt to acquire the single-instance mutex.
   - If another instance is running, signal it to activate and shutdown immediately.
3. Start the activation listener (`EventWaitHandle`).
4. Load `appsettings.json` into `AppRuntimeOptions`.
5. Apply UI scaling (see below).
6. Build the database path from `StorageOptions`.
7. Build the `IHost` and register services.
8. Start the host.
9. Call `ITimeEntryRepository.InitializeAsync()` to create/migrate the DB.
10. Call `MainViewModel.InitializeAsync(dbPath)` to load entries and state.
11. Show `MainWindow`.

### Single-Instance Mechanism

Use a per-user named mutex and named auto-reset event:

- Mutex name: `Local\\TimeTracker.SingleInstance.{userSid}`
- Event name: `Local\\TimeTracker.Activate.{userSid}`

When a second instance starts:
- It fails to acquire the mutex.
- It opens the existing event and sets it.
- It calls `Shutdown()` and exits.

The first instance registers a `ThreadPool.RegisterWaitForSingleObject` on the event. When signalled, it uses the dispatcher to activate/show the main window.

### DI Registration

```csharp
services.AddSingleton(runtimeOptions);
services.AddSingleton(new SqliteConnectionFactory(dbPath));
services.AddSingleton<TimeEntryRepository>();
services.AddSingleton<ITimeEntryRepository>(p => p.GetRequiredService<TimeEntryRepository>());
services.AddSingleton<IScreenSessionEventRepository>(p => p.GetRequiredService<TimeEntryRepository>());
services.AddSingleton<MainViewModel>();
services.AddSingleton<ScreenEventsViewModel>();
services.AddSingleton<ImportExportHistoryViewModel>();
services.AddSingleton<ScreenEventsWindow>(p => new ScreenEventsWindow
{
    DataContext = p.GetRequiredService<ScreenEventsViewModel>()
});
services.AddSingleton<ImportExportHistoryWindow>(p => new ImportExportHistoryWindow
{
    DataContext = p.GetRequiredService<ImportExportHistoryViewModel>()
});
services.AddSingleton<MainWindow>(p =>
{
    var window = new MainWindow(
        p.GetRequiredService<ScreenEventsWindow>(),
        p.GetRequiredService<ImportExportHistoryWindow>())
    {
        DataContext = p.GetRequiredService<MainViewModel>()
    };
    return window;
});
```

### UI Scaling

- Read `Ui.ScaleMultiplier` from config.
- Clamp to `[0.75, 2.5]`; default invalid values to `1.0`.
- Store `UiScaleMultiplier` and `BaseFontSize` (`15 * multiplier`) in application resources.
- Apply a `ScaleTransform(multiplier, multiplier)` to all `Window` instances via a typed style.
- TextBox, Button, DatePicker, ListViewItem, GridViewColumnHeader, MenuItem styles should bind font sizes to `BaseFontSize`.

### Exit Cleanup (`App.OnExit`)

- Unregister the activation wait handle.
- Dispose the activation event.
- Stop the host and dispose it.
- Release the single-instance mutex.


---

## 6. Implemented Features

### 6.1 Live Timer

#### UI Controls

- **Timer description TextBox** — default placeholder is the configured default timer text.
- **Start/Stop button** — toggles the timer.
- **Running status TextBlock** — shows `No running timer.` or `Running: hh:mm:ss – {Description}`.

#### Flow: Start Timer

1. User enters a description (may be empty).
2. `StartTimerCommand` executes.
3. If description is empty/whitespace, use `DefaultTimerDescription`.
4. Call `ITimeEntryRepository.StartTimerAsync(description, DateTime.UtcNow)`.
5. Store the returned `Id` and start time in the ViewModel.
6. Start a `DispatcherTimer` that ticks every second to update `RunningElapsed` and the UI.
7. Refresh the entries list and update the window title, taskbar progress, and tray icon state.

#### Flow: Stop Timer

1. `StopTimerCommand` executes.
2. Call `ITimeEntryRepository.StopTimerAsync(runningId, DateTime.UtcNow)`.
3. Clear the running timer state.
4. Stop the `DispatcherTimer`.
5. Refresh the entries list and UI indicators.

#### Running UI Updates

- `RunningElapsed` is computed as `UtcNow - _runningStartUtc`.
- `WindowTitle` toggles between the base title and `Running: hh:mm:ss – {Description}`.
- `TaskbarProgressState` is `Normal` while running; `None` when idle. `TaskbarProgressValue` pulses while running.
- Tray icon switches to red while running, green while idle.

#### Modifying Running Description

- The tray popup allows editing the running description.
- `MainViewModel` exposes `UpdateRunningDescriptionAsync(string newDescription)`.
- It updates the database row via `UpdateEntryAsync` with the current start/end/duration but new description.
- If the timer is no longer running, the update is skipped.

---

### 6.2 Manual Entry

#### UI Controls

- **Description TextBox**
- **Duration TextBox** — default `00:00:00`
- **Start Date DatePicker**
- **Start Time TextBox** — default `00:00:00`
- **End Time TextBox** — default `00:00:00`
- **+ Add / Save button** — label switches based on edit mode

#### Flow: Add Manual Entry by Duration

1. User fills description and duration.
2. `AddDurationEntryCommand` executes.
3. Parse the duration using `DurationParser.TryParse`.
4. Reject zero or negative durations.
5. Call `ITimeEntryRepository.AddDurationEntryAsync(description, duration)`.
6. Refresh entries list and clear the manual input fields.

#### Flow: Add Manual Entry by Start/End Range

1. User fills description, start date, start time, and end time.
2. `AddManualRangeEntryCommand` executes.
3. Combine date+time into local `DateTime` values.
4. Validate `startLocal < endLocal`.
5. Call `ITimeEntryRepository.AddManualRangeEntryAsync(description, startLocal, endLocal)`.
6. Refresh entries list and clear inputs.

#### Duration Parser Behaviour

`DurationParser.TryParse(input, out TimeSpan)` supports:

| Input | Interpretation |
|---|---|
| `34` | 34 minutes, 0 seconds |
| `33:33` | 33 minutes, 33 seconds |
| `33:` | 33 minutes, 0 seconds |
| `:33` | 0 minutes, 33 seconds |
| `1:02:30` | 1 hour, 2 minutes, 30 seconds |
| `5.3` / `1.0` | 5.3 / 1.0 hours (decimal hours) |

Invalid or empty input returns `false`.

#### Start/End/Duration Sync (Edit Mode)

When editing an existing entry:

- Losing focus from the **duration box** updates the end time to `start + duration`.
- Losing focus from the **start time box** ensures end is not older than start, then recalculates duration.
- Losing focus from the **end time box** ensures end is not older than start, then recalculates duration.
- If the adjusted end crosses midnight, the change is rejected.

#### Edit Existing Entry

1. User selects an entry from the list and clicks the edit action.
2. `MainViewModel` loads `EditingEntryId`, description, date, start/end times, and duration into the manual entry form.
3. The Add button label changes to `Save`.
4. On save, call `UpdateEntryAsync` with the edited values and clear edit mode.

#### Delete Entry

- `DeleteEntryCommand` accepts an `EntryRowViewModel`.
- Show a confirmation `MessageBox`.
- If confirmed, call `DeleteEntryAsync` and refresh.

#### Clear All Data

- Menu item **Edit → Clear All Data**.
- Show a confirmation `MessageBox`.
- If confirmed, call `ClearAllDataAsync` and refresh.


---

### 6.3 Entry List

#### ViewModel

```csharp
public sealed class EntryRowViewModel
{
    public long Id { get; init; }
    public string Description { get; init; } = string.Empty;
    public string Start { get; init; } = "-";
    public string End { get; init; } = "-";
    public string Duration { get; init; } = "00:00:00";
    public int DurationSeconds { get; init; }
    public string State { get; init; } = "Stopped";
    public DateTime? StartRaw { get; init; }
    public DateTime? EndRaw { get; init; }
    public DateTime GroupDate { get; init; }
    public DateTime SortDateTime { get; init; }
    public DateTime CreatedSortDateTime { get; init; }
}
```

#### Display Rules

- `Start` / `End` are formatted as `yyyy-MM-dd HH:mm:ss` in local time, or `-` if null.
- `Duration` is formatted as `hh:mm:ss`.
- `GroupDate` is the local date portion of `StartUtc` (or `CreatedUtc` if start is null).
- Entries are grouped by `GroupDate` in the UI.
- Sort order within the grouped `CollectionViewSource`: `GroupDate DESC`, then `CreatedSortDateTime DESC`.

#### Group Header

- Use a `DateGroupLabelConverter` to produce labels:
  - Today → "Today"
  - Yesterday → "Yesterday"
  - Within last 6 days → weekday name (e.g. "Monday")
  - Older → "ddd, MMM d, yyyy"
- Use an `EntryGroupHeaderConverter` to append the day's total duration: `{Label} – {hours} h {minutes} min`.

#### Start Timer from Entry

- Each row has a small play button.
- If no timer is running, it starts a new timer with the row's description.
- If a timer is already running, the action is a no-op (button can remain enabled but command checks state).

---

### 6.4 Description Suggestions

- Two suggestion lists exist: one for the timer description box, one for the manual description box.
- When the user types in either description box, debounce and call `SearchDescriptionsAsync(query, take: 8)`.
- Suggestions are displayed below the relevant text box.
- Selecting a suggestion fills the text box and hides suggestions.
- Suggestions are hidden on lost focus (with a small delay to allow click selection).


---

### 6.5 Import / Export

#### Export

1. Menu: **Import/Export → Export...**.
2. Open a `SaveFileDialog` filtering for `.json`.
3. Load all entries via `GetAllEntriesAsync`.
4. Sort descending by `StartUtc`.
5. Map each entry to:
   ```json
   {
     "Description": "...",
     "StartDate": "yyyy-MM-dd",
     "StartTime": "HH:mm:ss",
     "EndDate": "yyyy-MM-dd",
     "EndTime": "HH:mm:ss",
     "Duration": "hh:mm:ss"
   }
   ```
6. Wrap in `{ "TimeEntries": [...] }`.
7. Write JSON to the selected path with indented formatting.
8. Record an `ImportExportHistory` row with:
   - `ActionType = "Export"`
   - `ItemCount`
   - `FirstEntryDescription` / `FirstEntryStartUtc`
   - `LastEntryDescription` / `LastEntryStartUtc`
   - `FilePath`
   - `OccurredUtc = UtcNow`

#### Import

1. Menu: **Import/Export → Import...**.
2. Open an `OpenFileDialog` filtering for `.json`.
3. Deserialize the JSON envelope.
4. For each entry, parse dates/times and duration in local time, compute `StartUtc`/`EndUtc` and `DurationSeconds`.
5. Insert each entry as a completed entry.
6. Record an `ImportExportHistory` row with:
   - `ActionType = "Import"`
   - `ItemCount`
   - `FilePath`
   - `OccurredUtc = UtcNow`

#### Import/Export History Window

- Menu: **Import/Export → Import/Export History**.
- Shows a grouped list of history rows by date.
- Columns: ID, Time, Action, Count, First Entry Start, Last Entry Start, File, Notes.
- Refresh button reloads from `GetRecentImportExportHistoryAsync`.

---

### 6.6 Screen Lock / Unlock History

- Subscribe to `SystemEvents.SessionSwitch` in `MainViewModel`.
- On `SessionSwitchReason.SessionLock`, record `EventType = "Lock"`.
- On `SessionSwitchReason.SessionUnlock`, record `EventType = "Unlock"`.
- Store `OccurredUtc = DateTime.UtcNow`.
- Menu **View → Show/Hide Lock/Unlock History** opens the `ScreenEventsWindow`.
- Window shows recent events grouped by date with columns: ID, Time, Event.
- Refresh button reloads from `GetRecentScreenSessionEventsAsync`.
- Window hides on close (not closes) unless the app is exiting.


---

### 6.7 System Tray

#### Tray Icon

- Created in `MainWindow` constructor.
- Idle icon: vivid green circle.
- Tracking icon: vivid red circle.
- Icons are rendered at `SystemInformation.SmallIconSize.Width` using `System.Drawing` with anti-aliasing and a subtle dark outline.
- Tooltip text: `"Time Tracker - Idle"` / `"Time Tracker - Tracking"`.

#### Context Menu

- **Open** — shows and activates the main window.
- **Exit** — closes the application.

#### Click Behaviour

- Double-click → show main window.
- Single left-click → show the tray popup window near the tray icon.

#### Tray Popup Window

- Borderless, topmost, drop-shadowed dark popup.
- Shows elapsed time, status label, and running description.
- Allows editing the running description.
- **Start/Stop** button toggles the timer.
- **Open App** button activates the main window.
- Hides when the popup loses focus or the close button is clicked.

#### Window State

- When the main window is minimized, hide it (do not show in taskbar).
- Closing the main window minimizes to tray instead of exiting (unless explicitly exiting).

---

### 6.8 Default Timer Text Setting

- Menu: **Edit → Default Timer Text...**.
- Opens `DefaultTimerTextWindow` with a single text box and Save/Close buttons.
- Text box is bound to `MainViewModel.DefaultTimerDescription`.
- On save, write the value to `%LocalAppData%\TimeTracker\user-settings.json`:
  ```json
  { "DefaultTimerDescription": "..." }
  ```
- On startup, load this value; if missing or empty, fall back to `appsettings.json:Timer.DefaultDescription`, then to `"Untitled task"`.

---

### 6.9 About Window

- Menu: **Help → About**.
- Shows app name, short description, app version, and database version.
- App version is read from `Assembly.GetEntryAssembly().GetName().Version`.
- Database version should be read from `ITimeEntryRepository.GetDatabaseVersionAsync()` (currently the UI hard-codes "Unknown" but the repository supports it).


---

## 7. UI Specification

### 7.1 MainWindow

- Title: bound to `WindowTitle`.
- Default size: 1100×650; minimum 960×550.
- Icon: `Assets/TimeTracker.ico`.
- Taskbar progress: bound to `TaskbarProgressState` and `TaskbarProgressValue`.

#### Layout (rows)

1. **Menu bar**
   - `_File` → `E_xit`
   - `_Edit` → `Default _Timer Text...`, separator, `_Clear All Data`
   - `_View` → `Show/Hide Lock/Unlock History`
   - `_Import/Export` → `_Import...`, `_Export...`, separator, `Import/Export _History`
   - `_Help` → `_About`

2. **Status header**
   - Application name `Offline Time Tracker`.
   - Running status text below it.

3. **Timer section**
   - Description TextBox with autocomplete dropdown.
   - Start/Stop button.

4. **Manual entry section**
   - Description TextBox with autocomplete dropdown.
   - Duration TextBox.
   - Start Date DatePicker.
   - Start Time TextBox.
   - End Time TextBox.
   - Add/Save button.

5. **Entries list**
   - Grouped `ListView` with `GridView` columns:
     - ID (50)
     - ▶ play button (55)
     - Description (320)
     - Start (160, centered)
     - End (160, centered)
     - Duration (120, centered)

6. **Footer**
   - Read-only TextBox showing the database path (`DatabasePathText`).

#### Resources

- `BoolToVisibilityConverter`
- `DateGroupLabelConverter`
- `EntryGroupHeaderConverter`
- `RunningForegroundConverter`
- `GroupedEntries` `CollectionViewSource` with sort/group on `GroupDate`.

### 7.2 Tray Popup Window

- Width: 280, size-to-content height.
- Borderless, no resize, no taskbar icon, transparent background, allows transparency.
- Dark rounded card with drop shadow.
- Header: app name + close button.
- Elapsed time in large font.
- Status dot and label.
- Running description (editable on double-click).
- Bottom row: Start/Stop pill button and Open App pill button.

### 7.3 ScreenEventsWindow

- Title: `Screen Lock/Unlock History`.
- Size: 760×560; minimum 620×420.
- Header with title, description, and Refresh button.
- Grouped `ListView` with columns: ID, Time, Event.
- Footer summary text.

### 7.4 ImportExportHistoryWindow

- Title: `Import/Export History`.
- Size: 980×560; minimum 760×420.
- Header with title, description, and Refresh button.
- Grouped `ListView` with columns: ID, Time, Action, Count, First Entry Start, Last Entry Start, File, Notes.
- Footer summary text.

### 7.5 AboutWindow

- Title: `About Time Tracker`.
- Size: 420×250; non-resizable.
- Centered on owner.
- Shows app name, description, app version, database version.

### 7.6 DefaultTimerTextWindow

- Title: `Default Timer Text`.
- Size: 520×190; non-resizable.
- Centered on owner.
- Single text box bound to `DefaultTimerDescription`.
- Save and Close buttons.

---

## 8. Converters

### DateGroupLabelConverter

Converts a `DateTime` date to a label:
- Today → "Today"
- Yesterday → "Yesterday"
- Within last 6 days → weekday name
- Older → `ddd, MMM d, yyyy`

### EntryGroupHeaderConverter

Combines `DateGroupLabelConverter` with the sum of `DurationSeconds` in the group, formatted as `{hours} h {minutes} min`.

### RunningForegroundConverter

Returns a dark-red brush for `true` (running) and a dark-green brush for `false` (idle).


---

## 9. Configuration

### appsettings.json

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

### AppRuntimeOptions

```csharp
public sealed class AppRuntimeOptions
{
    public StorageOptions Storage { get; set; } = new();
    public HotkeyOptions Hotkeys { get; set; } = new();
    public SessionEventTrackingOptions SessionEvents { get; set; } = new();
    public UiOptions Ui { get; set; } = new();
    public TimerOptions Timer { get; set; } = new();
}

public sealed class StorageOptions
{
    public string DatabaseDirectory { get; set; } = "Data";
    public string DatabaseFileName { get; set; } = "timetracker.db";
}

public sealed class HotkeyOptions
{
    public bool Enabled { get; set; }
    public string StartStop { get; set; } = "Ctrl+Shift+T";
}

public sealed class SessionEventTrackingOptions
{
    public bool Enabled { get; set; } = true;
}

public sealed class UiOptions
{
    public double ScaleMultiplier { get; set; } = 1.0;
}

public sealed class TimerOptions
{
    public string DefaultDescription { get; set; } = "Untitled task";
}
```

### Database Path Resolution

1. Read `Storage.DatabaseDirectory` and `Storage.DatabaseFileName`.
2. If the directory is not rooted, combine it with `AppContext.BaseDirectory`.
3. Ensure the directory exists before opening the connection.

---

## 10. Build & Publish

### Build

```cmd
dotnet build TimeTracker.sln
```

### Run

```cmd
dotnet run --project .\TimeTracker.App\TimeTracker.App.csproj
```

### Publish (self-contained x64)

```cmd
dotnet publish .\TimeTracker.App\TimeTracker.App.csproj -c Release -r win-x64 --self-contained -o .\publish\release\
```

---

## 11. Assets & Tooling

### IconGenerator

- Located in `tools/IconGenerator`.
- Target: `net8.0-windows` with `UseWindowsForms=true`.
- Generates `Assets/TimeTracker.ico` containing PNG images at sizes 16, 20, 24, 32, 40, 48, 64, 128, 256.
- The icon is a stopwatch-style illustration drawn with `System.Drawing`.
- Run without arguments to output to the default app asset path.

---

## 12. Testing Approach

There is no test project in the current codebase. For a rewrite, recommended tests include:

- `DurationParser` tests for every supported format and invalid input.
- Repository tests using an in-memory SQLite database.
- ViewModel command tests where feasible.

---

## 13. Edge Cases & Behavioural Notes

- **Single instance**: if the mutex already exists, the second instance signals activation and exits before showing any UI.
- **Running timer across days**: the elapsed label simply counts up; there is no automatic day rollover.
- **Manual entry crossing midnight**: if adjusting duration causes end time to exceed 24:00:00, the change is rejected.
- **Empty description**: timer starts with the configured default description.
- **Lost focus suggestion handling**: a short delay is used so a mouse click on a suggestion can register before the lost-focus handler clears the list.
- **Tray popup lifecycle**: created on first tray click; disposed on app exit.
- **Window close vs exit**: closing the main window hides it to the tray. Exit is available only via the tray context menu or File menu.
- **Database path display**: shown in the footer as the resolved absolute path.
- **Date storage**: all database dates are UTC; UI displays local time.
- **Import/export history**: always records the action even if the file operation partially succeeds/fails; wrap in try/catch and show user feedback.

---

## 14. Migration Notes for Rewrite

When rebuilding from scratch:

1. Keep the same solution/project names and assembly output name.
2. Keep the same SQLite schema and versioning so existing `.db` files remain compatible.
3. Preserve the `ImportExportHistory` and `ScreenSessionEvents` tables.
4. Keep the `appsettings.json` shape identical for drop-in replacement.
5. The `%LocalAppData%\TimeTracker\user-settings.json` path should stay the same so user default timer text is preserved.
6. Maintain the same import/export JSON shape for backward compatibility.
7. Keep the tray icon colour semantics: green = idle, red = tracking.

