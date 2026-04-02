using System.Text;

namespace Advisio.API.Services;

public static class PredictHqDescriptionNormalizer
{
    public static bool LooksLikeRawPredictHqImportDescription(string? description)
    {
        if (string.IsNullOrWhiteSpace(description))
            return false;

        var t = description.TrimStart();
        if (t.Contains("predicthq", StringComparison.OrdinalIgnoreCase))
            return true;
        if (t.StartsWith("Sourced from", StringComparison.OrdinalIgnoreCase))
            return true;
        return false;
    }

    public static string StripAttributionLines(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        var lines = text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        var kept = new List<string>();
        foreach (var line in lines)
        {
            var t = line.Trim();
            if (string.IsNullOrEmpty(t))
                continue;
            if (t.Contains("predicthq", StringComparison.OrdinalIgnoreCase))
                continue;
            if (t.Contains("predict hq", StringComparison.OrdinalIgnoreCase))
                continue;
            if (t.StartsWith("Sourced from", StringComparison.OrdinalIgnoreCase))
                continue;
            kept.Add(t);
        }

        return string.Join(Environment.NewLine, kept);
    }

    public static string FinalizeImportedDescription(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        var sb = new StringBuilder();
        foreach (var line in text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n'))
        {
            var t = line.Trim();
            if (string.IsNullOrEmpty(t))
                continue;
            if (t.Contains("predicthq", StringComparison.OrdinalIgnoreCase))
                continue;
            if (t.StartsWith("Sourced from", StringComparison.OrdinalIgnoreCase))
                continue;
            sb.AppendLine(t);
        }

        return sb.ToString().Trim();
    }
}
