using System.Globalization;

namespace Advisio.API.Services;

/// <summary>
/// Если начало и конец мероприятия совпадают (та же дата и то же время), задаёт разумное дневное окно 08:00–20:00.
/// </summary>
public static class EventWindowNormalizer
{
    private static readonly CultureInfo Ru = CultureInfo.GetCultureInfo("ru-RU");

    public static void NormalizeIfStartEndSameInstant(ref DateTime? start, ref DateTime? end)
    {
        if (!start.HasValue || !end.HasValue)
            return;
        if (start.Value != end.Value)
            return;

        var t = start.Value;
        var d = t.Date;
        var k = t.Kind;
        start = new DateTime(d.Year, d.Month, d.Day, 8, 0, 0, k);
        end = new DateTime(d.Year, d.Month, d.Day, 20, 0, 0, k);
    }

    /// <summary>Обновляет строки «Начало: …» / «Окончание: …» в тексте описания под новые даты.</summary>
    public static string? PatchDescriptionRuEventLines(string? description, DateTime oldInstant, DateTime newStart, DateTime newEnd)
    {
        if (string.IsNullOrEmpty(description))
            return description;

        var oldLine = oldInstant.ToString("dd MMMM yyyy, HH:mm", Ru);
        var ns = newStart.ToString("dd MMMM yyyy, HH:mm", Ru);
        var ne = newEnd.ToString("dd MMMM yyyy, HH:mm", Ru);

        var d = description;
        d = d.Replace($"Начало: {oldLine}", $"Начало: {ns}", StringComparison.Ordinal);
        d = d.Replace($"Окончание: {oldLine}", $"Окончание: {ne}", StringComparison.Ordinal);
        return d;
    }
}
