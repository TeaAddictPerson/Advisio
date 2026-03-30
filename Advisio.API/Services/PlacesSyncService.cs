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
        
        var intervalHours = _configuration.GetValue<double>("PlacesSync:IntervalHours", 0.25);
        _syncInterval = TimeSpan.FromHours(intervalHours);
        
        _logger.LogInformation("⚙️ Настройки синхронизации: Интервал = {Interval} часов, Максимум мест = {MaxPlaces}", 
            intervalHours, _configuration.GetValue<int>("PlacesSync:MaxPlacesPerSync", 2000));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("🚀 Сервис синхронизации мест запущен. Интервал: {Interval}", _syncInterval);

        try
        {
            _logger.LogInformation("🔄 Запуск первоначальной синхронизации мест...");
            await SyncPlacesAsync();
            _logger.LogInformation("✅ Первоначальная синхронизация завершена успешно");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Ошибка при первоначальной синхронизации");
        }

        await Task.Delay(_syncInterval, stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                _logger.LogInformation("🔄 НАЧАЛО СИНХРОНИЗАЦИИ в {Time}", DateTime.Now);
                await SyncPlacesAsync();
                _logger.LogInformation("✅ СИНХРОНИЗАЦИЯ ЗАВЕРШЕНА в {Time}", DateTime.Now);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ ОШИБКА СИНХРОНИЗАЦИИ в {Time}: {Error}", DateTime.Now, ex.Message);
            }

            _logger.LogInformation("⏳ Ожидание {Interval} до следующей синхронизации...", _syncInterval);
            await Task.Delay(_syncInterval, stoppingToken);
        }
    }

    public async Task SyncPlacesAsync()
    {
        using var scope = _serviceProvider.CreateScope();
        var placeService = scope.ServiceProvider.GetRequiredService<IPlaceService>();

        try
        {
            _logger.LogInformation("Присвоение категорий местам без категорий...");
            await placeService.AssignCategoriesToPlacesWithoutCategoriesAsync();
            _logger.LogInformation("Категории присвоены успешно");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при присвоении категорий");
        }

        try
        {
            var maxImageBackfill = _configuration.GetValue<int>("PlacesSync:MaxPlacesImageBackfillPerRun", 50);
            if (maxImageBackfill > 0)
            {
                _logger.LogInformation("Автоподбор изображений для мест без картинки (до {Max})...", maxImageBackfill);
                var imageCount = await placeService.AssignImagesToPlacesWithoutImagesAsync(maxImageBackfill);
                _logger.LogInformation("Обновлено изображений у мест: {Count}", imageCount);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при автоподборе изображений для мест");
        }
    }
}

