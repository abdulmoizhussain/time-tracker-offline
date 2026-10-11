# Changelog

All notable changes to TimeTracker are documented in this file.

## [Unreleased]

### Fixed

- Fixed `StackOverflowException` at startup by renaming `App.OnStartup` to `OnApplicationStartup` and removing the recursive `base.OnStartup(e)` call.
- Fixed icon/XAML resource load failure by loading `Assets\TimeTracker.ico` from the output folder at runtime instead of through a case-sensitive pack URI.
- Fixed `ListViewItem` style/resource lookup error by removing problematic `BasedOn` style references.
- Fixed tray-popup double-close crash with a `SafeClose()` guard.

### Added

- Global exception logging in `App.xaml.cs` (`DispatcherUnhandledException`, `AppDomain.UnhandledException`, `TaskScheduler.UnobservedTaskException`).
- `--db-path` command-line argument support for isolated UI-test databases.
- `AutomationProperties.AutomationId` values to key `MainWindow` controls:
  - `StartTimerButton`
  - `StopTimerButton`
  - `ManualDescriptionTextBox`
  - `ManualDurationTextBox`
  - `AddDurationButton`
  - `AddRangeButton`

### Test Projects

- Added `TimeTracker.Tests` (xUnit) covering:
  - `DurationParser` for all supported formats and invalid input.
  - `TimeEntryRepository` schema creation, start/stop timer, manual duration, and manual range entries against an isolated SQLite database.
- Added `TimeTracker.UITests` (xUnit + built-in `System.Windows.Automation`) with smoke tests that launch the real compiled `TimeTracker.exe` against a temporary database.
- Added both test projects to `TimeTracker.sln`.

### Build

- Verified full solution builds with `dotnet build TimeTracker.sln -c Debug` (0 errors, 0 warnings).
- Verified `dotnet test TimeTracker.Tests -c Debug` passes (29 tests).

## Earlier Releases

See `docs/TODO.md` and `docs/SPEC.md` for the original feature set and backlog.
