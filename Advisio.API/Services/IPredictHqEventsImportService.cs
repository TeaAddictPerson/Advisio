namespace Advisio.API.Services;

public interface IPredictHqEventsImportService
{
    Task<int> ImportEventsAsync(
        bool forceRun = false,
        int? maxPlacesToAdd = null,
        CancellationToken cancellationToken = default,
        bool exhaustivePagination = false);
}
