namespace Advisio.API.Services;

public static class PlaceTypeFormatter
{
    public const string DefaultCanonicalType = "Фестиваль";

    public static readonly string[] CanonicalTypes =
    {
        "Концерт",
        "Фестиваль",
        "Ресторан",
        "Парк",
        "Турнир",
        "Выставка",
        "Спектакль",
        "Кино",
        "Образование",
        "Эко тропа"
    };

    private static readonly Dictionary<string, string> SlugToRussian =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["concerts"] = "Концерт",
            ["concert"] = "Концерт",
            ["music"] = "Концерт",
            ["festivals"] = "Фестиваль",
            ["festival"] = "Фестиваль",
            ["performing-arts"] = "Спектакль",
            ["performing arts"] = "Спектакль",
            ["theatre"] = "Спектакль",
            ["theater"] = "Спектакль",
            ["comedy"] = "Спектакль",
            ["dance"] = "Спектакль",
            ["expos"] = "Выставка",
            ["expo"] = "Выставка",
            ["exhibitions"] = "Выставка",
            ["exhibition"] = "Выставка",
            ["sports"] = "Турнир",
            ["sport"] = "Турнир",
            ["community"] = "Фестиваль",
            ["conferences"] = "Образование",
            ["conference"] = "Образование",
            ["academic"] = "Образование",
            ["education"] = "Образование",
            ["cinema"] = "Кино",
            ["film"] = "Кино",
            ["movies"] = "Кино",
            ["movie"] = "Кино",
            ["restaurants"] = "Ресторан",
            ["restaurant"] = "Ресторан",
            ["food"] = "Ресторан",
            ["parks"] = "Парк",
            ["park"] = "Парк",
            ["shopping"] = "Фестиваль",
            ["business"] = "Фестиваль",
            ["career"] = "Фестиваль",
            ["awards"] = "Фестиваль",
            ["politics"] = "Фестиваль",
            ["observances"] = "Фестиваль",
            ["public-holidays"] = "Фестиваль",
            ["school-holidays"] = "Фестиваль",
            ["hiking"] = "Эко тропа",
            ["trail"] = "Эко тропа",
            ["trails"] = "Эко тропа",
            ["nature"] = "Эко тропа",
            ["outdoor"] = "Эко тропа",
            ["tours"] = "Эко тропа",
            ["tour"] = "Эко тропа"
        };

    public static string FormatForDisplay(string? type)
    {
        if (string.IsNullOrWhiteSpace(type))
            return DefaultCanonicalType;

        var t = type.Trim();
        if (TryMatchCanonicalRu(t, out var canon))
            return canon;

        var key = NormalizeKey(t);
        if (SlugToRussian.TryGetValue(key, out var ru))
            return ru;
        if (SlugToRussian.TryGetValue(t, out ru))
            return ru;

        if (HasCyrillic(t))
            return DefaultCanonicalType;

        return DefaultCanonicalType;
    }

    private static bool TryMatchCanonicalRu(string t, out string canonical)
    {
        canonical = t;
        var lower = t.ToLowerInvariant().Replace('ё', 'е');
        foreach (var c in CanonicalTypes)
        {
            if (string.Equals(c, t, StringComparison.Ordinal))
            {
                canonical = c;
                return true;
            }
        }

        foreach (var c in CanonicalTypes)
        {
            if (lower == c.ToLowerInvariant().Replace('ё', 'е'))
            {
                canonical = c;
                return true;
            }
        }

        if (lower.Contains("эко", StringComparison.Ordinal) && lower.Contains("троп", StringComparison.Ordinal))
        {
            canonical = "Эко тропа";
            return true;
        }

        return false;
    }

    public static IReadOnlyList<string> LowercasedValuesMatchingTypeFilter(string filterType)
    {
        if (string.IsNullOrWhiteSpace(filterType))
            return Array.Empty<string>();

        var set = new HashSet<string>(StringComparer.Ordinal);
        void AddLower(string? s)
        {
            if (!string.IsNullOrWhiteSpace(s))
                set.Add(s.Trim().ToLowerInvariant());
        }

        var trimmed = filterType.Trim();
        var canonical = FormatForDisplay(trimmed);
        AddLower(canonical);
        AddLower(trimmed);
        AddLower(DefaultCanonicalType);
        AddLower("Мероприятие");
        AddLower("мероприятие");

        foreach (var kv in SlugToRussian)
        {
            if (string.Equals(kv.Value, canonical, StringComparison.Ordinal))
            {
                AddLower(kv.Key);
                AddLower(kv.Key.Replace('-', ' '));
            }
        }

        return set.ToList();
    }

    private static string NormalizeKey(string t) =>
        t.ToLowerInvariant().Replace('_', ' ').Replace('-', ' ').Trim();

    private static bool HasCyrillic(string s)
    {
        foreach (var c in s)
        {
            if (c is >= '\u0400' and <= '\u04FF')
                return true;
        }

        return false;
    }
}
