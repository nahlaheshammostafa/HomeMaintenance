namespace Tot.Integrations;

/// <summary>Describes a failure returned by an integration operation.</summary>
/// <param name="Kind">Broad classification used for routing and retry decisions.</param>
/// <param name="Code">Provider-specific error code string (e.g. "DEVICE_OFFLINE").</param>
/// <param name="Message">Human-readable message safe to log at Information level.</param>
/// <param name="Retryable">
/// <see langword="true"/> when the command dispatcher should schedule a retry;
/// <see langword="false"/> when the error is permanent (dead-letter immediately).
/// </param>
/// <param name="HttpStatus">HTTP status code if the error came from an HTTP response.</param>
public sealed record IntegrationError(
    ErrorKind Kind,
    string Code,
    string Message,
    bool Retryable,
    int? HttpStatus = null);
