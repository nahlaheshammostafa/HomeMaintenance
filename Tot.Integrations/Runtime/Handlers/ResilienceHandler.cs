using Polly;
using Polly.CircuitBreaker;
using Polly.Timeout;
using Tot.Integrations.Runtime.Resilience;

namespace Tot.Integrations.Runtime.Handlers;

/// <summary>
/// Applies a Polly resilience pipeline (retry + circuit breaker + timeout).
/// Idempotent methods (GET, HEAD, PUT, DELETE) get retry; POST/PATCH do not.
/// Circuit-open and timeout exceptions are surfaced as synthetic HTTP responses
/// so callers always receive an <see cref="IntegrationResult{T}"/> with a known <see cref="ErrorKind"/>.
/// </summary>
internal sealed class ResilienceHandler : DelegatingHandler
{
    private static readonly HashSet<HttpMethod> IdempotentMethods =
        [HttpMethod.Get, HttpMethod.Head, HttpMethod.Put, HttpMethod.Delete];

    private readonly IntegrationConnection _connection;
    private readonly ConnectionResilienceProvider _resilience;

    internal ResilienceHandler(IntegrationConnection connection, ConnectionResilienceProvider resilience)
    {
        _connection = connection;
        _resilience = resilience;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var isIdempotent = IdempotentMethods.Contains(request.Method);
        var pipeline = _resilience.Get(_connection, isIdempotent);

        try
        {
            return await pipeline.ExecuteAsync(
                async token => await base.SendAsync(request, token),
                ct);
        }
        catch (BrokenCircuitException)
        {
            return CircuitOpenResponse();
        }
        catch (TimeoutRejectedException)
        {
            return TimeoutResponse();
        }
    }

    private static HttpResponseMessage CircuitOpenResponse() =>
        new(System.Net.HttpStatusCode.ServiceUnavailable)
        {
            ReasonPhrase = "circuit-open"
        };

    private static HttpResponseMessage TimeoutResponse() =>
        new(System.Net.HttpStatusCode.RequestTimeout)
        {
            ReasonPhrase = "timeout"
        };
}
