using System.Collections.ObjectModel;
using System.Security.Cryptography;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TimeTracker.App.Services;
using TimeTracker.Core.Contracts;
using TimeTracker.Core.Models;

namespace TimeTracker.App.ViewModels;

public sealed partial class JiraUploadViewModel(
    ITimeEntryRepository entriesRepository, IJiraUploadRepository uploadRepository,
    JiraWorklogClient client, JiraUploadLog log) : ObservableObject
{
    private string _protectedToken = string.Empty;
    private IReadOnlyList<TimeEntry> _preview = [];
    private bool _stopRequested;

    [ObservableProperty] private string _domain = string.Empty;
    [ObservableProperty] private string _email = string.Empty;
    [ObservableProperty] private string _tokenDraft = string.Empty;
    [ObservableProperty] private string _tokenHint = "Enter your Jira API token. It will be saved encrypted for your Windows user.";
    [ObservableProperty] private DateTime? _fromDate;
    [ObservableProperty] private DateTime? _toDate;
    [ObservableProperty] private string _statusText = "Choose a date range, then review the entries to upload.";
    [ObservableProperty] private string _errorText = string.Empty;
    [ObservableProperty] private string _failedEntriesText = string.Empty;
    [ObservableProperty] private string _lastUploadText = "No upload history yet.";
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private bool _isUploading;
    [ObservableProperty] private bool _isConnectionExpanded = true;
    [ObservableProperty] private bool _isFailuresExpanded;
    [ObservableProperty] private int _processedCount;
    [ObservableProperty] private int _totalCount;

    public bool CanEdit => !IsBusy;
    public string LogDirectory => log.DirectoryPath;
    public ObservableCollection<JiraUploadHistoryItem> History { get; } = [];

    partial void OnIsBusyChanged(bool value)
    {
        OnPropertyChanged(nameof(CanEdit));
        SaveSettingsCommand.NotifyCanExecuteChanged();
        ReviewCommand.NotifyCanExecuteChanged();
        UploadCommand.NotifyCanExecuteChanged();
    }

    partial void OnIsUploadingChanged(bool value) => StopCommand.NotifyCanExecuteChanged();
    partial void OnFromDateChanged(DateTime? value) => InvalidateReview();
    partial void OnToDateChanged(DateTime? value) => InvalidateReview();
    partial void OnFailedEntriesTextChanged(string value) => IsFailuresExpanded = !string.IsNullOrWhiteSpace(value);

    private void InvalidateReview()
    {
        _preview = [];
        TotalCount = ProcessedCount = 0;
        StatusText = "Date range changed. Review entries before uploading.";
        UploadCommand.NotifyCanExecuteChanged();
    }

    public async Task InitializeAsync()
    {
        IsBusy = true;
        try
        {
            var settings = await uploadRepository.GetSettingsAsync();
            if (settings is not null)
            {
                Domain = settings.Domain;
                Email = settings.Email;
                _protectedToken = settings.ProtectedToken;
                UpdateTokenHint();
                IsConnectionExpanded = false;
            }
            await RefreshHistoryAsync();
        }
        catch (Exception) { ErrorText = "Could not load Jira settings or history. Close this window and try again."; }
        finally { IsBusy = false; }
    }

    private bool CanUseControls() => !IsBusy;
    private bool CanUpload() => !IsBusy && _preview.Count > 0;
    private bool CanStop() => IsUploading && !_stopRequested;

    private async Task<string> SaveConnectionAsync()
    {
        var domain = JiraCredentials.NormalizeDomain(Domain);
        var email = JiraCredentials.ValidateEmail(Email);
        var replacement = TokenDraft.Trim();
        var protectedToken = replacement.Length > 0 ? JiraCredentials.Protect(replacement) : _protectedToken;
        if (string.IsNullOrWhiteSpace(protectedToken)) throw new ArgumentException("Enter your Jira API token.");
        string token;
        try { token = JiraCredentials.Unprotect(protectedToken); }
        catch (Exception ex) when (ex is CryptographicException or FormatException)
        {
            throw new ArgumentException("The saved token cannot be opened by this Windows user. Enter a replacement token.");
        }
        await uploadRepository.SaveSettingsAsync(new() { Domain = domain, Email = email, ProtectedToken = protectedToken });
        Domain = domain;
        Email = email;
        _protectedToken = protectedToken;
        TokenDraft = string.Empty;
        UpdateTokenHint();
        return token;
    }

    private void UpdateTokenHint() => TokenHint = string.IsNullOrEmpty(_protectedToken)
        ? "Enter your Jira API token. It will be saved encrypted for your Windows user."
        : "A token is saved. Leave blank to keep it, or enter a replacement and save.";

    [RelayCommand(CanExecute = nameof(CanUseControls))]
    private async Task SaveSettingsAsync()
    {
        IsBusy = true;
        ErrorText = string.Empty;
        try
        {
            await SaveConnectionAsync();
            StatusText = "Connection settings saved. Uploading will use these settings.";
        }
        catch (ArgumentException ex) { ErrorText = ex.Message; }
        catch (Exception) { ErrorText = "Could not save Jira settings. Check that the database is writable."; }
        finally { IsBusy = false; }
    }

    [RelayCommand(CanExecute = nameof(CanUseControls))]
    private async Task ReviewAsync()
    {
        IsBusy = true;
        ErrorText = string.Empty;
        _preview = [];
        TotalCount = ProcessedCount = 0;
        try
        {
            var (fromUtc, toUtc) = GetRange();
            _preview = (await entriesRepository.GetExportEntriesAsync(fromUtc, toUtc))
                .Where(entry => !entry.IsRunning).OrderByDescending(JiraWorklogClient.EntryStartUtc).ToList();
            TotalCount = _preview.Count;
            ProcessedCount = 0;
            var seconds = _preview.Sum(entry => (long)entry.DurationSeconds);
            StatusText = $"{TotalCount} completed entries · {DurationFormatter.HoursMinutes(seconds)}. Ready to upload.";
            if (TotalCount == 0) StatusText = "No completed entries in this date range.";
        }
        catch (ArgumentException ex) { ErrorText = ex.Message; }
        catch (Exception) { ErrorText = "Could not load entries for this date range."; }
        finally { IsBusy = false; }
    }

    private (DateTime FromUtc, DateTime ToUtcExclusive) GetRange()
    {
        if (!FromDate.HasValue || !ToDate.HasValue) throw new ArgumentException("Choose both From and To dates.");
        if (FromDate.Value.Date > ToDate.Value.Date) throw new ArgumentException("From date must be on or before To date.");
        if (ToDate.Value.Date == DateTime.MaxValue.Date) throw new ArgumentException("Choose a To date earlier than 31 December 9999.");
        return (FromDate.Value.Date.ToUniversalTime(), ToDate.Value.Date.AddDays(1).ToUniversalTime());
    }

    [RelayCommand(CanExecute = nameof(CanStop))]
    private void Stop()
    {
        _stopRequested = true;
        StatusText = "Stopping after the current entry finishes…";
        StopCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand(CanExecute = nameof(CanUpload))]
    private async Task UploadAsync()
    {
        IsBusy = true;
        ErrorText = string.Empty;
        var failures = new StringBuilder();
        JiraUploadHistory? batch = null;
        string token = string.Empty;
        var nextIndex = 0;
        var historySaved = false;
        try
        {
            var (fromUtc, toUtc) = GetRange();
            token = await SaveConnectionAsync();
            // Test log access before any remote operation. Never silently perform unlogged uploads.
            await log.WriteAsync("info", $"Starting to process time entries for {Domain}. Local date range: {FromDate:yyyy-MM-dd} to {ToDate:yyyy-MM-dd}; {_preview.Count} entries.");
            batch = new()
            {
                Domain = Domain, Email = Email, FromUtc = fromUtc, ToUtcExclusive = toUtc,
                StartedUtc = DateTime.UtcNow, TotalCount = _preview.Count
            };
            batch.Id = await uploadRepository.CreateHistoryAsync(batch);
            IsUploading = true;
            _stopRequested = false;
            StopCommand.NotifyCanExecuteChanged();
            FailedEntriesText = string.Empty;
            ProcessedCount = 0;
            for (; nextIndex < _preview.Count && !_stopRequested; nextIndex++)
            {
                var entry = _preview[nextIndex];
                StatusText = $"Uploading entry {nextIndex + 1} of {TotalCount}…";
                await log.WriteAsync("info", Safe($"Processing entry {nextIndex + 1}: {entry.Description} ({JiraWorklogClient.EntryStartUtc(entry).ToLocalTime():yyyy-MM-dd HH:mm:ss})", token));
                var result = await client.UploadAsync(entry, Domain, Email, token);
                // Account for the response before logging or persisting, so a local write failure cannot cause a re-send.
                if (result.Success)
                {
                    batch.SuccessCount++;
                    var start = JiraWorklogClient.EntryStartUtc(entry);
                    batch.FirstUploadedStartUtc = batch.FirstUploadedStartUtc is { } first && first < start ? first : start;
                    batch.LastUploadedStartUtc = batch.LastUploadedStartUtc is { } last && last > start ? last : start;
                }
                else
                {
                    batch.FailedCount++;
                    AppendFailure(failures, entry, result.Details);
                }
                ProcessedCount = nextIndex + 1;
                await uploadRepository.UpdateHistoryAsync(batch);
                var level = result.Success ? "info" : result.Details.StartsWith("API error:", StringComparison.Ordinal)
                    || result.Details.StartsWith("Network error.", StringComparison.Ordinal)
                    || result.Details.StartsWith("Request timed out.", StringComparison.Ordinal) ? "error" : "warn";
                await log.WriteAsync(level, Safe(result.Success
                    ? $"Success! Work log created for {result.TicketId}."
                    : $"Failed entry {ProcessedCount}: {result.Details}", token));
                if (nextIndex + 1 < _preview.Count && !_stopRequested) await Task.Delay(500);
            }
            if (_stopRequested)
            {
                for (var i = nextIndex; i < _preview.Count; i++) AppendFailure(failures, _preview[i], "Not uploaded: stopped by user.");
            }
            batch.Status = _stopRequested ? "Stopped" : batch.FailedCount > 0 ? "Completed with failures" : "Completed";
        }
        catch (ArgumentException ex) { ErrorText = ex.Message; }
        catch (Exception)
        {
            ErrorText = batch is { Id: > 0 }
                ? "Upload interrupted by a local error. Check history and Jira before trying again."
                : "Could not start upload. Check connection settings and database/log directory access.";
            if (batch is { Id: > 0 })
            {
                batch.Status = "Interrupted";
                // ProcessedCount is updated immediately after the remote result, including on local log/history failures.
                for (var i = ProcessedCount; i < _preview.Count; i++)
                    AppendFailure(failures, _preview[i], "Not uploaded: upload interrupted.");
            }
        }
        finally
        {
            if (batch is { Id: > 0 })
            {
                batch.CompletedUtc = DateTime.UtcNow;
                try { await uploadRepository.UpdateHistoryAsync(batch); historySaved = true; }
                catch (Exception) { ErrorText += " Could not save final history; check Jira before retrying."; }
                try
                {
                    await log.WriteAsync("info", $"Processing Summary: {batch.Status}. Total: {batch.TotalCount}; successful: {batch.SuccessCount}; failed: {batch.FailedCount}; not attempted: {batch.TotalCount - batch.SuccessCount - batch.FailedCount}.");
                    if (failures.Length > 0) await log.WriteAsync("warn", Safe("Failed Entries:\n" + failures, token));
                }
                catch (Exception) { ErrorText += " Could not write the final upload log."; }
                StatusText = $"{batch.Status}: {batch.SuccessCount} uploaded, {batch.FailedCount} failed, {batch.TotalCount - batch.SuccessCount - batch.FailedCount} not attempted.";
                if (!historySaved) StatusText += " History could not be fully saved.";
            }
            if (batch is { Id: > 0 }) FailedEntriesText = Safe(failures.ToString(), token);
            try { await RefreshHistoryAsync(); }
            catch (Exception) { ErrorText += " Could not refresh upload history."; }
            IsUploading = false;
            IsBusy = false;
        }
    }

    private string Safe(string text, string token) => JiraWorklogClient.Redact(text, token,
        string.IsNullOrEmpty(token) ? string.Empty : Convert.ToBase64String(Encoding.UTF8.GetBytes($"{Email}:{token}")));

    private static void AppendFailure(StringBuilder text, TimeEntry entry, string reason)
    {
        text.AppendLine($"Description: {entry.Description}")
            .AppendLine($"Start Date: {JiraWorklogClient.EntryStartUtc(entry).ToLocalTime():yyyy-MM-dd}")
            .AppendLine($"Start Time: {JiraWorklogClient.EntryStartUtc(entry).ToLocalTime():HH:mm:ss}")
            .AppendLine($"Duration: {DurationFormatter.Clock(entry.DurationSeconds)}")
            .AppendLine($"Reason: {reason}").AppendLine("----------------------------------------");
    }

    private async Task RefreshHistoryAsync()
    {
        var records = await uploadRepository.GetHistoryAsync();
        History.Clear();
        foreach (var record in records) History.Add(new(record));
        var last = records.FirstOrDefault(record => record.SuccessCount > 0);
        LastUploadText = last is null ? "No successfully uploaded entries recorded yet."
            : $"Last upload with successes: {last.StartedUtc.ToLocalTime():g} · {last.Domain} · latest entry date: {last.LastUploadedStartUtc?.ToLocalTime():yyyy-MM-dd}.";
    }
}

public sealed class JiraUploadHistoryItem(JiraUploadHistory record)
{
    public DateTime UploadedLocal => record.StartedUtc.ToLocalTime();
    public string Domain => record.Domain;
    public string Email => record.Email;
    public string Range => $"{record.FromUtc.ToLocalTime():yyyy-MM-dd} – {record.ToUtcExclusive.ToLocalTime().AddDays(-1):yyyy-MM-dd}";
    public string Counts => $"{record.SuccessCount} / {record.FailedCount} / {record.TotalCount - record.SuccessCount - record.FailedCount}";
    public string Status => record.CompletedUtc is null ? "Incomplete (completion not recorded)" : record.Status;
    public string LastEntry => record.LastUploadedStartUtc?.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss") ?? "—";
}
