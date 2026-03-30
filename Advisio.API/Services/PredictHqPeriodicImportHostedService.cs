using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Advisio.API.Services;

public sealed class PredictHqPeriodicImportHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<PredictHqPeriodicImportHostedService> _logger;

    public PredictHqPeriodicImportHostedService(
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration,
        ILogger<PredictHqPeriodicImportHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _configuration = configuration;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_configuration.GetValue<bool>("PlacesSync:ExternalImportEnabled", true))
            return;

        if (!_configuration.GetValue<bool>("PredictHQ:PeriodicImportEnabled", true))
            return;

        var hours = _configuration.GetValue<double>("PredictHQ:PeriodicImportIntervalHours", 6);
        if (hours <= 0)
            hours = 6;

        var minBatch = _configuration.GetValue<int>("PredictHQ:PeriodicImportBatchMin", 50);
        var maxBatch = _configuration.GetValue<int>("PredictHQ:PeriodicImportBatchMax", 100);
        if (minBatch > maxBatch)
            (minBatch, maxBatch) = (maxBatch, minBatch);
        minBatch = Math.Clamp(minBatch, 1, 500);
        maxBatch = Math.Clamp(maxBatch, minBatch, 500);

        var delay = TimeSpan.FromHours(hours);
        _logger.LogInformation(
            "PredictHQ: периодический импорт каждые {Hours} ч., размер партии {Min}-{Max} мест",
            hours,
            minBatch,
            maxBatch);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(delay, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            try
            {
                await using var scope = _scopeFactory.CreateAsyncScope();
                var importer = scope.ServiceProvider.GetService<IPredictHqEventsImportService>();
                if (importer == null)
                    continue;

                var batch = Random.Shared.Next(minBatch, maxBatch + 1);
                var n = await importer.ImportEventsAsync(forceRun: true, maxPlacesToAdd: batch, stoppingToken).ConfigureAwait(false);
                if (n > 0)
                    _logger.LogInformation("PredictHQ (по расписанию): добавлено {Added} новых мест (лимит партии {Batch})", n, batch);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "PredictHQ: ошибка периодического импорта");
            }
        }
    }
}
