namespace Conduit.Api;

/// <summary>
/// Defines a retry policy for pipeline operations.
/// </summary>
public class RetryPolicy
{
    /// <summary>
    /// Gets or sets the maximum number of retry attempts.
    /// </summary>
    public int MaxRetries { get; set; } = 3;

    /// <summary>
    /// Gets or sets the delay between retry attempts.
    /// </summary>
    public TimeSpan RetryDelay { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Gets or sets the base delay for retry calculations.
    /// </summary>
    public TimeSpan BaseDelay { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Gets or sets the backoff multiplier for exponential backoff.
    /// </summary>
    public double BackoffMultiplier { get; set; } = 2.0;

    /// <summary>
    /// Gets or sets the maximum delay between retries.
    /// </summary>
    public TimeSpan MaxDelay { get; set; } = TimeSpan.FromMinutes(1);

    /// <summary>
    /// Gets or sets whether to use exponential backoff.
    /// </summary>
    public bool UseExponentialBackoff { get; set; } = true;

    /// <summary>
    /// Gets or sets the jitter factor for retry delays (0.0 to 1.0).
    /// </summary>
    public double JitterFactor { get; set; } = 0.1;

    /// <summary>
    /// Gets or sets a predicate to determine if an exception should trigger a retry.
    /// </summary>
    public Func<Exception, bool>? ShouldRetry { get; set; }

    /// <summary>
    /// Gets or sets whether this retry policy is retryable.
    /// </summary>
    public bool IsRetryable { get; set; } = true;

    /// <summary>
    /// Creates a retry policy with fixed delay.
    /// </summary>
    /// <param name="maxRetries">Maximum number of retries</param>
    /// <param name="delay">Delay between retries</param>
    /// <returns>A new retry policy</returns>
    public static RetryPolicy FixedDelay(int maxRetries, TimeSpan delay)
    {
        return new RetryPolicy
        {
            MaxRetries = maxRetries,
            RetryDelay = delay,
            UseExponentialBackoff = false
        };
    }

    /// <summary>
    /// Creates a retry policy with exponential backoff.
    /// </summary>
    /// <param name="maxRetries">Maximum number of retries</param>
    /// <param name="initialDelay">Initial delay between retries</param>
    /// <param name="backoffMultiplier">Backoff multiplier</param>
    /// <returns>A new retry policy</returns>
    public static RetryPolicy ExponentialBackoff(int maxRetries, TimeSpan initialDelay, double backoffMultiplier = 2.0)
    {
        return new RetryPolicy
        {
            MaxRetries = maxRetries,
            RetryDelay = initialDelay,
            BackoffMultiplier = backoffMultiplier,
            UseExponentialBackoff = true
        };
    }

    /// <summary>
    /// Calculates the delay for a specific retry attempt.
    /// </summary>
    /// <param name="attemptNumber">The attempt number (1-based)</param>
    /// <returns>The delay to use before the retry</returns>
    public TimeSpan CalculateDelay(int attemptNumber)
    {
        if (!UseExponentialBackoff)
        {
            return RetryDelay;
        }

        var delay = TimeSpan.FromMilliseconds(
            RetryDelay.TotalMilliseconds * Math.Pow(BackoffMultiplier, attemptNumber - 1));

        if (delay > MaxDelay)
        {
            delay = MaxDelay;
        }

        if (JitterFactor > 0)
        {
            var random = new Random();
            var jitter = delay.TotalMilliseconds * JitterFactor * (random.NextDouble() - 0.5);
            delay = TimeSpan.FromMilliseconds(delay.TotalMilliseconds + jitter);
        }

        return delay;
    }
}