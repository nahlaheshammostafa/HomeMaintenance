using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Tot.Integrations.Runtime.Webhooks;

namespace Tot.Integrations.Endpoints;

/// <summary>
/// Extension methods for mapping inbound webhook endpoints.
/// </summary>
public static class IntegrationWebhookEndpoints
{
    private const int MaxBodyBytes = 1_048_576; // 1 MB

    /// <summary>
    /// Maps the integration webhook ingestion endpoint at
    /// <c>POST {prefix}/{providerCode}/{webhookKey}</c>.
    /// The endpoint persists the raw request, verifies the signature, deduplicates on
    /// provider event id, and returns 200 fast.
    /// <para>
    /// Prerequisites: call <c>AddIntegrations(…).UsePostgres(…)</c> before mapping.
    /// The host must also call <c>app.UseRateLimiter()</c> if rate limiting is configured.
    /// </para>
    /// </summary>
    /// <param name="endpoints">The route builder.</param>
    /// <param name="prefix">URL prefix for all webhook routes (default: <c>/integrations/webhooks</c>).</param>
    public static IEndpointRouteBuilder MapIntegrationWebhooks(
        this IEndpointRouteBuilder endpoints,
        string prefix = "/integrations/webhooks")
    {
        prefix = prefix.TrimEnd('/');

        endpoints.MapPost($"{prefix}/{{providerCode}}/{{webhookKey}}",
            async (string providerCode, string webhookKey,
                   HttpRequest request,
                   WebhookIngestor ingestor,
                   CancellationToken ct) =>
            {
                if (request.ContentLength > MaxBodyBytes)
                    return Results.StatusCode(413);

                var body = await ReadBodyAsync(request.Body, MaxBodyBytes, ct);
                if (body is null)
                    return Results.StatusCode(413);

                var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var header in request.Headers)
                    headers[header.Key] = header.Value.ToString();

                return await ingestor.IngestAsync(providerCode, webhookKey, headers, body, ct);
            });

        return endpoints;
    }

    private static async Task<byte[]?> ReadBodyAsync(Stream body, int limit, CancellationToken ct)
    {
        using var ms = new MemoryStream();
        var buffer = new byte[65536];
        int total = 0;
        int read;
        while ((read = await body.ReadAsync(buffer, ct)) > 0)
        {
            total += read;
            if (total > limit) return null;
            ms.Write(buffer, 0, read);
        }
        return ms.ToArray();
    }
}
