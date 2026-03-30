namespace Advisio.API.Services;

/// <summary>Импорт событий PredictHQ (<c>PredictHQ:ApiToken</c>, <c>PredictHQ:EventsUrl</c>).</summary>
public interface IPredictHqEventsImportService
{
    /// <summary>
    /// Загрузка страниц событий и создание мест.
    /// При <paramref name="forceRun"/> = true не учитывается интервал <c>PlacesSync:ExternalImportIntervalHours</c>.
    /// <paramref name="maxPlacesToAdd"/> — сколько новых мест максимум создать за этот запуск (иначе <c>PlacesSync:MaxPlacesPerSync</c>).
    /// </summary>
    Task<int> ImportEventsAsync(bool forceRun = false, int? maxPlacesToAdd = null, CancellationToken cancellationToken = default);
}
