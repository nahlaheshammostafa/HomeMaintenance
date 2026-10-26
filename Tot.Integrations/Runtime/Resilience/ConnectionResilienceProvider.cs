using System.Collections.Concurrent;
using System.Net;
using Microsoft.Extensions.Http.Resilience;
using Polly;
using Tot.Integrations.DependencyInjection;
using Polly.CircuitBreaker;
using Polly.Retry;
using Polly.Timeout;

namespace Tot.Integrations.Runtime.Resilience;

/// <summary>
/// Singleton: lazily creates and caches one Polly resilience pipeline per connection.
/// Two pipelines are stored per connection:
/// <list type="bullet">
///   <item><description><b>Idempotent</b>: retry 3× + circuit breaker + per-attempt timeout.</description></item>
///   <item><description><b>Non-idempotent</b>: circuit breaker + per-attempt timeout only (no retry).</description></item>
/// </list>
/// </summary>
internal sealed class ConnectionResilienceProvider
{
    private sealed record PipelinePair(
        ResiliencePipeline<HttpResponseMessage> Idempotent,
        ResiliencePipeline<HttpResponseMessage> NonIdempotent);

    private readonly ConcurrentDictionary<Guid, PipelinePair> _pipelines = new();
    private readonly IntegrationsOptions _options;

    public ConnectionResilienceProvider(IntegrationsOptions options) => _options = options;

    internal ResiliencePipeline<HttpResponseMessage> Get(IntegrationConnection connection, bool isIdempotent)
    {
        var pair = _pipelines.GetOrAdd(connection.Id, _ => Build(connection));
        return isIdempotent ? pair.Idempotent : pair.NonIdempotent;
    }

    private PipelinePair Build(IntegrationConnection connection)
    {
        var attemptTimeout = _options.DefaultAttemptTimeout;
        var totalTimeout = _options.DefaultTotalTimeout;

        var breakDuration = TimeSpan.FromSeconds(30);

        // Override timeouts and circuit-breaker settings from connection settings if present
        if (connection.Settings.ValueKind == System.Text.Json.JsonValueKind.Object)
        {
            if (connection.Settings.TryGetProperty("timeoutSeconds", out var t) && t.TryGetDouble(out var sec))
                attemptTimeout = TimeSpan.FromSeconds(sec);
            if (connection.Settings.TryGetProperty("totalTimeoutSeconds", out var tt) && tt.TryGetDouble(out var tsec))
                totalTimeout = TimeSpan.FromSeconds(tsec);
            if (connection.Settings.TryGetProperty("breakDurationSeconds", out var bd) && bd.TryGetDouble(out var bdsec))
                breakDuration = TimeSpan.FromSeconds(bdsec);
        }

        var cbOptions = new HttpCircuitBreakerStrategyOptions
        {
            // Open after 50% failures in a 30-second window (min 10 calls)
            FailureRatio = 0.5,
            SamplingDuration = TimeSpan.FromSeconds(30),
            MinimumThroughput = 10,
            BreakDuration = breakDuration,
            ShouldHandle = new PredicateBuilder<HttpResponseMessage>()
                .Handle<HttpRequestException>()
                .Handle<TimeoutRejectedException>()
                .HandleResult(r => (int)r.StatusCode >= 500 || r.StatusCode == HttpStatusCode.RequestTimeout),
            OnOpened = _ =>
            {
                // Circuit opened — will surface as ErrorKind.CircuitOpen via ResilienceHandler
                return ValueTask.CompletedTask;
            }
        };

        var retryOptions = new HttpRetryStrategyOptions
        {
            MaxRetryAttempts = 3,
            BackoffType = DelayBackoffType.Exponential,
            UseJitter = true,
            Delay = TimeSpan.FromSeconds(1),
            ShouldHandle = new PredicateBuilder<HttpResponseMessage>()
                .Handle<HttpRequestException>()
                .Handle<TimeoutRejectedException>()
                .HandleResult(r =>
                    r.StatusCode is HttpStatusCode.RequestTimeout
                        or HttpStatusCode.TooManyRequests
                        or HttpStatusCode.ServiceUnavailable
                        || (int)r.StatusCode >= 500),
            // Respect Retry-After header
            DelayGenerator = args =>
            {
                if (args.Outcome.Result?.Headers.RetryAfter?.Delta is { } delta)
                    return new ValueTask<TimeSpan?>(delta);
                return new ValueTask<TimeSpan?>((TimeSpan?)null); // use default backoff
            }
        };

        var attemptTimeoutOptions = new TimeoutStrategyOptions
        {
            Timeout = attemptTimeout
        };

        var idempotent = new ResiliencePipelineBuilder<HttpResponseMessage>()
            .AddTimeout(new TimeoutStrategyOptions { Timeout = totalTimeout })
            .AddRetry(retryOptions)
            .AddCircuitBreaker(cbOptions)
            .AddTimeout(attemptTimeoutOptions)
            .Build();

        var nonIdempotent = new ResiliencePipelineBuilder<HttpResponseMessage>()
            .AddTimeout(new TimeoutStrategyOptions { Timeout = totalTimeout })
            .AddCircuitBreaker(cbOptions)
            .AddTimeout(attemptTimeoutOptions)
            .Build();

        return new PipelinePair(idempotent, nonIdempotent);
    }
}
