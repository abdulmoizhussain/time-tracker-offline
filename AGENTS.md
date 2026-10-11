# Agent Guidelines

This document helps AI agents and human contributors work effectively in the TimeTracker codebase.

---

## Project Context

TimeTracker is a WPF (.NET 8) desktop time-tracking app. It uses:

- **WPF** for the main UI
- **Windows Forms** only for the system-tray `NotifyIcon`
- **SQLite** with Dapper for persistence
- **CommunityToolkit.Mvvm** for MVVM
- **Microsoft.Extensions.Hosting** for DI

All data is local; there is no cloud backend.

---

## How to Approach Changes

1. **Read before writing** — understand the relevant view-model, repository, and XAML before changing behaviour.
   - Read relevant entries in `docs/AUTHOR_REQUIREMENTS.md` to check the author's original wording and distinguish it from agent assumptions. Later explicit author corrections take precedence.
2. **Prefer small, focused changes** — this is a single-user desktop app; keep PRs easy to review.
3. **Preserve existing patterns**:
   - Use `CommunityToolkit.Mvvm` source generators (`[ObservableProperty]`, `[RelayCommand]`).
   - Keep repository methods async and accept `CancellationToken`.
   - Store UTC dates in the database; convert to local time only in the UI layer.
4. **Update documentation** when adding features:
   - Update `README.md` for user-facing changes.
   - Update `docs/TODO.md` when a TODO is completed or added.
   - Append new author requirements and meaningful corrections to `docs/AUTHOR_REQUIREMENTS.md` using its maintenance rules. Preserve exact quotes when available, keep original quotes unchanged, and label interpretation/implementation decisions separately. Do not invent historical quotations from backlog/spec text.
5. **Build before finishing** — run `dotnet build TimeTracker.sln` and ensure zero errors.

---

## Key Files to Know

| File | Purpose |
|---|---|
| `TimeTracker.App/App.xaml.cs` | App bootstrap, DI registration, single-instance logic, UI scaling |
| `TimeTracker.App/MainWindow.xaml` | Main UI layout |
| `TimeTracker.App/MainWindow.xaml.cs` | Window code-behind, tray icon, menu handlers |
| `TimeTracker.App/ViewModels/MainViewModel.cs` | Core timer/manual-entry logic, import/export |
| `TimeTracker.App/Helpers/DurationParser.cs` | Flexible duration input parsing |
| `TimeTracker.Data/TimeEntryRepository.cs` | All SQLite data access + schema migrations |
| `TimeTracker.Core/Configuration/AppRuntimeOptions.cs` | Typed `appsettings.json` options |

---

## Coding Conventions

- `ImplicitUsings` and `Nullable` are enabled.
- Use file-scoped namespaces.
- Use `sealed` for non-derivable classes where appropriate.
- Use `ConfigureAwait(false)` in library code (`Core` and `Data`).
- UI-layer code (`App`) uses the WPF dispatcher directly.
- Raw SQL lives in the repository as `const string` literals (raw string literals preferred).

---

## Testing

There is currently **no test project**. When adding tests:

- Create a new xUnit project, e.g. `TimeTracker.Tests`.
- Start with tests for `DurationParser` and repository edge cases.
- Use an in-memory SQLite database for repository tests.

---

## Common Pitfalls

- **Do not block the UI thread** — repository calls are async; keep them async all the way up.
- **Do not forget `ConfigureAwait(false)`** in `Core`/`Data` projects.
- **Date handling** — the database stores UTC; the UI converts to local time for display.
- **Single instance** — changes to startup/activation logic must keep the named mutex and `EventWaitHandle` working.
- **UI scaling** — the app supports a `Ui.ScaleMultiplier`; avoid hard-coded pixel sizes in new UI code.

---

## Feature Priority (from `docs/TODO.md`)

High-value open items:

1. Implement global hotkeys.
2. Add unit tests.
3. Show the real database version in the About window.
4. Add week-wise pagination.
5. Add a 15-minute running-timer reminder.
6. Add backup/restore workflow.

Always check `docs/TODO.md` for the latest backlog before starting a new feature.
