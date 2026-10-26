using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using Tot.Integrations.Persistence.Entities;

namespace Tot.Integrations.Persistence;

internal sealed class EfExternalIdMap : IExternalIdMap
{
    private readonly IntegrationsDbContext _db;
    private readonly Guid _connectionId;

    internal EfExternalIdMap(IntegrationsDbContext db, Guid connectionId)
    {
        _db = db;
        _connectionId = connectionId;
    }

    /// <inheritdoc/>
    public async Task<ExternalRef?> GetExternalAsync(
        string internalType, string internalId, string externalType, CancellationToken ct)
    {
        var entity = await _db.ExternalIds
            .AsNoTracking()
            .FirstOrDefaultAsync(e =>
                e.ConnectionId == _connectionId
                && e.InternalType == internalType
                && e.InternalId == internalId
                && e.ExternalType == externalType, ct);

        return entity is null ? null : new ExternalRef(entity.ExternalType, entity.ExternalId);
    }

    /// <inheritdoc/>
    public async Task<(string InternalType, string InternalId)?> GetInternalAsync(
        ExternalRef externalRef, CancellationToken ct)
    {
        var entity = await _db.ExternalIds
            .AsNoTracking()
            .FirstOrDefaultAsync(e =>
                e.ConnectionId == _connectionId
                && e.ExternalType == externalRef.Type
                && e.ExternalId == externalRef.Id, ct);

        return entity is null ? null : (entity.InternalType, entity.InternalId);
    }

    /// <inheritdoc/>
    public async Task UpsertAsync(
        string internalType, string internalId, ExternalRef externalRef,
        JsonElement? meta = null, CancellationToken ct = default)
    {
        // Detect if the external ref is already claimed by a different internal entity
        var conflict = await _db.ExternalIds
            .AsNoTracking()
            .Where(e =>
                e.ConnectionId == _connectionId
                && e.ExternalType == externalRef.Type
                && e.ExternalId == externalRef.Id
                && !(e.InternalType == internalType && e.InternalId == internalId))
            .FirstOrDefaultAsync(ct);

        if (conflict is not null)
            throw new InvalidOperationException(
                $"External ref ({externalRef.Type}, {externalRef.Id}) is already mapped to " +
                $"({conflict.InternalType}, {conflict.InternalId}).");

        var entity = await _db.ExternalIds
            .FirstOrDefaultAsync(e =>
                e.ConnectionId == _connectionId
                && e.InternalType == internalType
                && e.InternalId == internalId
                && e.ExternalType == externalRef.Type, ct);

        if (entity is null)
        {
            _db.ExternalIds.Add(new ExternalIdEntity
            {
                ConnectionId = _connectionId,
                InternalType = internalType,
                InternalId = internalId,
                ExternalType = externalRef.Type,
                ExternalId = externalRef.Id,
                Meta = meta?.GetRawText()
            });
        }
        else
        {
            entity.ExternalId = externalRef.Id;
            entity.Meta = meta?.GetRawText();
        }

        await _db.SaveChangesAsync(ct);
    }

    /// <inheritdoc/>
    public async Task DeleteAsync(
        string internalType, string internalId, string externalType, CancellationToken ct)
    {
        var entity = await _db.ExternalIds
            .FirstOrDefaultAsync(e =>
                e.ConnectionId == _connectionId
                && e.InternalType == internalType
                && e.InternalId == internalId
                && e.ExternalType == externalType, ct);

        if (entity is not null)
        {
            _db.ExternalIds.Remove(entity);
            await _db.SaveChangesAsync(ct);
        }
    }
}
