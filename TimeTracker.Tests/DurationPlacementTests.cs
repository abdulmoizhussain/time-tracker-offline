using TimeTracker.App.Helpers;
using Xunit;

namespace TimeTracker.Tests;

public sealed class DurationPlacementTests
{
    [Theory]
    [InlineData(10, 2, 8, 0, 0, 10, 0, 0)]
    [InlineData(23, 2, 21, 0, 0, 23, 0, 0)]
    [InlineData(0, 2, 0, 0, 0, 2, 0, 0)]
    [InlineData(1, 2, 0, 0, 0, 2, 0, 0)]
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
    public void FullDayStartsAtMidnightAndEndsAtFollowingMidnight()
    {
        var date = new DateTime(2026, 10, 12);
        Assert.True(ManualEntryValidation.TryDurationRange(date, TimeSpan.FromHours(23),
            TimeSpan.FromHours(24), out var start, out var end));
        Assert.Equal(date, start);
        Assert.Equal(date.AddDays(1), end);
    }

    [Fact]
    public void RejectsDurationOverTwentyFourHours()
        => Assert.False(ManualEntryValidation.TryDurationRange(new DateTime(2026, 10, 12),
            TimeSpan.FromHours(23), TimeSpan.FromHours(24) + TimeSpan.FromSeconds(1), out _, out _));

    [Fact]
    public void RejectsEndBeyondSupportedCalendarWithoutThrowing()
        => Assert.False(ManualEntryValidation.TryDurationRange(DateTime.MaxValue.Date,
            TimeSpan.Zero, TimeSpan.FromHours(24), out _, out _));

    [Fact]
    public void PreservesCurrentMomentIncludingSubsecondPrecision()
    {
        var date = new DateTime(2026, 10, 12);
        var now = TimeSpan.FromHours(10) + TimeSpan.FromMilliseconds(123);
        var duration = TimeSpan.FromSeconds(3723);
        Assert.True(ManualEntryValidation.TryDurationRange(date, now, duration, out var start, out var end));
        Assert.Equal(date.Add(now), end);
        Assert.Equal(duration, end - start);
    }
}
