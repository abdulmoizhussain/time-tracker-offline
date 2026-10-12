using TimeTracker.App.Helpers;
using Xunit;

namespace TimeTracker.Tests;

public sealed class DurationPlacementTests
{
    [Theory]
    [InlineData(10, 2, 10, 0, 0, 12, 0, 0)]
    [InlineData(23, 2, 21, 59, 59, 23, 59, 59)]
    [InlineData(0, 2, 0, 0, 0, 2, 0, 0)]
    public void PlacesDurationOnSelectedDay(int hour, int durationHours,
        int startHour, int startMinute, int startSecond, int endHour, int endMinute, int endSecond)
    {
        var date = new DateTime(2026, 10, 12);
        Assert.True(ManualEntryValidation.TryDurationRange(date, TimeSpan.FromHours(hour),
            TimeSpan.FromHours(durationHours), out var start, out var end));
        Assert.Equal(date.Add(new TimeSpan(startHour, startMinute, startSecond)), start);
        Assert.Equal(date.Add(new TimeSpan(endHour, endMinute, endSecond)), end);
        Assert.Equal(TimeSpan.FromHours(durationHours), end.ToUniversalTime() - start.ToUniversalTime());
    }

    [Fact]
    public void RejectsFullDayRatherThanMovingStartIntoPreviousDay()
        => Assert.False(ManualEntryValidation.TryDurationRange(new DateTime(2026, 10, 12),
            TimeSpan.FromHours(23), TimeSpan.FromHours(24), out _, out _));

    [Fact]
    public void MaximumSingleDayDurationEndsAtLastSecond()
    {
        var date = new DateTime(2026, 10, 12);
        var duration = new TimeSpan(23, 59, 59);
        Assert.True(ManualEntryValidation.TryDurationRange(date, TimeSpan.FromHours(23), duration, out var start, out var end));
        Assert.Equal(date, start);
        Assert.Equal(date.Add(duration), end);
    }
}
