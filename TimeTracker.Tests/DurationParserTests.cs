using TimeTracker.App.Helpers;
using Xunit;

namespace TimeTracker.Tests;

public sealed class DurationParserTests
{
    [Theory]
    [InlineData("1h 2m 3s", 3723)]
    [InlineData("1h 55m", 6900)]
    [InlineData("59m 59s", 3599)]
    [InlineData("90m", 5400)]
    [InlineData("1000s", 1000)]
    [InlineData("600s", 600)]
    [InlineData("29", 1740)]
    [InlineData(" 1H2M3S ", 3723)]
    [InlineData("0h 0m 0s", 0)]
    public void UnitAndClockPatternsParseWithoutChangingBareNumberMeaning(string input, int seconds)
    {
        Assert.True(DurationParser.TryParse(input, out var duration));
        Assert.Equal(TimeSpan.FromSeconds(seconds), duration);
        Assert.Equal(seconds == 0, !ManualEntryValidation.TryDuration(input, out _));
        Assert.True(TimerInput.TryInitialDuration(input, out _));
    }

    [Theory]
    [InlineData("1m 2")]
    [InlineData("1h 123")]
    [InlineData("-2s")]
    [InlineData("2 2m")]
    [InlineData("1h 90m")]
    [InlineData("1m 60s")]
    [InlineData("60m 1s")]
    [InlineData("1h 1h")]
    [InlineData("1s 1m")]
    [InlineData("1.5h")]
    [InlineData("1:02m")]
    [InlineData("h")]
    [InlineData("999999999999999999999999999999s")]
    [InlineData("9223372036854775807h")]
    public void UnitPatternRejectsIncompleteMixedOrOverflowingInput(string input)
    {
        Assert.False(DurationParser.TryParse(input, out var duration));
        Assert.Equal(TimeSpan.Zero, duration);
        Assert.False(ManualEntryValidation.TryDuration(input, out _));
        Assert.False(TimerInput.TryInitialDuration(input, out _));
    }

    [Theory]
    [InlineData("30", 0, 30, 0)]
    [InlineData("90", 1, 30, 0)]
    [InlineData("0", 0, 0, 0)]
    public void TryParse_MinutesOnly_ReturnsExpected(string input, int expectedHours, int expectedMinutes, int expectedSeconds)
    {
        var success = DurationParser.TryParse(input, out var result);

        Assert.True(success);
        Assert.Equal(expectedHours, result.Hours);
        Assert.Equal(expectedMinutes, result.Minutes);
        Assert.Equal(expectedSeconds, result.Seconds);
    }

    [Theory]
    [InlineData("1:30", 0, 1, 30)]
    [InlineData("10:05", 0, 10, 5)]
    [InlineData("0:00", 0, 0, 0)]
    public void TryParse_MinutesSeconds_ReturnsExpected(string input, int expectedHours, int expectedMinutes, int expectedSeconds)
    {
        var success = DurationParser.TryParse(input, out var result);

        Assert.True(success);
        Assert.Equal(expectedHours, result.Hours);
        Assert.Equal(expectedMinutes, result.Minutes);
        Assert.Equal(expectedSeconds, result.Seconds);
    }

    [Theory]
    [InlineData("1:30:45", 1, 30, 45)]
    [InlineData("0:00:00", 0, 0, 0)]
    [InlineData("12:00:00", 12, 0, 0)]
    public void TryParse_HoursMinutesSeconds_ReturnsExpected(string input, int expectedHours, int expectedMinutes, int expectedSeconds)
    {
        var success = DurationParser.TryParse(input, out var result);

        Assert.True(success);
        Assert.Equal(expectedHours, result.Hours);
        Assert.Equal(expectedMinutes, result.Minutes);
        Assert.Equal(expectedSeconds, result.Seconds);
    }

    [Theory]
    [InlineData("1.5", 1, 30, 0)]
    [InlineData("2,25", 2, 15, 0)]
    [InlineData("0.5", 0, 30, 0)]
    public void TryParse_DecimalHours_ReturnsExpected(string input, int expectedHours, int expectedMinutes, int expectedSeconds)
    {
        var success = DurationParser.TryParse(input, out var result);

        Assert.True(success);
        Assert.Equal(expectedHours, result.Hours);
        Assert.Equal(expectedMinutes, result.Minutes);
        Assert.Equal(expectedSeconds, result.Seconds);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    [InlineData("abc")]
    [InlineData("1:2:3:4")]
    [InlineData("-10")]
    [InlineData("-1:30")]
    public void TryParse_Invalid_ReturnsFalse(string? input)
    {
        var success = DurationParser.TryParse(input, out var result);

        Assert.False(success);
        Assert.Equal(TimeSpan.Zero, result);
    }
}
