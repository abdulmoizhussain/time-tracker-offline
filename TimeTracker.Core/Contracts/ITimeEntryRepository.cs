using TimeTracker.Core.Models;

namespace TimeTracker.Core.Contracts;

public interface ITimeEntryRepository
{
    Task InitializeAsync(CancellationToken cancellationToken = default);
    Task<int> StartTimerAsync(string description, DateTime startUtc, CancellationToken cancellationToken = default);
    Task StopTimerAsync(int id, DateTime endUtc, CancellationToken cancellationToken = default);
    Task<int> AddDurationEntryAsync(string description, TimeSpan duration, CancellationToken cancellationToken = default);
    Task<int> AddDurationEntryAsync(string description, TimeSpan duration, DateTime startDateLocal, CancellationToken cancellationToken = default);
    Task<int> AddManualRangeEntryAsync(string description, DateTime startLocal, DateTime endLocal, CancellationToken cancellationToken = default);
    Task UpdateEntryAsync(TimeEntry entry, CancellationToken cancellationToken = default);
    Task UpdateRunningDescriptionAsync(int id, string description, CancellationToken cancellationToken = default);
    Task DeleteEntryAsync(int id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TimeEntry>> GetRecentEntriesAsync(int take, CancellationToken cancellationToken = default);
    Task<TimeEntryDateBounds> GetEntryDateBoundsAsync(TimeEntryFilter filter, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TimeEntry>> GetWeekEntriesAsync(TimeEntryFilter filter, DateTime weekStartUtc, DateTime weekEndUtcExclusive,
        TimeEntrySort sort, bool descending, CancellationToken cancellationToken = default);
    Task<TimeEntry?> GetRunningEntryAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<string>> SearchDescriptionsAsync(string search, int take, CancellationToken cancellationToken = default);
    Task<int> GetDatabaseVersionAsync(CancellationToken cancellationToken = default);
    Task ClearAllDataAsync(CancellationToken cancellationToken = default);
    Task RecordImportExportAsync(ImportExportHistory record, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ImportExportHistory>> GetRecentImportExportHistoryAsync(int take, CancellationToken cancellationToken = default);
}
