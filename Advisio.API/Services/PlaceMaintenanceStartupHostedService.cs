using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Advisio.API.Services;

/// <summary>
/// Один раз после старта: нормализация дат/описаний и удаление просроченных (не зависит от интервала PlacesSync).
/// </summary>
public sealed class PlaceMaintenanceStartupHostedService : IHostedService
{
    private readonly IHostApplicationLifetime _lifetime;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<PlaceMaintenanceStartupHostedService> _logger;

    public PlaceMaintenanceStartupHostedService(
        IHostApplicationLifetime lifetime,
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration,
        ILogger<PlaceMaintenanceStartupHostedService> logger)
    {
        _lifetime = lifetime;
        _scopeFactory = scopeFactory;
        _configuration = configuration;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (!_configuration.GetValue<bool>("PlacesSync:RunNormalizeAndExpireOnStartup", true))
            return Task.CompletedTask;

        _lifetime.ApplicationStarted.Register(() =>
        {
            _ = RunAsync(_lifetime.ApplicationStopping);
        });

        return Task.CompletedTask;
    }

    private async Task RunAsync(CancellationToken stoppingToken)
    {
        try
        {
            var delaySec = _configuration.GetValue<int>("PlacesSync:StartupMaintenanceDelaySeconds", 6);
            if (delaySec > 0)
                await Task.Delay(TimeSpan.FromSeconds(delaySec), stoppingToken).ConfigureAwait(false);

            await using var scope = _scopeFactory.CreateAsyncScope();
            var places = scope.ServiceProvider.GetRequiredService<IPlaceService>();

            var normalizeOn = _configuration.GetValue<bool>("PlacesSync:NormalizePlaceEventDescriptions", true);
            var normalizationOk = true;
            if (normalizeOn)
            {
                try
                {
                    var n = await places.NormalizeAllPlacesEventDescriptionsAndDatesAsync(stoppingToken).ConfigureAwait(false);
                    if (n > 0)
                        _logger.LogInformation(
                            "Стартовая нормализация мест (описание + EventStart/EventEnd): обновлено записей {Count}",
                            n);
                    _logger.LogInformation("Стартовая нормализация мест завершена; затем удаление просроченных.");
                }
                catch (Exception ex)
                {
                    normalizationOk = false;
                    _logger.LogError(ex, "Стартовая нормализация мест завершилась с ошибкой");
                }
            }

            if (_configuration.GetValue<bool>("PlacesSync:DeleteExpiredEventPlaces", true))
            {
                if (normalizeOn && !normalizationOk)
                {
                    _logger.LogWarning(
                        "Стартовое удаление просроченных пропущено: нормализация не завершилась успешно.");
                }
                else
                {
                    var d = await places.DeleteExpiredEventPlacesAsync(stoppingToken).ConfigureAwait(false);
                    if (d > 0)
                        _logger.LogInformation("Стартовое удаление просроченных мероприятий: {Count}", d);
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Стартовое обслуживание мест (нормализация / сроки) не выполнено полностью");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
