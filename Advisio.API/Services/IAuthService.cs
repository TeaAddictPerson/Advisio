using Advisio.API.DTOs;

namespace Advisio.API.Services;

public interface IAuthService
{
    Task<AuthResultDto> RegisterAsync(string username, string email, string password);
    Task<AuthResultDto> LoginAsync(string email, string password);
    Task<bool> UserExistsByEmailAsync(string email);
    Task<bool> UserExistsByUsernameAsync(string username);
    Task<AuthResultDto> UpdateProfileAsync(int userId, string username, string email, string? newPassword, string? currentPassword);
}

