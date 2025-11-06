using System;
using System.Collections.Generic;

namespace Conduit.Resilience.ErrorHandling;

/// <summary>
/// Provides detailed context information about errors for better error handling and correlation
/// </summary>
public class ErrorContext
{
    /// <summary>
    /// Unique identifier for this error occurrence
    /// </summary>
    public string ErrorId { get; }

    /// <summary>
    /// Correlation ID for tracking errors across distributed operations
    /// </summary>
    public string? CorrelationId { get; set; }

    /// <summary>
    /// The original exception that occurred
    /// </summary>
    public Exception Exception { get; }

    /// <summary>
    /// Timestamp when the error occurred
    /// </summary>
    public DateTimeOffset Timestamp { get; }

    /// <summary>
    /// Operation name where the error occurred
    /// </summary>
    public string? OperationName { get; set; }

    /// <summary>
    /// Component or service where the error occurred
    /// </summary>
    public string? ComponentName { get; set; }

    /// <summary>
    /// User or system context when the error occurred
    /// </summary>
    public string? UserContext { get; set; }

    /// <summary>
    /// Additional properties for error context
    /// </summary>
    public Dictionary<string, object> Properties { get; }

    /// <summary>
    /// Number of retry attempts made for this error
    /// </summary>
    public int RetryAttempt { get; set; }

    /// <summary>
    /// Whether this error is considered transient (retryable)
    /// </summary>
    public bool IsTransient { get; set; }

    /// <summary>
    /// Whether this error is considered critical (requires immediate attention)
    /// </summary>
    public bool IsCritical { get; set; }

    /// <summary>
    /// Error category for classification
    /// </summary>
    public ErrorCategory Category { get; set; }

    /// <summary>
    /// Severity level of the error
    /// </summary>
    public ErrorSeverity Severity { get; set; }

    /// <summary>
    /// Tags for error classification and filtering
    /// </summary>
    public HashSet<string> Tags { get; }

    /// <summary>
    /// Initializes a new instance of the ErrorContext class
    /// </summary>
    public ErrorContext(Exception exception)
    {
        ErrorId = Guid.NewGuid().ToString();
        Exception = exception ?? throw new ArgumentNullException(nameof(exception));
        Timestamp = DateTimeOffset.UtcNow;
        Properties = new Dictionary<string, object>();
        Tags = new HashSet<string>();

        // Automatically classify the error
        ClassifyError(exception);
    }

    /// <summary>
    /// Adds a property to the error context
    /// </summary>
    public ErrorContext WithProperty(string key, object value)
    {
        Properties[key] = value;
        return this;
    }

    /// <summary>
    /// Adds a tag to the error context
    /// </summary>
    public ErrorContext WithTag(string tag)
    {
        Tags.Add(tag);
        return this;
    }

    /// <summary>
    /// Sets the correlation ID for this error
    /// </summary>
    public ErrorContext WithCorrelationId(string correlationId)
    {
        CorrelationId = correlationId;
        return this;
    }

    /// <summary>
    /// Sets the operation name for this error
    /// </summary>
    public ErrorContext WithOperation(string operationName)
    {
        OperationName = operationName;
        return this;
    }

    /// <summary>
    /// Sets the component name for this error
    /// </summary>
    public ErrorContext WithComponent(string componentName)
    {
        ComponentName = componentName;
        return this;
    }

    /// <summary>
    /// Gets a property value from the error context
    /// </summary>
    public T? GetProperty<T>(string key)
    {
        if (Properties.TryGetValue(key, out var value) && value is T typedValue)
        {
            return typedValue;
        }
        return default;
    }

    /// <summary>
    /// Checks if the error has a specific tag
    /// </summary>
    public bool HasTag(string tag)
    {
        return Tags.Contains(tag);
    }

    /// <summary>
    /// Creates a copy of this error context with updated retry attempt
    /// </summary>
    public ErrorContext WithRetryAttempt(int attempt)
    {
        var copy = new ErrorContext(Exception)
        {
            CorrelationId = CorrelationId,
            OperationName = OperationName,
            ComponentName = ComponentName,
            UserContext = UserContext,
            RetryAttempt = attempt,
            IsTransient = IsTransient,
            IsCritical = IsCritical,
            Category = Category,
            Severity = Severity
        };

        foreach (var prop in Properties)
        {
            copy.Properties[prop.Key] = prop.Value;
        }

        foreach (var tag in Tags)
        {
            copy.Tags.Add(tag);
        }

        return copy;
    }

    private void ClassifyError(Exception exception)
    {
        // Classify based on exception type
        switch (exception)
        {
            case ArgumentNullException:
            case ArgumentOutOfRangeException:
            case ArgumentException:
            case InvalidOperationException:
                Category = ErrorCategory.ValidationError;
                Severity = ErrorSeverity.Medium;
                IsTransient = false;
                Tags.Add("validation");
                break;

            case TimeoutException:
                Category = ErrorCategory.TimeoutError;
                Severity = ErrorSeverity.Medium;
                IsTransient = true;
                Tags.Add("timeout");
                break;

            case UnauthorizedAccessException:
                Category = ErrorCategory.SecurityError;
                Severity = ErrorSeverity.High;
                IsTransient = false;
                Tags.Add("security");
                break;

            case System.Net.Http.HttpRequestException:
                Category = ErrorCategory.NetworkError;
                Severity = ErrorSeverity.Medium;
                IsTransient = true;
                Tags.Add("network");
                break;

            case System.IO.IOException:
                Category = ErrorCategory.IoError;
                Severity = ErrorSeverity.Medium;
                IsTransient = true;
                Tags.Add("io");
                break;

            case OutOfMemoryException:
                Category = ErrorCategory.SystemError;
                Severity = ErrorSeverity.Critical;
                IsTransient = false;
                IsCritical = true;
                Tags.Add("critical");
                break;

            case StackOverflowException:
                Category = ErrorCategory.SystemError;
                Severity = ErrorSeverity.Critical;
                IsTransient = false;
                IsCritical = true;
                Tags.Add("critical");
                break;

            default:
                Category = ErrorCategory.UnknownError;
                Severity = ErrorSeverity.Medium;
                IsTransient = true;
                Tags.Add("unknown");
                break;
        }
    }
}

/// <summary>
/// Categories for error classification
/// </summary>
public enum ErrorCategory
{
    /// <summary>
    /// Unknown error type
    /// </summary>
    UnknownError,

    /// <summary>
    /// Validation or argument errors
    /// </summary>
    ValidationError,

    /// <summary>
    /// Network communication errors
    /// </summary>
    NetworkError,

    /// <summary>
    /// Timeout errors
    /// </summary>
    TimeoutError,

    /// <summary>
    /// Security and authorization errors
    /// </summary>
    SecurityError,

    /// <summary>
    /// I/O and file system errors
    /// </summary>
    IoError,

    /// <summary>
    /// System-level errors
    /// </summary>
    SystemError,

    /// <summary>
    /// Business logic errors
    /// </summary>
    BusinessError,

    /// <summary>
    /// Configuration errors
    /// </summary>
    ConfigurationError,

    /// <summary>
    /// Dependency errors (external services)
    /// </summary>
    DependencyError
}

/// <summary>
/// Severity levels for errors
/// </summary>
public enum ErrorSeverity
{
    /// <summary>
    /// Low severity - informational
    /// </summary>
    Low,

    /// <summary>
    /// Medium severity - warning
    /// </summary>
    Medium,

    /// <summary>
    /// High severity - error
    /// </summary>
    High,

    /// <summary>
    /// Critical severity - system failure
    /// </summary>
    Critical
}