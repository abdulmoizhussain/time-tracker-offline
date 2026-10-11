# Testing Guide

TimeTracker has two test projects:

- `TimeTracker.Tests` — fast, in-process unit / integration tests.
- `TimeTracker.UITests` — end-to-end smoke tests that launch the real WPF app and drive it through Windows UI Automation.

---

## `TimeTracker.Tests`

### What it covers

| Test class | Purpose |
|---|---|
| `DurationParserTests` | Validates every supported duration format and rejects invalid input. |
| `TimeEntryRepositoryTests` | Runs the SQLite repository against a temporary on-disk database and verifies schema creation, start/stop timer, manual duration entry, and manual range entry behaviour. |

### Running the tests

```cmd
cd c:\Users\Abdul\Documents\dev\personal\TimeTracker-2.0
dotnet test TimeTracker.Tests -c Debug
```

You can also run them without rebuilding:

```cmd
dotnet test TimeTracker.Tests -c Debug --no-build
```

### Implementation notes

- Uses **xUnit**.
- Uses the real `SqliteConnectionFactory` + `TimeEntryRepository` with a temporary `.db` file created in `%TEMP%`.
- Each test class cleans up its database file in `Dispose()`.
- `ConfigureAwait(false)` is respected in repository code because the tests await the async repository methods.

### Adding new tests

1. Add a new class under `TimeTracker.Tests/`.
2. For repository tests, create a temp DB path in the constructor and dispose it in `IDisposable.Dispose`.
3. For parser/helper tests, prefer `[Theory]`/`[InlineData]` to cover edge cases.

---

## `TimeTracker.UITests`

### What it covers

| Test | Behaviour exercised |
|---|---|
| `App_Launches_MainWindowVisible` | Verifies the app starts and the main window is enabled. |
| `StartStopTimer_ButtonsWork` | Clicks **Start**, waits, clicks **Stop**, and confirms the app is still alive. |
| `AddDurationEntry_ButtonWorks` | Types a description and duration, clicks **Add**, and confirms the app is still alive. |

### Running the tests

> **Important:** These tests require an interactive Windows session with a real display. They will not run in a headless / CI environment without an unlocked UI session.

```cmd
cd c:\Users\Abdul\Documents\dev\personal\TimeTracker-2.0
dotnet test TimeTracker.UITests -c Debug
```

Prerequisites:

1. `TimeTracker.App` must be built first so that `TimeTracker.App\bin\Debug\net8.0-windows\TimeTracker.exe` exists.
2. The test runner must be able to launch WPF windows (interactive session, not RDP-disconnected).

### How the tests isolate data

Each test launches the app with a unique temp database:

```cmd
TimeTracker.exe --db-path "C:\Users\<user>\AppData\Local\Temp\TimeTracker.UITests.<guid>.db"
```

The `--db-path` argument overrides the directory and file name from `appsettings.json`, so UI tests never touch your production database.

### Implementation notes

- Uses **xUnit**.
- Uses built-in `System.Windows.Automation` (part of .NET Windows Desktop) instead of third-party libraries.
- Each test starts its own `Process`, waits for the main window, finds controls by `AutomationId`, invokes buttons, sets text values, and cleans up the process and temp DB in `Dispose`.

### Adding new UI tests

1. Add an `AutomationProperties.AutomationId` to the control in XAML if it does not already have one.
2. Find the control in the test with `FindByAutomationId`.
3. Use `InvokePattern` for buttons and `ValuePattern` for text boxes.
4. Keep tests independent — each test launches its own app instance.

---

## Running all tests

To run the full suite:

```cmd
dotnet test TimeTracker.sln -c Debug
```

This runs unit tests first, then UI tests. UI tests may take longer because each one launches the real application.

---

## CI / headless environments

- `TimeTracker.Tests` can run in any environment that supports `dotnet test`.
- `TimeTracker.UITests` currently requires an interactive Windows session. For CI, consider using a self-hosted runner with an unlocked desktop session, or exclude the UI test project with:

```cmd
dotnet test TimeTracker.Tests -c Debug
```
