using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Advisio.API.Data;
using Advisio.API.Models;

namespace Advisio.Web.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CategoriesController : ControllerBase
{
    private readonly AdvisioDbContext _context;

    public CategoriesController(AdvisioDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<List<CategoryDto>>> GetAllCategories()
    {
        var categories = await _context.Categories
            .OrderBy(c => c.Name)
            .Select(c => new CategoryDto
            {
                Id = c.Id,
                Name = c.Name,
                Description = c.Description,
                Type = c.Type.ToString()
            })
            .ToListAsync();

        return Ok(categories);
    }

    [HttpPost("{id}/subscribe")]
    public async Task<ActionResult> SubscribeToCategory(int id, [FromQuery] int userId)
    {
        if (!await _context.Categories.AnyAsync(c => c.Id == id)) return NotFound();
        if (!await _context.Users.AnyAsync(u => u.Id == userId)) return NotFound();
        if (await _context.CategorySubscriptions.AnyAsync(s => s.UserId == userId && s.CategoryId == id))
            return BadRequest("Уже подписаны");

        _context.CategorySubscriptions.Add(new CategorySubscription
        {
            UserId = userId,
            CategoryId = id,
            SubscribedAt = DateTime.UtcNow
        });
        await _context.SaveChangesAsync();
        return Ok(new { message = "Подписка на категорию оформлена" });
    }

    [HttpDelete("{id}/subscribe")]
    public async Task<ActionResult> UnsubscribeFromCategory(int id, [FromQuery] int userId)
    {
        var sub = await _context.CategorySubscriptions
            .FirstOrDefaultAsync(s => s.UserId == userId && s.CategoryId == id);
        if (sub == null) return NotFound();

        _context.CategorySubscriptions.Remove(sub);
        await _context.SaveChangesAsync();
        return Ok(new { message = "Подписка отменена" });
    }
}

public class CategoryDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
}


