namespace Conduit.Api;

/// <summary>
/// Defines the strategy for handling errors in pipeline execution.
/// </summary>
public enum ErrorHandlingStrategy
{
    /// <summary>
    /// Retry the operation according to the retry policy.
    /// </summary>
    Retry = 0,

    /// <summary>
    /// Continue processing despite the error.
    /// </summary>
    Continue = 1,

    /// <summary>
    /// Send the failed message to a dead letter queue.
    /// </summary>
    DeadLetter = 2,

    /// <summary>
    /// Fail immediately without retrying.
    /// </summary>
    FailFast = 3,

    /// <summary>
    /// Use a custom error handling strategy.
    /// </summary>
    Custom = 4
}