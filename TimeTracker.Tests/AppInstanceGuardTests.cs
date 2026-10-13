using TimeTracker.App.Helpers;
using TimeTracker.Core.Configuration;
using Xunit;

namespace TimeTracker.Tests;

public sealed class AppInstanceGuardTests
{
    private static string User() => $"tests-{Guid.NewGuid():N}";
    private const string A = @"C:\TimeTrackerTests\A";
    private const string B = @"C:\TimeTrackerTests\B";

    [Fact]
    public void MultipleInstancesDefaultToFalse() => Assert.False(new AppRuntimeOptions().AllowMultipleInstances);

    [Theory]
    [InlineData(A)]
    [InlineData(@"c:\timetrackertests\a\")]
    [InlineData(@"C:\TimeTrackerTests\B\..\A")]
    public void SameDirectoryAlwaysRejectedBeforeReadingConfiguration(string directory)
    {
        var user = User();
        using var first = AppInstanceGuard.TryAcquire(A, user, () => true, false);
        Assert.NotNull(first);
        using var duplicate = AppInstanceGuard.TryAcquire(directory, user,
            () => throw new InvalidOperationException("Configuration must not be read"), true);
        Assert.Null(duplicate);
        Assert.True(first.DirectoryActivation.WaitOne(0));
        Assert.False(first.AnyActivation.WaitOne(0));
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void LaunchingCopyControlsOtherDirectoryAdmission(bool existingAllows, bool launchingAllows)
    {
        var user = User();
        using var first = AppInstanceGuard.TryAcquire(A, user, () => existingAllows, false);
        using var second = AppInstanceGuard.TryAcquire(B, user, () => launchingAllows, false);
        Assert.Equal(launchingAllows, second is not null);
    }

    [Fact]
    public void RemainingCopyKeepsGlobalRestrictionAfterFirstExits()
    {
        var user = User();
        var first = AppInstanceGuard.TryAcquire(A, user, () => false, false)!;
        using var second = AppInstanceGuard.TryAcquire(B, user, () => true, false);
        first.Dispose();
        using var third = AppInstanceGuard.TryAcquire(A, user, () => false, true);
        Assert.Null(third);
        Assert.True(second!.AnyActivation.WaitOne(0));
        second.Dispose();
        using var restarted = AppInstanceGuard.TryAcquire(A, user, () => false, false);
        Assert.NotNull(restarted);
    }

    [Fact]
    public void SameDirectoryActivationTargetsItsOwnCopyAndStartupStaysQuiet()
    {
        var user = User();
        using var first = AppInstanceGuard.TryAcquire(A, user, () => true, false);
        using var second = AppInstanceGuard.TryAcquire(B, user, () => true, false);
        Assert.Null(AppInstanceGuard.TryAcquire(B, user, () => true, false));
        Assert.False(second!.DirectoryActivation.WaitOne(0));
        Assert.Null(AppInstanceGuard.TryAcquire(B, user, () => true, true));
        Assert.True(second.DirectoryActivation.WaitOne(0));
        Assert.False(first!.DirectoryActivation.WaitOne(0));
    }

    [Fact]
    public void FailedConfigurationDoesNotLeavePresenceBehind()
    {
        var user = User();
        Assert.Throws<InvalidOperationException>(() => AppInstanceGuard.TryAcquire(A, user,
            () => throw new InvalidOperationException(), false));
        using var retry = AppInstanceGuard.TryAcquire(A, user, () => false, false);
        Assert.NotNull(retry);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ConcurrentLaunchesAdmitOnlyOneWhenRequired(bool sameDirectory)
    {
        var user = User();
        var launches = Enumerable.Range(0, 12).Select(i => Task.Run(() =>
            AppInstanceGuard.TryAcquire(sameDirectory ? A : $@"C:\TimeTrackerTests\{i}", user,
                () => sameDirectory, false)));
        var guards = await Task.WhenAll(launches);
        try { Assert.Single(guards.Where(guard => guard is not null)); }
        finally { foreach (var guard in guards) guard?.Dispose(); }
    }
}
