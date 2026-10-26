using System.Text.Json;

namespace Tot.Integrations;

/// <summary>Represents a raw inbound webhook delivery before parsing or verification.</summary>
/// <param name="ConnectionId">The connection this webhook was received on.</param>
/// <param name="ProviderCode">The provider code parsed from the webhook URL.</param>
/// <param name="Headers">All HTTP headers from the inbound request.</param>
/// <param name="Body">Raw request body bytes (up to 1 MB).</param>
/// <param name="ReceivedAt">UTC timestamp of receipt.</param>
public sealed record InboundWebhook(
    Guid ConnectionId,
    string ProviderCode,
    IReadOnlyDictionary<string, string> Headers,
    byte[] Body,
    DateTimeOffset ReceivedAt)
{
    /// <summary>Returns the body as a UTF-8 string. Allocates — use sparingly.</summary>
    public string BodyText => System.Text.Encoding.UTF8.GetString(Body);

    /// <summary>Parses the body as JSON. Throws <see cref="JsonException"/> on invalid input.</summary>
    public JsonElement BodyJson => JsonDocument.Parse(Body).RootElement;
}
