namespace Conduit.Resilience;

/// <summary>
/// Defines retry strategies for retry policies.
/// </summary>
public enum RetryStrategy
{
    /// <summary>
    /// Linear backoff strategy with constant delays.
    /// </summary>
    Linear,

    /// <summary>
    /// Exponential backoff strategy with exponentially increasing delays.
    /// </summary>
    Exponential,

    /// <summary>
    /// Fixed delay strategy with constant delay between retries.
    /// </summary>
    Fixed,

    /// <summary>
    /// No delay between retries.
    /// </summary>
    Immediate
}