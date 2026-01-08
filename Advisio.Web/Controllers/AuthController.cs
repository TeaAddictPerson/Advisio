using Microsoft.AspNetCore.Mvc;
using Advisio.API.DTOs;
using Advisio.API.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Advisio.Web.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }

    [HttpPost("register")]
    public async Task<ActionResult<AuthResultDto>> Register([FromBody] RegisterDto dto)
    {
        var result = await _authService.RegisterAsync(dto.Username, dto.Email, dto.Password);
        
        if (!result.Success)
        {
            return BadRequest(result);
        }

        // Сохраняем ID пользователя в сессии
        HttpContext.Session.SetInt32("UserId", result.User!.Id);
        HttpContext.Session.SetString("Username", result.User.Username);
        HttpContext.Session.SetString("UserRole", result.User.Role.ToString());

        return Ok(result);
    }

    [HttpPost("login")]
    public async Task<ActionResult<AuthResultDto>> Login([FromBody] LoginDto dto)
    {
        var result = await _authService.LoginAsync(dto.Email, dto.Password);
        
        if (!result.Success)
        {
            return Unauthorized(result);
        }

        // Сохраняем ID пользователя в сессии
        HttpContext.Session.SetInt32("UserId", result.User!.Id);
        HttpContext.Session.SetString("Username", result.User.Username);
        HttpContext.Session.SetString("UserRole", result.User.Role.ToString());

        return Ok(result);
    }

    [HttpPost("logout")]
    public ActionResult Logout()
    {
        HttpContext.Session.Clear();
        return Ok(new { message = "Выход выполнен успешно" });
    }

    [HttpGet("check")]
    public async Task<ActionResult> CheckAuth()
    {
        var userId = HttpContext.Session.GetInt32("UserId");
        var username = HttpContext.Session.GetString("Username");
        var role = HttpContext.Session.GetString("UserRole");

        if (userId == null)
        {
            return Ok(new { isAuthenticated = false });
        }

        // Get user email from database
        var userService = HttpContext.RequestServices.GetRequiredService<IUserService>();
        var user = await userService.GetUserByIdAsync(userId.Value);

        // Возвращаем роль как число (0 = User, 1 = Admin)
        int roleValue = 0;
        if (user != null)
        {
            roleValue = (int)user.Role;
        }
        else if (!string.IsNullOrEmpty(role))
        {
            // Если пользователь не найден, но роль есть в сессии, пытаемся распарсить
            if (role == "Admin" || role == "1")
            {
                roleValue = 1;
            }
        }

        return Ok(new
        {
            isAuthenticated = true,
            userId = userId.Value,
            username = username ?? "Пользователь",
            email = user?.Email ?? "",
            role = roleValue
        });
    }

    [HttpPut("profile")]
    public async Task<ActionResult<AuthResultDto>> UpdateProfile([FromBody] UpdateProfileDto dto)
    {
        var userId = HttpContext.Session.GetInt32("UserId");
        if (userId == null)
        {
            return Unauthorized(new { message = "Необходима авторизация" });
        }

        var result = await _authService.UpdateProfileAsync(
            userId.Value,
            dto.Username,
            dto.Email,
            dto.NewPassword,
            dto.CurrentPassword
        );

        if (!result.Success)
        {
            return BadRequest(result);
        }

        // Обновляем данные в сессии
        if (result.User != null)
        {
            HttpContext.Session.SetString("Username", result.User.Username);
            HttpContext.Session.SetString("UserRole", result.User.Role.ToString());
        }

        return Ok(result);
    }
}

