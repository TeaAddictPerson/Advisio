using System.Globalization;
using System.Text;
using System.Text.Json;
using Advisio.API.DTOs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Advisio.API.Services;

public class PredictHqEventsImportService : IPredictHqEventsImportService
{
    public const string HttpClientName = "PredictHqEvents";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly IPlaceService _placeService;
    private readonly IHostEnvironment _hostEnvironment;
    private readonly ILogger<PredictHqEventsImportService> _logger;

    public PredictHqEventsImportService(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        IPlaceService placeService,
        IHostEnvironment hostEnvironment,
        ILogger<PredictHqEventsImportService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _placeService = placeService;
        _hostEnvironment = hostEnvironment;
        _logger = logger;
    }

    public bool IsConfiguredForImport()
    {
        if (!_configuration.GetValue<bool>("PlacesSync:ExternalImportEnabled", true))
            return false;
        return !string.IsNullOrWhiteSpace(ResolvePredictHqToken());
    }

    public async Task<int> ImportEventsAsync(
        bool forceRun = false,
        int? maxPlacesToAdd = null,
        CancellationToken cancellationToken = default,
        bool exhaustivePagination = false)
    {
        if (!_configuration.GetValue<bool>("PlacesSync:ExternalImportEnabled", true))
        {
            _logger.LogInformation("PredictHQ: импорт выключен (PlacesSync:ExternalImportEnabled = false)");
            return 0;
        }

        var token = ResolvePredictHqToken();
        if (string.IsNullOrWhiteSpace(token))
        {
            _logger.LogDebug(
                "PredictHQ: ApiToken не задан — импорт пропущен (user-secrets, PredictHQ:ApiToken или env PredictHQ__ApiToken / PREDICTHQ_API_TOKEN).");
            return 0;
        }

        if (!forceRun)
        {
            var intervalHours = _configuration.GetValue<double>("PlacesSync:ExternalImportIntervalHours", 3);
            if (intervalHours <= 0)
                intervalHours = 3;
            var statePath = GetStatePath();
            if (File.Exists(statePath))
            {
                var last = File.GetLastWriteTimeUtc(statePath);
                if (DateTime.UtcNow - last < TimeSpan.FromHours(intervalHours))
                    return 0;
            }
        }

        var baseUrl = (_configuration["PredictHQ:EventsUrl"] ?? "https://api.predicthq.com/v1/events").TrimEnd('/');
        var pageSize = _configuration.GetValue<int>("PredictHQ:Limit", 100);
        if (pageSize <= 0)
            pageSize = 100;
        pageSize = Math.Clamp(pageSize, 1, 100);

        var country = _configuration["PredictHQ:Country"];
        var maxTotal = maxPlacesToAdd ?? _configuration.GetValue<int>("PlacesSync:MaxPlacesPerSync", 500);
        if (maxTotal <= 0)
            maxTotal = 200;

        _logger.LogInformation("PredictHQ: за этот запрос создаём до {Max} новых мест (страница до {Page} событий)", maxTotal, pageSize);

        var client = _httpClientFactory.CreateClient(HttpClientName);
        var (existingPhqIds, existingNameKeys) = await _placeService.GetPredictHqImportDedupSnapshotAsync(cancellationToken)
            .ConfigureAwait(false);
        var added = 0;
        var offset = 0;

        while (added < maxTotal && !cancellationToken.IsCancellationRequested)
        {
            var take = Math.Min(pageSize, maxTotal - added);
            var qs = new List<string> { $"limit={take}", $"offset={offset}" };
            if (!string.IsNullOrWhiteSpace(country))
                qs.Add($"country={Uri.EscapeDataString(country)}");

            var url = $"{baseUrl}/?{string.Join("&", qs)}";

            string json;
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.TryAddWithoutValidation("Authorization", $"Bearer {token}");
                request.Headers.Accept.TryParseAdd("application/json");
                request.Headers.UserAgent.ParseAdd("Advisio/1.0");
                using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    var errBody = await response.Content.ReadAsStringAsync(cancellationToken);
                    var snippet = errBody.Length > 800 ? errBody[..800] + "…" : errBody;
                    if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                    {
                        _logger.LogError(
                            "PredictHQ: 401 Unauthorized — токен отклонён. " +
                            "Новый токен: https://control.predicthq.com/tokens (scope Events). " +
                            "Укажите PredictHQ:ApiToken, user-secrets или env PredictHQ__ApiToken / PREDICTHQ_API_TOKEN; без префикса Bearer. " +
                            "Ответ API: {Snippet}",
                            snippet);
                    }
                    else
                    {
                        _logger.LogError("PredictHQ: HTTP {Status} для {Url}. Ответ: {Snippet}", (int)response.StatusCode, url, snippet);
                    }

                    break;
                }

                json = await response.Content.ReadAsStringAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "PredictHQ: запрос не удался {Url}", url);
                break;
            }

            try
            {
                using var doc = JsonDocument.Parse(json);
                var items = ExtractEventItemsFromRoot(doc.RootElement);
                if (items.Count == 0)
                {
                    _logger.LogInformation("PredictHQ: пустая страница (offset {Offset})", offset);
                    break;
                }

                var addedBeforePage = added;
                foreach (var el in items)
                {
                    if (added >= maxTotal)
                        break;
                    cancellationToken.ThrowIfCancellationRequested();
                    if (await TryCreatePlaceFromEventAsync(el, existingPhqIds, existingNameKeys, cancellationToken)
                            .ConfigureAwait(false))
                        added++;
                }

                var addedThisPage = added - addedBeforePage;
                if (!exhaustivePagination
                    && items.Count > 0
                    && addedThisPage == 0)
                {
                    _logger.LogInformation(
                        "PredictHQ: на странице offset {Offset} нет новых мест (уже в базе) — дальнейшие запросы к API не выполняем",
                        offset);
                    break;
                }

                offset += items.Count;
                if (items.Count < take)
                    break;
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "PredictHQ: ошибка разбора JSON");
                break;
            }
        }

        TouchStateFile(GetStatePath());
        if (added > 0)
            _logger.LogInformation("PredictHQ: добавлено новых мест: {Count}", added);

        return added;
    }

    private string? ResolvePredictHqToken()
    {
        var raw =
            _configuration["PredictHQ:ApiToken"]
            ?? _configuration["PredictHQ:AccessToken"]
            ?? Environment.GetEnvironmentVariable("PREDICTHQ_API_TOKEN")
            ?? Environment.GetEnvironmentVariable("PredictHQ__ApiToken");
        return NormalizePredictHqToken(raw);
    }

    private static string? NormalizePredictHqToken(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;
        var t = raw.Trim().Trim('"', '\'');
        if (t.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            t = t["Bearer ".Length..].Trim();
        return string.IsNullOrEmpty(t) ? null : t;
    }

    private string GetStatePath()
    {
        var dir = Path.Combine(_hostEnvironment.ContentRootPath, "App_Data");
        return Path.Combine(dir, "last-predicthq-events-import.txt");
    }

    private static void TouchStateFile(string statePath)
    {
        var dir = Path.GetDirectoryName(statePath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);
        File.WriteAllText(statePath, DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture));
    }

    private async Task<bool> TryCreatePlaceFromEventAsync(
        JsonElement el,
        HashSet<string> existingPhqIds,
        HashSet<string> existingNameKeys,
        CancellationToken cancellationToken)
    {
        var dto = MapToCreateDto(el);
        if (dto == null || string.IsNullOrWhiteSpace(dto.Name))
            return false;

        dto.Description = PredictHqDescriptionNormalizer.FinalizeImportedDescription(dto.Description ?? string.Empty);

        if (!string.IsNullOrWhiteSpace(dto.PredictHqEventId) && existingPhqIds.Contains(dto.PredictHqEventId))
            return false;

        var nameKey = PlaceNameSanitizer.LogicalNameKey(dto.Name);
        if (existingNameKeys.Contains(nameKey))
            return false;

        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            await _placeService.CreatePlaceAsync(dto).ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(dto.PredictHqEventId))
                existingPhqIds.Add(dto.PredictHqEventId);
            existingNameKeys.Add(nameKey);
            return true;
        }
        catch (InvalidOperationException)
        {
            if (!string.IsNullOrWhiteSpace(dto.PredictHqEventId))
                existingPhqIds.Add(dto.PredictHqEventId);
            existingNameKeys.Add(nameKey);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "PredictHQ: пропуск «{Name}»", dto.Name);
            return false;
        }
    }

    private static List<JsonElement> ExtractEventItemsFromRoot(JsonElement root)
    {
        if (root.ValueKind == JsonValueKind.Array)
            return ReadResultsArray(root);

        foreach (var key in new[] { "results", "data", "events", "items" })
        {
            if (!TryGetPropertyCi(root, key, out var arr) || arr.ValueKind != JsonValueKind.Array)
                continue;
            var list = ReadResultsArray(arr);
            if (list.Count > 0)
                return list;
        }

        return new List<JsonElement>();
    }

    private static List<JsonElement> ReadResultsArray(JsonElement arr)
    {
        var list = new List<JsonElement>();
        foreach (var x in arr.EnumerateArray())
            list.Add(x);
        return list;
    }

    private static string? ExtractPredictHqCategorySlug(JsonElement e)
    {
        if (TryGetPropertyCi(e, "category", out var cat))
        {
            if (cat.ValueKind == JsonValueKind.String)
                return cat.GetString();
            if (cat.ValueKind == JsonValueKind.Object)
            {
                if (TryGetPropertyCi(cat, "label", out var lab) && lab.ValueKind == JsonValueKind.String)
                    return lab.GetString();
                if (TryGetPropertyCi(cat, "id", out var id) && id.ValueKind == JsonValueKind.String)
                    return id.GetString();
            }
        }

        if (TryGetPropertyCi(e, "phq_labels", out var labels) && labels.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in labels.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.String)
                    return item.GetString();
                if (item.ValueKind == JsonValueKind.Object)
                {
                    if (TryGetPropertyCi(item, "label", out var l) && l.ValueKind == JsonValueKind.String)
                        return l.GetString();
                    if (TryGetPropertyCi(item, "id", out var lid) && lid.ValueKind == JsonValueKind.String)
                        return lid.GetString();
                }
            }
        }

        return GetStringCandidate(e, "parent", "genre");
    }

    private static CreatePlaceDto? MapToCreateDto(JsonElement e)
    {
        var name = PlaceNameSanitizer.ForDisplay(GetStringCandidate(e, "title", "name", "headline"));
        if (string.IsNullOrWhiteSpace(name))
            return null;

        var descRaw = GetStringCandidate(e, "description", "text", "summary");
        var address = GetStringCandidate(e, "formatted_address", "address");
        if (string.IsNullOrWhiteSpace(address) && TryGetPropertyCi(e, "geo", out var g0) && TryGetPropertyCi(g0, "address", out var ga)
            && ga.ValueKind == JsonValueKind.Object)
            address = GetStringCandidate(ga, "formatted_address", "name");

        var imageUrl = GetStringCandidate(e, "image_url", "image");
        if (PlaceImageUrlHeuristics.ShouldAutopickInsteadOfImportedUrl(imageUrl))
            imageUrl = null;

        decimal? lat = null, lon = null;
        TryPredictHqGeo(e, ref lat, ref lon, ref address);

        TryParseEventDate(e, out var start, out var end);
        EventWindowNormalizer.NormalizeIfStartEndSameInstant(ref start, ref end);

        var venueForText = string.IsNullOrWhiteSpace(address) ? TryExtractVenueName(e) : address;
        var description = BuildPredictHqEventDescription(e, descRaw, start, end, venueForText);

        var rawCategory = ExtractPredictHqCategorySlug(e);
        var placeType = PlaceTypeFormatter.FormatForDisplay(rawCategory);

        return new CreatePlaceDto
        {
            Name = name,
            Description = description,
            Address = string.IsNullOrWhiteSpace(address) ? null : address.Trim(),
            ImageUrl = string.IsNullOrWhiteSpace(imageUrl) ? null : imageUrl.Trim(),
            Latitude = lat,
            Longitude = lon,
            Type = placeType,
            CategoryIds = new List<int>(),
            EventStartDate = start,
            EventEndDate = end,
            PredictHqEventId = TryGetPredictHqEventId(e)
        };
    }

    private static string? TryGetPredictHqEventId(JsonElement e)
    {
        if (!TryGetPropertyCi(e, "id", out var idEl) || idEl.ValueKind != JsonValueKind.String)
            return null;
        var s = idEl.GetString()?.Trim();
        if (string.IsNullOrEmpty(s))
            return null;
        return s.Length <= 64 ? s : s[..64];
    }

    private static string? TryExtractVenueName(JsonElement e)
    {
        if (!TryGetPropertyCi(e, "geo", out var geo) || geo.ValueKind != JsonValueKind.Object)
            return null;
        if (!TryGetPropertyCi(geo, "address", out var addr) || addr.ValueKind != JsonValueKind.Object)
            return null;
        return GetStringCandidate(addr, "name", "venue_name", "formatted_address");
    }

    private static string BuildPredictHqEventDescription(
        JsonElement e,
        string? apiDescription,
        DateTime? start,
        DateTime? end,
        string? venue)
    {
        var sb = new StringBuilder();
        var ru = CultureInfo.GetCultureInfo("ru-RU");

        var clean = PredictHqDescriptionNormalizer.StripAttributionLines(apiDescription);
        if (!string.IsNullOrWhiteSpace(clean))
        {
            sb.AppendLine(clean.Trim());
            sb.AppendLine();
        }

        if (start.HasValue)
            sb.AppendLine($"Начало: {start.Value.ToString("dd MMMM yyyy, HH:mm", ru)}");
        if (end.HasValue)
            sb.AppendLine($"Окончание: {end.Value.ToString("dd MMMM yyyy, HH:mm", ru)}");
        if (!string.IsNullOrWhiteSpace(venue))
            sb.AppendLine($"Место проведения: {venue.Trim()}");

        AppendPhqAttendance(sb, e);
        AppendPredictedSpend(sb, e);

        return PredictHqDescriptionNormalizer.FinalizeImportedDescription(sb.ToString().Trim());
    }

    private static void AppendPhqAttendance(StringBuilder sb, JsonElement e)
    {
        if (!TryGetPropertyCi(e, "phq_attendance", out var att))
            return;

        if (att.ValueKind == JsonValueKind.Number && att.TryGetInt32(out var n) && n > 0)
        {
            sb.AppendLine($"Ожидаемая посещаемость: около {n:n0} человек");
            return;
        }

        if (att.ValueKind != JsonValueKind.Object)
            return;

        if (TryGetPropertyCi(att, "average", out var avg) && avg.ValueKind == JsonValueKind.Number && avg.TryGetInt32(out var a) && a > 0)
            sb.AppendLine($"Ожидаемая посещаемость: около {a:n0} человек");
    }

    private static void AppendPredictedSpend(StringBuilder sb, JsonElement e)
    {
        if (!TryGetPropertyCi(e, "predicted_event_spend", out var pes) || pes.ValueKind != JsonValueKind.Object)
            return;

        if (TryGetPropertyCi(pes, "min_amount", out var min) && min.ValueKind == JsonValueKind.Number && min.TryGetDecimal(out var dmin) && dmin > 0)
            sb.AppendLine($"Ориентировочный уровень расходов посетителей: от {dmin:n0} (валюта региона события)");
        if (TryGetPropertyCi(pes, "max_amount", out var max) && max.ValueKind == JsonValueKind.Number && max.TryGetDecimal(out var dmax) && dmax > 0)
            sb.AppendLine($"Оценка верхней границы расходов: до {dmax:n0}");
    }

    /// <summary>
    /// GeoJSON: Point [lon, lat]; Polygon/MultiPolygon — вложенные массивы; берём первую пару чисел.
    /// </summary>
    private static bool TryReadLonLatFromGeoJsonCoordinates(JsonElement coords, out decimal lon, out decimal lat)
    {
        lon = lat = default;
        if (!TryFindFirstLonLatPair(coords, out var pair))
            return false;
        var arr = pair.EnumerateArray().ToArray();
        return arr.Length >= 2 && arr[0].TryGetDecimal(out lon) && arr[1].TryGetDecimal(out lat);
    }

    private static bool TryFindFirstLonLatPair(JsonElement el, out JsonElement pairElement)
    {
        pairElement = default;
        if (el.ValueKind != JsonValueKind.Array)
            return false;

        var items = el.EnumerateArray().ToArray();
        if (items.Length >= 2
            && items[0].ValueKind == JsonValueKind.Number
            && items[1].ValueKind == JsonValueKind.Number)
        {
            pairElement = el;
            return true;
        }

        foreach (var sub in items)
        {
            if (TryFindFirstLonLatPair(sub, out pairElement))
                return true;
        }

        return false;
    }

    private static void TryPredictHqGeo(JsonElement e, ref decimal? lat, ref decimal? lon, ref string? address)
    {
        if (!TryGetPropertyCi(e, "geo", out var geo) || geo.ValueKind != JsonValueKind.Object)
            return;

        if (TryGetPropertyCi(geo, "geometry", out var geom) && TryGetPropertyCi(geom, "coordinates", out var coords)
            && coords.ValueKind == JsonValueKind.Array
            && TryReadLonLatFromGeoJsonCoordinates(coords, out var gx, out var gy))
        {
            lon = gx;
            lat = gy;
        }

        if (string.IsNullOrWhiteSpace(address) && TryGetPropertyCi(geo, "address", out var addr) && addr.ValueKind == JsonValueKind.Object)
            address = GetStringCandidate(addr, "formatted_address", "name");
    }

    private static bool TryParseEventDate(JsonElement e, out DateTime? start, out DateTime? end)
    {
        start = end = null;
        foreach (var key in new[] { "start", "start_local", "startDate", "begin" })
        {
            if (TryReadDateTimeFromElement(e, key, out var dt))
            {
                start = dt;
                break;
            }
        }

        foreach (var key in new[] { "end", "end_local", "endDate", "finish" })
        {
            if (TryReadDateTimeFromElement(e, key, out var dt))
            {
                end = dt;
                break;
            }
        }

        return start != null || end != null;
    }

    private static bool TryReadDateTimeFromElement(JsonElement e, string prop, out DateTime dt)
    {
        dt = default;
        if (!TryGetPropertyCi(e, prop, out var v))
            return false;
        if (v.ValueKind == JsonValueKind.Object)
        {
            if (TryGetPropertyCi(v, "utc", out var utc) && utc.ValueKind == JsonValueKind.String
                && DateTime.TryParse(utc.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out dt))
                return true;
            if (TryGetPropertyCi(v, "local", out var loc) && loc.ValueKind == JsonValueKind.String
                && DateTime.TryParse(loc.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out dt))
                return true;
            if (TryGetPropertyCi(v, "datetime", out var dtm) && dtm.ValueKind == JsonValueKind.String
                && DateTime.TryParse(dtm.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out dt))
                return true;
        }

        if (v.ValueKind == JsonValueKind.String
            && DateTime.TryParse(v.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out dt))
            return true;

        return false;
    }

    private static string? GetStringCandidate(JsonElement e, params string[] names)
    {
        foreach (var n in names)
        {
            if (!TryGetPropertyCi(e, n, out var v))
                continue;
            if (v.ValueKind == JsonValueKind.String)
                return v.GetString();
            if (v.ValueKind == JsonValueKind.Object && TryGetPropertyCi(v, "text", out var inner) && inner.ValueKind == JsonValueKind.String)
                return inner.GetString();
        }

        return null;
    }

    private static bool TryGetPropertyCi(JsonElement e, string name, out JsonElement value)
    {
        if (e.ValueKind != JsonValueKind.Object)
        {
            value = default;
            return false;
        }

        if (e.TryGetProperty(name, out value))
            return true;

        foreach (var p in e.EnumerateObject())
        {
            if (p.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
            {
                value = p.Value;
                return true;
            }
        }

        value = default;
        return false;
    }
}
