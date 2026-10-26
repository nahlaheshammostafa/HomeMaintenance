using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Tot.Integrations.Runtime.Redaction;

/// <summary>
/// Redacts sensitive fields from HTTP bodies and headers.
/// Global rules cover the most common secret field names; per-provider rules
/// are added via <see cref="AddPatterns"/>.
/// Bodies are also truncated to <see cref="MaxBodyBytes"/> (16 KB).
/// </summary>
internal sealed class Redactor
{
    internal const int MaxBodyBytes = 16 * 1024;

    private static readonly string[] GlobalHeaderRules =
        ["authorization", "x-api-key", "x-auth-token", "cookie", "set-cookie"];

    // Global JSON path fragments to redact (case-insensitive wildcards)
    private static readonly string[] GlobalJsonFieldPatterns =
        ["password", "token", "secret", "apikey", "api_key", "cardnumber", "card_number", "cvv", "pin"];

    private readonly List<string> _extraPatterns = [];

    internal void AddPatterns(IEnumerable<string> patterns) => _extraPatterns.AddRange(patterns);

    /// <summary>Redacts and truncates a JSON body string. Returns <see langword="null"/> for empty input.</summary>
    internal string? RedactBody(string? body)
    {
        if (string.IsNullOrWhiteSpace(body))
            return null;

        // Truncate first (cheap), then redact
        if (Encoding.UTF8.GetByteCount(body) > MaxBodyBytes)
        {
            // Find a safe truncation point
            var truncated = body[..(MaxBodyBytes / 2)] + "…[truncated]";
            body = truncated;
        }

        return TryRedactJson(body, out var redacted) ? redacted : "[non-JSON body redacted]";
    }

    /// <summary>Returns <see langword="true"/> when the header name should be omitted from the journal.</summary>
    internal bool IsRedactedHeader(string headerName)
    {
        var lower = headerName.ToLowerInvariant();
        if (GlobalHeaderRules.Any(r => lower == r))
            return true;
        return _extraPatterns.Any(p => MatchesPattern(lower, p.ToLowerInvariant()));
    }

    // ── JSON redaction ───────────────────────────────────────────────────────

    private bool TryRedactJson(string body, out string result)
    {
        result = body;
        try
        {
            using var doc = JsonDocument.Parse(body);
            var redacted = RedactElement(doc.RootElement);
            result = redacted.GetRawText();
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private JsonElement RedactElement(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.Object => RedactObject(element),
            JsonValueKind.Array => RedactArray(element),
            _ => element.Clone()
        };
    }

    private JsonElement RedactObject(JsonElement obj)
    {
        var buffer = new System.IO.MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            foreach (var prop in obj.EnumerateObject())
            {
                if (ShouldRedactField(prop.Name))
                {
                    writer.WriteString(prop.Name, "***");
                }
                else
                {
                    writer.WritePropertyName(prop.Name);
                    RedactElement(prop.Value).WriteTo(writer);
                }
            }
            writer.WriteEndObject();
        }
        return JsonDocument.Parse(buffer.ToArray()).RootElement.Clone();
    }

    private JsonElement RedactArray(JsonElement arr)
    {
        var buffer = new System.IO.MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartArray();
            foreach (var item in arr.EnumerateArray())
                RedactElement(item).WriteTo(writer);
            writer.WriteEndArray();
        }
        return JsonDocument.Parse(buffer.ToArray()).RootElement.Clone();
    }

    private bool ShouldRedactField(string fieldName)
    {
        var lower = fieldName.ToLowerInvariant();
        if (GlobalJsonFieldPatterns.Any(p => MatchesPattern(lower, p)))
            return true;
        return _extraPatterns.Any(p => MatchesPattern(lower, p.ToLowerInvariant()));
    }

    private static bool MatchesPattern(string value, string pattern)
    {
        if (!pattern.Contains('*'))
            return value == pattern || value.Contains(pattern, StringComparison.Ordinal);
        // Convert glob-style wildcard to regex
        var regex = "^" + Regex.Escape(pattern).Replace("\\*", ".*") + "$";
        return Regex.IsMatch(value, regex);
    }
}
