# UI Automation IDs

This document lists the `AutomationProperties.AutomationId` values exposed by TimeTracker controls. These IDs are used by `TimeTracker.UITests` and can also be used by accessibility tools or third-party automation frameworks.

---

## Main Window (`MainWindow.xaml`)

| Control | Automation ID | Type | Notes |
|---|---|---|---|
| Start Timer button | `StartTimerButton` | Button | Visible when no timer is running. |
| Stop Timer button | `StopTimerButton` | Button | Visible while a timer is running. |
| Manual description box | `ManualDescriptionTextBox` | TextBox | Description for manual duration/range entries. |
| Manual duration box | `ManualDurationTextBox` | TextBox | Duration input for manual entries. |
| Add Duration button | `AddDurationButton` | Button | Adds a single duration entry. |
| Add Range button | `AddRangeButton` | Button | Adds an entry from start/end date-time. |
| Duration start date | `DurationStartDatePicker` | DatePicker | Date for the duration row. |
| Range description | `RangeDescriptionTextBox` | TextBox | Independent description for the start/end row. |
| Range start date | `RangeStartDatePicker` | DatePicker | Date for the start/end row. |
| Range start time | `RangeStartTimeTextBox` | TextBox | Start time on the selected day. |
| Range end time | `RangeEndTimeTextBox` | TextBox | End time on the selected day. |
| Description filter | `EntryDescriptionFilter` | TextBox | Literal substring filter. |
| Advanced filters toggle | `EntryFiltersToggle` | ToggleButton | Expands date/status controls; count represents applied conditions. |
| From/To date filters | `EntryFromDateFilter`, `EntryToDateFilter` | DatePicker | Inclusive local dates. |
| Status filter | `EntryStatusFilter` | ComboBox | All, Running, Completed. |
| Sort field | `EntrySortField` | ComboBox | Sort within daily groups. |
| Apply/Reset filters | `ApplyEntryFiltersButton`, `ResetEntryFiltersButton` | Button | Reload the first matching/default week. |
| Older/Newer week | `OlderWeekButton`, `NewerWeekButton` | Button | Disabled at navigation boundaries or during loading. |
| Week label | `WeekPageLabel` | TextBlock | Date range and week number. |
| Entry list | `EntriesListView` | ListView | Entries for the selected week. |
| Row timer button | `EntryTimerButton` | Button | Repeated per row; scope lookup to the intended row. Shows Start or Stop according to that row's running state. |
| Remove entry menu item | `RemoveEntryMenuItem` | MenuItem | On the row context menu, targets its placement row; confirms before deletion. |
| Stop entry menu item | `StopEntryMenuItem` | MenuItem | Visible only for a running row in its context menu. |
| Page summary | `EntryPageSummary` | TextBlock | Counts and filtered completed-duration total. |

### Notes

- The Start/Stop buttons occupy the same cell in the layout and toggle visibility based on `IsTimerRunning`. UI tests should look for whichever button is appropriate for the current state.
- Date/status filters require expanding `EntryFiltersToggle`; filter/sort drafts take effect on Apply. At small window sizes, scroll the composer or filter controls into view.
- Removal/Clear confirmations expose `ConfirmDestructiveAction`; Cancel is the default button and Escape cancels.
- The timer description box does **not** yet have an automation ID. Add one before writing tests that interact with it.

---

## Adding a new automation ID

Export range dialog: `ExportUseDateRange`, `ExportFromDate`, `ExportToDate`, `ExportContinueButton`, `ExportRangeError`. Continue validates dates before opening the save-file dialog; Cancel does not open that dialog.

Use the attached property in XAML:

```xml
<Button x:Name="MyButton"
        AutomationProperties.AutomationId="MyButton"
        Content="Click Me"
        Command="{Binding MyCommand}" />
```

Guidelines:

1. Use PascalCase IDs that match the control purpose.
2. Keep IDs stable — changing them breaks existing UI tests.
3. Prefer IDs over names or text content, because text can be localized or changed for styling.

---

## Programmatic access example

With `System.Windows.Automation`:

```csharp
var desktop = AutomationElement.RootElement;
var mainWindow = desktop.FindFirst(
    TreeScope.Children,
    new PropertyCondition(AutomationElement.NameProperty, "Time Tracker"));

var button = mainWindow?.FindFirst(
    TreeScope.Descendants,
    new PropertyCondition(AutomationElement.AutomationIdProperty, "StartTimerButton"));

if (button is not null)
{
    var pattern = (InvokePattern)button.GetCurrentPattern(InvokePattern.Pattern);
    pattern.Invoke();
}
```
