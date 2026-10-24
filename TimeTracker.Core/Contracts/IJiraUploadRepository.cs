using TimeTracker.Core.Models;

namespace TimeTracker.Core.Contracts;

public interface IJiraUploadRepository
{
    Task<JiraConnectionSettings?> GetSettingsAsync(CancellationToken cancellationToken = default);
    Task SaveSettingsAsync(JiraConnectionSettings settings, CancellationToken cancellationToken = default);
    Task<int> CreateHistoryAsync(JiraUploadHistory history, CancellationToken cancellationToken = default);
    Task UpdateHistoryAsync(JiraUploadHistory history, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<JiraUploadHistory>> GetHistoryAsync(CancellationToken cancellationToken = default);
}
