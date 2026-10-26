namespace Tot.Integrations.Persistence.Entities;

internal sealed class SecretEntity
{
    /// <summary>Stable key (e.g. <c>connection:{id}</c>).</summary>
    public string Ref { get; set; } = "";
    /// <summary>ASP.NET Data Protection ciphertext.</summary>
    public byte[] Ciphertext { get; set; } = [];
    public DateTimeOffset RotatedAt { get; set; }
}
