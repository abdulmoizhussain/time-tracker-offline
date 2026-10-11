using TimeTracker.App.Helpers;
using TimeTracker.App.ViewModels;
using TimeTracker.Core.Models;
using Xunit;

namespace TimeTracker.Tests;

public sealed class DurationValidationTests
{
    [Theory]
    [InlineData("33:")]
    [InlineData(":33")]
    [InlineData("1:02:30")]
    [InlineData("1.5")]
    [InlineData("24.0")]
    [InlineData("1440")]
    [InlineData("24:00:00")]
    [InlineData("0:01")]
    public void ManualDurationAcceptsDocumentedFormatsWithinDailyLimit(string input)
        => Assert.True(ManualEntryValidation.TryDuration(input, out _));

    [Theory]
    [InlineData("0")]
    [InlineData("0.0")]
    [InlineData("0.00001")]
    [InlineData("1441")]
    [InlineData("24:00:01")]
    [InlineData("333333:00:00")]
    [InlineData("19999980")]
    [InlineData("09:99")]
    [InlineData("1:60:00")]
    [InlineData("1:00:60")]
    [InlineData("-1:30")]
    [InlineData("1:-1:00")]
    [InlineData("NaN")]
    [InlineData("Infinity")]
    [InlineData("1e10")]
    [InlineData("1.2.3")]
    [InlineData("1:30.5")]
    [InlineData(":")]
    [InlineData("9999999999999999999999999999999999999999999")]
    [InlineData("9999999999999999999999999999999999999999999.0")]
    public void ManualDurationRejectsInvalidOrExcessiveInputWithoutThrowing(string input)
        => Assert.False(ManualEntryValidation.TryDuration(input, out _));

    [Theory]
    [InlineData("00:00", true)]
    [InlineData("9:30", true)]
    [InlineData("09:30:00", true)]
    [InlineData("23:59:59", true)]
    [InlineData("24:00", false)]
    [InlineData("09:99", false)]
    [InlineData("10:60:00", false)]
    [InlineData("10:00:60", false)]
    [InlineData("-1:30", false)]
    [InlineData("9", false)]
    [InlineData("1.09:00:00", false)]
    [InlineData("999999999999:00", false)]
    public void TimeInputRequiresValidClockComponents(string input, bool expected)
        => Assert.Equal(expected, ManualEntryValidation.TryTime(input, out _));

    [Theory]
    [InlineData(1199998800L, "333333:00:00")]
    [InlineData(119998800L, "33333:00:00")]
    [InlineData(1999980L, "555:33:00")]
    [InlineData(90061L, "25:01:01")]
    [InlineData(2147483647L, "596523:14:07")]
    public void ExistingLargeDurationsDisplayFullHoursAndRoundTripThroughExportFormat(long seconds, string text)
    {
        Assert.Equal(text, DurationFormatter.Clock(seconds));
        Assert.Equal(text, TimeEntryViewModel.FromModel(new TimeEntry { DurationSeconds = (int)seconds }).DurationText);
        Assert.True(DurationParser.TryParse(text, out var parsed));
        Assert.Equal(seconds, parsed.Ticks / TimeSpan.TicksPerSecond);
    }

    [Fact]
    public void TotalsSupportLargeValuesIncludingBeyondTimeSpanRange()
    {
        Assert.Equal("367233 h 11 min", DurationFormatter.HoursMinutes(1322039478));
        Assert.Equal("2562047788015215 h 30 min", DurationFormatter.HoursMinutes(long.MaxValue));
        Assert.Equal("-2562047788015215:30:08", DurationFormatter.Clock(long.MinValue));
    }
}
