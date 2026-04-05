namespace Advisio.API.Services;

public sealed record VisitTriggeredExpireCleanupResult(int RemovedExpiredPlaces, bool SkippedDueToInterval);

/// <summary>
/// Удаление просроченных мероприятий при открытии сайта (интервал и блокировка параллельных запусков).
/// </summary>
public interface IVisitTriggeredExpiredPlacesCleanupService
{
    Task<VisitTriggeredExpireCleanupResult> TryRemoveExpiredPlacesOnSiteVisitAsync(CancellationToken cancellationToken = default);
}
