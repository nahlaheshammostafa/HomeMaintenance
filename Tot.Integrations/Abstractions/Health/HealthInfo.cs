namespace Tot.Integrations;

/// <summary>Result returned by a provider's <c>PingAsync</c> check.</summary>
/// <param name="Latency">Round-trip time of the ping call.</param>
/// <param name="Version">Optional version string reported by the provider API.</param>
/// <param name="Details">Provider-specific diagnostic details.</param>
public sealed record HealthInfo(
    TimeSpan Latency,
    string? Version = null,
    string? Details = null);
