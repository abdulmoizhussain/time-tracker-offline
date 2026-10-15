using System.Windows;
using TimeTracker.App.Helpers;
using TimeTracker.Core.Models;

namespace TimeTracker.App;

public enum UnfinishedTimerAction { Continue, StopAtRecordedTime, StopNow }

public partial class UnfinishedTimerWindow : Window
{
    public string Message { get; }
    public string RecordedStopLabel { get; }
    public bool CanStopAtRecordedTime { get; }
    public string ContinueHint => "Keep this timer running. Time while you were away is included. Enter, Escape, or closing this popup also chooses Continue.";
    public string RecordedStopHint { get; }
    public string StopNowHint => "End this task at the current time. Time while you were away is included in its final duration.";
    public UnfinishedTimerAction Action { get; private set; }

    public UnfinishedTimerWindow(string description, TimerInterruption interruption)
    {
        Message = TimerRecoveryMessage.Format(description, interruption);
        CanStopAtRecordedTime = interruption.OccurredUtc.HasValue;
        RecordedStopLabel = interruption.Reason == TimerInterruptionReason.LastSeen ? "Stop at last activity" : "Stop at recorded time";
        RecordedStopHint = !interruption.OccurredUtc.HasValue
            ? "Unavailable because no interruption time was recorded. Choose Continue or Stop now."
            : $"End this task at {interruption.OccurredUtc.Value.ToLocalTime():d MMM yyyy HH:mm:ss}. Time after that moment is excluded."
                + (interruption.Reason == TimerInterruptionReason.LastSeen ? " This is the last saved activity time, not the exact shutdown time." : string.Empty);
        InitializeComponent();
        DataContext = this;
    }

    public void BringToFront()
    {
        if (!IsVisible) return;
        WindowState = WindowState.Normal;
        Topmost = true;
        Activate();
        Focus();
    }

    private void OnContentRendered(object? sender, EventArgs e) => BringToFront();

    private void OnContinue(object sender, RoutedEventArgs e) => Close();
    private void OnRecordedStop(object sender, RoutedEventArgs e) { Action = UnfinishedTimerAction.StopAtRecordedTime; Close(); }
    private void OnStopNow(object sender, RoutedEventArgs e) { Action = UnfinishedTimerAction.StopNow; Close(); }
}
