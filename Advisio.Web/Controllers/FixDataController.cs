using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Advisio.API.Data;

namespace Advisio.Web.Controllers;

[ApiController]
[Route("api/[controller]")]
public class FixDataController : ControllerBase
{
    private readonly AdvisioDbContext _context;

    public FixDataController(AdvisioDbContext context)
    {
        _context = context;
    }

    [HttpPost("fix-encoding")]
    public async Task<ActionResult> FixEncoding()
    {
        try
        {
            // Обновляем места с правильной кодировкой
            var places = await _context.Places.ToListAsync();
            
            foreach (var place in places)
            {
                switch (place.Id)
                {
                    case 1:
                        place.Name = "Парк Горького";
                        place.Description = "Центральный парк культуры и отдыха с аттракционами, кафе и зонами для пикников";
                        place.Address = "пр-т Независимости, 1";
                        break;
                    case 2:
                        place.Name = "Национальный художественный музей";
                        place.Description = "Крупнейший музей Беларуси с коллекцией произведений искусства";
                        place.Address = "ул. Ленина, 20";
                        break;
                    case 3:
                        place.Name = "Спортивный комплекс \"Минск-Арена\"";
                        place.Description = "Современный спортивный комплекс с ледовой ареной, бассейном и тренажерным залом";
                        place.Address = "пр-т Победителей, 111";
                        break;
                    case 4:
                        place.Name = "Центральный ботанический сад";
                        place.Description = "Красивый ботанический сад с разнообразными растениями и зонами для прогулок";
                        place.Address = "ул. Сурганова, 2в";
                        break;
                    case 5:
                        place.Name = "Квест-комната \"Эскейп-рум\"";
                        place.Description = "Увлекательные квесты для друзей и семьи с различными сценариями";
                        place.Address = "ул. Немига, 5";
                        break;
                    case 6:
                        place.Name = "Каток \"Ледовый дворец\"";
                        place.Description = "Крытый каток для катания на коньках всей семьей";
                        place.Address = "ул. Притыцкого, 27";
                        break;
                }
            }

            // Обновляем категории
            var categories = await _context.Categories.OrderBy(c => c.Type).ThenBy(c => c.Id).ToListAsync();
            if (categories.Count >= 6)
            {
                categories[0].Name = "С друзьями";
                categories[0].Description = "Места для отдыха с друзьями";
                categories[1].Name = "С ребенком";
                categories[1].Description = "Места для отдыха с детьми";
                categories[2].Name = "С семьей";
                categories[2].Description = "Места для семейного отдыха";
                categories[3].Name = "Активный отдых";
                categories[3].Description = "Активные виды отдыха: спорт, экстремальные развлечения";
                categories[4].Name = "На природе";
                categories[4].Description = "Отдых на природе: парки, леса, водоемы";
                categories[5].Name = "Спокойный отдых";
                categories[5].Description = "Спокойные виды отдыха: музеи, театры, кафе";
            }

            await _context.SaveChangesAsync();

            return Ok(new { message = "Кодировка данных исправлена успешно!" });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }
}

