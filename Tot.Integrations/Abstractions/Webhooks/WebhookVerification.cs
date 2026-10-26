namespace Tot.Integrations;

/// <summary>Result of a webhook signature verification attempt.</summary>
/// <param name="IsValid">
/// <see langword="true"/> if the signature is valid and the webhook should be processed.
/// </param>
/// <param name="ProviderEventId">
/// Optional provider-assigned event id used for deduplication.
/// <see langword="null"/> when the provider does not include an event id.
/// </param>
/// <param name="Reason">
/// Human-readable reason for rejection, logged at Warning level when <paramref name="IsValid"/> is <see langword="false"/>.
/// </param>
public sealed record WebhookVerification(
    bool IsValid,
    string? ProviderEventId = null,
    string? Reason = null)
{
    /// <summary>Creates a passing verification result.</summary>
    public static WebhookVerification Valid(string? providerEventId = null) =>
        new(IsValid: true, ProviderEventId: providerEventId);

    /// <summary>Creates a failing verification result.</summary>
    public static WebhookVerification Invalid(string reason) =>
        new(IsValid: false, Reason: reason);
}
