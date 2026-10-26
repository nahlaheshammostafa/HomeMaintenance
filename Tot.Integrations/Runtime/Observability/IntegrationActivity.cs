using System.Diagnostics;

namespace Tot.Integrations.Runtime.Observability;

/// <summary>
/// Singleton <see cref="ActivitySource"/> for all distributed-tracing spans emitted by
/// <c>Tot.Integrations</c>. Consumers enable it by name in their OpenTelemetry configuration:
/// <c>tracerProvider.AddSource(IntegrationActivity.SourceName)</c>.
/// </summary>
internal static class IntegrationActivity
{
    /// <summary>The source name passed to <c>AddSource</c> in OpenTelemetry configuration.</summary>
    public const string SourceName = "Tot.Integrations";

    internal static readonly ActivitySource Source = new(SourceName, "1.0.0");

    /// <summary>
    /// Starts a client span for one outbound HTTP call, propagating the current trace context.
    /// Returns <see langword="null"/> when no listener is attached (zero overhead in production
    /// unless OpenTelemetry is configured).
    /// </summary>
    internal static Activity? StartCall(
        string providerCode,
        Guid connectionId,
        string method,
        string urlPath)
    {
        var activity = Source.StartActivity(
            $"integration.http {method}",
            ActivityKind.Client);

        if (activity is null) return null;

        activity.SetTag("integration.provider", providerCode);
        activity.SetTag("integration.connection_id", connectionId.ToString());
        activity.SetTag("http.request.method", method);
        activity.SetTag("url.path", urlPath);
        return activity;
    }
}
