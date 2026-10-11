# Troubleshooting

Common issues encountered while developing or running TimeTracker, and how they were resolved.

---

## Startup

### StackOverflowException at startup

**Symptom:** The app crashes immediately on launch with a `StackOverflowException`.

**Cause:** `App.xaml` wired the `Startup` event to a handler named `OnStartup`. Because `OnStartup` is also the protected virtual method on `Application`, calling `base.OnStartup(e)` recursively re-raised the event, overflowing the stack.

**Fix:** Renamed the handler to `OnApplicationStartup` and removed the `base.OnStartup(e)` call.

---

### Icon / XAML resource load failure

**Symptom:** `IOException` or `XamlParseException` referring to the window icon.

**Cause:** WPF pack URIs are case-sensitive. The embedded resource was lowercase (`assets/timetracker.ico`) while XAML referenced mixed case (`/Assets/TimeTracker.ico`).

**Fix:** Removed `Icon="/Assets/..."` from `MainWindow.xaml` and load `Assets\TimeTracker.ico` from the output folder in `MainWindow.LoadWindowIcon()` at runtime.

---

### ListViewItem style lookup error

**Symptom:** Resource-not-found exception for styles based on `{StaticResource {x:Type ListViewItem}}`.

**Fix:** Removed explicit `BasedOn="{StaticResource {x:Type ...}}"` style references that were resolving incorrectly.

---

### Tray popup double-close crash

**Symptom:** Intermittent crash when dismissing the tray popup.

**Fix:** Added a `SafeClose()` guard that checks `IsClosed` / `IsClosing` before calling `Close()`.

---

## Build

### `dotnet build` appears to hang

**Symptom:** The build command seems to stop at `Determining projects to restore...`.

**Check:** Look for leftover `dotnet` processes from previous runs:

```cmd
tasklist | findstr dotnet
```

If any are stuck, terminate them and rebuild:

```cmd
taskkill /F /IM dotnet.exe
dotnet build TimeTracker.sln -c Debug
```

---

## UI Tests

### UI tests fail with timeout finding the main window

**Symptom:** `MainWindowSmokeTests` throws a timeout while waiting for the window.

**Likely causes:**

1. `TimeTracker.App` has not been built. Build the app first:
   ```cmd
   dotnet build TimeTracker.App -c Debug
   ```
2. The test is running in a headless / disconnected session. WPF requires an interactive desktop session.
3. The app crashed during startup. Check the Windows Event Viewer or attach a debugger.

---

### UI tests touch my real database

**Symptom:** Running UI tests creates entries in your normal TimeTracker database.

**Fix:** Ensure each test uses `--db-path` with a temporary file. The current `MainWindowSmokeTests` already does this; if you add new test helpers, make sure they do the same.

---

## General

### Unhandled exceptions

The app logs unhandled exceptions to the console/debug output via handlers registered in `App.xaml.cs`:

- `DispatcherUnhandledException`
- `AppDomain.CurrentDomain.UnhandledException`
- `TaskScheduler.UnobservedTaskException`

When running under a debugger, check the **Output** window for details.
