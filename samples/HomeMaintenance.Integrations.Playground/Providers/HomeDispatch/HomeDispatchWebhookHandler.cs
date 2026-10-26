using System.Text.Json;
using Tot.Integrations;

namespace HomeMaintenance.Integrations.Playground.Providers.HomeDispatch;

/// <summary>
/// Webhook handler for inbound status updates from external contractors.
/// Provider: "homedispatch"
/// </summary>
public sealed class HomeDispatchWebhookHandler : IWebhookHandler
{
    public string ProviderCode => "homedispatch";

    public Task<WebhookVerification> VerifyAsync(
        InboundWebhook hook, IConnectionContext ctx, CancellationToken ct)
    {
        var settings = ctx.Settings<HomeDispatchSettings>();

        // Check for signature or secret token in request headers
        if (!hook.Headers.TryGetValue("X-Signature", out var signature) &&
            !hook.Headers.TryGetValue("X-Webhook-Secret", out signature))
        {
            return Task.FromResult(WebhookVerification.Invalid("Missing 'X-Signature' or 'X-Webhook-Secret' header"));
        }

        // Accept configured secret, or default test secret for easy playground verification
        if (signature != settings.WebhookSecret && signature != "test-secret" && signature != "demo-secret")
        {
            return Task.FromResult(WebhookVerification.Invalid($"Signature '{signature}' does not match expected secret"));
        }

        return Task.FromResult(WebhookVerification.Valid());
    }

    public Task<IReadOnlyList<IntegrationEvent>> ParseAsync(
        InboundWebhook hook, IConnectionContext ctx, CancellationToken ct)
    {
        try
        {
            using var doc = JsonDocument.Parse(hook.Body);
            var root = doc.RootElement;

            var eventType = root.TryGetProperty("event", out var evProp)
                ? evProp.GetString()!
                : "technician.status_update";

            var eventId = root.TryGetProperty("eventId", out var idProp)
                ? idProp.GetString()
                : Guid.NewGuid().ToString("N");

            var integrationEvent = new IntegrationEvent(
                Type: eventType,
                ProviderCode: ProviderCode,
                ConnectionId: hook.ConnectionId,
                ProviderEventId: eventId,
                OccurredAt: DateTimeOffset.UtcNow,
                Data: root.Clone());

            return Task.FromResult<IReadOnlyList<IntegrationEvent>>([integrationEvent]);
        }
        catch (JsonException)
        {
            // Invalid JSON produces empty event list
            return Task.FromResult<IReadOnlyList<IntegrationEvent>>([]);
        }
    }
}
