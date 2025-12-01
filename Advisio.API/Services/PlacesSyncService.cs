using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Advisio.API.Services;

public interface IPlacesSyncService
{
    Task SyncPlacesAsync();
}

public class PlacesSyncService : BackgroundService, IPlacesSyncService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<PlacesSyncService> _logger;
    private readonly IConfiguration _configuration;
    private readonly TimeSpan _syncInterval;

    public PlacesSyncService(
        IServiceProvider serviceProvider,
        ILogger<PlacesSyncService> logger,
        IConfiguration configuration)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _configuration = configuration;
        
        // Получаем интервал синхронизации из конфигурации (по умолчанию 24 часа)
        var intervalHours = _configuration.GetValue<int>("PlacesSync:IntervalHours", 24);
        _syncInterval = TimeSpan.FromHours(intervalHours);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Сервис синхронизации мест запущен. Интервал: {Interval}", _syncInterval);

        // Ждем 30 секунд перед первой синхронизацией (чтобы приложение успело запуститься)
        await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                _logger.LogInformation("Начало синхронизации мест в {Time}", DateTime.Now);
                await SyncPlacesAsync();
                _logger.LogInformation("Синхронизация мест завершена успешно в {Time}", DateTime.Now);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при синхронизации мест в {Time}", DateTime.Now);
            }

            // Ждем до следующей синхронизации
            await Task.Delay(_syncInterval, stoppingToken);
        }
    }

    public async Task SyncPlacesAsync()
    {
        using var scope = _serviceProvider.CreateScope();
        var kufarService = scope.ServiceProvider.GetRequiredService<IKufarService>();

        try
        {
            // Загружаем места из Kufar
            _logger.LogInformation("Загрузка мест из Kufar.by...");
            await kufarService.LoadPlacesFromKufarAsync();
            _logger.LogInformation("Места из Kufar.by загружены успешно");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при загрузке мест из Kufar.by");
            // Продолжаем выполнение даже если один источник не работает
        }

        try
        {
            // Загружаем места из Kvitki
            _logger.LogInformation("Загрузка мест из Kvitki.by...");
            await kufarService.LoadPlacesFromKvitkiAsync();
            _logger.LogInformation("Места из Kvitki.by загружены успешно");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при загрузке мест из Kvitki.by");
            // Продолжаем выполнение даже если один источник не работает
        }
    }
}

