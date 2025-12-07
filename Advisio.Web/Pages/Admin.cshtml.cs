using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Advisio.API.Services;

namespace Advisio.Web.Pages;

public class AdminModel : PageModel
{
    private readonly IPlaceService _placeService;
    private readonly IKufarService _kufarService;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<AdminModel> _logger;

    public AdminModel(
        IPlaceService placeService,
        IKufarService kufarService,
        IServiceProvider serviceProvider,
        ILogger<AdminModel> logger)
    {
        _placeService = placeService;
        _kufarService = kufarService;
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public string? Message { get; set; }
    public string? ErrorMessage { get; set; }

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostSyncPlacesAsync()
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var kufarService = scope.ServiceProvider.GetRequiredService<IKufarService>();
            var placeService = scope.ServiceProvider.GetRequiredService<IPlaceService>();

            Message = "Начало синхронизации...";
            
            await kufarService.LoadPlacesFromKufarAsync();
            Message += " Kufar загружен. ";
            
            await kufarService.LoadPlacesFromKvitkiAsync();
            Message += " Kvitki загружен. ";
            
            await placeService.AssignCategoriesToPlacesWithoutCategoriesAsync();
            Message += " Категории присвоены. Синхронизация завершена!";

            return Page();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка синхронизации");
            ErrorMessage = $"Ошибка: {ex.Message}";
            return Page();
        }
    }

    public async Task<IActionResult> OnPostAssignCategoriesAsync()
    {
        try
        {
            await _placeService.AssignCategoriesToPlacesWithoutCategoriesAsync();
            Message = "Категории успешно присвоены местам без категорий";
            return Page();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка присвоения категорий");
            ErrorMessage = $"Ошибка: {ex.Message}";
            return Page();
        }
    }
}


