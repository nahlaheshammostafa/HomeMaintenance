namespace Tot.Integrations.Persistence.Entities;

internal sealed class ExternalIdEntity
{
    public Guid ConnectionId { get; set; }
    public string InternalType { get; set; } = "";
    public string InternalId { get; set; } = "";
    public string ExternalType { get; set; } = "";
    public string ExternalId { get; set; } = "";
    /// <summary>Optional extra metadata (stored as jsonb).</summary>
    public string? Meta { get; set; }
}
