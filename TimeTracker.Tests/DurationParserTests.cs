using TimeTracker.App.Helpers;
using Xunit;

namespace TimeTracker.Tests;

public sealed class DurationParserTests
{
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
