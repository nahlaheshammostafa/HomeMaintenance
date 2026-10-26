using System.Text;
using System.Text.Json;

namespace Tot.Integrations.Runtime.Transport;

/// <summary>
/// Wraps <see cref="HttpClient"/> for provider typed-clients.
/// Provides JSON, form-encoded and raw helpers with URL building and unified error mapping.
/// </summary>
internal sealed class IntegrationHttpTransport
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _client;

    internal IntegrationHttpTransport(HttpClient client) => _client = client;

    // ── Request builders ──────────────────────────────────────────────────────

    internal Task<IntegrationResult<T>> GetAsync<T>(
        string path,
        IReadOnlyDictionary<string, string>? query = null,
        CancellationToken ct = default)
        => SendAsync<T>(BuildRequest(HttpMethod.Get, path, query), ct);

    internal Task<IntegrationResult<T>> PostJsonAsync<T>(
        string path,
        object body,
        IReadOnlyDictionary<string, string>? query = null,
        CancellationToken ct = default)
        => SendAsync<T>(BuildJsonRequest(HttpMethod.Post, path, body, query), ct);

    internal Task<IntegrationResult<T>> PutJsonAsync<T>(
        string path,
        object body,
        IReadOnlyDictionary<string, string>? query = null,
        CancellationToken ct = default)
        => SendAsync<T>(BuildJsonRequest(HttpMethod.Put, path, body, query), ct);

    internal Task<IntegrationResult<T>> PatchJsonAsync<T>(
        string path,
        object body,
        IReadOnlyDictionary<string, string>? query = null,
        CancellationToken ct = default)
        => SendAsync<T>(BuildJsonRequest(HttpMethod.Patch, path, body, query), ct);

    internal Task<IntegrationResult<T>> DeleteAsync<T>(
        string path,
        IReadOnlyDictionary<string, string>? query = null,
        CancellationToken ct = default)
        => SendAsync<T>(BuildRequest(HttpMethod.Delete, path, query), ct);

    internal Task<IntegrationResult<T>> PostFormAsync<T>(
        string path,
        IReadOnlyDictionary<string, string> form,
        CancellationToken ct = default)
    {
        var req = BuildRequest(HttpMethod.Post, path, null);
        req.Content = new FormUrlEncodedContent(form);
        return SendAsync<T>(req, ct);
    }

    // ── URL building ──────────────────────────────────────────────────────────

    /// <summary>
    /// Formats a path template, replacing <c>{key}</c> tokens with URL-encoded values.
    /// E.g. <c>FormatPath("/devices/{id}/open", new{"id","8841"})</c>.
    /// </summary>
    internal static string FormatPath(string template, IReadOnlyDictionary<string, string>? routeValues)
    {
        if (routeValues is null) return template;
        foreach (var (key, value) in routeValues)
            template = template.Replace($"{{{key}}}", Uri.EscapeDataString(value), StringComparison.OrdinalIgnoreCase);
        return template;
    }

    // ── Core send + error mapping ─────────────────────────────────────────────

    internal async Task<IntegrationResult<T>> SendAsync<T>(HttpRequestMessage request, CancellationToken ct)
    {
        HttpResponseMessage response;
        try
        {
            response = await _client.SendAsync(request, ct);
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            return IntegrationResult<T>.Fail(new IntegrationError(
                Kind: ErrorKind.Timeout,
                Code: "TIMEOUT",
                Message: "The request timed out.",
                Retryable: true));
        }
        catch (HttpRequestException ex)
        {
            return IntegrationResult<T>.Fail(new IntegrationError(
                Kind: ErrorKind.Transient,
                Code: "NETWORK_ERROR",
                Message: ex.Message,
                Retryable: true));
        }

        return await MapResponseAsync<T>(response, ct);
    }

    private async Task<IntegrationResult<T>> MapResponseAsync<T>(HttpResponseMessage response, CancellationToken ct)
    {
        var status = (int)response.StatusCode;

        if (response.IsSuccessStatusCode)
        {
            // 204 No Content: definitively no body.
            if (response.StatusCode == System.Net.HttpStatusCode.NoContent)
                return IntegrationResult<T>.Ok(default!);

            T? value;
            try
            {
                await using var stream = await response.Content.ReadAsStreamAsync(ct);
                value = await JsonSerializer.DeserializeAsync<T>(stream, JsonOptions, ct);
            }
            catch (JsonException ex)
            {
                return IntegrationResult<T>.Fail(new IntegrationError(
                    Kind: ErrorKind.Unknown,
                    Code: "INVALID_JSON",
                    Message: $"Failed to deserialize response: {ex.Message}",
                    Retryable: false,
                    HttpStatus: status));
            }

            return IntegrationResult<T>.Ok(value!);
        }

        // Error response
        string errorBody = await TryReadBodyAsync(response, ct);
        return IntegrationResult<T>.Fail(MapHttpError(status, errorBody));
    }

    internal static IntegrationError MapHttpError(int status, string? body = null)
    {
        var (kind, retryable) = status switch
        {
            400 => (ErrorKind.Validation, false),
            401 => (ErrorKind.Unauthorized, false),
            403 => (ErrorKind.Forbidden, false),
            404 => (ErrorKind.NotFound, false),
            408 => (ErrorKind.Timeout, true),
            409 => (ErrorKind.Conflict, false),
            422 => (ErrorKind.Validation, false),
            429 => (ErrorKind.RateLimited, true),
            503 => (ErrorKind.ProviderUnavailable, true),
            _ when status >= 500 => (ErrorKind.Transient, true),
            _ => (ErrorKind.Unknown, false),
        };

        return new IntegrationError(
            Kind: kind,
            Code: $"HTTP_{status}",
            Message: body ?? $"Provider returned HTTP {status}.",
            Retryable: retryable,
            HttpStatus: status);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static HttpRequestMessage BuildRequest(
        HttpMethod method,
        string path,
        IReadOnlyDictionary<string, string>? query)
    {
        var uri = BuildUri(path, query);
        return new HttpRequestMessage(method, uri);
    }

    private static HttpRequestMessage BuildJsonRequest(
        HttpMethod method,
        string path,
        object body,
        IReadOnlyDictionary<string, string>? query)
    {
        var req = BuildRequest(method, path, query);
        req.Content = new StringContent(
            JsonSerializer.Serialize(body, JsonOptions),
            Encoding.UTF8,
            "application/json");
        return req;
    }

    private static string BuildUri(string path, IReadOnlyDictionary<string, string>? query)
    {
        if (query is null || query.Count == 0)
            return path;

        var sb = new StringBuilder(path).Append('?');
        var first = true;
        foreach (var (key, value) in query)
        {
            if (!first) sb.Append('&');
            sb.Append(Uri.EscapeDataString(key)).Append('=').Append(Uri.EscapeDataString(value));
            first = false;
        }
        return sb.ToString();
    }

    private static async Task<string> TryReadBodyAsync(HttpResponseMessage response, CancellationToken ct)
    {
        try
        {
            var content = await response.Content.ReadAsStringAsync(ct);
            // Truncate long error bodies so they don't flood logs
            return content.Length > 2048 ? string.Concat(content.AsSpan(0, 2048), "…") : content;
        }
        catch
        {
            return string.Empty;
        }
    }
}
