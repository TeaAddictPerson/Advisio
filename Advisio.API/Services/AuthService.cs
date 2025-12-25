using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;
using Advisio.API.Data;
using Advisio.API.DTOs;
using Advisio.API.Models;

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
        // Проверка существования пользователя
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

        // Создание нового пользователя
        var user = new User
        {
            Username = username,
            Email = email,
            PasswordHash = HashPassword(password),
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
                Role = user.Role,
                CreatedAt = user.CreatedAt
            }
        };
    }

    public async Task<AuthResultDto> LoginAsync(string email, string password)
    {
        // Создаем админа автоматически, если его нет
        if (email.ToLower() == "admin@gmail.com" && password == "admin123")
        {
            var existingAdmin = await _context.Users
                .FirstOrDefaultAsync(u => u.Email.ToLower() == "admin@gmail.com");
            
            if (existingAdmin == null)
            {
                // Создаем админа
                var admin = new User
                {
                    Username = "Admin",
                    Email = "admin@gmail.com",
                    PasswordHash = HashPassword("admin123"),
                    Role = UserRole.Admin,
                    CreatedAt = DateTime.UtcNow
                };
                _context.Users.Add(admin);
                await _context.SaveChangesAsync();
                
                return new AuthResultDto
                {
                    Success = true,
                    Message = "Вход выполнен успешно",
                    User = new UserDto
                    {
                        Id = admin.Id,
                        Username = admin.Username,
                        Email = admin.Email,
                        Role = admin.Role,
                        CreatedAt = admin.CreatedAt
                    }
                };
            }
        }

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

        // Если это admin@gmail.com, убеждаемся что роль установлена как Admin
        if (user.Email.ToLower() == "admin@gmail.com" && user.Role != UserRole.Admin)
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
                Role = user.Role,
                CreatedAt = user.CreatedAt
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

    private string HashPassword(string password)
    {
        using var sha256 = SHA256.Create();
        var hashedBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
        return Convert.ToBase64String(hashedBytes);
    }

    private bool VerifyPassword(string password, string passwordHash)
    {
        var hashOfInput = HashPassword(password);
        return hashOfInput == passwordHash;
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

        // Проверка текущего пароля, если меняется пароль
        if (!string.IsNullOrEmpty(newPassword))
        {
            if (string.IsNullOrEmpty(currentPassword) || !VerifyPassword(currentPassword, user.PasswordHash))
            {
                return new AuthResultDto
                {
                    Success = false,
                    Message = "Неверный текущий пароль"
                };
            }
        }

        // Проверка уникальности email, если он меняется
        if (user.Email != email && await UserExistsByEmailAsync(email))
        {
            return new AuthResultDto
            {
                Success = false,
                Message = "Пользователь с таким email уже существует"
            };
        }

        // Проверка уникальности username, если он меняется
        if (user.Username != username && await UserExistsByUsernameAsync(username))
        {
            return new AuthResultDto
            {
                Success = false,
                Message = "Пользователь с таким именем уже существует"
            };
        }

        // Обновление данных
        user.Username = username;
        user.Email = email;
        user.UpdatedAt = DateTime.UtcNow;

        // Обновление пароля, если указан новый
        if (!string.IsNullOrEmpty(newPassword))
        {
            user.PasswordHash = HashPassword(newPassword);
        }

        await _context.SaveChangesAsync();

        return new AuthResultDto
        {
            Success = true,
            Message = "Профиль успешно обновлен",
            User = new UserDto
            {
                Id = user.Id,
                Username = user.Username,
                Email = user.Email,
                Role = user.Role,
                CreatedAt = user.CreatedAt
            }
        };
    }
}

