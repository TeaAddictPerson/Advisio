using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Advisio.API.Services;

namespace Advisio.Web.Pages;

public class AdminModel : PageModel
{
    private readonly IPlaceService _placeService;
    private readonly IPredictHQService _predictHQService;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<AdminModel> _logger;

    public AdminModel(
        IPlaceService placeService,
        IPredictHQService predictHQService,
        IServiceProvider serviceProvider,
        ILogger<AdminModel> logger)
    {
        _placeService = placeService;
        _predictHQService = predictHQService;
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public string? Message { get; set; }
    public string? ErrorMessage { get; set; }

    public IActionResult OnGet()
    {
        var userId = HttpContext.Session.GetInt32("UserId");
        var role = HttpContext.Session.GetString("Role");

        if (userId == null || role != "Admin")
        {
            return RedirectToPage("/Login");
        }

        return Page();
    }

    public async Task<IActionResult> OnPostSyncPlacesAsync()
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var predictHQService = scope.ServiceProvider.GetRequiredService<IPredictHQService>();
            var placeService = scope.ServiceProvider.GetRequiredService<IPlaceService>();

            Message = "Начало синхронизации...";
            
            await predictHQService.LoadPlacesFromPredictHQAsync();
            Message += " PredictHQ загружен. ";
            
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
        var userId = HttpContext.Session.GetInt32("UserId");
        var role = HttpContext.Session.GetString("Role");

        if (userId == null || role != "Admin")
        {
            return RedirectToPage("/Login");
        }

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


