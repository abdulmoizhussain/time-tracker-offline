namespace TimeTracker.Core.Models;

public enum TimerInterruptionReason { Unknown, LastSeen, Lock, Exit, Shutdown }

public sealed record TimerInterruption(int EntryId, TimerInterruptionReason Reason, DateTime? OccurredUtc);
