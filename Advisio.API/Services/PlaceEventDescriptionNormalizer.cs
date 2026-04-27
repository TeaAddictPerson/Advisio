using System.Globalization;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Advisio.API.Models;

namespace Advisio.API.Services;

/// <summary>
/// Даты из описания (RU/EN, HTML, ISO, «13 October 2025») + поля EventStart/EventEnd БД. Нормализация в формат Начало/Окончание.
/// </summary>
public static class PlaceEventDescriptionNormalizer
{
    private static readonly CultureInfo Ru = CultureInfo.GetCultureInfo("ru-RU");
    private static readonly CultureInfo En = CultureInfo.GetCultureInfo("en-US");
    private static readonly TimeSpan RxTimeout = TimeSpan.FromSeconds(2);

    /// <summary>Russian «Дата и время: dd.MM.yyyy, h:mm – dd.MM.yyyy, h:mm»; запятая после даты необязательна; по всему тексту.</summary>
    private static readonly Regex RelaxedRuDateTimeRange = new(
        @"Дата\s*и\s*время\s*:\s*(?<d1>\d{1,2})\.(?<m1>\d{1,2})\.(?<y1>\d{4})\s*,?\s*(?<t1>\d{1,2}:\d{2})\s*[-–—]\s*(?<d2>\d{1,2})\.(?<m2>\d{1,2})\.(?<y2>\d{4})\s*,?\s*(?<t2>\d{1,2}:\d{2})",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
        RxTimeout);

    private static readonly Regex EnDateTimeRange = new(
        @"Date\s*(?:and\s*time)?\s*:\s*(?<d1>\d{1,2})\.(?<m1>\d{1,2})\.(?<y1>\d{4})\s*,?\s*(?<t1>\d{1,2}:\d{2})\s*[-–—]\s*(?<d2>\d{1,2})\.(?<m2>\d{1,2})\.(?<y2>\d{4})\s*,?\s*(?<t2>\d{1,2}:\d{2})",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
        RxTimeout);

    /// <summary>Один день: «Дата и время: dd.MM.yyyy, 9:00-18:00» (второй фрагмент — только время).</summary>
    private static readonly Regex RuSameDayTimeRange = new(
        @"Дата\s*и\s*время\s*:\s*(?<d1>\d{1,2})\.(?<m1>\d{1,2})\.(?<y1>\d{4})\s*,?\s*(?<t1>\d{1,2}:\d{2})\s*[-–—]\s*(?<t2>\d{1,2}:\d{2})\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
        RxTimeout);

    private static readonly Regex EnSameDayTimeRange = new(
        @"Date\s*(?:and\s*time)?\s*:\s*(?<d1>\d{1,2})\.(?<m1>\d{1,2})\.(?<y1>\d{4})\s*,?\s*(?<t1>\d{1,2}:\d{2})\s*[-–—]\s*(?<t2>\d{1,2}:\d{2})\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
        RxTimeout);

    /// <summary>Только даты dd.MM.yyyy – dd.MM.yyyy без времени.</summary>
    private static readonly Regex DateOnlyDdMmRange = new(
        @"(?<!\d)(?<d1>\d{1,2})\.(?<m1>\d{1,2})\.(?<y1>\d{4})\s*[-–—]\s*(?<d2>\d{1,2})\.(?<m2>\d{1,2})\.(?<y2>\d{4})(?!\d)",
        RegexOptions.CultureInvariant,
        RxTimeout);

    private static readonly Regex RuStartLine = new(@"^\s*Начало:\s*(.+)\s*$", RegexOptions.IgnoreCase, RxTimeout);
    private static readonly Regex RuEndLine = new(@"^\s*Окончание:\s*(.+)\s*$", RegexOptions.IgnoreCase, RxTimeout);
    private static readonly Regex RuLocationLine = new(@"^\s*Место\s+проведения:\s*(.+)\s*$", RegexOptions.IgnoreCase, RxTimeout);
    private static readonly Regex EnStartLine = new(@"^\s*Start(?:ed|ing|s)?\s*[:\-]\s*(.+)\s*$", RegexOptions.IgnoreCase, RxTimeout);
    private static readonly Regex EnEndLine = new(@"^\s*End(?:ing|s)?(?:\s+date)?\s*[:\-]\s*(.+)\s*$", RegexOptions.IgnoreCase, RxTimeout);

