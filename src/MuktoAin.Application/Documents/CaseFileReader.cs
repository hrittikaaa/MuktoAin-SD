using System.Text.Json;

namespace MuktoAin.Application.Documents;

/// <summary>
/// Reads structured field values from a Case.Description, which may be either:
/// - Flattened "key: value\n" lines (from CaseFileToDescription)
/// - Raw JSON (if the case file JSON was stored directly)
/// Falls back gracefully — always returns the placeholder if the field isn't found.
/// </summary>
public static class CaseFileReader
{
    private const string Placeholder = "________";

    /// <summary>Reads a field value from the case description. Returns the placeholder if not found.</summary>
    public static string Read(string? description, string fieldKey, string fallback = Placeholder)
    {
        if (string.IsNullOrWhiteSpace(description)) return fallback;
        var jsonValue = TryReadJson(description, fieldKey);
        if (!string.IsNullOrWhiteSpace(jsonValue)) return jsonValue;
        var lineValue = TryReadFlatLine(description, fieldKey);
        return !string.IsNullOrWhiteSpace(lineValue) ? lineValue : fallback;
    }

    /// <summary>Reads a field, returns null if not found (no placeholder).</summary>
    public static string? ReadOrNull(string? description, string fieldKey)
    {
        if (string.IsNullOrWhiteSpace(description)) return null;
        var jsonValue = TryReadJson(description, fieldKey);
        if (!string.IsNullOrWhiteSpace(jsonValue)) return jsonValue;
        var lineValue = TryReadFlatLine(description, fieldKey);
        return !string.IsNullOrWhiteSpace(lineValue) ? lineValue : null;
    }

    private static string? TryReadJson(string text, string key)
    {
        try
        {
            var trimmed = text.TrimStart();
            if (!trimmed.StartsWith('{')) return null;
            using var doc = JsonDocument.Parse(trimmed);
            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                if (string.Equals(prop.Name, key, StringComparison.OrdinalIgnoreCase))
                {
                    return prop.Value.ValueKind == JsonValueKind.String
                        ? prop.Value.GetString()
                        : prop.Value.ValueKind == JsonValueKind.Null ? null : prop.Value.GetRawText();
                }
            }
        }
        catch { /* not JSON or parse failure */ }
        return null;
    }

    private static string? TryReadFlatLine(string text, string key)
    {
        foreach (var line in text.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var colonIdx = line.IndexOf(':');
            if (colonIdx <= 0) continue;
            var lineKey = line[..colonIdx].Trim();
            if (string.Equals(lineKey, key, StringComparison.OrdinalIgnoreCase))
            {
                var value = line[(colonIdx + 1)..].Trim();
                return string.IsNullOrWhiteSpace(value) ? null : value;
            }
        }
        return null;
    }
}
