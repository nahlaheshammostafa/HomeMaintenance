using Microsoft.Extensions.Logging;
using Tot.Integrations.Runtime.Handlers;
using Tot.Integrations.Runtime.Observability;
using Tot.Integrations.Runtime.Redaction;
using Tot.Integrations.Runtime.Resilience;

namespace Tot.Integrations.Runtime.Transport;

/// <summary>
/// Creates per-connection <see cref="HttpClient"/> instances with the full handler pipeline:
/// Correlation → Auth → RateLimit → Resilience → Journal.
/// Uses <see cref="IHttpMessageHandlerFactory"/> for the primary (pooled) inner handler so that
/// DNS changes are picked up without leaking sockets.
/// </summary>
internal sealed class IntegrationHttpClientFactory
{
    private readonly IHttpMessageHandlerFactory _handlerFactory;
    private readonly ConnectionRateLimiterRegistry _rateLimiters;
    private readonly ConnectionResilienceProvider _resilience;
    private readonly JournalChannel _journalChannel;
    private readonly Redactor _redactor;
    private readonly IntegrationMetrics _metrics;
    private readonly ILoggerFactory _loggerFactory;

    /// <summary>Initialises the factory with all shared infrastructure dependencies.</summary>
    public IntegrationHttpClientFactory(
        IHttpMessageHandlerFactory handlerFactory,
        ConnectionRateLimiterRegistry rateLimiters,
        ConnectionResilienceProvider resilience,
        JournalChannel journalChannel,
        Redactor redactor,
        IntegrationMetrics metrics,
        ILoggerFactory loggerFactory)
    {
        _handlerFactory = handlerFactory;
        _rateLimiters = rateLimiters;
        _resilience = resilience;
        _journalChannel = journalChannel;
        _redactor = redactor;
        _metrics = metrics;
        _loggerFactory = loggerFactory;
    }

    /// <summary>
    /// Creates an <see cref="HttpClient"/> for <paramref name="connection"/> with the full pipeline.
    /// The base address is set from <paramref name="baseUrl"/>.
    /// </summary>
    internal HttpClient CreateClient(
        IntegrationConnection connection,
        IAuthenticationProvider? authProvider,
        string? baseUrl)
    {
        var primary = _handlerFactory.CreateHandler("integrations");

        // Build from innermost to outermost
        var journal = new JournalHandler(connection, _redactor, _journalChannel, _metrics, _loggerFactory)
        {
            InnerHandler = primary
        };
        var resilience = new ResilienceHandler(connection, _resilience)
        {
            InnerHandler = journal
        };
        var rateLimit = new RateLimitHandler(connection, _rateLimiters)
        {
            InnerHandler = resilience
        };
        var auth = new AuthHandler(authProvider)
        {
            InnerHandler = rateLimit
        };
        var correlation = new CorrelationHandler
        {
            InnerHandler = auth
        };

        var client = new HttpClient(correlation, disposeHandler: true);
        if (!string.IsNullOrWhiteSpace(baseUrl))
            client.BaseAddress = new Uri(baseUrl.TrimEnd('/') + '/');

        return client;
    }
}
