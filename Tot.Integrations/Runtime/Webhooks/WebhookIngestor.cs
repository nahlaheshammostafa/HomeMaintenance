using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using Tot.Integrations.Persistence;
using Tot.Integrations.Persistence.Entities;
using Tot.Integrations.Runtime.Redaction;
using Tot.Integrations.Runtime.Registry;

namespace Tot.Integrations.Runtime.Webhooks;

/// <summary>
/// Handles the ingest half of a webhook delivery: persists the raw request,
/// verifies the signature, deduplicates on provider event id, and returns the
/// appropriate HTTP result.
/// </summary>
internal sealed class WebhookIngestor
{
    private readonly IConnectionStore _connectionStore;
    private readonly ProviderRegistry _registry;
    private readonly CapabilityResolver _capabilityResolver;
    private readonly IntegrationsDbContext _db;
    private readonly IServiceProvider _services;
    private readonly Redactor _redactor;
    private readonly ILogger<WebhookIngestor> _logger;

    public WebhookIngestor(
        IConnectionStore connectionStore,
        ProviderRegistry registry,
        CapabilityResolver capabilityResolver,
        IntegrationsDbContext db,
        IServiceProvider services,
        Redactor redactor,
        ILogger<WebhookIngestor> logger)
    {
        _connectionStore = connectionStore;
        _registry = registry;
        _capabilityResolver = capabilityResolver;
        _db = db;
        _services = services;
        _redactor = redactor;
        _logger = logger;
    }

    internal async Task<IResult> IngestAsync(
        string providerCode,
        string webhookKey,
        IReadOnlyDictionary<string, string> headers,
        byte[] body,
        CancellationToken ct)
    {
        // 1. Find connection by webhook key
        var connection = await _connectionStore.GetByWebhookKeyAsync(webhookKey, ct);
        if (connection is null || !connection.Enabled)
            return Results.NotFound();

        // 2. Verify provider code matches the connection
        if (!string.Equals(connection.ProviderCode, providerCode, StringComparison.OrdinalIgnoreCase))
            return Results.NotFound();

        // 3. Persist raw payload before verification (always store, even invalid).
        // R-12: filter sensitive headers (Authorization, x-api-key, etc.) before persisting.
        var safeHeaders = headers
            .Where(kv => !_redactor.IsRedactedHeader(kv.Key))
            .ToDictionary(kv => kv.Key, kv => kv.Value);
        var headersJson = JsonSerializer.Serialize(safeHeaders);
        var entity = new InboundWebhookEntity
        {
            ConnectionId = connection.Id,
            Headers = headersJson,
            Body = body,
            ReceivedAt = DateTimeOffset.UtcNow,
            SignatureValid = false,
            Status = "Received"
        };
        _db.InboundWebhooks.Add(entity);
        await _db.SaveChangesAsync(ct);

        // 4. Resolve the webhook handler for this provider
        _registry.EnsureIndexed(_services);
        var handlerType = _registry.GetWebhookHandlerTypeForProvider(connection.ProviderCode);
        if (handlerType is null)
        {
            _logger.LogWarning(
                "WebhookIngestor: no handler registered for provider '{Provider}'.", connection.ProviderCode);
            return Results.Ok(); // still 200 — we stored it
        }

        // 5. Build connection context
        IConnectionContext ctx;
        try
        {
            ctx = await _capabilityResolver.BuildConnectionContextAsync(connection, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "WebhookIngestor: failed to build context for connection {Id}.", connection.Id);
            return Results.Ok(); // 200 to avoid endless provider retries
        }

        // 6. Verify signature
        var hook = new InboundWebhook(
            ConnectionId: connection.Id,
            ProviderCode: connection.ProviderCode,
            Headers: headers,
            Body: body,
            ReceivedAt: entity.ReceivedAt);

        var handler = (IWebhookHandler)ActivatorUtilities.CreateInstance(_services, handlerType);
        WebhookVerification verification;
        try
        {
            verification = await handler.VerifyAsync(hook, ctx, ct);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            _logger.LogError(ex, "WebhookIngestor: VerifyAsync threw for connection {Id}.", connection.Id);
            entity.Error = ex.Message;
            await _db.SaveChangesAsync(ct);
            return Results.Ok(); // 200 to avoid triggering unbounded provider retries
        }

        if (!verification.IsValid)
        {
            _logger.LogWarning(
                "WebhookIngestor: invalid signature on connection {Id} — {Reason}.",
                connection.Id, verification.Reason);
            // Row stays in DB with SignatureValid = false
            return Results.StatusCode(401);
        }

        // 7. Deduplication on provider event id
        if (verification.ProviderEventId is not null)
        {
            entity.ProviderEventId = verification.ProviderEventId;
            var isDuplicate = await _db.InboundWebhooks
                .AsNoTracking()
                .AnyAsync(w =>
                    w.ConnectionId == connection.Id &&
                    w.ProviderEventId == verification.ProviderEventId &&
                    w.Id != entity.Id &&
                    w.SignatureValid,
                    ct);

            if (isDuplicate)
            {
                _db.InboundWebhooks.Remove(entity);
                await _db.SaveChangesAsync(ct);
                return Results.Ok();
            }
        }

        // 8. Mark valid and commit
        entity.SignatureValid = true;
        await _db.SaveChangesAsync(ct);

        return Results.Ok();
    }
}