    private static readonly Regex IsoLike = new(
        @"\d{4}-\d{2}-\d{2}(?:[T\s]\d{2}:\d{2}(?::\d{2}(?:\.\d+)?)?)?",
        RegexOptions.CultureInvariant,
        RxTimeout);

    private static readonly Regex EnMonthDayYear = new(
        @"\b(?<d>\d{1,2})\s+(?<mon>January|February|March|April|May|June|July|August|September|October|November|December)\s+(?<y>\d{4})\b",
        RegexOptions.IgnoreCase,
        RxTimeout);

    private static readonly string[] ShortDateTimeFormats =
    {
        "d.M.yyyy H:mm", "dd.MM.yyyy H:mm", "d.M.yyyy HH:mm", "dd.MM.yyyy HH:mm",
        "d.M.yyyy H:mm:ss", "dd.MM.yyyy HH:mm:ss",
    };

    /// <summary>Конец события: после нормализации приоритет у <see cref="Place.EventEndDate"/>; иначе разбор описания.</summary>
    public static DateTime? GetEffectiveEventEnd(DateTime? eventEndDate, string? description) =>
        eventEndDate ?? GetParsedEventEndFromDescription(description);

    /// <summary>Только текст описания (без полей БД). После массовой нормализации обычно не нужен.</summary>
    public static DateTime? GetParsedEventEndFromDescription(string? description)
    {
        var prep = PrepareTextForParsing(description);
        ExtractBestEventBounds(prep, null, null, out _, out var e);
        return e;
    }

    public static bool TryParseEventRangeFromDescription(string? description, out DateTime? start, out DateTime? end)
    {
        var prep = PrepareTextForParsing(description);
        ExtractBestEventBounds(prep, null, null, out start, out end);
        return start.HasValue || end.HasValue;
    }

    public static bool ApplyNormalizationToPlace(Place place, out bool anyChange)
    {
        anyChange = false;
        var originalDesc = place.Description ?? string.Empty;
        var origS = place.EventStartDate;
        var origE = place.EventEndDate;

        var prep = PrepareTextForParsing(place.Description);
        ExtractBestEventBounds(prep, origS, origE, out var s, out var e);

        EventWindowNormalizer.NormalizeIfStartEndSameInstant(ref s, ref e);

        var body = ExtractBodyWithoutMetadata(prep);

        var sb = new StringBuilder();
        foreach (var bl in body)
        {
            if (string.IsNullOrWhiteSpace(bl))
                sb.AppendLine();
            else
                sb.AppendLine(bl.TrimEnd());
        }

        if (!string.IsNullOrWhiteSpace(place.Address))
            sb.AppendLine($"Место проведения: {place.Address.Trim()}");
        if (s.HasValue)
            sb.AppendLine($"Начало: {s.Value.ToString("dd MMMM yyyy, HH:mm", Ru)}");
        if (e.HasValue)
            sb.AppendLine($"Окончание: {e.Value.ToString("dd MMMM yyyy, HH:mm", Ru)}");

        var merged = PredictHqDescriptionNormalizer.FinalizeImportedDescription(sb.ToString().Trim());

        if (!string.Equals(merged.TrimEnd(), originalDesc.TrimEnd(), StringComparison.Ordinal))
            anyChange = true;
        if (s != origS || e != origE)
            anyChange = true;

        place.Description = merged;
        place.EventStartDate = s;
        place.EventEndDate = e;
        if (anyChange)
            place.UpdatedAt = DateTime.UtcNow;

        return anyChange;
    }

    private static string PrepareTextForParsing(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return string.Empty;

        var t = WebUtility.HtmlDecode(raw);
        t = Regex.Replace(t, @"<br\s*/?>", "\n", RegexOptions.IgnoreCase, RxTimeout);
        t = Regex.Replace(t, @"</p\s*>", "\n\n", RegexOptions.IgnoreCase, RxTimeout);
        t = Regex.Replace(t, @"<p[^>]*>", "\n", RegexOptions.IgnoreCase, RxTimeout);
        t = Regex.Replace(t, @"<[^>]+>", string.Empty, RegexOptions.None, RxTimeout);
        return t.Trim();
    }

