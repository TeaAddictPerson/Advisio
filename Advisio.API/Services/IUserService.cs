using Advisio.API.DTOs;

namespace Advisio.API.Services;

public interface IUserService
{
    Task<UserProfileDto> GetUserProfileAsync(int userId);
    Task<UserDto?> GetUserByIdAsync(int id);
}

