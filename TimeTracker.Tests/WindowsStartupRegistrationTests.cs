using TimeTracker.App.Helpers;
using Xunit;

namespace TimeTracker.Tests;

public sealed class WindowsStartupRegistrationTests
{
    [Fact]
    public void StartupCommandQuotesExecutableAndStartsInTray()
    {
        Assert.Equal("\"C:\\Apps\\Time Tracker\\TimeTracker.exe\" --startup",
            WindowsStartupRegistration.BuildCommand(@"C:\Apps\Time Tracker\TimeTracker.exe"));
    }

    [Theory]
    [InlineData("TimeTracker.exe")]
    [InlineData("C:\\Apps\\TimeTracker.dll")]
    [InlineData("C:\\Apps\\bad\"name.exe")]
    public void InvalidExecutablePathsAreRejected(string path)
    {
        Assert.Throws<ArgumentException>(() => WindowsStartupRegistration.BuildCommand(path));
    }

    [Fact]
    public void CommandsExceedingRunKeyLimitAreRejected()
    {
        Assert.Throws<ArgumentException>(() => WindowsStartupRegistration.BuildCommand(
            @"C:\Apps\" + new string('a', 260) + @"\TimeTracker.exe"));
    }

    [Fact]
    public void OnlyExplicitStartupArgumentStartsInTray()
    {
        Assert.True(WindowsStartupRegistration.IsStartupLaunch(["--STARTUP"]));
        Assert.True(WindowsStartupRegistration.IsStartupLaunch(["--startup", "--db-path", "test.db"]));
        Assert.False(WindowsStartupRegistration.IsStartupLaunch([]));
        Assert.False(WindowsStartupRegistration.IsStartupLaunch(["--startup=false"]));
    }
}
