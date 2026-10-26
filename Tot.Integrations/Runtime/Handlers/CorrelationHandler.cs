using System.Diagnostics;

namespace Tot.Integrations.Runtime.Handlers;

/// <summary>
/// Adds W3C <c>traceparent</c> (from <see cref="Activity.Current"/>) and
/// <c>X-Request-Id</c> to every outbound request. These appear in the journal
/// and in any distributed tracing system (Jaeger, Zipkin, Application Insights).
/// </summary>
internal sealed class CorrelationHandler : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        // W3C trace context — only add if an Activity is already running
        var activity = Activity.Current;
        if (activity is not null && !request.Headers.Contains("traceparent"))
            request.Headers.TryAddWithoutValidation("traceparent", activity.Id);

        // Unique request id for log correlation
        if (!request.Headers.Contains("X-Request-Id"))
            request.Headers.TryAddWithoutValidation("X-Request-Id", Guid.NewGuid().ToString("N"));

        return base.SendAsync(request, ct);
    }
}
