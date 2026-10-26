using System.Diagnostics;
using System.Text;
using Microsoft.Extensions.Logging;
using Tot.Integrations.Runtime.Observability;
using Tot.Integrations.Runtime.Redaction;

namespace Tot.Integrations.Runtime.Handlers;

/// <summary>
/// Captures method, URL path (never query string), status, duration, outcome,
/// redacted bodies and trace id for every outbound call, then writes to
/// <see cref="JournalChannel"/> without blocking the request.
/// Also records <see cref="IntegrationMetrics"/> instruments and starts an
/// <see cref="IntegrationActivity"/> span for distributed tracing.
/// </summary>
internal sealed class JournalHandler : DelegatingHandler
{
    private readonly IntegrationConnection _connection;
    private readonly Redactor _redactor;
    private readonly JournalChannel _channel;
    private readonly IntegrationMetrics _metrics;
    private readonly ILogger _logger;

    internal JournalHandler(
        IntegrationConnection connection,
        Redactor redactor,
        JournalChannel channel,
        IntegrationMetrics metrics,
        ILoggerFactory loggerFactory)
    {
        _connection = connection;
        _redactor = redactor;
        _channel = channel;
        _metrics = metrics;
        _logger = loggerFactory.CreateLogger("Tot.Integrations.Call");
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var method = request.Method.Method;
        var path = request.RequestUri?.AbsolutePath ?? "/";

        // Log scope: all log messages within this call carry these properties.
        using var scope = _logger.BeginScope(new Dictionary<string, object?>
        {
            ["ProviderCode"] = _connection.ProviderCode,
            ["ConnectionId"] = _connection.Id,
            ["UrlPath"] = path,
        });

        // Distributed-tracing span — no-op if no ActivityListener is attached.
        using var activity = IntegrationActivity.StartCall(
            _connection.ProviderCode, _connection.Id, method, path);

        var sw = Stopwatch.StartNew();
        string? requestBody = null;

        // Capture request body before sending (provider may have set it).
        if (request.Content is not null)
        {
            var raw = await request.Content.ReadAsStringAsync(ct);
            requestBody = _redactor.RedactBody(raw);
            // Re-create the content so the inner handler can still read it.
            request.Content = new StringContent(raw,
                Encoding.UTF8,
                request.Content.Headers.ContentType?.MediaType ?? "application/json");
        }

        HttpResponseMessage response;
        string outcome;
        string? errorKind = null;

        try
        {
            response = await base.SendAsync(request, ct);
            outcome = response.IsSuccessStatusCode ? "success" : "error";
            if (response.ReasonPhrase == "circuit-open") { outcome = "circuit_open"; errorKind = nameof(ErrorKind.CircuitOpen); }
            else if (response.ReasonPhrase == "timeout") { outcome = "timeout"; errorKind = nameof(ErrorKind.Timeout); }
        }
        catch (Exception ex)
        {
            sw.Stop();
            outcome = "error";
            errorKind = ex is TaskCanceledException ? nameof(ErrorKind.Timeout) : nameof(ErrorKind.Transient);
            RecordMetrics(method, outcome, sw.Elapsed.TotalMilliseconds);
            SetActivityOutcome(activity, outcome, ex);
            WriteRecord(request, null, null, sw, outcome, errorKind, requestBody);
            throw;
        }

        sw.Stop();
        string? responseBody = null;
        if (response.Content is not null && response.Content.Headers.ContentType?.MediaType?.Contains("json") == true)
        {
            var raw = await response.Content.ReadAsStringAsync(ct);
            responseBody = _redactor.RedactBody(raw);
            response.Content = new StringContent(raw,
                Encoding.UTF8,
                response.Content.Headers.ContentType?.MediaType ?? "application/json");
        }

        RecordMetrics(method, outcome, sw.Elapsed.TotalMilliseconds);
        SetActivityOutcome(activity, outcome, null);

        activity?.SetTag("http.response.status_code", (int)response.StatusCode);

        _logger.LogDebug("Integration call completed: {Method} {Path} → {Status} ({Outcome}) in {DurationMs}ms",
            method, path, (int)response.StatusCode, outcome, sw.ElapsedMilliseconds);

        WriteRecord(request, response, responseBody, sw, outcome, errorKind, requestBody);
        return response;
    }

    private void RecordMetrics(string method, string outcome, double durationMs)
    {
        var tags = new TagList
        {
            { "provider", _connection.ProviderCode },
            { "method", method },
            { "outcome", outcome },
        };

        _metrics.RequestsTotal.Add(1, tags);
        _metrics.RequestDurationMs.Record(durationMs, tags);
    }

    private static void SetActivityOutcome(Activity? activity, string outcome, Exception? ex)
    {
        if (activity is null) return;
        if (outcome == "success")
        {
            activity.SetStatus(ActivityStatusCode.Ok);
        }
        else
        {
            activity.SetStatus(ActivityStatusCode.Error, outcome);
            if (ex is not null)
                activity.SetTag("error.type", ex.GetType().Name);
        }
    }

    private void WriteRecord(
        HttpRequestMessage request,
        HttpResponseMessage? response,
        string? responseBody,
        Stopwatch sw,
        string outcome,
        string? errorKind,
        string? requestBody)
    {
        var path = request.RequestUri?.AbsolutePath ?? "/";
        var traceId = Activity.Current?.TraceId.ToString();

        _channel.TryWrite(new CallRecord(
            Ts: DateTimeOffset.UtcNow,
            ConnectionId: _connection.Id,
            ProviderCode: _connection.ProviderCode,
            Operation: null,  // filled by CommandDispatcher for command calls
            Method: request.Method.Method,
            UrlPath: path,
            StatusCode: (int?)response?.StatusCode,
            DurationMs: (int)sw.ElapsedMilliseconds,
            Outcome: outcome,
            ErrorKind: errorKind,
            RequestBody: requestBody,
            ResponseBody: responseBody,
            TraceId: traceId,
            CommandId: null));
    }
}
