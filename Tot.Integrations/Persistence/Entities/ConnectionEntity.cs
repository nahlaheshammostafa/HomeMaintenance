namespace Tot.Integrations.Persistence.Entities;

internal sealed class ConnectionEntity
{
    public Guid Id { get; set; }
    public string ProviderCode { get; set; } = "";
    public string Name { get; set; } = "";
    public string? OwnerType { get; set; }
    public string? OwnerId { get; set; }
    public string Environment { get; set; } = "production";
    public bool Enabled { get; set; } = true;
    /// <summary>Non-secret settings JSON (stored as jsonb).</summary>
    public string Settings { get; set; } = "{}";
    public string SecretRef { get; set; } = "";
    public string WebhookKey { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
}
