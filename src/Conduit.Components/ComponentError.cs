using System;

namespace Conduit.Components
{
    /// <summary>
    /// Represents an error that occurred in a component.
    /// </summary>
    public class ComponentError
    {
        /// <summary>
        /// Gets the component ID where the error occurred.
        /// </summary>
        public string ComponentId { get; }

        /// <summary>
        /// Gets the error code.
        /// </summary>
        public string Code { get; }

        /// <summary>
        /// Gets the error message.
        /// </summary>
        public string Message { get; }

        /// <summary>
        /// Gets the exception that caused the error, if any.
        /// </summary>
        public Exception? Exception { get; }

        /// <summary>
        /// Gets the severity level of the error.
        /// </summary>
        public ComponentErrorSeverity Severity { get; }

        /// <summary>
        /// Gets the timestamp when the error occurred.
        /// </summary>
        public DateTimeOffset Timestamp { get; }

        /// <summary>
        /// Gets additional error context data.
        /// </summary>
        public object? Context { get; }

        /// <summary>
        /// Initializes a new instance of ComponentError.
        /// </summary>
        public ComponentError(
            string componentId,
            string code,
            string message,
            ComponentErrorSeverity severity = ComponentErrorSeverity.Error,
            Exception? exception = null,
            object? context = null)
        {
            ComponentId = componentId;
            Code = code;
            Message = message;
            Severity = severity;
            Exception = exception;
            Context = context;
            Timestamp = DateTimeOffset.UtcNow;
        }

        /// <summary>
        /// Creates a warning error.
        /// </summary>
        public static ComponentError Warning(string componentId, string code, string message, object? context = null)
        {
            return new ComponentError(componentId, code, message, ComponentErrorSeverity.Warning, context: context);
        }

        /// <summary>
        /// Creates an error.
        /// </summary>
        public static ComponentError Error(string componentId, string code, string message, Exception? exception = null, object? context = null)
        {
            return new ComponentError(componentId, code, message, ComponentErrorSeverity.Error, exception, context);
        }

        /// <summary>
        /// Creates a critical error.
        /// </summary>
        public static ComponentError Critical(string componentId, string code, string message, Exception? exception = null, object? context = null)
        {
            return new ComponentError(componentId, code, message, ComponentErrorSeverity.Critical, exception, context);
        }

        /// <summary>
        /// Creates an error from an exception.
        /// </summary>
        public static ComponentError FromException(string componentId, Exception exception, string? code = null, object? context = null)
        {
            return new ComponentError(
                componentId,
                code ?? exception.GetType().Name,
                exception.Message,
                ComponentErrorSeverity.Error,
                exception,
                context);
        }
    }

    /// <summary>
    /// Severity levels for component errors.
    /// </summary>
    public enum ComponentErrorSeverity
    {
        /// <summary>
        /// Information level - non-critical issue.
        /// </summary>
        Information = 0,

        /// <summary>
        /// Warning level - potential issue that should be monitored.
        /// </summary>
        Warning = 1,

        /// <summary>
        /// Error level - significant issue that affects functionality.
        /// </summary>
        Error = 2,

        /// <summary>
        /// Critical level - severe issue that may cause system failure.
        /// </summary>
        Critical = 3
    }
}