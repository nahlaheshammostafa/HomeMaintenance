using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Tot.Integrations.Endpoints;
using Tot.Integrations.Persistence;
using Tot.Integrations.Persistence.Entities;
using Tot.Integrations.Runtime.Registry;

namespace Tot.Integrations.Runtime.Admin;

/// <summary>
/// Business logic for the integration admin API. Registered as a scoped service and
/// consumed by route handlers mapped via <c>MapIntegrationAdminApi</c>.
/// </summary>
internal sealed class AdminApiHandler
{
    private readonly IConnectionStore _connectionStore;
    private readonly ISecretStore _secretStore;
    private readonly CapabilityResolver _capabilityResolver;
    private readonly IntegrationsDbContext _db;
    private readonly ProviderRegistry _registry;
    private readonly IServiceProvider _services;
    private readonly ILogger<AdminApiHandler> _logger;

    public AdminApiHandler(
        IConnectionStore connectionStore,
        ISecretStore secretStore,
        CapabilityResolver capabilityResolver,
        IntegrationsDbContext db,
        ProviderRegistry registry,
        IServiceProvider services,
        ILogger<AdminApiHandler> logger)
    {
        _connectionStore = connectionStore;
        _secretStore = secretStore;
        _capabilityResolver = capabilityResolver;
        _db = db;
        _registry = registry;
        _services = services;
        _logger = logger;
    }

    // ── Connections ──────────────────────────────────────────────────────────

    internal async Task<IResult> ListConnectionsAsync(
        string? providerCode, string? ownerType, string? ownerId, CancellationToken ct)
    {
        var list = await _connectionStore.ListAsync(providerCode, ownerType, ownerId, ct);
        return Results.Ok(list.Select(ToDto).ToList());
    }

    internal async Task<IResult> GetConnectionAsync(Guid id, CancellationToken ct)
    {
        var conn = await _connectionStore.GetByIdAsync(id, ct);
        return conn is null ? Results.NotFound() : Results.Ok(ToDto(conn));
    }

