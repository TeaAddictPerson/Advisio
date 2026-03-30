using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Advisio.API.Services;

public sealed class PredictHqStartupImportHostedService : IHostedService
{
    private readonly IHostApplicationLifetime _lifetime;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<PredictHqStartupImportHostedService> _logger;

    public PredictHqStartupImportHostedService(
        IHostApplicationLifetime lifetime,
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration,
        ILogger<PredictHqStartupImportHostedService> logger)
    {
        _lifetime = lifetime;
        _scopeFactory = scopeFactory;
        _configuration = configuration;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (!_configuration.GetValue<bool>("PlacesSync:ExternalImportEnabled", true))
            return Task.CompletedTask;

        var max = _configuration.GetValue<int>("PredictHQ:MaxPlacesOnStartup", 150);
        if (max <= 0)
            return Task.CompletedTask;

        _lifetime.ApplicationStarted.Register(() =>
        {
            _ = RunImportAsync(max, _lifetime.ApplicationStopping);
        });

        return Task.CompletedTask;
    }

    private async Task RunImportAsync(int max, CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken).ConfigureAwait(false);
            await using var scope = _scopeFactory.CreateAsyncScope();
            var importer = scope.ServiceProvider.GetService<IPredictHqEventsImportService>();
            if (importer == null)
                return;
            var n = await importer.ImportEventsAsync(forceRun: true, maxPlacesToAdd: max, stoppingToken).ConfigureAwait(false);
            if (n > 0)
                _logger.LogInformation("PredictHQ (старт приложения): добавлено новых мест: {Count} (лимит {Max})", n, max);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "PredictHQ: стартовый импорт не выполнен");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
