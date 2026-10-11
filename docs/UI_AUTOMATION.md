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

### Notes

- The Start/Stop buttons occupy the same cell in the layout and toggle visibility based on `IsTimerRunning`. UI tests should look for whichever button is appropriate for the current state.
- The timer description box and the entries list-view do **not** yet have automation IDs. Add them before writing tests that interact with those areas.

---

## Adding a new automation ID

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
