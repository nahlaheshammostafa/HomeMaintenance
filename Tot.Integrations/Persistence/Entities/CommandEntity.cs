namespace Tot.Integrations.Persistence.Entities;

internal sealed class CommandEntity
{
    public Guid Id { get; set; }
    public Guid ConnectionId { get; set; }
    public string Operation { get; set; } = "";
    public string IdempotencyKey { get; set; } = "";
    /// <summary>Command payload (stored as jsonb).</summary>
    public string Payload { get; set; } = "{}";
    /// <summary>Pending | Running | Succeeded | Failed | DeadLettered</summary>
    public string Status { get; set; } = "Pending";
    public int Attempts { get; set; }
    public DateTimeOffset? NextAttemptAt { get; set; }
    /// <summary>Last error details (stored as jsonb).</summary>
    public string? LastError { get; set; }
    /// <summary>Final result on success (stored as jsonb).</summary>
    public string? Result { get; set; }
    public string? CorrelationId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    /// <summary>Set to now() when the dispatcher claims a row (status → Running). Used to detect stuck rows.</summary>
    public DateTimeOffset? LockedAt { get; set; }
}
