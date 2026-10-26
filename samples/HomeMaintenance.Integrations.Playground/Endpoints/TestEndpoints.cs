using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Tot.Integrations;
using Tot.Integrations.Persistence;
using Tot.Integrations.Runtime.Webhooks;
using HomeMaintenance.Integrations.Playground.Providers.HomeDispatch;

namespace HomeMaintenance.Integrations.Playground.Endpoints;

public static class TestEndpoints
{
    public static RouteGroupBuilder MapPlaygroundTestApi(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/test")
            .WithTags("1. Interactive Playground Tests");

        // 1. Setup Demo Connection
        group.MapPost("/setup-demo-connection", async (
            IConnectionStore connStore,
            ISecretStore secretStore,
            CancellationToken ct) =>
        {
            var connections = await connStore.ListAsync(providerCode: "homedispatch", ct: ct);
            var existing = connections.FirstOrDefault(c => c.Name == "HomeDispatch Demo Connection");

            if (existing is not null)
            {
                return Results.Ok(new
                {
                    message = "Connection already exists and is configured.",
                    connectionId = existing.Id,
                    webhookKey = existing.WebhookKey,
                    providerCode = existing.ProviderCode,
                    enabled = existing.Enabled
                });
            }

            var settings = JsonSerializer.SerializeToElement(new HomeDispatchSettings
            {
                BaseUrl = "https://httpbin.org",
                ApiKeyHeader = "X-Dispatch-Api-Key",
                WebhookSecret = "whsec_homemaintenance_secret_123",
                TimeoutSeconds = 30
            });

            const string secretRef = "homedispatch:demo:apikey";
            var created = await connStore.CreateAsync(new IntegrationConnection(
                Id: Guid.NewGuid(),
                ProviderCode: "homedispatch",
                Name: "HomeDispatch Demo Connection",
                OwnerType: "System",
                OwnerId: "Default",
                Environment: "sandbox",
                Enabled: true,
                Settings: settings,
                SecretRef: secretRef,
                WebhookKey: "placeholder"), ct);

            var secretPayload = JsonSerializer.Serialize(new
            {
                apiKey = "demo-dispatch-api-key-xyz987",
                apiKeyHeader = "X-Dispatch-Api-Key"
            });
            await secretStore.SetAsync(secretRef, secretPayload, ct);

            return Results.Ok(new
            {
                message = "Demo connection and credentials successfully created!",
                connectionId = created.Id,
                webhookKey = created.WebhookKey,
                providerCode = created.ProviderCode,
                secretRef = secretRef
            });
        })
        .WithSummary("1. Seed demo connection and secret")
        .WithDescription("Creates a 'homedispatch' connection in the database and stores the API secret.");

        // 2. Direct RPC Capability
        group.MapPost("/call-availability-capability", async (
            Guid? connectionId,
            string category = "plumbing",
            string postalCode = "90210",
            IConnectionStore connStore = null!,
            ICapabilityResolver resolver = null!,
            CancellationToken ct = default) =>
        {
            var targetId = await ResolveConnectionIdAsync(connectionId, connStore, ct);
            var capability = await resolver.GetAsync<IDispatchCapability>(targetId, ct);
            var result = await capability.CheckAvailabilityAsync(category, postalCode, ct);

            return result.IsSuccess
                ? Results.Ok(result.Value)
                : Results.BadRequest(result.Error);
        })
        .WithSummary("2. Test RPC Capability (IDispatchCapability)")
        .WithDescription("Direct RPC call to vendor. If connectionId is omitted, automatically uses the seeded demo connection.");

        // 3. Enqueue Outbox Command
        group.MapPost("/enqueue-dispatch-command", async (
            Guid? connectionId,
            string bookingId = "BK-1001",
            string serviceType = "PlumbingRepair",
            string customerAddress = "123 Main Street, New York",
            IConnectionStore connStore = null!,
            IIntegrationCommandQueue queue = null!,
            CancellationToken ct = default) =>
        {
            var targetId = await ResolveConnectionIdAsync(connectionId, connStore, ct);

            var payload = JsonSerializer.SerializeToElement(new
            {
                bookingId = bookingId,
                serviceType = serviceType,
                address = customerAddress,
                requestedAt = DateTimeOffset.UtcNow
            });

            var command = new IntegrationCommand(
                ConnectionId: targetId,
                Operation: "booking.dispatch_technician",
                Payload: payload,
                IdempotencyKey: $"disp-{bookingId}-{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}");

            var commandId = await queue.EnqueueAsync(command, dbTransaction: null, ct);

            return Results.Ok(new
            {
                message = "Command enqueued in outbox table! The CommandDispatcher background worker will process it shortly.",
                commandId = commandId,
                connectionId = targetId,
                operation = command.Operation,
                idempotencyKey = command.IdempotencyKey
            });
        })
        .WithSummary("3. Enqueue Outbox Command (booking.dispatch_technician)")
        .WithDescription("Inserts command into outbox table for reliable background dispatch. If connectionId is omitted, automatically uses the demo connection.");

        // 4. Simulate Inbound Webhook
        group.MapPost("/simulate-vendor-webhook", async (
            Guid? connectionId,
            string eventType = "technician.arrived",
            string technicianName = "Alex Carpenter",
            string notes = "Arrived at site with tools.",
            IConnectionStore connStore = null!,
            WebhookIngestor ingestor = null!,
            CancellationToken ct = default) =>
        {
            var targetId = await ResolveConnectionIdAsync(connectionId, connStore, ct);
            var conn = await connStore.GetByIdAsync(targetId, ct);
            if (conn is null)
                return Results.NotFound(new { error = $"Connection '{targetId}' not found" });

            var payloadObj = new
            {
                @event = string.IsNullOrWhiteSpace(eventType) ? "technician.arrived" : eventType,
                eventId = "EVT-" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant(),
                technician = string.IsNullOrWhiteSpace(technicianName) ? "Alex Carpenter" : technicianName,
                notes = string.IsNullOrWhiteSpace(notes) ? "Technician arrived at site with tools." : notes,
                timestamp = DateTimeOffset.UtcNow
            };

            var body = JsonSerializer.Serialize(payloadObj);
            var bodyBytes = Encoding.UTF8.GetBytes(body);
            var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["X-Signature"] = "whsec_homemaintenance_secret_123",
                ["Content-Type"] = "application/json"
            };

            var result = await ingestor.IngestAsync("homedispatch", conn.WebhookKey, headers, bodyBytes, ct);
            return Results.Ok(new
            {
                message = "Webhook submitted to ingestion pipeline!",
                result = result.ToString(),
                connectionId = targetId,
                simulatedEventId = payloadObj.eventId,
                simulatedEventType = payloadObj.@event
            });
        })
        .WithSummary("4. Simulate Inbound Webhook (technician status update)")
        .WithDescription("Sends signed webhook to ingestor; WebhookProcessor parses it and triggers HomeDispatchEventHandler.");