    private static void ExtractBestEventBounds(string text, DateTime? dbStart, DateTime? dbEnd, out DateTime? start, out DateTime? end)
    {
        start = dbStart;
        end = dbEnd;
        if (string.IsNullOrWhiteSpace(text))
            return;

        if (TryMatchTwoPartNumericRange(RelaxedRuDateTimeRange.Match(text), out var rs, out var re))
        {
            start = rs;
            end = re;
            return;
        }

        if (TryMatchTwoPartNumericRange(EnDateTimeRange.Match(text), out rs, out re))
        {
            start = rs;
            end = re;
            return;
        }

        if (TryMatchSameDayTimeRange(RuSameDayTimeRange.Match(text), out rs, out re))
        {
            start = rs;
            end = re;
            return;
        }

        if (TryMatchSameDayTimeRange(EnSameDayTimeRange.Match(text), out rs, out re))
        {
            start = rs;
            end = re;
            return;
        }

        if ((!start.HasValue || !end.HasValue)
            && DateOnlyDdMmRange.Match(text) is { Success: true } dm
            && TryDateOnlyParts(dm.Groups["d1"].Value, dm.Groups["m1"].Value, dm.Groups["y1"].Value, out var ds)
            && TryDateOnlyParts(dm.Groups["d2"].Value, dm.Groups["m2"].Value, dm.Groups["y2"].Value, out var de))
        {
            if (!start.HasValue) start = DateTime.SpecifyKind(ds, DateTimeKind.Unspecified);
            if (!end.HasValue) end = DateTime.SpecifyKind(de.Date.AddHours(23).AddMinutes(59).AddSeconds(59), DateTimeKind.Unspecified);
            if (start.HasValue && end.HasValue)
                return;
        }

        if ((!start.HasValue || !end.HasValue) && TryIsoFirstLastBounds(text, out rs, out re))
        {
            if (rs.HasValue) start = rs;
            if (re.HasValue) end = re;
            if (start.HasValue && end.HasValue)
                return;
        }

        if ((!start.HasValue || !end.HasValue) && TryEnglishMonthNameBounds(text, out rs, out re))
        {
            if (rs.HasValue) start = rs;
            if (re.HasValue) end = re;
            if (start.HasValue && end.HasValue)
                return;
        }

        FillFromLines(text, ref start, ref end);
    }

    private static bool TryMatchTwoPartNumericRange(Match m, out DateTime? start, out DateTime? end)
    {
        start = end = null;
        if (!m.Success) return false;
        if (!TryParseShortDateTimeParts(m.Groups["d1"].Value, m.Groups["m1"].Value, m.Groups["y1"].Value, m.Groups["t1"].Value, out var s))
            return false;
        if (!TryParseShortDateTimeParts(m.Groups["d2"].Value, m.Groups["m2"].Value, m.Groups["y2"].Value, m.Groups["t2"].Value, out var e))
            return false;
        start = DateTime.SpecifyKind(s, DateTimeKind.Unspecified);
        end = DateTime.SpecifyKind(e, DateTimeKind.Unspecified);
        return true;
    }

    private static bool TryMatchSameDayTimeRange(Match m, out DateTime? start, out DateTime? end)
    {
        start = end = null;
        if (!m.Success) return false;
        var d = m.Groups["d1"].Value;
        var mo = m.Groups["m1"].Value;
        var y = m.Groups["y1"].Value;
        if (!TryParseShortDateTimeParts(d, mo, y, m.Groups["t1"].Value, out var s))
            return false;
        if (!TryParseShortDateTimeParts(d, mo, y, m.Groups["t2"].Value, out var e))
            return false;
        start = DateTime.SpecifyKind(s, DateTimeKind.Unspecified);
        end = DateTime.SpecifyKind(e, DateTimeKind.Unspecified);
        return true;
    }

    private static bool TryDateOnlyParts(string day, string month, string year, out DateTime dt)
    {
        var ds = $"{day.Trim()}.{month.Trim()}.{year.Trim()}";
        return DateTime.TryParseExact(ds, new[] { "d.M.yyyy", "dd.MM.yyyy" }, CultureInfo.InvariantCulture, DateTimeStyles.None, out dt);
    }

    private static bool TryIsoFirstLastBounds(string text, out DateTime? start, out DateTime? end)
    {
        start = end = null;
        var matches = IsoLike.Matches(text);
        if (matches.Count < 2)
            return false;
        var parsed = new List<DateTime>();
        foreach (Match m in matches)
        {
            if (DateTime.TryParse(m.Value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var dt))
                parsed.Add(dt);
        }

        if (parsed.Count < 2)
            return false;
        parsed.Sort();
        start = DateTime.SpecifyKind(parsed[0], DateTimeKind.Unspecified);
        end = DateTime.SpecifyKind(parsed[^1], DateTimeKind.Unspecified);
        return true;
    }

