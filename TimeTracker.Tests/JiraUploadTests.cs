using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using Dapper;
using TimeTracker.App.Services;
using TimeTracker.App.ViewModels;
using TimeTracker.Core.Models;
using TimeTracker.Data;
using Xunit;

namespace TimeTracker.Tests;

public sealed class JiraUploadTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "TimeTracker.JiraTests." + Guid.NewGuid().ToString("N"));
    private readonly SqliteConnectionFactory _factory;
    private readonly TimeEntryRepository _entries;
    private readonly JiraUploadRepository _uploads;

    public JiraUploadTests()
    {
        Directory.CreateDirectory(_directory);
        _factory = new(Path.Combine(_directory, "test.db"));
        _entries = new(_factory);
        _entries.InitializeAsync().GetAwaiter().GetResult();
        _uploads = new(_factory);
    }

    public void Dispose()
    {
        using var connection = _factory.CreateConnection();
        Microsoft.Data.Sqlite.SqliteConnection.ClearPool(connection);
        try { Directory.Delete(_directory, true); } catch (IOException) { }
    }

    [Fact]
    public async Task MigrationPreservesTimeEntrySchemaAndDataAndHistorySurvivesEntryDeletion()
    {
        var id = await _entries.AddManualRangeEntryAsync("ABC-1 Existing", DateTime.Today.AddHours(9), DateTime.Today.AddHours(10));
        var before = JsonSerializer.Serialize(await _entries.GetRecentEntriesAsync(10));
        using var connection = _factory.CreateConnection();
        await connection.OpenAsync();
        var schema = await connection.ExecuteScalarAsync<string>("SELECT sql FROM sqlite_master WHERE name = 'TimeEntries';");
        await connection.ExecuteAsync("DROP TABLE JiraSettings; DROP TABLE JiraUploadHistory; PRAGMA user_version = 3;");
        await _entries.InitializeAsync();
        await _entries.InitializeAsync();
        Assert.Equal(schema, await connection.ExecuteScalarAsync<string>("SELECT sql FROM sqlite_master WHERE name = 'TimeEntries';"));
        Assert.Equal(before, JsonSerializer.Serialize(await _entries.GetRecentEntriesAsync(10)));
        var history = new JiraUploadHistory
        {
            Domain = "example.atlassian.net", Email = "user@example.com", FromUtc = DateTime.Today.ToUniversalTime(),
            ToUtcExclusive = DateTime.Today.AddDays(1).ToUniversalTime(), StartedUtc = DateTime.UtcNow, TotalCount = 1
        };
        history.Id = await _uploads.CreateHistoryAsync(history);
        history.SuccessCount = 1;
        history.LastUploadedStartUtc = DateTime.Today.AddHours(9).ToUniversalTime();
        history.CompletedUtc = DateTime.UtcNow;
        history.Status = "Completed";
        await _uploads.UpdateHistoryAsync(history);
        await _entries.DeleteEntryAsync(id);
        var saved = Assert.Single(await new JiraUploadRepository(_factory).GetHistoryAsync());
        Assert.Equal(history.LastUploadedStartUtc, saved.LastUploadedStartUtc);
        Assert.Equal(history.FromUtc, saved.FromUtc);
        Assert.Equal(DateTimeKind.Utc, saved.StartedUtc.Kind);
        Assert.Equal(1, saved.SuccessCount);
    }

    [Fact]
    public async Task RequestMatchesScriptWithFirstTicketUtcDateFullDurationAndAdfComment()
    {
        using var handler = new StubHandler(async request =>
        {
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.Equal("https://example.atlassian.net/rest/api/3/issue/abc-123/worklog", request.RequestUri!.AbsoluteUri);
            Assert.Equal("Basic", request.Headers.Authorization!.Scheme);
            Assert.Equal(Convert.ToBase64String(Encoding.UTF8.GetBytes("user@example.com:test-token")), request.Headers.Authorization.Parameter);
            using var json = JsonDocument.Parse(await request.Content!.ReadAsStringAsync());
            Assert.Equal(90061, json.RootElement.GetProperty("timeSpentSeconds").GetInt32());
            Assert.Equal("2026-10-24T04:03:02.000+0000", json.RootElement.GetProperty("started").GetString());
            Assert.Equal("abc-123 review XYZ-999", json.RootElement.GetProperty("comment").GetProperty("content")[0].GetProperty("content")[0].GetProperty("text").GetString());
            return new HttpResponseMessage(HttpStatusCode.Created); // Empty success body must still be successful.
        });
        using var http = new HttpClient(handler);
        var result = await new JiraWorklogClient(http).UploadAsync(new()
        {
            Description = "abc-123 review XYZ-999", StartUtc = new(2026, 10, 24, 4, 3, 2, DateTimeKind.Utc), DurationSeconds = 90061
        }, "https://EXAMPLE.atlassian.net/", "user@example.com", "test-token");
        Assert.True(result.Success);
        Assert.Equal("abc-123", result.TicketId);
        Assert.Equal(1, handler.Calls);
    }

    [Theory]
    [InlineData("No ticket", 3600, "No ticket ID")]
    [InlineData("ABC-1 Too short", 59, "shorter than 1 minute")]
    public async Task InvalidEntriesAreReportedWithoutHttpRequests(string description, int seconds, string reason)
    {
        using var handler = new StubHandler(_ => throw new InvalidOperationException("Must not send"));
        using var http = new HttpClient(handler);
        var result = await new JiraWorklogClient(http).UploadAsync(new()
        {
            Description = description, StartUtc = DateTime.UtcNow, DurationSeconds = seconds
        }, "example.atlassian.net", "user@example.com", "test-token");
        Assert.False(result.Success);
        Assert.Contains(reason, result.Details);
        Assert.Equal(0, handler.Calls);
    }

    [Fact]
    public async Task ApiErrorsAreRedactedAndTransportFailuresAreNotRetried()
    {
        const string token = "private-token";
        var authorization = Convert.ToBase64String(Encoding.UTF8.GetBytes("user@example.com:" + token));
        using var handler = new StubHandler(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized)
        {
            Content = new StringContent($"bad {token} {authorization}")
        }));
        using var http = new HttpClient(handler);
        var client = new JiraWorklogClient(http);
        var entry = new TimeEntry { Description = "ABC-1", StartUtc = DateTime.UtcNow, DurationSeconds = 60 };
        var failed = await client.UploadAsync(entry, "example.atlassian.net", "user@example.com", token);
        Assert.False(failed.Success);
        Assert.Contains("401", failed.Details);
        Assert.Contains("email/token", failed.Details);
        Assert.DoesNotContain(token, failed.Details);
        Assert.DoesNotContain(authorization, failed.Details);
        using var networkHandler = new StubHandler(_ => throw new HttpRequestException("secret transport internals"));
        using var networkHttp = new HttpClient(networkHandler);
        var unknown = await new JiraWorklogClient(networkHttp).UploadAsync(entry, "example.atlassian.net", "user@example.com", token);
        Assert.Contains("outcome may be unknown", unknown.Details);
        Assert.Equal(1, networkHandler.Calls);
    }

    [Theory]
    [InlineData("http://example.atlassian.net")]
    [InlineData("https://user:password@example.atlassian.net")]
    [InlineData("https://example.atlassian.net/some/path")]
    [InlineData("https://example.atlassian.net?token=bad")]
    public void InvalidConnectionDomainsAreRejected(string domain) => Assert.Throws<ArgumentException>(() => JiraCredentials.NormalizeDomain(domain));

    [Fact]
    public async Task RangeUploadKeepsFailuresAndHistoryAndAllowsDuplicateUploadsAndTokenReplacement()
    {
        var day = DateTime.Today.AddDays(-2);
        await _entries.AddManualRangeEntryAsync("ABC-1 Good", day.AddHours(9), day.AddHours(10));
        await _entries.AddManualRangeEntryAsync("ABC-2 Rejected", day.AddHours(10), day.AddHours(11));
        await _entries.AddManualRangeEntryAsync("No ticket", day.AddHours(11), day.AddHours(12));
        await _entries.AddManualRangeEntryAsync("ABC-3 outside", day.AddDays(1), day.AddDays(1).AddHours(1));
        await _entries.StartTimerAsync("ABC-4 running", day.AddHours(8).ToUniversalTime());
        var before = JsonSerializer.Serialize(await _entries.GetRecentEntriesAsync(100));
        var tokensUsed = new List<string>();
        using var handler = new StubHandler(request =>
        {
            tokensUsed.Add(Encoding.UTF8.GetString(Convert.FromBase64String(request.Headers.Authorization!.Parameter!)));
            return Task.FromResult(new HttpResponseMessage(request.RequestUri!.AbsolutePath.Contains("ABC-2") ? HttpStatusCode.Forbidden : HttpStatusCode.Created));
        });
        using var http = new HttpClient(handler);
        var vm = new JiraUploadViewModel(_entries, _uploads, new(http), new(_factory.DatabasePath))
        {
            Domain = "example.atlassian.net", Email = "user@example.com", TokenDraft = "old-secret", FromDate = day, ToDate = day
        };
        await vm.ReviewCommand.ExecuteAsync(null);
        Assert.Equal(3, vm.TotalCount);
        await vm.UploadCommand.ExecuteAsync(null);
        Assert.Equal(2, handler.Calls);
        var first = Assert.Single(await _uploads.GetHistoryAsync());
        Assert.Equal(1, first.SuccessCount);
        Assert.Equal(2, first.FailedCount);
        Assert.Equal("Completed with failures", first.Status);
        Assert.Contains("ABC-2 Rejected", vm.FailedEntriesText);
        Assert.Contains("Duration: 01:00:00", vm.FailedEntriesText);
        Assert.Contains(day.ToString("yyyy-MM-dd"), vm.FailedEntriesText);
        Assert.Contains("No ticket ID", vm.FailedEntriesText);
        Assert.Equal(before, JsonSerializer.Serialize(await _entries.GetRecentEntriesAsync(100)));
        var saved = (await _uploads.GetSettingsAsync())!;
        Assert.DoesNotContain("old-secret", saved.ProtectedToken);
        Assert.Equal("old-secret", JiraCredentials.Unprotect(saved.ProtectedToken));
        vm.TokenDraft = "new-secret";
        await vm.SaveSettingsCommand.ExecuteAsync(null);
        Assert.Equal("", vm.TokenDraft);
        Assert.Equal("new-secret", JiraCredentials.Unprotect((await _uploads.GetSettingsAsync())!.ProtectedToken));
        await vm.UploadCommand.ExecuteAsync(null); // Same entries intentionally upload again.
        Assert.Equal(4, handler.Calls);
        Assert.Equal(2, (await _uploads.GetHistoryAsync()).Count);
        Assert.Equal("user@example.com:new-secret", tokensUsed.Last());
        var logText = await File.ReadAllTextAsync(Assert.Single(Directory.GetFiles(_directory, "*.log")));
        Assert.Contains("worker [info]:", logText);
        Assert.Contains("Processing Summary:", logText);
        Assert.DoesNotContain("old-secret", logText);
        Assert.DoesNotContain("new-secret", logText);
        vm.FromDate = day.AddDays(-1);
        Assert.False(vm.UploadCommand.CanExecute(null));
    }

    [Fact]
    public async Task StopFinishesCurrentRequestAndListsRemainingEntriesWithoutSendingThem()
    {
        var day = DateTime.Today.AddDays(-1);
        await _entries.AddManualRangeEntryAsync("ABC-1", day.AddHours(9), day.AddHours(10));
        await _entries.AddManualRangeEntryAsync("ABC-2", day.AddHours(10), day.AddHours(11));
        JiraUploadViewModel? vm = null;
        using var handler = new StubHandler(_ =>
        {
            vm!.StopCommand.Execute(null);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Created));
        });
        using var http = new HttpClient(handler);
        vm = new(_entries, _uploads, new(http), new(_factory.DatabasePath))
        {
            Domain = "example.atlassian.net", Email = "user@example.com", TokenDraft = "secret", FromDate = day, ToDate = day
        };
        await vm.ReviewCommand.ExecuteAsync(null);
        await vm.UploadCommand.ExecuteAsync(null);
        Assert.Equal(1, handler.Calls);
        var history = Assert.Single(await _uploads.GetHistoryAsync());
        Assert.Equal("Stopped", history.Status);
        Assert.Equal(1, history.SuccessCount);
        Assert.Equal(0, history.FailedCount);
        Assert.Contains("Not uploaded: stopped by user", vm.FailedEntriesText);
        Assert.False(vm.IsBusy);
    }

    private sealed class StubHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        public int Calls { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            return send(request);
        }
    }
}
