using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Advisio.API.Services;
using Advisio.API.DTOs;
using Advisio.API.Models;

namespace Advisio.Web.Pages;

public class RegisterModel : PageModel
{
    private readonly IAuthService _authService;
    private readonly ILogger<RegisterModel> _logger;

    public RegisterModel(IAuthService authService, ILogger<RegisterModel> logger)
    {
        _authService = authService;
        _logger = logger;
    }

    [BindProperty]
    public RegisterDto RegisterDto { get; set; } = new();

    public string? ErrorMessage { get; set; }

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        var result = await _authService.RegisterAsync(
            RegisterDto.Username,
            RegisterDto.Email,
            RegisterDto.Password
        );

        if (!result.Success)
        {
            ErrorMessage = result.Message;
            return Page();
        }

        // Сохраняем ID пользователя в сессии
        HttpContext.Session.SetInt32("UserId", result.User!.Id);
        HttpContext.Session.SetString("Username", result.User.Username);
        HttpContext.Session.SetString("Role", result.User.Role.ToString());

        // Если админ, перенаправляем на админскую панель
        if (result.User.Role == UserRole.Admin)
        {
            return RedirectToPage("/Admin");
        }

        return RedirectToPage("/Profile");
    }
}


