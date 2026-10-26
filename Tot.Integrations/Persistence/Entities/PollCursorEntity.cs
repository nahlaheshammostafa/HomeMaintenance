namespace Tot.Integrations.Persistence.Entities;

internal sealed class PollCursorEntity
{
    public Guid ConnectionId { get; set; }
    /// <summary>Fully-qualified source type name used as discriminator.</summary>
    public string Source { get; set; } = "";
    public string? Cursor { get; set; }
    public DateTimeOffset? PolledAt { get; set; }
}
