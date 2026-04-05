namespace Advisio.API.Services;

public interface IPredictHqEventsImportService
{
    /// <summary>Внешний импорт включён и задан токен PredictHQ.</summary>
    bool IsConfiguredForImport();

    Task<int> ImportEventsAsync(
        bool forceRun = false,
        int? maxPlacesToAdd = null,
        CancellationToken cancellationToken = default,
        bool exhaustivePagination = false);
}
