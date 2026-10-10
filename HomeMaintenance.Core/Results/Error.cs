namespace HomeMaintenance.Core.Results;

/// <summary>
/// Represents an immutable error description in the system.
/// Uses a C# record for value-based equality and immutability.
/// </summary>
public record Error(
    string Code,
    string Description,
    ErrorType Type = ErrorType.Failure,
    IDictionary<string, string[]>? ValidationErrors = null)
{
    /// <summary>
    /// Represents no error (used for successful results).
    /// </summary>
    public static readonly Error None = new(string.Empty, string.Empty);

    /// <summary>
    /// Represents an error where a provided argument or required value is null.
    /// </summary>
    public static readonly Error NullValue = new("General.NullValue", "A required value was null.", ErrorType.Failure);

    public static Error Failure(string code, string description) =>
        new(code, description, ErrorType.Failure);

    public static Error NotFound(string code, string description) =>
        new(code, description, ErrorType.NotFound);

    public static Error Validation(
        string code,
        string description,
        IDictionary<string, string[]>? validationErrors = null) =>
        new(code, description, ErrorType.Validation, validationErrors);

    public static Error Conflict(string code, string description) =>
        new(code, description, ErrorType.Conflict);

    public static Error Forbidden(string code, string description) =>
        new(code, description, ErrorType.Forbidden);

    public static Error Unauthorized(string code, string description) =>
        new(code, description, ErrorType.Unauthorized);
}
