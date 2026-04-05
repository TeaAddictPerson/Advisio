using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Advisio.API.Services;

public sealed class VisitTriggeredPredictHqImportService : IVisitTriggeredPredictHqImportService
{
    private const string LastRunUtcKey = nameof(VisitTriggeredPredictHqImportService) + "_LastRunUtc";

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IMemoryCache _cache;
    private readonly IConfiguration _configuration;
    private readonly ILogger<VisitTriggeredPredictHqImportService> _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public VisitTriggeredPredictHqImportService(
        IServiceScopeFactory scopeFactory,
        IMemoryCache cache,
        IConfiguration configuration,
        ILogger<VisitTriggeredPredictHqImportService> logger)
    {
        _scopeFactory = scopeFactory;
        _cache = cache;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<VisitTriggeredImportResult> TryPullNewPlacesOnSiteVisitAsync(CancellationToken cancellationToken = default)
    {
        if (!_configuration.GetValue<bool>("PlacesSync:ImportOnVisitEnabled", true))
            return new VisitTriggeredImportResult(0, false);

        await using (var probe = _scopeFactory.CreateAsyncScope())
        {
            var importerProbe = probe.ServiceProvider.GetService<IPredictHqEventsImportService>();
            if (importerProbe == null || !importerProbe.IsConfiguredForImport())
                return new VisitTriggeredImportResult(0, false);
        }

        var minMinutes = _configuration.GetValue<double>("PlacesSync:ImportOnVisitMinIntervalMinutes", 3);
        if (minMinutes > 0
            && _cache.TryGetValue(LastRunUtcKey, out DateTime lastUtc)
            && DateTime.UtcNow - lastUtc < TimeSpan.FromMinutes(minMinutes))
        {
            return new VisitTriggeredImportResult(0, true);
        }

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (minMinutes > 0
                && _cache.TryGetValue(LastRunUtcKey, out lastUtc)
                && DateTime.UtcNow - lastUtc < TimeSpan.FromMinutes(minMinutes))
            {
                return new VisitTriggeredImportResult(0, true);
            }

            await using (var scope = _scopeFactory.CreateAsyncScope())
            {
                var importer = scope.ServiceProvider.GetRequiredService<IPredictHqEventsImportService>();
                if (!importer.IsConfiguredForImport())
                    return new VisitTriggeredImportResult(0, false);

                var maxNew = _configuration.GetValue<int>("PlacesSync:ImportOnVisitMaxNewPlaces", 80);
                if (maxNew <= 0)
                    maxNew = 80;

                var added = await importer
                    .ImportEventsAsync(forceRun: true, maxPlacesToAdd: maxNew, cancellationToken, exhaustivePagination: false)
                    .ConfigureAwait(false);

                _cache.Set(
                    LastRunUtcKey,
                    DateTime.UtcNow,
                    new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(6) });

                if (added > 0)
                    _logger.LogInformation("PredictHQ (открытие сайта): добавлено новых мест: {Count}", added);

                return new VisitTriggeredImportResult(added, false);
            }
        }
        finally
        {
            _gate.Release();
        }
    }
}
