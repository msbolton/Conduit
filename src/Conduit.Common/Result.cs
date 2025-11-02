namespace Conduit.Common;

/// <summary>
/// Represents the result of an operation that can succeed or fail.
/// </summary>
/// <typeparam name="T">The type of the value</typeparam>
public readonly struct Result<T> : IEquatable<Result<T>>
{
    private readonly T? _value;
    private readonly Error? _error;
    private readonly bool _isSuccess;

    private Result(T? value, Error? error, bool isSuccess)
    {
        _value = value;
        _error = error;
        _isSuccess = isSuccess;
    }

    /// <summary>
    /// Gets a value indicating whether the operation succeeded.
    /// </summary>
    public bool IsSuccess => _isSuccess;

    /// <summary>
    /// Gets a value indicating whether the operation failed.
    /// </summary>
    public bool IsFailure => !_isSuccess;

    /// <summary>
    /// Gets the value if successful.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when result is failure</exception>
    public T Value
    {
        get
        {
            if (!_isSuccess)
                throw new InvalidOperationException("Cannot get value from a failed result.");
            return _value!;
        }
    }

    /// <summary>
    /// Gets the error if failed.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when result is success</exception>
    public Error Error
    {
        get
        {
            if (_isSuccess)
                throw new InvalidOperationException("Cannot get error from a successful result.");
            return _error!;
        }
    }

    /// <summary>
    /// Creates a successful result.
    /// </summary>
    public static Result<T> Success(T value)
    {
        return new Result<T>(value, null, true);
    }

    /// <summary>
    /// Creates a failed result.
    /// </summary>
    public static Result<T> Failure(Error error)
    {
        return new Result<T>(default, error, false);
    }

    /// <summary>
    /// Creates a failed result with a message.
    /// </summary>
    public static Result<T> Failure(string message, string? code = null)
    {
        return new Result<T>(default, new Error(code ?? "ERROR", message), false);
    }

    /// <summary>
    /// Gets the value or a default value if the result is a failure.
    /// </summary>
    public T? GetValueOrDefault(T? defaultValue = default)
    {
        return _isSuccess ? _value : defaultValue;
    }

    /// <summary>
    /// Executes an action if the result is successful.
    /// </summary>
    public Result<T> OnSuccess(Action<T> action)
    {
        if (_isSuccess && _value != null)
        {
            action(_value);
        }
        return this;
    }

    /// <summary>
    /// Executes an action if the result is a failure.
    /// </summary>
    public Result<T> OnFailure(Action<Error> action)
    {
        if (!_isSuccess && _error != null)
        {
            action(_error);
        }
        return this;
    }

    /// <summary>
    /// Maps the value to a new type if successful.
    /// </summary>
    public Result<TNew> Map<TNew>(Func<T, TNew> mapper)
    {
        return _isSuccess && _value != null
            ? Result<TNew>.Success(mapper(_value))
            : Result<TNew>.Failure(_error!);
    }

    /// <summary>
    /// Binds the result to another result-producing function.
    /// </summary>
    public Result<TNew> Bind<TNew>(Func<T, Result<TNew>> binder)
    {
        return _isSuccess && _value != null
            ? binder(_value)
            : Result<TNew>.Failure(_error!);
    }

    /// <summary>
    /// Matches the result to produce a value.
    /// </summary>
    public TResult Match<TResult>(Func<T, TResult> success, Func<Error, TResult> failure)
    {
        return _isSuccess ? success(_value!) : failure(_error!);
    }

    /// <summary>
    /// Converts to a nullable value.
    /// </summary>
    public T? ToNullable()
    {
        return _isSuccess ? _value : default;
    }

    /// <summary>
    /// Implicit conversion from value to Result.
    /// </summary>
    public static implicit operator Result<T>(T value)
    {
        return Success(value);
    }

    /// <summary>
    /// Implicit conversion from Error to Result.
    /// </summary>
    public static implicit operator Result<T>(Error error)
    {
        return Failure(error);
    }

    public bool Equals(Result<T> other)
    {
        if (_isSuccess != other._isSuccess) return false;
        if (_isSuccess)
            return EqualityComparer<T>.Default.Equals(_value, other._value);
        return _error?.Equals(other._error) ?? other._error == null;
    }

    public override bool Equals(object? obj)
    {
        return obj is Result<T> other && Equals(other);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(_isSuccess, _value, _error);
    }

    public static bool operator ==(Result<T> left, Result<T> right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(Result<T> left, Result<T> right)
    {
        return !left.Equals(right);
    }

    public override string ToString()
    {
        return _isSuccess
            ? $"Success({_value})"
            : $"Failure({_error})";
    }
}

/// <summary>
/// Represents the result of an operation without a value.
/// </summary>
public readonly struct Result : IEquatable<Result>
{
    private readonly Error? _error;
    private readonly bool _isSuccess;

    private Result(Error? error, bool isSuccess)
    {
        _error = error;
        _isSuccess = isSuccess;
    }

    /// <summary>
    /// Gets a value indicating whether the operation succeeded.
    /// </summary>
    public bool IsSuccess => _isSuccess;

    /// <summary>
    /// Gets a value indicating whether the operation failed.
    /// </summary>
    public bool IsFailure => !_isSuccess;

    /// <summary>
    /// Gets the error if failed.
    /// </summary>
    public Error? Error => _error;

    /// <summary>
    /// Creates a successful result.
    /// </summary>
    public static Result Success()
    {
        return new Result(null, true);
    }

    /// <summary>
    /// Creates a failed result.
    /// </summary>
    public static Result Failure(Error error)
    {
        return new Result(error, false);
    }

    /// <summary>
    /// Creates a failed result with a message.
    /// </summary>
    public static Result Failure(string message, string? code = null)
    {
        return new Result(new Error(code ?? "ERROR", message), false);
    }

    /// <summary>
    /// Combines multiple results.
    /// </summary>
    public static Result Combine(params Result[] results)
    {
        var failures = results.Where(r => r.IsFailure).ToList();

        if (failures.Count == 0)
            return Success();

        var combinedError = new Error(
            "MULTIPLE_ERRORS",
            string.Join("; ", failures.Select(f => f._error?.Message ?? "Unknown error")),
            failures.SelectMany(f => f._error?.Details ?? Enumerable.Empty<ErrorDetail>()).ToList()
        );

        return Failure(combinedError);
    }

    public bool Equals(Result other)
    {
        if (_isSuccess != other._isSuccess) return false;
        return _error?.Equals(other._error) ?? other._error == null;
    }

    public override bool Equals(object? obj)
    {
        return obj is Result other && Equals(other);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(_isSuccess, _error);
    }

    public static bool operator ==(Result left, Result right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(Result left, Result right)
    {
        return !left.Equals(right);
    }

    public override string ToString()
    {
        return _isSuccess ? "Success" : $"Failure({_error})";
    }
}

/// <summary>
/// Represents an error.
/// </summary>
public class Error : IEquatable<Error>
{
    /// <summary>
    /// Initializes a new instance of the Error class.
    /// </summary>
    public Error(string code, string message, IReadOnlyList<ErrorDetail>? details = null)
    {
        Code = code ?? throw new ArgumentNullException(nameof(code));
        Message = message ?? throw new ArgumentNullException(nameof(message));
        Details = details ?? Array.Empty<ErrorDetail>();
    }

    /// <summary>
    /// Gets the error code.
    /// </summary>
    public string Code { get; }

    /// <summary>
    /// Gets the error message.
    /// </summary>
    public string Message { get; }

    /// <summary>
    /// Gets additional error details.
    /// </summary>
    public IReadOnlyList<ErrorDetail> Details { get; }

    /// <summary>
    /// Creates a validation error.
    /// </summary>
    public static Error Validation(string message, params ErrorDetail[] details)
    {
        return new Error("VALIDATION_ERROR", message, details);
    }

    /// <summary>
    /// Creates a not found error.
    /// </summary>
    public static Error NotFound(string message)
    {
        return new Error("NOT_FOUND", message);
    }

    /// <summary>
    /// Creates a conflict error.
    /// </summary>
    public static Error Conflict(string message)
    {
        return new Error("CONFLICT", message);
    }

    /// <summary>
    /// Creates an unauthorized error.
    /// </summary>
    public static Error Unauthorized(string message = "Unauthorized")
    {
        return new Error("UNAUTHORIZED", message);
    }

    /// <summary>
    /// Creates a forbidden error.
    /// </summary>
    public static Error Forbidden(string message = "Forbidden")
    {
        return new Error("FORBIDDEN", message);
    }

    public bool Equals(Error? other)
    {
        if (other is null) return false;
        return Code == other.Code && Message == other.Message;
    }

    public override bool Equals(object? obj)
    {
        return obj is Error other && Equals(other);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Code, Message);
    }

    public override string ToString()
    {
        return $"[{Code}] {Message}";
    }
}

/// <summary>
/// Represents additional error detail.
/// </summary>
public class ErrorDetail
{
    /// <summary>
    /// Initializes a new instance of the ErrorDetail class.
    /// </summary>
    public ErrorDetail(string field, string message)
    {
        Field = field ?? throw new ArgumentNullException(nameof(field));
        Message = message ?? throw new ArgumentNullException(nameof(message));
    }

    /// <summary>
    /// Gets the field name.
    /// </summary>
    public string Field { get; }

    /// <summary>
    /// Gets the detail message.
    /// </summary>
    public string Message { get; }

    public override string ToString()
    {
        return $"{Field}: {Message}";
    }
}