namespace Tot.Integrations;

/// <summary>
/// Implemented by a provider to receive and process inbound webhook deliveries.
/// Register via <c>IIntegrationsBuilder.AddWebhookHandler&lt;T&gt;()</c>.
/// </summary>
public interface IWebhookHandler
{
    /// <summary>
    /// The provider code this handler serves (e.g. <c>"stripe"</c>).
    /// Must match the <see cref="ProviderDescriptor.Code"/> of the registered provider.
    /// Used by the endpoint router to select the correct handler.
    /// </summary>
    string ProviderCode { get; }

    /// <summary>
    /// Verifies the webhook's authenticity (e.g. HMAC signature, shared token).
    /// Called synchronously in the inbound request — must complete in &lt; 50 ms.
    /// The raw <paramref name="hook"/> is persisted regardless of the outcome.
    /// </summary>
    Task<WebhookVerification> VerifyAsync(InboundWebhook hook, IConnectionContext ctx, CancellationToken ct);

    /// <summary>
    /// Parses the verified raw payload into zero or more <see cref="IntegrationEvent"/>s.
    /// Called asynchronously by the <c>WebhookProcessor</c> background service.
    /// </summary>
    Task<IReadOnlyList<IntegrationEvent>> ParseAsync(InboundWebhook hook, IConnectionContext ctx, CancellationToken ct);
}
