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
        
        _logger.LogInformation("Настройки синхронизации: интервал = {Interval} ч., максимум мест = {MaxPlaces}", 
            intervalHours, _configuration.GetValue<int>("PlacesSync:MaxPlacesPerSync", 2000));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Сервис синхронизации мест запущен. Интервал: {Interval}", _syncInterval);

        try
        {
            _logger.LogInformation("Запуск первоначальной синхронизации мест...");
            await SyncPlacesAsync();
            _logger.LogInformation("Первоначальная синхронизация завершена успешно");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при первоначальной синхронизации");
        }

        await Task.Delay(_syncInterval, stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                _logger.LogInformation("Начало синхронизации в {Time}", DateTime.Now);
                await SyncPlacesAsync();
                _logger.LogInformation("Синхронизация завершена в {Time}", DateTime.Now);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка синхронизации в {Time}: {Error}", DateTime.Now, ex.Message);
            }

            _logger.LogInformation("Ожидание {Interval} до следующей синхронизации...", _syncInterval);
            await Task.Delay(_syncInterval, stoppingToken);
        }
    }

    public async Task SyncPlacesAsync()
    {
        using var scope = _serviceProvider.CreateScope();
        var placeService = scope.ServiceProvider.GetRequiredService<IPlaceService>();

        try
        {
            if (_configuration.GetValue<bool>("PlacesSync:PruneLegacyPredictHqDuplicates", true))
            {
                var removed = await placeService.PruneLegacyPredictHqPlaceDuplicatesAsync();
                if (removed > 0)
                    _logger.LogInformation("Удалено дубликатов / сырых мест PredictHQ (строк Places): {Count}", removed);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при очистке дубликатов PredictHQ");
        }

        var normalizeEnabled = _configuration.GetValue<bool>("PlacesSync:NormalizePlaceEventDescriptions", true);
        var normalizationSucceeded = true;
        try
        {
            if (normalizeEnabled)
            {
                var normalized = await placeService.NormalizeAllPlacesEventDescriptionsAndDatesAsync();
                if (normalized > 0)
                    _logger.LogInformation(
                        "Нормализовано описаний и дат мероприятий (в т.ч. «Дата и время: …» → Начало/Окончание): {Count}",
                        normalized);
                _logger.LogInformation("Фаза нормализации мест завершена; далее — удаление просроченных по БД.");
            }
        }
        catch (Exception ex)
        {
            normalizationSucceeded = false;
            _logger.LogError(ex, "Ошибка при нормализации описаний и дат мероприятий");
        }

        try
        {
            if (_configuration.GetValue<bool>("PlacesSync:DeleteExpiredEventPlaces", true))
            {
                if (normalizeEnabled && !normalizationSucceeded)
                {
                    _logger.LogWarning(
                        "Удаление просроченных мероприятий пропущено: сначала должна успешно завершиться нормализация всех мест.");
                }
                else
                {
                    var expiredRemoved = await placeService.DeleteExpiredEventPlacesAsync();
                    if (expiredRemoved > 0)
                        _logger.LogInformation(
                            "Удалено просроченных мероприятий (по EventEndDate в БД и при необходимости по описанию, UTC): {Count}",
                            expiredRemoved);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при удалении мероприятий с истёкшей датой");
        }

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
            var imageBackfillOn = _configuration.GetValue<bool>("PlacesSync:ImageBackfillEnabled", true);
            var maxImageBackfill = _configuration.GetValue<int>("PlacesSync:MaxPlacesImageBackfillPerRun", 50);
            if (imageBackfillOn && maxImageBackfill > 0)
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

