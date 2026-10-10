namespace HomeMaintenance.Core.Results;

/// <summary>
/// Represents the outcome of an operation without a return value.
/// Also serves as the base class for generic <see cref="Result{T}"/>.
/// </summary>
public class Result
{
    protected Result(bool isSuccess, Error error)
    {
        if (isSuccess && error != Error.None)
        {
            throw new InvalidOperationException("A successful result cannot have an error.");
        }

        if (!isSuccess && error == Error.None)
        {
            throw new InvalidOperationException("A failed result must have an error.");
        }

        IsSuccess = isSuccess;
        Error = error;
    }

    /// <summary>
    /// Indicates whether the operation completed successfully.
    /// </summary>
    public bool IsSuccess { get; }

    /// <summary>
    /// Indicates whether the operation failed.
    /// </summary>
    public bool IsFailure => !IsSuccess;

    /// <summary>
    /// Holds the error details if the operation failed.
    /// </summary>
    public Error Error { get; }

    // Static Factory Methods for non-generic results
    public static Result Success() => new(true, Error.None);
    public static Result Failure(Error error) => new(false, error);

    // Static Factory Methods for generic results
    public static Result<T> Success<T>(T value) => new(value, true, Error.None);
    public static Result<T> Failure<T>(Error error) => new(default, false, error);

    // Implicit operator allowing an Error to be directly returned as a failed Result
    public static implicit operator Result(Error error) => Failure(error);
}

/// <summary>
/// Represents the outcome of an operation that produces a value of type <typeparamref name="T"/>.
/// </summary>
/// <typeparam name="T">The type of the returned value.</typeparam>
public class Result<T> : Result
{
    private readonly T? _value;

    protected internal Result(T? value, bool isSuccess, Error error)
        : base(isSuccess, error)
    {
        _value = value;
    }

    /// <summary>
    /// Gets the payload value if the operation was successful.
    /// Throws an <see cref="InvalidOperationException"/> if accessed on a failed result.
    /// </summary>
    public T Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException("Cannot access the value of a failed result.");

    // Implicit operator allowing a value of type T to be directly returned as a successful Result<T>
    public static implicit operator Result<T>(T value) => Success(value);

    // Implicit operator allowing an Error to be directly returned as a failed Result<T>
    public static implicit operator Result<T>(Error error) => Failure<T>(error);
}