    private static bool TryEnglishMonthNameBounds(string text, out DateTime? start, out DateTime? end)
    {
        start = end = null;
        var hits = new List<DateTime>();
        foreach (Match m in EnMonthDayYear.Matches(text))
        {
            var chunk = $"{m.Groups["d"].Value} {m.Groups["mon"].Value} {m.Groups["y"].Value}";
            if (DateTime.TryParse(chunk, En, DateTimeStyles.None, out var dt))
                hits.Add(dt.Date);
        }

        if (hits.Count == 0)
            return false;
        hits.Sort();
        start = DateTime.SpecifyKind(hits[0], DateTimeKind.Unspecified);
        end = DateTime.SpecifyKind(
            hits.Count >= 2 ? hits[^1].Date.AddHours(23).AddMinutes(59).AddSeconds(59) : hits[0].Date.AddHours(23).AddMinutes(59).AddSeconds(59),
            DateTimeKind.Unspecified);
        return true;
    }

    private static void FillFromLines(string text, ref DateTime? start, ref DateTime? end)
    {
        foreach (var raw in text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n'))
        {
            var line = raw.Trim();
            if (line.Length == 0) continue;

            if (RuStartLine.Match(line) is { Success: true } rm && TryParseRuLongDateTime(rm.Groups[1].Value.Trim(), out var ps))
                start = DateTime.SpecifyKind(ps, DateTimeKind.Unspecified);
            if (RuEndLine.Match(line) is { Success: true } rme && TryParseRuLongDateTime(rme.Groups[1].Value.Trim(), out var pe))
                end = DateTime.SpecifyKind(pe, DateTimeKind.Unspecified);
            if (EnStartLine.Match(line) is { Success: true } es && TryParseEnFlexible(es.Groups[1].Value.Trim(), out var esd))
                start = DateTime.SpecifyKind(esd, DateTimeKind.Unspecified);
            if (EnEndLine.Match(line) is { Success: true } ee && TryParseEnFlexible(ee.Groups[1].Value.Trim(), out var eed))
                end = DateTime.SpecifyKind(eed, DateTimeKind.Unspecified);
        }
    }

    private static List<string> ExtractBodyWithoutMetadata(string prep)
    {
        var body = new List<string>();
        if (string.IsNullOrWhiteSpace(prep))
            return body;

        foreach (var raw in prep.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n'))
        {
            var line = raw.TrimEnd();
            var t = line.Trim();
            if (t.Length == 0)
            {
                body.Add(string.Empty);
                continue;
            }

            var cleaned = RelaxedRuDateTimeRange.Replace(line, string.Empty);
            cleaned = EnDateTimeRange.Replace(cleaned, string.Empty);
            cleaned = DateOnlyDdMmRange.Replace(cleaned, string.Empty);
            cleaned = cleaned.Trim();
            if (cleaned.Length == 0)
                continue;

            t = cleaned.Trim();
            if (RuLocationLine.IsMatch(t) || RuStartLine.IsMatch(t) || RuEndLine.IsMatch(t) || EnStartLine.IsMatch(t) || EnEndLine.IsMatch(t))
                continue;

            body.Add(cleaned);
        }

        while (body.Count > 0 && string.IsNullOrWhiteSpace(body[^1]))
            body.RemoveAt(body.Count - 1);
        while (body.Count > 0 && string.IsNullOrWhiteSpace(body[0]))
            body.RemoveAt(0);

        return body;
    }

    private static bool TryParseShortDateTimeParts(string day, string month, string year, string time, out DateTime dt)
    {
        var ds = $"{day.Trim()}.{month.Trim()}.{year.Trim()} {time.Trim()}";
        return DateTime.TryParseExact(
            ds,
            ShortDateTimeFormats,
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out dt);
    }

    private static bool TryParseRuLongDateTime(string value, out DateTime dt)
    {
        if (DateTime.TryParse(value, Ru, DateTimeStyles.None, out dt))
            return true;
        return DateTime.TryParseExact(
            value,
            new[] { "dd MMMM yyyy, HH:mm", "d MMMM yyyy, H:mm", "dd MMMM yyyy г., HH:mm" },
            Ru,
            DateTimeStyles.None,
            out dt);
    }

    private static bool TryParseEnFlexible(string value, out DateTime dt)
    {
        if (DateTime.TryParse(value, En, DateTimeStyles.None, out dt))
            return true;
        if (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out dt))
            return true;
        return DateTime.TryParse(value, Ru, DateTimeStyles.None, out dt);
    }
}
