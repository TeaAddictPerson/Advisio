namespace Advisio.API.Services;

public sealed record VisitTriggeredImportResult(int AddedNewPlaces, bool SkippedDueToInterval);

/// <summary>
/// Импорт новых мест из PredictHQ при открытии сайта (с интервалом и блокировкой параллельных запусков).
/// </summary>
public interface IVisitTriggeredPredictHqImportService
{
    Task<VisitTriggeredImportResult> TryPullNewPlacesOnSiteVisitAsync(CancellationToken cancellationToken = default);
}
