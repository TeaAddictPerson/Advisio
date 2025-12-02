using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Advisio.API.Services;
using Advisio.API.DTOs;

namespace Advisio.Web.Pages;

public class ProfileModel : PageModel
{
    private readonly IUserService _userService;
    private readonly IAuthService _authService;
    private readonly ILogger<ProfileModel> _logger;

    public ProfileModel(IUserService userService, IAuthService authService, ILogger<ProfileModel> logger)
    {
        _userService = userService;
        _authService = authService;
        _logger = logger;
    }

    public UserProfileDto? Profile { get; set; }
    public string? ErrorMessage { get; set; }
    public string? SuccessMessage { get; set; }
    
    [BindProperty]
    public UpdateProfileDto UpdateProfileDto { get; set; } = new();

    public async Task<IActionResult> OnGetAsync()
    {
        var userId = HttpContext.Session.GetInt32("UserId");
        
        if (userId == null)
        {
            return RedirectToPage("/Login");
        }

        try
        {
            await LoadProfileAsync(userId.Value);
            return Page();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при загрузке профиля пользователя {UserId}", userId);
            ErrorMessage = "Не удалось загрузить профиль";
            return Page();
        }
    }

    public IActionResult OnPostLogout()
    {
        HttpContext.Session.Clear();
        return RedirectToPage("/Index");
    }

    public async Task<IActionResult> OnPostUpdateProfileAsync()
    {
        var userId = HttpContext.Session.GetInt32("UserId");
        
        if (userId == null)
        {
            return RedirectToPage("/Login");
        }

        if (!ModelState.IsValid)
        {
            await LoadProfileAsync(userId.Value);
            return Page();
        }

        var result = await _authService.UpdateProfileAsync(
            userId.Value,
            UpdateProfileDto.Username,
            UpdateProfileDto.Email,
            UpdateProfileDto.NewPassword,
            UpdateProfileDto.CurrentPassword
        );

        if (!result.Success)
        {
            ErrorMessage = result.Message;
            await LoadProfileAsync(userId.Value);
            return Page();
        }

        // Обновляем имя пользователя в сессии
        HttpContext.Session.SetString("Username", result.User!.Username);
        
        SuccessMessage = result.Message;
        await LoadProfileAsync(userId.Value);
        return Page();
    }

    private async Task LoadProfileAsync(int userId)
    {
        try
        {
            Profile = await _userService.GetUserProfileAsync(userId);
            if (Profile != null)
            {
                UpdateProfileDto.Username = Profile.User.Username;
                UpdateProfileDto.Email = Profile.User.Email;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при загрузке профиля пользователя {UserId}", userId);
            ErrorMessage = "Не удалось загрузить профиль";
        }
    }
}

