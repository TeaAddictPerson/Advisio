namespace Advisio.API.Services;

/// <summary>
/// URL картинок из PredictHQ и часть CDN часто дают 403/битую картинку в браузере — лучше подобрать через тот же поиск, что в админке.
/// </summary>
public static class PlaceImageUrlHeuristics
{
    /// <summary>Пустой, не http(s) или известный ненадёжный источник — заменить автоподбором.</summary>
    public static bool ShouldAutopickInsteadOfImportedUrl(string? imageUrl)
    {
        if (string.IsNullOrWhiteSpace(imageUrl))
            return true;

        var t = imageUrl.Trim();
        if (!t.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            && !t.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            return true;

        if (!Uri.TryCreate(t, UriKind.Absolute, out var uri))
            return true;

        var host = uri.Host ?? "";
        if (host.Contains("predicthq", StringComparison.OrdinalIgnoreCase))
            return true;
        if (host.Contains("phq.imgix", StringComparison.OrdinalIgnoreCase))
            return true;
        if (host.EndsWith(".phq.io", StringComparison.OrdinalIgnoreCase)
            || host.Contains(".phq.io", StringComparison.OrdinalIgnoreCase))
            return true;

        return false;
    }
}
