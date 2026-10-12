using TimeTracker.App.Helpers;
using TimeTracker.Core.Models;
using Xunit;

namespace TimeTracker.Tests;

public sealed class TimerInputTests
{
    [Theory]
    [InlineData(null, 0)]
    [InlineData("", 0)]
    [InlineData("0", 0)]
    [InlineData("00:00:00", 0)]
    [InlineData("34", 2040)]
    [InlineData("1.5", 5400)]
    [InlineData("2:00:00", 7200)]
    [InlineData("23:59:59", 86399)]
    public void InitialDurationAcceptsZeroAndManualFormats(string? input, int seconds)
    {
        Assert.True(TimerInput.TryInitialDuration(input, out var duration));
        Assert.Equal(seconds, duration.TotalSeconds);
    }

    [Theory]
    [InlineData("-1")]
    [InlineData("invalid")]
    [InlineData("24:00:00")]
    [InlineData("1:60:00")]
    [InlineData("999999999999999999999999")]
    public void InvalidInitialDurationIsRejected(string input)
        => Assert.False(TimerInput.TryInitialDuration(input, out _));

    [Fact]
    public void RecoveryMessageDoesNotClaimAnExactShutdownTimeFromLastActivity()
    {
        var message = TimerRecoveryMessage.Format("Task", new(1, TimerInterruptionReason.LastSeen, DateTime.UtcNow));
        Assert.Contains("last recorded activity", message);
        Assert.Contains("exact interruption time wasn’t recorded", message);
        Assert.DoesNotContain("shutting down", message);
    }

    [Fact]
    public void LegacyRecoveryMessageHandlesMissingTimestamp()
        => Assert.Contains("No interruption time was recorded", TimerRecoveryMessage.Format("Task", new(1, TimerInterruptionReason.Unknown, null)));
}
