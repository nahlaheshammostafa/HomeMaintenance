namespace Tot.Integrations;

/// <summary>
/// The single result type for every integration operation.
/// Either <see cref="Value"/> is set (success) or <see cref="Error"/> is set (failure) — never both.
/// </summary>
/// <typeparam name="T">The payload type on success.</typeparam>
public readonly record struct IntegrationResult<T>
{
    /// <summary>The successful value; <see langword="null"/> when <see cref="IsSuccess"/> is <see langword="false"/>.</summary>
    public T? Value { get; init; }

    /// <summary>The error detail; <see langword="null"/> when <see cref="IsSuccess"/> is <see langword="true"/>.</summary>
    public IntegrationError? Error { get; init; }

    /// <summary><see langword="true"/> when the operation completed without error.</summary>
    public bool IsSuccess => Error is null;

    /// <summary>Creates a successful result carrying <paramref name="value"/>.</summary>
    public static IntegrationResult<T> Ok(T value) => new() { Value = value };

    /// <summary>Creates a failed result carrying <paramref name="error"/>.</summary>
    public static IntegrationResult<T> Fail(IntegrationError error) => new() { Error = error };

    /// <summary>
    /// Creates a failed result from an <see cref="Exception"/>, classifying it as
    /// <see cref="ErrorKind.Transient"/> and retryable.
    /// </summary>
    public static IntegrationResult<T> FromException(Exception ex) => Fail(new IntegrationError(
        Kind: ErrorKind.Transient,
        Code: ex.GetType().Name,
        Message: ex.Message,
        Retryable: true));
}
