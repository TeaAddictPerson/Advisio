using Microsoft.AspNetCore.Mvc;
using Advisio.API.DTOs;
using Advisio.API.Services;

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
    public async Task<ActionResult<AuthResultDto>> Register([FromBody] RegisterDto registerDto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var result = await _authService.RegisterAsync(
            registerDto.Username,
            registerDto.Email,
            registerDto.Password
        );

        if (!result.Success)
        {
            return BadRequest(result);
        }

        return Ok(result);
    }

    [HttpPost("login")]
    public async Task<ActionResult<AuthResultDto>> Login([FromBody] LoginDto loginDto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var result = await _authService.LoginAsync(
            loginDto.Email,
            loginDto.Password
        );

        if (!result.Success)
        {
            return Unauthorized(result);
        }

        return Ok(result);
    }

    [HttpPost("logout")]
    public IActionResult Logout()
    {
        HttpContext.Session.Clear();
        return Ok(new { message = "Выход выполнен успешно" });
    }

    [HttpGet("check")]
    public IActionResult CheckAuth()
    {
        var userId = HttpContext.Session.GetInt32("UserId");
        var username = HttpContext.Session.GetString("Username");

        if (userId == null)
        {
            return Ok(new { isAuthenticated = false });
        }

        return Ok(new
        {
            isAuthenticated = true,
            userId = userId,
            username = username
        });
    }
}


