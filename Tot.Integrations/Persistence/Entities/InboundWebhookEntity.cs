namespace Tot.Integrations.Persistence.Entities;

internal sealed class InboundWebhookEntity
{
    /// <summary>bigint GENERATED ALWAYS AS IDENTITY</summary>
    public long Id { get; set; }
    public Guid ConnectionId { get; set; }
    /// <summary>Provider-supplied deduplication id; may be null.</summary>
    public string? ProviderEventId { get; set; }
    public DateTimeOffset ReceivedAt { get; set; }
    /// <summary>Raw request headers (stored as jsonb).</summary>
    public string Headers { get; set; } = "{}";
    /// <summary>Raw request body bytes.</summary>
    public byte[] Body { get; set; } = [];
    public bool SignatureValid { get; set; }
    /// <summary>Received | Processed | Ignored | Failed</summary>
    public string Status { get; set; } = "Received";
    public int Attempts { get; set; }
    public string? Error { get; set; }
}
