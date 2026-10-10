namespace HomeMaintenance.Core.Results;

/// <summary>
/// Represents the semantic category of an error in the application.
/// Used to decouple domain/application logic from presentation protocols (e.g., HTTP status codes).
/// </summary>
public enum ErrorType
{
    Failure,        // Generic business rule failure
    Validation,     // Input validation failed (e.g. invalid date, negative price)
    NotFound,       // The requested resource does not exist
    Conflict,       // State conflict (e.g. overlapping booking, technician already assigned)
    Forbidden,      // User is authenticated but lacks required permissions
    Unauthorized    // User is unauthenticated
}
