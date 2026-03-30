namespace Advisio.API.Services;

public static class PlaceNameSanitizer
{
    public static string ForDisplay(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return name ?? string.Empty;
        return StripTrailingIdSuffix(name.Trim());
    }

    private static string StripTrailingIdSuffix(string t)
    {
        var idx = t.LastIndexOf('\u00B7');
        if (idx <= 0 || idx >= t.Length - 1)
            return t;

        var right = t[(idx + 1)..].Trim();
        if (right.Length < 8 || !IsLikelyOpaqueIdToken(right))
            return t;

        return t[..idx].TrimEnd();
    }

    private static bool IsLikelyOpaqueIdToken(string s)
    {
        foreach (var c in s)
        {
            if (!char.IsAsciiLetterOrDigit(c) && c != '_' && c != '-')
                return false;
        }

        return true;
    }
}
