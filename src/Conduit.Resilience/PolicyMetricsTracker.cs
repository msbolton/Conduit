using System;
using System.Threading;

namespace Conduit.Resilience;

/// <summary>
/// Thread-safe tracker for resilience policy execution metrics
/// </summary>
public class PolicyMetricsTracker
{
    private long _totalExecutions;
    private long _successfulExecutions;
    private long _failedExecutions;
    private long _rejectedExecutions;
    private long _timeoutExecutions;
    private long _retriedExecutions;
    private long _fallbackExecutions;
    private long _failedFallbackExecutions;
    private long _compensationExecutions;
    private long _failedCompensationExecutions;
    private long _totalExecutionTimeMs;
    private long _executionCount;

    /// <summary>
    /// Gets the policy name
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the resilience pattern type
    /// </summary>
    public ResiliencePattern Pattern { get; }

    /// <summary>
    /// Gets the total number of executions
    /// </summary>
    public long TotalExecutions => _totalExecutions;

    /// <summary>
    /// Gets the number of successful executions
    /// </summary>
    public long SuccessfulExecutions => _successfulExecutions;

    /// <summary>
    /// Gets the number of failed executions
    /// </summary>
    public long FailedExecutions => _failedExecutions;

    /// <summary>
    /// Gets the number of rejected executions
    /// </summary>
    public long RejectedExecutions => _rejectedExecutions;

    /// <summary>
    /// Gets the number of timeout executions
    /// </summary>
    public long TimeoutExecutions => _timeoutExecutions;

    /// <summary>
    /// Gets the number of retried executions
    /// </summary>
    public long RetriedExecutions => _retriedExecutions;

    /// <summary>
    /// Gets the number of fallback executions
    /// </summary>
    public long FallbackExecutions => _fallbackExecutions;

    /// <summary>
    /// Gets the number of failed fallback executions
    /// </summary>
    public long FailedFallbackExecutions => _failedFallbackExecutions;

    /// <summary>
    /// Gets the number of compensation executions
    /// </summary>
    public long CompensationExecutions => _compensationExecutions;

    /// <summary>
    /// Gets the number of failed compensation executions
    /// </summary>
    public long FailedCompensationExecutions => _failedCompensationExecutions;

    /// <summary>
    /// Gets the average execution time in milliseconds
    /// </summary>
    public double AverageExecutionTimeMs => _executionCount > 0
        ? (double)_totalExecutionTimeMs / _executionCount
        : 0.0;

    /// <summary>
    /// Gets additional pattern-specific metrics
    /// </summary>
    public object? AdditionalMetrics { get; set; }

    /// <summary>
    /// Calculates the failure rate
    /// </summary>
    public double FailureRate => _totalExecutions > 0
        ? (double)_failedExecutions / _totalExecutions
        : 0.0;

    /// <summary>
    /// Calculates the success rate
    /// </summary>
    public double SuccessRate => _totalExecutions > 0
        ? (double)_successfulExecutions / _totalExecutions
        : 0.0;

    /// <summary>
    /// Calculates the fallback rate
    /// </summary>
    public double FallbackRate => _totalExecutions > 0
        ? (double)_fallbackExecutions / _totalExecutions
        : 0.0;

    /// <summary>
    /// Calculates the compensation rate
    /// </summary>
    public double CompensationRate => _totalExecutions > 0
        ? (double)_compensationExecutions / _totalExecutions
        : 0.0;

