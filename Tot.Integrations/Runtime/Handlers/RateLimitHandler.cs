using System.Threading.RateLimiting;
using Tot.Integrations.Runtime.Transport;

namespace Tot.Integrations.Runtime.Handlers;

/// <summary>
/// Applies a per-connection token-bucket rate limit.
/// When the bucket is exhausted, waits up to <see cref="MaxWait"/> then returns
/// <see cref="ErrorKind.RateLimited"/> as an HTTP 429 synthetic response (never blocks indefinitely).
/// </summary>
internal sealed class RateLimitHandler : DelegatingHandler
{
    private static readonly TimeSpan MaxWait = TimeSpan.FromSeconds(5);

    private readonly ConnectionRateLimiterRegistry _registry;
    private readonly IntegrationConnection _connection;

    internal RateLimitHandler(IntegrationConnection connection, ConnectionRateLimiterRegistry registry)
    {
        _connection = connection;
        _registry = registry;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var limiter = _registry.GetOrCreate(_connection);

        using var maxWaitCts = new CancellationTokenSource(MaxWait);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, maxWaitCts.Token);
        using var lease = await limiter.AcquireAsync(permitCount: 1, cancellationToken: linkedCts.Token);

        if (!lease.IsAcquired)
            return RateLimitedResponse();

        return await base.SendAsync(request, ct);
    }

    private static HttpResponseMessage RateLimitedResponse() =>
        new(System.Net.HttpStatusCode.TooManyRequests)
        {
            ReasonPhrase = "Rate limit exceeded (client-side token bucket)"
        };
}

/// <summary>
/// Singleton registry of per-connection <see cref="TokenBucketRateLimiter"/>s.
/// Lazily creates a limiter when a connection first makes a request.
/// </summary>
internal sealed class ConnectionRateLimiterRegistry
{
    private readonly System.Collections.Concurrent.ConcurrentDictionary<Guid, TokenBucketRateLimiter> _limiters = new();

    // Default: 10 requests per second per connection (overridable via settings)
    private const int DefaultPerSecond = 10;
    private const int DefaultBurst = 20;

    internal TokenBucketRateLimiter GetOrCreate(IntegrationConnection connection)
    {
        return _limiters.GetOrAdd(connection.Id, _ =>
        {
            // Parse "rateLimitPerSecond" from connection settings if present
            var rps = DefaultPerSecond;
            if (connection.Settings.ValueKind == System.Text.Json.JsonValueKind.Object &&
                connection.Settings.TryGetProperty("rateLimitPerSecond", out var rpsEl) &&
                rpsEl.TryGetInt32(out var parsed))
                rps = parsed;

            return new TokenBucketRateLimiter(new TokenBucketRateLimiterOptions
            {
                TokenLimit = Math.Max(rps * 2, DefaultBurst),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0, // No queuing — reject immediately when bucket empty
                ReplenishmentPeriod = TimeSpan.FromSeconds(1),
                TokensPerPeriod = rps,
                AutoReplenishment = true
            });
        });
    }

    internal void Remove(Guid connectionId)
    {
        if (_limiters.TryRemove(connectionId, out var limiter))
            limiter.Dispose();
    }
}
