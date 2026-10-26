namespace Tot.Integrations;

/// <summary>Classifies an integration error for routing, retry logic, and observability.</summary>
public enum ErrorKind
{
    /// <summary>The request payload or parameters failed validation.</summary>
    Validation,
    /// <summary>The requested resource was not found at the provider.</summary>
    NotFound,
    /// <summary>The operation conflicts with existing provider state.</summary>
    Conflict,
    /// <summary>The credentials were rejected by the provider.</summary>
    Unauthorized,
    /// <summary>The credentials are valid but lack permission for this operation.</summary>
    Forbidden,
    /// <summary>The provider rate limit was exceeded.</summary>
    RateLimited,
    /// <summary>A transient network or server error; the operation may be retried.</summary>
    Transient,
    /// <summary>The provider service is unavailable (e.g. 503).</summary>
    ProviderUnavailable,
    /// <summary>The per-connection circuit breaker is open.</summary>
    CircuitOpen,
    /// <summary>The request timed out before a response was received.</summary>
    Timeout,
    /// <summary>The target physical device is offline or unreachable.</summary>
    DeviceOffline,
    /// <summary>An unclassified error returned by the provider.</summary>
    Unknown
}