        // 5. System Status
        group.MapGet("/system-status", async (IntegrationsDbContext db, CancellationToken ct) =>
        {
            var connectionsCount = await db.Connections.CountAsync(ct);
            var commands = await db.Commands
                .GroupBy(c => c.Status)
                .Select(g => new { Status = g.Key, Count = g.Count() })
                .ToListAsync(ct);

            var webhooks = await db.InboundWebhooks
                .GroupBy(w => w.Status)
                .Select(g => new { Status = g.Key, Count = g.Count() })
                .ToListAsync(ct);

            var recentCommands = await db.Commands
                .OrderByDescending(c => c.CreatedAt)
                .Take(5)
                .Select(c => new
                {
                    c.Id,
                    c.Operation,
                    c.Status,
                    c.Attempts,
                    c.CreatedAt,
                    c.CompletedAt,
                    c.LastError
                })
                .ToListAsync(ct);

            var recentWebhooks = await db.InboundWebhooks
                .OrderByDescending(w => w.ReceivedAt)
                .Take(5)
                .Select(w => new
                {
                    w.Id,
                    w.ConnectionId,
                    w.SignatureValid,
                    w.Status,
                    w.ReceivedAt,
                    w.Attempts,
                    w.Error
                })
                .ToListAsync(ct);

            return Results.Ok(new
            {
                totalConnections = connectionsCount,
                commandSummary = commands,
                webhookSummary = webhooks,
                latestCommands = recentCommands,
                latestWebhooks = recentWebhooks
            });
        })
        .WithSummary("5. Inspect database queue status")
        .WithDescription("Displays live counts and recent rows from commands and webhooks tables.");

        return group;
    }

    private static async Task<Guid> ResolveConnectionIdAsync(Guid? connectionId, IConnectionStore connStore, CancellationToken ct)
    {
        if (connectionId.HasValue && connectionId.Value != Guid.Empty)
            return connectionId.Value;

        var connections = await connStore.ListAsync(providerCode: "homedispatch", ct: ct);
        var first = connections.FirstOrDefault();
        if (first is null)
            throw new InvalidOperationException("No 'homedispatch' connection found. Please execute 'POST /api/test/setup-demo-connection' first.");

        return first.Id;
    }
}