    internal async Task<IResult> CreateConnectionAsync(CreateConnectionRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.ProviderCode))
            return Results.ValidationProblem(new Dictionary<string, string[]>
                { ["ProviderCode"] = ["ProviderCode is required."] });

        _registry.EnsureIndexed(_services);
        if (!_registry.TryGetProvider(req.ProviderCode, out var reg))
            return Results.ValidationProblem(new Dictionary<string, string[]>
                { ["ProviderCode"] = [$"Provider '{req.ProviderCode}' is not registered."] });

        var settingsErrors = ValidateSettings(req.Settings, reg.Descriptor.SettingsType);
        if (settingsErrors.Length > 0)
            return Results.ValidationProblem(new Dictionary<string, string[]>
                { ["Settings"] = settingsErrors });

        var secretRef = $"integration:{req.ProviderCode}:{Guid.NewGuid():N}";

        try
        {
            await _secretStore.SetAsync(secretRef, req.SecretValue, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "AdminApiHandler: failed to store secret for new connection.");
            return Results.Problem("Failed to store the connection secret.");
        }

        var connection = new IntegrationConnection(
            Id: Guid.Empty,
            ProviderCode: req.ProviderCode,
            Name: req.Name,
            OwnerType: req.OwnerType,
            OwnerId: req.OwnerId,
            Environment: req.Environment,
            Enabled: true,
            Settings: req.Settings,
            SecretRef: secretRef,
            WebhookKey: "");

        var created = await _connectionStore.CreateAsync(connection, ct);
        return Results.Created($"connections/{created.Id}", ToDto(created));
    }

    internal async Task<IResult> UpdateConnectionAsync(Guid id, UpdateConnectionRequest req, CancellationToken ct)
    {
        var existing = await _connectionStore.GetByIdAsync(id, ct);
        if (existing is null) return Results.NotFound();

        _registry.EnsureIndexed(_services);
        if (_registry.TryGetProvider(existing.ProviderCode, out var reg))
        {
            var settingsErrors = ValidateSettings(req.Settings, reg.Descriptor.SettingsType);
            if (settingsErrors.Length > 0)
                return Results.ValidationProblem(new Dictionary<string, string[]>
                    { ["Settings"] = settingsErrors });
        }

        var updated = existing with
        {
            Name = req.Name,
            OwnerType = req.OwnerType,
            OwnerId = req.OwnerId,
            Environment = req.Environment,
            Settings = req.Settings
        };

        try
        {
            var result = await _connectionStore.UpdateAsync(updated, ct);
            return Results.Ok(ToDto(result));
        }
        catch (ConnectionConcurrencyException)
        {
            return Results.Conflict("Connection was modified concurrently. Reload and retry.");
        }
    }

    internal async Task<IResult> DeleteConnectionAsync(Guid id, CancellationToken ct)
    {
        var existing = await _connectionStore.GetByIdAsync(id, ct);
        if (existing is null) return Results.NotFound();

        await _connectionStore.DeleteAsync(id, ct);
        await _secretStore.DeleteAsync(existing.SecretRef, ct);
        return Results.NoContent();
    }

    internal async Task<IResult> SetEnabledAsync(Guid id, bool enabled, CancellationToken ct)
    {
        var existing = await _connectionStore.GetByIdAsync(id, ct);
        if (existing is null) return Results.NotFound();

        var updated = await _connectionStore.UpdateAsync(existing with { Enabled = enabled }, ct);
        return Results.Ok(ToDto(updated));
    }

    internal async Task<IResult> SetSecretAsync(Guid id, SetSecretRequest req, CancellationToken ct)
    {
        var existing = await _connectionStore.GetByIdAsync(id, ct);
        if (existing is null) return Results.NotFound();

        if (string.IsNullOrWhiteSpace(req.SecretValue))
            return Results.ValidationProblem(new Dictionary<string, string[]>
                { ["SecretValue"] = ["SecretValue must not be empty."] });

        try
        {
            await _secretStore.RotateAsync(existing.SecretRef, ct);
            await _secretStore.SetAsync(existing.SecretRef, req.SecretValue, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "AdminApiHandler: failed to rotate secret for connection {Id}.", id);
            return Results.Problem("Failed to update the connection secret.");
        }

        // 204 — write-only, never echo the secret.
        return Results.NoContent();
    }

    internal async Task<IResult> TestConnectionAsync(Guid id, CancellationToken ct)
    {
        var connection = await _connectionStore.GetByIdAsync(id, ct);
        if (connection is null) return Results.NotFound();

        if (!connection.Enabled)
            return Results.BadRequest("Connection is disabled.");

        _registry.EnsureIndexed(_services);
        if (!_registry.TryGetProvider(connection.ProviderCode, out var reg))
            return Results.Problem($"Provider '{connection.ProviderCode}' is not registered.");

        IConnectionContext ctx;
        try
        {
            ctx = await _capabilityResolver.BuildConnectionContextAsync(connection, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return Results.Problem($"Failed to build connection context: {ex.Message}");
        }

        var provider = (IIntegrationProvider)Microsoft.Extensions.DependencyInjection
            .ActivatorUtilities.CreateInstance(_services, reg.ProviderType);

        var pingResult = await provider.PingAsync(ctx, ct);
        if (pingResult.IsSuccess)
            return Results.Ok(pingResult.Value);

        return Results.Problem(
            detail: pingResult.Error!.Message,
            title: pingResult.Error.Code);
    }

    // ── External IDs ─────────────────────────────────────────────────────────

    internal async Task<IResult> ListExternalIdsAsync(Guid connectionId, CancellationToken ct)
    {
        var exists = await _connectionStore.GetByIdAsync(connectionId, ct);
        if (exists is null) return Results.NotFound();

        var entities = await _db.ExternalIds
            .AsNoTracking()
            .Where(e => e.ConnectionId == connectionId)
            .ToListAsync(ct);

        return Results.Ok(entities.Select(ToExternalIdDto).ToList());
    }

    internal async Task<IResult> UpsertExternalIdAsync(Guid connectionId, UpsertExternalIdRequest req, CancellationToken ct)
    {
        var exists = await _connectionStore.GetByIdAsync(connectionId, ct);
        if (exists is null) return Results.NotFound();

        var idMap = new EfExternalIdMap(_db, connectionId);

        try
        {
            await idMap.UpsertAsync(
                req.InternalType, req.InternalId,
                new ExternalRef(req.ExternalType, req.ExternalId),
                req.Meta, ct);
        }
        catch (InvalidOperationException ex)
        {
            return Results.Conflict(ex.Message);
        }

        var entity = await _db.ExternalIds
            .AsNoTracking()
            .FirstAsync(e =>
                e.ConnectionId == connectionId &&
                e.InternalType == req.InternalType &&
                e.InternalId == req.InternalId &&
                e.ExternalType == req.ExternalType, ct);

        return Results.Ok(ToExternalIdDto(entity));
    }

    internal async Task<IResult> DeleteExternalIdAsync(
        Guid connectionId,
        string internalType, string internalId, string externalType,
        CancellationToken ct)
    {
        var exists = await _connectionStore.GetByIdAsync(connectionId, ct);
        if (exists is null) return Results.NotFound();

        var idMap = new EfExternalIdMap(_db, connectionId);
        await idMap.DeleteAsync(internalType, internalId, externalType, ct);
        return Results.NoContent();
    }

    // ── Commands ─────────────────────────────────────────────────────────────

    internal async Task<IResult> ListCommandsAsync(Guid? connectionId, string? status, CancellationToken ct)
    {
        var query = _db.Commands.AsNoTracking();
        if (connectionId is not null)
            query = query.Where(c => c.ConnectionId == connectionId.Value);
        if (!string.IsNullOrEmpty(status))
            query = query.Where(c => c.Status == status);

        var entities = await query.OrderByDescending(c => c.CreatedAt).Take(200).ToListAsync(ct);
        return Results.Ok(entities.Select(ToCommandDto).ToList());
    }

    internal async Task<IResult> RetryCommandAsync(Guid id, CancellationToken ct)
    {
        var entity = await _db.Commands.FirstOrDefaultAsync(c => c.Id == id, ct);
        if (entity is null) return Results.NotFound();

        if (entity.Status != "DeadLettered")
            return Results.BadRequest($"Only DeadLettered commands can be retried. Current status: {entity.Status}");

        entity.Status = "Pending";
        entity.Attempts = 0;
        entity.NextAttemptAt = DateTimeOffset.UtcNow;
        entity.LastError = null;
        entity.CompletedAt = null;

        await _db.SaveChangesAsync(ct);
        return Results.Ok(ToCommandDto(entity));
    }

    // ── Webhooks ─────────────────────────────────────────────────────────────

    internal async Task<IResult> ListWebhooksAsync(Guid? connectionId, string? status, CancellationToken ct)
    {
        var query = _db.InboundWebhooks.AsNoTracking();
        if (connectionId is not null)
            query = query.Where(w => w.ConnectionId == connectionId.Value);
        if (!string.IsNullOrEmpty(status))
            query = query.Where(w => w.Status == status);

        var entities = await query.OrderByDescending(w => w.ReceivedAt).Take(200).ToListAsync(ct);
        return Results.Ok(entities.Select(ToWebhookDto).ToList());
    }

    internal async Task<IResult> ReplayWebhookAsync(long id, CancellationToken ct)
    {
        var entity = await _db.InboundWebhooks.FirstOrDefaultAsync(w => w.Id == id, ct);
        if (entity is null) return Results.NotFound();

        if (entity.Status is not ("Processed" or "Failed" or "Ignored"))
            return Results.BadRequest(
                $"Webhooks with status '{entity.Status}' cannot be replayed. Only Processed, Failed, or Ignored.");

        entity.Status = "Received";
        entity.SignatureValid = true;
        entity.Attempts = 0;
        entity.Error = null;

        await _db.SaveChangesAsync(ct);
        return Results.Accepted(value: ToWebhookDto(entity));
    }

    // ── Providers ────────────────────────────────────────────────────────────

    internal Task<IResult> ListProvidersAsync(CancellationToken ct)
    {
        _registry.EnsureIndexed(_services);
        var providers = _registry.ProviderTypes
            .Select(t =>
            {
                if (_services.GetService(t) is not IIntegrationProvider p)
                    return null;
                var d = p.Descriptor;
                return new ProviderDto(
                    d.Code, d.DisplayName, d.Category,
                    d.Auth.ToString(),
                    d.SupportsWebhooks, d.SupportsPolling,
                    d.Capabilities.Select(c => c.Name).ToArray());
            })
            .Where(d => d is not null)
            .ToList();

        return Task.FromResult(Results.Ok(providers));
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static string[] ValidateSettings(JsonElement settings, Type settingsType)
    {
        if (settingsType == typeof(object) || settingsType == typeof(JsonElement))
            return [];

        object? obj;
        try
        {
            obj = settings.Deserialize(settingsType,
                new JsonSerializerOptions(JsonSerializerDefaults.Web));
        }
        catch (JsonException ex)
        {
            return [$"Settings cannot be deserialized to {settingsType.Name}: {ex.Message}"];
        }

        if (obj is null)
            return [$"Settings deserialized to null for type {settingsType.Name}."];

        var results = new List<ValidationResult>();
        Validator.TryValidateObject(obj, new ValidationContext(obj), results, validateAllProperties: true);
        return results.Select(r => r.ErrorMessage ?? "Validation error.").ToArray();
    }

    private static ConnectionDto ToDto(IntegrationConnection c) =>
        new(c.Id, c.ProviderCode, c.Name, c.OwnerType, c.OwnerId,
            c.Environment, c.Enabled, c.Settings, c.WebhookKey);

    private static ExternalIdDto ToExternalIdDto(ExternalIdEntity e)
    {
        JsonElement? meta = null;
        if (e.Meta is not null)
        {
            try { meta = JsonDocument.Parse(e.Meta).RootElement.Clone(); }
            catch { /* malformed stored json — skip */ }
        }
        return new ExternalIdDto(e.ConnectionId, e.InternalType, e.InternalId,
            e.ExternalType, e.ExternalId, meta);
    }

    private static CommandDto ToCommandDto(CommandEntity e) =>
        new(e.Id, e.ConnectionId, e.Operation, e.IdempotencyKey,
            e.Status, e.Attempts, e.NextAttemptAt, e.CreatedAt, e.CompletedAt);

    private static WebhookSummaryDto ToWebhookDto(InboundWebhookEntity e) =>
        new(e.Id, e.ConnectionId, e.ProviderEventId, e.ReceivedAt,
            e.SignatureValid, e.Status, e.Attempts, e.Error);
}
