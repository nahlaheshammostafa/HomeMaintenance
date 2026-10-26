using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using System.Security.Cryptography;
using System.Text.Json;
using Tot.Integrations.Persistence.Entities;

namespace Tot.Integrations.Persistence;

internal sealed class EfConnectionStore : IConnectionStore
{
    private readonly IntegrationsDbContext _db;
    private readonly IMemoryCache _cache;

    public EfConnectionStore(IntegrationsDbContext db, IMemoryCache cache)
    {
        _db = db;
        _cache = cache;
    }

    /// <inheritdoc/>
    public async Task<IntegrationConnection?> GetByIdAsync(Guid id, CancellationToken ct)
    {
        var entity = await _db.Connections
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id, ct);
        return entity is null ? null : ToModel(entity);
    }

    /// <inheritdoc/>
    public async Task<IntegrationConnection?> GetByWebhookKeyAsync(string webhookKey, CancellationToken ct)
    {
        var entity = await _db.Connections
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.WebhookKey == webhookKey, ct);
        return entity is null ? null : ToModel(entity);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<IntegrationConnection>> ListAsync(
        string? providerCode = null,
        string? ownerType = null,
        string? ownerId = null,
        CancellationToken ct = default)
    {
        var query = _db.Connections.AsNoTracking();

        if (providerCode is not null)
            query = query.Where(c => c.ProviderCode == providerCode);
        if (ownerType is not null)
            query = query.Where(c => c.OwnerType == ownerType);
        if (ownerId is not null)
            query = query.Where(c => c.OwnerId == ownerId);

        var entities = await query.ToListAsync(ct);
        return entities.Select(ToModel).ToList();
    }

    /// <inheritdoc/>
    public async Task<IntegrationConnection> CreateAsync(IntegrationConnection connection, CancellationToken ct)
    {
        var entity = new ConnectionEntity
        {
            Id = Guid.NewGuid(),
            ProviderCode = connection.ProviderCode,
            Name = connection.Name,
            OwnerType = connection.OwnerType,
            OwnerId = connection.OwnerId,
            Environment = connection.Environment,
            Enabled = connection.Enabled,
            Settings = connection.Settings.GetRawText(),
            SecretRef = connection.SecretRef,
            WebhookKey = GenerateWebhookKey(),
            CreatedAt = DateTimeOffset.UtcNow
        };

        _db.Connections.Add(entity);
        await _db.SaveChangesAsync(ct);
        return ToModel(entity);
    }

    /// <inheritdoc/>
    public async Task<IntegrationConnection> UpdateAsync(IntegrationConnection connection, CancellationToken ct)
    {
        var entity = await _db.Connections
            .FirstOrDefaultAsync(c => c.Id == connection.Id, ct)
            ?? throw new InvalidOperationException($"Integration connection '{connection.Id}' not found.");

        entity.ProviderCode = connection.ProviderCode;
        entity.Name = connection.Name;
        entity.OwnerType = connection.OwnerType;
        entity.OwnerId = connection.OwnerId;
        entity.Environment = connection.Environment;
        entity.Enabled = connection.Enabled;
        entity.Settings = connection.Settings.GetRawText();
        entity.SecretRef = connection.SecretRef;
        entity.UpdatedAt = DateTimeOffset.UtcNow;

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            throw new ConnectionConcurrencyException(connection.Id, ex);
        }

        _cache.Remove($"integration:connection:{connection.Id}");
        return ToModel(entity);
    }

    /// <inheritdoc/>
    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        var entity = await _db.Connections
            .FirstOrDefaultAsync(c => c.Id == id, ct);

        if (entity is not null)
        {
            _db.Connections.Remove(entity);
            await _db.SaveChangesAsync(ct);
            _cache.Remove($"integration:connection:{id}");
        }
    }

    private static IntegrationConnection ToModel(ConnectionEntity e)
    {
        using var doc = JsonDocument.Parse(e.Settings);
        return new IntegrationConnection(
            e.Id,
            e.ProviderCode,
            e.Name,
            e.OwnerType,
            e.OwnerId,
            e.Environment,
            e.Enabled,
            doc.RootElement.Clone(),
            e.SecretRef,
            e.WebhookKey);
    }

    private static string GenerateWebhookKey()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }
}
