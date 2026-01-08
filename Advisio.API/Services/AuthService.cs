using Microsoft.EntityFrameworkCore;
using Advisio.API.Data;
using Advisio.API.DTOs;
using Advisio.API.Models;
using System.Security.Cryptography;
using System.Text;

namespace Advisio.API.Services;

public class AuthService : IAuthService
{
    private readonly AdvisioDbContext _context;

    public AuthService(AdvisioDbContext context)
    {
        _context = context;
    }

    public async Task<AuthResultDto> RegisterAsync(string username, string email, string password)
    {
        if (await UserExistsByEmailAsync(email))
        {
            return new AuthResultDto
            {
                Success = false,
                Message = "Пользователь с таким email уже существует"
            };
        }

        if (await UserExistsByUsernameAsync(username))
        {
            return new AuthResultDto
            {
                Success = false,
                Message = "Пользователь с таким именем уже существует"
            };
        }

        var passwordHash = HashPassword(password);
        var user = new User
        {
            Username = username,
            Email = email,
            PasswordHash = passwordHash,
            Role = email.ToLower() == "admin@gmail.com" ? UserRole.Admin : UserRole.User,
            CreatedAt = DateTime.UtcNow
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        return new AuthResultDto
        {
            Success = true,
            Message = "Регистрация успешна",
            User = new UserDto
            {
                Id = user.Id,
                Username = user.Username,
                Email = user.Email,
                Role = user.Role
            }
        };
    }

    public async Task<AuthResultDto> LoginAsync(string email, string password)
    {
        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.Email == email);

        if (user == null || !VerifyPassword(password, user.PasswordHash))
        {
            return new AuthResultDto
            {
                Success = false,
                Message = "Неверный email или пароль"
            };
        }

        // Ensure admin@gmail.com always has Admin role
        if (email.ToLower() == "admin@gmail.com" && user.Role != UserRole.Admin)
        {
            user.Role = UserRole.Admin;
            await _context.SaveChangesAsync();
        }

        return new AuthResultDto
        {
            Success = true,
            Message = "Вход выполнен успешно",
            User = new UserDto
            {
                Id = user.Id,
                Username = user.Username,
                Email = user.Email,
                Role = user.Role
            }
        };
    }

    public async Task<bool> UserExistsByEmailAsync(string email)
    {
        return await _context.Users.AnyAsync(u => u.Email == email);
    }

    public async Task<bool> UserExistsByUsernameAsync(string username)
    {
        return await _context.Users.AnyAsync(u => u.Username == username);
    }

    public async Task<AuthResultDto> UpdateProfileAsync(int userId, string username, string email, string? newPassword, string? currentPassword)
    {
        var user = await _context.Users.FindAsync(userId);
        if (user == null)
        {
            return new AuthResultDto
            {
                Success = false,
                Message = "Пользователь не найден"
            };
        }

        // Check if email is already taken by another user
        if (email != user.Email && await UserExistsByEmailAsync(email))
        {
            return new AuthResultDto
            {
                Success = false,
                Message = "Email уже используется другим пользователем"
            };
        }

        // Check if username is already taken by another user
        if (username != user.Username && await UserExistsByUsernameAsync(username))
        {
            return new AuthResultDto
            {
                Success = false,
                Message = "Имя пользователя уже занято"
            };
        }

        // Verify current password if changing password
        if (!string.IsNullOrEmpty(newPassword))
        {
            if (string.IsNullOrEmpty(currentPassword) || !VerifyPassword(currentPassword, user.PasswordHash))
            {
                return new AuthResultDto
                {
                    Success = false,
                    Message = "Текущий пароль неверен"
                };
            }
            user.PasswordHash = HashPassword(newPassword);
        }

        user.Username = username;
        user.Email = email;
        user.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return new AuthResultDto
        {
            Success = true,
            Message = "Профиль обновлен успешно",
            User = new UserDto
            {
                Id = user.Id,
                Username = user.Username,
                Email = user.Email,
                Role = user.Role
            }
        };
    }

    private string HashPassword(string password)
    {
        using var sha256 = SHA256.Create();
        var hashedBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
        return Convert.ToBase64String(hashedBytes);
    }

    private bool VerifyPassword(string password, string hash)
    {
        var passwordHash = HashPassword(password);
        return passwordHash == hash;
    }
}



