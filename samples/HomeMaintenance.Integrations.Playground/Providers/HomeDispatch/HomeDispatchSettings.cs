namespace HomeMaintenance.Integrations.Playground.Providers.HomeDispatch;

/// <summary>
/// Settings contract for the Home Maintenance contractor dispatch vendor.
/// </summary>
public sealed record HomeDispatchSettings
{
    /// <summary>Base URL of the external vendor API.</summary>
    public string BaseUrl { get; init; } = "https://httpbin.org";

    /// <summary>HTTP header name used for sending the vendor API key.</summary>
    public string ApiKeyHeader { get; init; } = "X-Dispatch-Api-Key";

    /// <summary>Shared secret for verifying incoming webhook signatures.</summary>
    public string WebhookSecret { get; init; } = "whsec_homemaintenance_secret_123";

    /// <summary>Timeout in seconds for outbound calls.</summary>
    public int TimeoutSeconds { get; init; } = 30;
}
