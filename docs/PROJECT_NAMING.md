# Project Naming & GitHub Discoverability Guide

This document collects keyword descriptions and alternative project-name ideas for TimeTracker to help the project stand out on GitHub and attract the right contributors and users.

---

## Current name

- Repository: `TimeTracker-2.0`
- Project/assembly: `TimeTracker`
- Executable: `TimeTracker.exe`

The current name is clear but very generic. A more specific name will improve search ranking and make the project’s purpose obvious at a glance.

---

## What the app actually is

Use these facts when choosing a name or writing the GitHub “About” description:

- Offline-first, single-user Windows desktop app.
- Stores all data in a local SQLite file.
- Runs from the system tray; no floating widget.
- Supports live timers and manual time entries.
- Records Windows lock/unlock session events.
- Imports/exports JSON for backup and portability.
- Built with WPF, .NET 8, CommunityToolkit.Mvvm, and Dapper.
- Single-instance: launching twice activates the existing window.

---

## Recommended primary name

For clarity and discoverability, the best choices are:

> **TrayTimeTracker** — emphasizes the system-tray design.
>
> **OfflineTimeTracker** — emphasizes local-first, privacy-friendly storage.

Both immediately communicate the app’s core identity and are far less generic than “TimeTracker.”

---

## Alternative names by emphasis

### Descriptive / clarity-first

| Name | Why it works |
|---|---|
| `TrayTimeTracker` | Highlights the system-tray, no-floating-widget design. |
| `OfflineTimeTracker` | Emphasizes local-first, no-cloud, privacy-friendly storage. |
| `WindowsTimeTracker` | Very explicit platform; great for Windows developers searching GitHub. |
| `SQLiteTimeTracker` | Appeals to devs looking for a full WPF + SQLite reference app. |
| `DeskTimeTracker` | Suggests a desktop-only, work-focused tool. |

### Benefit / workflow-focused

| Name | Why it works |
|---|---|
| `FocusHours` | Short; speaks to the productivity use case. |
| `WorkSessionTracker` | Emphasizes session/lock awareness and deep work. |
| `SimpleTimeLog` | Positions it as lightweight and uncluttered. |
| `PrivacyFirstTimeTracker` | Strong selling point: no cloud, no account. |
| `LocalTimeSheets` | Targets freelancers/contractors who need offline timesheets. |

### Clever / brandable

| Name | Why it works |
|---|---|
| `TickTray` | Short, memorable, tray + timer pun. |
| `ChronoTray` | “Chrono” = time; “Tray” = where it lives. |
| `PauseWatch` | Nods to the lock/unlock session detection feature. |
| `TimeAnchor` | Implies the app stays put in the tray. |
| `SessionChronicle` | Highlights the lock/unlock history feature. |

---

## Suggested GitHub “About” descriptions

Pick the one that matches the name you choose:

1. **Privacy-first, offline Windows time tracker that lives in your system tray and stores everything in a local SQLite database.**
2. **A WPF desktop time tracker for Windows with SQLite storage, session lock/unlock detection, and no cloud required.**
3. **Minimal system-tray time tracker for Windows — start timers, log manual entries, and export your data locally.**
4. **Offline time tracking app for Windows. Live timers, manual entries, import/export, and screen-lock awareness.**

---

## Suggested GitHub Topics / Tags

Use as many of these as GitHub allows to improve searchability:

```text
time-tracker
timesheet
productivity
windows
wpf
dotnet
csharp
sqlite
desktop-app
system-tray
offline-first
open-source
time-tracking
session-tracking
```

Optional extras depending on the final name and focus:

```text
mvvm
dapper
communitytoolkit
single-instance
privacy
local-first
freeware
```

---

## Final recommendation

If the goal is **maximum clarity and community discovery**, rename the repository to:

> **`TrayTimeTracker`**

Use the short description:

> **Privacy-first, offline Windows time tracker that lives in your system tray and stores everything in a local SQLite database.**

If you prefer to keep “TimeTracker” for continuity, a good compromise is:

> **`TimeTracker-Windows`** or **`TimeTracker-Offline`**

These preserve brand recognition while distinguishing the project from the many other generic “TimeTracker” repositories on GitHub.
