using Microsoft.AspNetCore.Mvc;
using Advisio.API.DTOs;
using Advisio.API.Services;

namespace Advisio.Web.Controllers;

[ApiController]
[Route("api/[controller]")]
public class UsersController : ControllerBase
{
    private readonly IUserService _userService;

    public UsersController(IUserService userService)
    {
        _userService = userService;
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<UserDto>> GetUser(int id)
    {
        var user = await _userService.GetUserByIdAsync(id);
        if (user == null)
            return NotFound();

        return Ok(user);
    }

    [HttpGet("{id}/profile")]
    public async Task<ActionResult<UserProfileDto>> GetUserProfile(int id)
    {
        try
        {
            var profile = await _userService.GetUserProfileAsync(id);
            return Ok(profile);
        }
        catch (ArgumentException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    [HttpGet("{id}/stats")]
    public async Task<ActionResult<ProfileStatsDto>> GetProfileStats(int id)
    {
        try
        {
            var stats = await _userService.GetProfileStatsAsync(id);
            return Ok(stats);
        }
        catch (ArgumentException)
        {
            return NotFound();
        }
    }
}

