using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Advisio.API.Services;
using Advisio.API.DTOs;

namespace Advisio.Web.Pages;

public class LoginModel : PageModel
{
    private readonly IAuthService _authService;
    private readonly ILogger<LoginModel> _logger;

    public LoginModel(IAuthService authService, ILogger<LoginModel> logger)
    {
        _authService = authService;
        _logger = logger;
    }

    [BindProperty]
    public LoginDto LoginDto { get; set; } = new();

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

        var result = await _authService.LoginAsync(
            LoginDto.Email,
            LoginDto.Password
        );

        if (!result.Success)
        {
            ErrorMessage = result.Message;
            return Page();
        }

        // Сохраняем ID пользователя в сессии
        HttpContext.Session.SetInt32("UserId", result.User!.Id);
        HttpContext.Session.SetString("Username", result.User.Username);

        return RedirectToPage("/Profile");
    }
}

