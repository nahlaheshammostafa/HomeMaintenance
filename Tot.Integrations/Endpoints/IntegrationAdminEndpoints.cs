using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Tot.Integrations.Runtime.Admin;

namespace Tot.Integrations.Endpoints;

/// <summary>
/// Extension methods for mapping the integration admin API.
/// The host is responsible for adding authorization before calling this method.
/// </summary>
public static class IntegrationAdminEndpoints
{
    /// <summary>
    /// Maps all integration admin endpoints under <paramref name="prefix"/>.
    /// The host should add <c>.RequireAuthorization(…)</c> to the returned group,
    /// or configure authorization globally on the route builder before calling this method.
    /// </summary>
    /// <param name="endpoints">The route builder.</param>
    /// <param name="prefix">URL prefix (default: <c>/integrations/admin</c>).</param>
    public static RouteGroupBuilder MapIntegrationAdminApi(
        this IEndpointRouteBuilder endpoints,
        string prefix = "/integrations/admin")
    {
        prefix = prefix.TrimEnd('/');
        var group = endpoints.MapGroup(prefix);

        // ── Connections ──────────────────────────────────────────────────────

        group.MapGet("/connections",
            async (AdminApiHandler h,
                   string? providerCode, string? ownerType, string? ownerId,
                   CancellationToken ct) =>
                await h.ListConnectionsAsync(providerCode, ownerType, ownerId, ct));

        group.MapGet("/connections/{id:guid}",
            async (Guid id, AdminApiHandler h, CancellationToken ct) =>
                await h.GetConnectionAsync(id, ct));

        group.MapPost("/connections",
            async (CreateConnectionRequest req, AdminApiHandler h, CancellationToken ct) =>
                await h.CreateConnectionAsync(req, ct));

        group.MapPut("/connections/{id:guid}",
            async (Guid id, UpdateConnectionRequest req, AdminApiHandler h, CancellationToken ct) =>
                await h.UpdateConnectionAsync(id, req, ct));

        group.MapDelete("/connections/{id:guid}",
            async (Guid id, AdminApiHandler h, CancellationToken ct) =>
                await h.DeleteConnectionAsync(id, ct));

        group.MapPost("/connections/{id:guid}/enable",
            async (Guid id, AdminApiHandler h, CancellationToken ct) =>
                await h.SetEnabledAsync(id, enabled: true, ct));

        group.MapPost("/connections/{id:guid}/disable",
            async (Guid id, AdminApiHandler h, CancellationToken ct) =>
                await h.SetEnabledAsync(id, enabled: false, ct));

        group.MapPut("/connections/{id:guid}/secret",
            async (Guid id, SetSecretRequest req, AdminApiHandler h, CancellationToken ct) =>
                await h.SetSecretAsync(id, req, ct));

        group.MapPost("/connections/{id:guid}/test",
            async (Guid id, AdminApiHandler h, CancellationToken ct) =>
                await h.TestConnectionAsync(id, ct));

        // ── External IDs ─────────────────────────────────────────────────────

        group.MapGet("/connections/{id:guid}/external-ids",
            async (Guid id, AdminApiHandler h, CancellationToken ct) =>
                await h.ListExternalIdsAsync(id, ct));

        group.MapPut("/connections/{id:guid}/external-ids",
            async (Guid id, UpsertExternalIdRequest req, AdminApiHandler h, CancellationToken ct) =>
                await h.UpsertExternalIdAsync(id, req, ct));

        group.MapDelete("/connections/{id:guid}/external-ids/{internalType}/{internalId}/{externalType}",
            async (Guid id, string internalType, string internalId, string externalType,
                   AdminApiHandler h, CancellationToken ct) =>
                await h.DeleteExternalIdAsync(id, internalType, internalId, externalType, ct));

        // ── Commands ─────────────────────────────────────────────────────────

        group.MapGet("/commands",
            async (Guid? connectionId, string? status, AdminApiHandler h, CancellationToken ct) =>
                await h.ListCommandsAsync(connectionId, status, ct));

        group.MapPost("/commands/{id:guid}/retry",
            async (Guid id, AdminApiHandler h, CancellationToken ct) =>
                await h.RetryCommandAsync(id, ct));

        // ── Webhooks ─────────────────────────────────────────────────────────

        group.MapGet("/webhooks",
            async (Guid? connectionId, string? status, AdminApiHandler h, CancellationToken ct) =>
                await h.ListWebhooksAsync(connectionId, status, ct));

        group.MapPost("/webhooks/{id:long}/replay",
            async (long id, AdminApiHandler h, CancellationToken ct) =>
                await h.ReplayWebhookAsync(id, ct));

        // ── Providers ─────────────────────────────────────────────────────────

        group.MapGet("/providers",
            async (AdminApiHandler h, CancellationToken ct) =>
                await h.ListProvidersAsync(ct));

        return group;
    }
}
