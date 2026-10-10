namespace HomeMaintenance.Core.Common;

/// <summary>
/// Generic base entity providing standard identification and audit timestamp properties.
/// </summary>
/// <typeparam name="TKey">The primary key type (e.g., Guid, int).</typeparam>
public abstract class BaseEntity<TKey>
{
    public TKey Id { get; set; } = default!;

    /// <summary>
    /// UTC timestamp of when the entity was created.
    /// </summary>
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// UTC timestamp of when the entity was last updated.
    /// Null until the first update occurs.
    /// </summary>
    public DateTime? UpdatedAtUtc { get; set; }
}

/// <summary>
/// Default base entity using a GUID primary key.
/// </summary>
public abstract class BaseEntity : BaseEntity<Guid>
{
    protected BaseEntity()
    {
        CreatedAtUtc = DateTime.UtcNow;
    }

    protected BaseEntity(Guid id)
    {
        Id = id;
        CreatedAtUtc = DateTime.UtcNow;
    }
}
