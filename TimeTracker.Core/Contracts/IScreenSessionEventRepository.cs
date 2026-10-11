using TimeTracker.Core.Models;

namespace TimeTracker.Core.Contracts;

public interface IScreenSessionEventRepository
{
    Task RecordSessionEventAsync(string eventType, DateTime occurredUtc, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ScreenSessionEvent>> GetRecentSessionEventsAsync(int take, CancellationToken cancellationToken = default);
    Task ClearAllSessionEventsAsync(CancellationToken cancellationToken = default);
}