    /// <summary>
    /// Initializes a new instance of the PolicyMetricsTracker class
    /// </summary>
    public PolicyMetricsTracker(string name, ResiliencePattern pattern)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Pattern = pattern;
    }

    /// <summary>
    /// Increments the total execution count
    /// </summary>
    public void IncrementExecutions()
    {
        Interlocked.Increment(ref _totalExecutions);
    }

    /// <summary>
    /// Increments the successful execution count
    /// </summary>
    public void IncrementSuccesses()
    {
        Interlocked.Increment(ref _successfulExecutions);
    }

    /// <summary>
    /// Increments the failed execution count
    /// </summary>
    public void IncrementFailures()
    {
        Interlocked.Increment(ref _failedExecutions);
    }

    /// <summary>
    /// Increments the rejected execution count
    /// </summary>
    public void IncrementRejections()
    {
        Interlocked.Increment(ref _rejectedExecutions);
    }

    /// <summary>
    /// Increments the timeout execution count
    /// </summary>
    public void IncrementTimeouts()
    {
        Interlocked.Increment(ref _timeoutExecutions);
    }

    /// <summary>
    /// Increments the retry execution count
    /// </summary>
    public void IncrementRetries()
    {
        Interlocked.Increment(ref _retriedExecutions);
    }

    /// <summary>
    /// Increments the fallback execution count
    /// </summary>
    public void IncrementFallbacks()
    {
        Interlocked.Increment(ref _fallbackExecutions);
    }

    /// <summary>
    /// Increments the failed fallback execution count
    /// </summary>
    public void IncrementFallbackFailures()
    {
        Interlocked.Increment(ref _failedFallbackExecutions);
    }

    /// <summary>
    /// Increments the compensation execution count
    /// </summary>
    public void IncrementCompensations()
    {
        Interlocked.Increment(ref _compensationExecutions);
    }

    /// <summary>
    /// Increments the failed compensation execution count
    /// </summary>
    public void IncrementCompensationFailures()
    {
        Interlocked.Increment(ref _failedCompensationExecutions);
    }

    /// <summary>
    /// Records execution time for calculating average
    /// </summary>
    public void RecordExecutionTime(TimeSpan executionTime)
    {
        var timeMs = (long)executionTime.TotalMilliseconds;
        Interlocked.Add(ref _totalExecutionTimeMs, timeMs);
        Interlocked.Increment(ref _executionCount);
    }

    /// <summary>
    /// Resets all metrics to zero
    /// </summary>
    public void Reset()
    {
        Interlocked.Exchange(ref _totalExecutions, 0);
        Interlocked.Exchange(ref _successfulExecutions, 0);
        Interlocked.Exchange(ref _failedExecutions, 0);
        Interlocked.Exchange(ref _rejectedExecutions, 0);
        Interlocked.Exchange(ref _timeoutExecutions, 0);
        Interlocked.Exchange(ref _retriedExecutions, 0);
        Interlocked.Exchange(ref _fallbackExecutions, 0);
        Interlocked.Exchange(ref _failedFallbackExecutions, 0);
        Interlocked.Exchange(ref _compensationExecutions, 0);
        Interlocked.Exchange(ref _failedCompensationExecutions, 0);
        Interlocked.Exchange(ref _totalExecutionTimeMs, 0);
        Interlocked.Exchange(ref _executionCount, 0);
        AdditionalMetrics = null;
    }

    /// <summary>
    /// Creates a PolicyMetrics data transfer object with current values
    /// </summary>
    public PolicyMetrics ToMetrics()
    {
        return new PolicyMetrics
        {
            Name = Name,
            Pattern = Pattern,
            TotalExecutions = _totalExecutions,
            SuccessfulExecutions = _successfulExecutions,
            FailedExecutions = _failedExecutions,
            RejectedExecutions = _rejectedExecutions,
            TimeoutExecutions = _timeoutExecutions,
            RetriedExecutions = _retriedExecutions,
            FallbackExecutions = _fallbackExecutions,
            FailedFallbackExecutions = _failedFallbackExecutions,
            CompensationExecutions = _compensationExecutions,
            FailedCompensationExecutions = _failedCompensationExecutions,
            AverageExecutionTimeMs = AverageExecutionTimeMs,
            AdditionalMetrics = AdditionalMetrics
        };
    }

    /// <summary>
    /// Creates a snapshot of the current metrics
    /// </summary>
    public PolicyMetricsSnapshot CreateSnapshot()
    {
        return new PolicyMetricsSnapshot
        {
            Name = Name,
            Pattern = Pattern,
            TotalExecutions = _totalExecutions,
            SuccessfulExecutions = _successfulExecutions,
            FailedExecutions = _failedExecutions,
            RejectedExecutions = _rejectedExecutions,
            TimeoutExecutions = _timeoutExecutions,
            RetriedExecutions = _retriedExecutions,
            FallbackExecutions = _fallbackExecutions,
            FailedFallbackExecutions = _failedFallbackExecutions,
            CompensationExecutions = _compensationExecutions,
            FailedCompensationExecutions = _failedCompensationExecutions,
            AverageExecutionTimeMs = AverageExecutionTimeMs,
            FailureRate = FailureRate,
            SuccessRate = SuccessRate,
            FallbackRate = FallbackRate,
            CompensationRate = CompensationRate,
            AdditionalMetrics = AdditionalMetrics,
            SnapshotTime = DateTimeOffset.UtcNow
        };
    }
}

/// <summary>
/// Immutable snapshot of policy metrics at a point in time
/// </summary>
public class PolicyMetricsSnapshot
{
    /// <summary>
    /// Gets or sets the policy name
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the resilience pattern type
    /// </summary>
    public ResiliencePattern Pattern { get; set; }

    /// <summary>
    /// Gets or sets the total number of executions
    /// </summary>
    public long TotalExecutions { get; set; }

    /// <summary>
    /// Gets or sets the number of successful executions
    /// </summary>
    public long SuccessfulExecutions { get; set; }

    /// <summary>
    /// Gets or sets the number of failed executions
    /// </summary>
    public long FailedExecutions { get; set; }

    /// <summary>
    /// Gets or sets the number of rejected executions
    /// </summary>
    public long RejectedExecutions { get; set; }

    /// <summary>
    /// Gets or sets the number of timeout executions
    /// </summary>
    public long TimeoutExecutions { get; set; }

    /// <summary>
    /// Gets or sets the number of retried executions
    /// </summary>
    public long RetriedExecutions { get; set; }

    /// <summary>
    /// Gets or sets the number of fallback executions
    /// </summary>
    public long FallbackExecutions { get; set; }

    /// <summary>
    /// Gets or sets the number of failed fallback executions
    /// </summary>
    public long FailedFallbackExecutions { get; set; }

    /// <summary>
    /// Gets or sets the number of compensation executions
    /// </summary>
    public long CompensationExecutions { get; set; }

    /// <summary>
    /// Gets or sets the number of failed compensation executions
    /// </summary>
    public long FailedCompensationExecutions { get; set; }

    /// <summary>
    /// Gets or sets the average execution time in milliseconds
    /// </summary>
    public double AverageExecutionTimeMs { get; set; }

    /// <summary>
    /// Gets or sets the failure rate
    /// </summary>
    public double FailureRate { get; set; }

    /// <summary>
    /// Gets or sets the success rate
    /// </summary>
    public double SuccessRate { get; set; }

    /// <summary>
    /// Gets or sets the fallback rate
    /// </summary>
    public double FallbackRate { get; set; }

    /// <summary>
    /// Gets or sets the compensation rate
    /// </summary>
    public double CompensationRate { get; set; }

    /// <summary>
    /// Gets or sets additional pattern-specific metrics
    /// </summary>
    public object? AdditionalMetrics { get; set; }

    /// <summary>
    /// Gets or sets the time when this snapshot was created
    /// </summary>
    public DateTimeOffset SnapshotTime { get; set; }
}