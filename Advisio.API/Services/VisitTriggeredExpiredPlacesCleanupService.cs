using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Advisio.API.Services;

public sealed class VisitTriggeredExpiredPlacesCleanupService : IVisitTriggeredExpiredPlacesCleanupService
{
    private const string LastRunUtcKey = nameof(VisitTriggeredExpiredPlacesCleanupService) + "_LastRunUtc";

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IMemoryCache _cache;
    private readonly IConfiguration _configuration;
    private readonly ILogger<VisitTriggeredExpiredPlacesCleanupService> _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public VisitTriggeredExpiredPlacesCleanupService(
        IServiceScopeFactory scopeFactory,
        IMemoryCache cache,
        IConfiguration configuration,
        ILogger<VisitTriggeredExpiredPlacesCleanupService> logger)
    {
        _scopeFactory = scopeFactory;
        _cache = cache;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<VisitTriggeredExpireCleanupResult> TryRemoveExpiredPlacesOnSiteVisitAsync(
        CancellationToken cancellationToken = default)
    {
        if (!_configuration.GetValue<bool>("PlacesSync:ExpireOnVisitEnabled", true))
            return new VisitTriggeredExpireCleanupResult(0, false);

        if (!_configuration.GetValue<bool>("PlacesSync:DeleteExpiredEventPlaces", true))
            return new VisitTriggeredExpireCleanupResult(0, false);

        var minMinutes = _configuration.GetValue<double>("PlacesSync:ExpireOnVisitMinIntervalMinutes", 3);
        if (minMinutes > 0
            && _cache.TryGetValue(LastRunUtcKey, out DateTime lastUtc)
            && DateTime.UtcNow - lastUtc < TimeSpan.FromMinutes(minMinutes))
        {
            return new VisitTriggeredExpireCleanupResult(0, true);
        }

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (minMinutes > 0
                && _cache.TryGetValue(LastRunUtcKey, out lastUtc)
                && DateTime.UtcNow - lastUtc < TimeSpan.FromMinutes(minMinutes))
            {
                return new VisitTriggeredExpireCleanupResult(0, true);
            }

            await using var scope = _scopeFactory.CreateAsyncScope();
            var placeService = scope.ServiceProvider.GetRequiredService<IPlaceService>();

            var normalizeOnVisit = _configuration.GetValue<bool>("PlacesSync:ExpireOnVisitNormalizeFirst", true);
            var normalizeEnabled = _configuration.GetValue<bool>("PlacesSync:NormalizePlaceEventDescriptions", true);

            if (normalizeOnVisit && normalizeEnabled)
            {
                try
                {
                    var normalized = await placeService.NormalizeAllPlacesEventDescriptionsAndDatesAsync(cancellationToken)
                        .ConfigureAwait(false);
                    if (normalized > 0)
                        _logger.LogInformation(
                            "Очистка по визиту: нормализовано описаний и дат перед удалением просроченных: {Count}",
                            normalized);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex,
                        "Очистка просроченных по визиту: нормализация не удалась — удаление пропущено (как при синхронизации).");
                    return new VisitTriggeredExpireCleanupResult(0, false);
                }
            }

            var removed = await placeService.DeleteExpiredEventPlacesAsync(cancellationToken).ConfigureAwait(false);

            _cache.Set(
                LastRunUtcKey,
                DateTime.UtcNow,
                new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(6) });

            if (removed > 0)
                _logger.LogInformation("Очистка по открытию сайта: удалено просроченных мероприятий: {Count}", removed);

            return new VisitTriggeredExpireCleanupResult(removed, false);
        }
        finally
        {
            _gate.Release();
        }
    }
}
