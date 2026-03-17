namespace Advisio.API.Services;

/// <summary>
/// Простой фильтр нецензурной лексики для комментариев
/// </summary>
public class ProfanityFilterService
{
    private static readonly HashSet<string> BannedWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "мат", "брань", "ругательство", "оскорбление",
        // Добавьте сюда список запрещённых слов (можно расширить)
    };

    private static readonly string[] BannedPatterns = new[]
    {
        @"\b[бвгджзклмнпрстфхцчшщБВГДЖЗКЛМНПРСТФХЦЧШЩ]{4,}\b", // Подозрительные длинные последовательности
    };

    public bool ContainsProfanity(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return false;

        var lower = text.ToLowerInvariant();
        foreach (var word in BannedWords)
        {
            if (lower.Contains(word)) return true;
        }

        // Проверка на распространённые замены букв
        var normalized = lower
            .Replace("а", "a").Replace("е", "e").Replace("о", "o").Replace("р", "p")
            .Replace("с", "c").Replace("у", "y").Replace("х", "x");
        foreach (var word in BannedWords)
        {
            if (normalized.Contains(word)) return true;
        }

        return false;
    }

    public string? Sanitize(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return text;
        return ContainsProfanity(text) ? null : text.Trim();
    }
}
