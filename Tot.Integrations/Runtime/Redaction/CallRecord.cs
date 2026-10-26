namespace Tot.Integrations.Runtime.Redaction;

/// <summary>
/// A single outbound HTTP call captured by <c>JournalHandler</c>.
/// Written to the bounded channel and flushed to the DB by <c>JournalWriter</c> (INT-18).
/// All bodies are already redacted and truncated.
/// </summary>
internal sealed record CallRecord(
    DateTimeOffset Ts,
    Guid ConnectionId,
    string ProviderCode,
    string? Operation,
    string Method,
    string UrlPath,           // path only — never query string
    int? StatusCode,
    int DurationMs,
    string Outcome,           // "success" | "error" | "timeout" | "circuit_open"
    string? ErrorKind,
    string? RequestBody,      // redacted JSON or null
    string? ResponseBody,     // redacted JSON or null
    string? TraceId,
    Guid? CommandId);
