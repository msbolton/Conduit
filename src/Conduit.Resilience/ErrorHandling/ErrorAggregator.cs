using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Conduit.Resilience.ErrorHandling;

/// <summary>
/// Aggregates and correlates errors across distributed operations for better error analysis
/// </summary>
public class ErrorAggregator : IDisposable
{
    private readonly ILogger<ErrorAggregator>? _logger;
    private readonly ErrorAggregatorConfiguration _config;
    private readonly ConcurrentDictionary<string, ErrorCorrelation> _correlations;
    private readonly Timer? _cleanupTimer;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the ErrorAggregator class
    /// </summary>
    public ErrorAggregator(ErrorAggregatorConfiguration? config = null, ILogger<ErrorAggregator>? logger = null)
    {
        _config = config ?? new ErrorAggregatorConfiguration();
        _logger = logger;
        _correlations = new ConcurrentDictionary<string, ErrorCorrelation>();

        // Start cleanup timer to remove old correlations
        _cleanupTimer = new Timer(CleanupExpiredCorrelations, null,
            _config.CleanupInterval, _config.CleanupInterval);
    }

    /// <summary>
    /// Records an error context for correlation and analysis
    /// </summary>
    public void RecordError(ErrorContext errorContext)
    {
        if (_disposed) return;

        var correlationId = errorContext.CorrelationId ?? errorContext.ErrorId;

        var correlation = _correlations.GetOrAdd(correlationId, id => new ErrorCorrelation
        {
            CorrelationId = id,
            StartTime = DateTimeOffset.UtcNow
        });

        lock (correlation)
        {
            correlation.Errors.Add(errorContext);
            correlation.LastErrorTime = DateTimeOffset.UtcNow;
            correlation.ErrorCount++;

            // Update correlation metadata
            UpdateCorrelationMetadata(correlation, errorContext);
        }

        _logger?.LogDebug("Recorded error {ErrorId} for correlation {CorrelationId} (total errors: {ErrorCount})",
            errorContext.ErrorId, correlationId, correlation.ErrorCount);

        // Check if correlation meets threshold for notification
        CheckThresholds(correlation);
    }

    /// <summary>
    /// Gets all error correlations
    /// </summary>
    public IEnumerable<ErrorCorrelation> GetCorrelations()
    {
        return _correlations.Values.ToList();
    }

    /// <summary>
    /// Gets a specific error correlation by ID
    /// </summary>
    public ErrorCorrelation? GetCorrelation(string correlationId)
    {
        _correlations.TryGetValue(correlationId, out var correlation);
        return correlation;
    }

    /// <summary>
    /// Gets correlations that match specific criteria
    /// </summary>
    public IEnumerable<ErrorCorrelation> GetCorrelations(Func<ErrorCorrelation, bool> predicate)
    {
        return _correlations.Values.Where(predicate).ToList();
    }

    /// <summary>
    /// Gets error patterns and trends
    /// </summary>
    public ErrorAnalysis AnalyzeErrors(TimeSpan? timeWindow = null)
    {
        var window = timeWindow ?? TimeSpan.FromHours(1);
        var cutoff = DateTimeOffset.UtcNow - window;

        var recentCorrelations = _correlations.Values
            .Where(c => c.LastErrorTime >= cutoff)
            .ToList();

        var allErrors = recentCorrelations
            .SelectMany(c => c.Errors)
            .Where(e => e.Timestamp >= cutoff)
            .ToList();

        return new ErrorAnalysis
        {
            TimeWindow = window,
            TotalCorrelations = recentCorrelations.Count,
            TotalErrors = allErrors.Count,
            ErrorsByCategory = allErrors
                .GroupBy(e => e.Category)
                .ToDictionary(g => g.Key, g => g.Count()),
            ErrorsBySeverity = allErrors
                .GroupBy(e => e.Severity)
                .ToDictionary(g => g.Key, g => g.Count()),
            ErrorsByComponent = allErrors
                .Where(e => !string.IsNullOrEmpty(e.ComponentName))
                .GroupBy(e => e.ComponentName!)
                .ToDictionary(g => g.Key, g => g.Count()),
            MostFrequentErrors = allErrors
                .GroupBy(e => e.Exception.GetType().Name)
                .OrderByDescending(g => g.Count())
                .Take(10)
                .ToDictionary(g => g.Key, g => g.Count()),
            HighSeverityCorrelations = recentCorrelations
                .Where(c => c.HighestSeverity >= ErrorSeverity.High)
                .Count(),
            CriticalErrorCount = allErrors
                .Count(e => e.Severity == ErrorSeverity.Critical),
            AverageErrorsPerCorrelation = recentCorrelations.Count > 0
                ? allErrors.Count / (double)recentCorrelations.Count
                : 0
        };
    }

    /// <summary>
    /// Clears all correlations
    /// </summary>
    public void ClearCorrelations()
    {
        var count = _correlations.Count;
        _correlations.Clear();
        _logger?.LogInformation("Cleared {Count} error correlations", count);
    }

    /// <summary>
    /// Removes a specific correlation
    /// </summary>
    public bool RemoveCorrelation(string correlationId)
    {
        var removed = _correlations.TryRemove(correlationId, out _);
        if (removed)
        {
            _logger?.LogDebug("Removed correlation {CorrelationId}", correlationId);
        }
        return removed;
    }

    private void UpdateCorrelationMetadata(ErrorCorrelation correlation, ErrorContext errorContext)
    {
        // Update highest severity
        if (errorContext.Severity > correlation.HighestSeverity)
        {
            correlation.HighestSeverity = errorContext.Severity;
        }

        // Add unique components
        if (!string.IsNullOrEmpty(errorContext.ComponentName))
        {
            correlation.AffectedComponents.Add(errorContext.ComponentName);
        }

        // Add unique operations
        if (!string.IsNullOrEmpty(errorContext.OperationName))
        {
            correlation.AffectedOperations.Add(errorContext.OperationName);
        }

        // Update pattern flags
        if (errorContext.IsCritical)
        {
            correlation.HasCriticalErrors = true;
        }

        if (!errorContext.IsTransient)
        {
            correlation.HasNonTransientErrors = true;
        }

        // Merge tags
        foreach (var tag in errorContext.Tags)
        {
            correlation.Tags.Add(tag);
        }
    }

    private void CheckThresholds(ErrorCorrelation correlation)
    {
        // Check error count threshold
        if (_config.ErrorCountThreshold > 0 &&
            correlation.ErrorCount >= _config.ErrorCountThreshold &&
            !correlation.ThresholdNotificationSent)
        {
            correlation.ThresholdNotificationSent = true;
            OnThresholdExceeded?.Invoke(new ThresholdExceededEventArgs
            {
                CorrelationId = correlation.CorrelationId,
                ThresholdType = "ErrorCount",
                CurrentValue = correlation.ErrorCount,
                ThresholdValue = _config.ErrorCountThreshold,
                Correlation = correlation
            });

            _logger?.LogWarning("Error count threshold exceeded for correlation {CorrelationId}: {ErrorCount} errors",
                correlation.CorrelationId, correlation.ErrorCount);
        }

        // Check time window threshold
        if (_config.TimeWindowThreshold.HasValue &&
            correlation.LastErrorTime - correlation.StartTime >= _config.TimeWindowThreshold.Value &&
            !correlation.TimeThresholdNotificationSent)
        {
            correlation.TimeThresholdNotificationSent = true;
            OnThresholdExceeded?.Invoke(new ThresholdExceededEventArgs
            {
                CorrelationId = correlation.CorrelationId,
                ThresholdType = "TimeWindow",
                CurrentValue = (correlation.LastErrorTime - correlation.StartTime).TotalMinutes,
                ThresholdValue = _config.TimeWindowThreshold.Value.TotalMinutes,
                Correlation = correlation
            });

            _logger?.LogWarning("Time window threshold exceeded for correlation {CorrelationId}: {Duration} minutes",
                correlation.CorrelationId, (correlation.LastErrorTime - correlation.StartTime).TotalMinutes);
        }
    }

    private void CleanupExpiredCorrelations(object? state)
    {
        if (_disposed) return;

        try
        {
            var cutoff = DateTimeOffset.UtcNow - _config.CorrelationRetention;
            var expiredIds = _correlations
                .Where(kvp => kvp.Value.LastErrorTime < cutoff)
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var id in expiredIds)
            {
                _correlations.TryRemove(id, out _);
            }

            if (expiredIds.Count > 0)
            {
                _logger?.LogDebug("Cleaned up {Count} expired error correlations", expiredIds.Count);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error during correlation cleanup");
        }
    }

    /// <summary>
    /// Event triggered when a threshold is exceeded
    /// </summary>
    public event Action<ThresholdExceededEventArgs>? OnThresholdExceeded;

    /// <summary>
    /// Disposes the error aggregator
    /// </summary>
    public void Dispose()
    {
        if (!_disposed)
        {
            _cleanupTimer?.Dispose();
            _disposed = true;
        }
    }
}

/// <summary>
/// Configuration for error aggregator
/// </summary>
public class ErrorAggregatorConfiguration
{
    /// <summary>
    /// How long to retain error correlations
    /// </summary>
    public TimeSpan CorrelationRetention { get; set; } = TimeSpan.FromHours(24);

    /// <summary>
    /// Interval for cleaning up expired correlations
    /// </summary>
    public TimeSpan CleanupInterval { get; set; } = TimeSpan.FromMinutes(30);

    /// <summary>
    /// Threshold for error count per correlation
    /// </summary>
    public int ErrorCountThreshold { get; set; } = 10;

    /// <summary>
    /// Threshold for time window duration
    /// </summary>
    public TimeSpan? TimeWindowThreshold { get; set; } = TimeSpan.FromMinutes(5);
}

/// <summary>
/// Represents a correlation of related errors
/// </summary>
public class ErrorCorrelation
{
    /// <summary>
    /// Correlation identifier
    /// </summary>
    public string CorrelationId { get; set; } = string.Empty;

    /// <summary>
    /// When the first error in this correlation occurred
    /// </summary>
    public DateTimeOffset StartTime { get; set; }

    /// <summary>
    /// When the last error in this correlation occurred
    /// </summary>
    public DateTimeOffset LastErrorTime { get; set; }

    /// <summary>
    /// All errors in this correlation
    /// </summary>
    public List<ErrorContext> Errors { get; } = new();

    /// <summary>
    /// Total number of errors in this correlation
    /// </summary>
    public int ErrorCount { get; set; }

    /// <summary>
    /// Highest severity level in this correlation
    /// </summary>
    public ErrorSeverity HighestSeverity { get; set; }

    /// <summary>
    /// Components affected by errors in this correlation
    /// </summary>
    public HashSet<string> AffectedComponents { get; } = new();

    /// <summary>
    /// Operations affected by errors in this correlation
    /// </summary>
    public HashSet<string> AffectedOperations { get; } = new();

    /// <summary>
    /// Whether this correlation contains critical errors
    /// </summary>
    public bool HasCriticalErrors { get; set; }

    /// <summary>
    /// Whether this correlation contains non-transient errors
    /// </summary>
    public bool HasNonTransientErrors { get; set; }

    /// <summary>
    /// Tags associated with this correlation
    /// </summary>
    public HashSet<string> Tags { get; } = new();

    /// <summary>
    /// Whether threshold notification has been sent
    /// </summary>
    public bool ThresholdNotificationSent { get; set; }

    /// <summary>
    /// Whether time threshold notification has been sent
    /// </summary>
    public bool TimeThresholdNotificationSent { get; set; }
}

/// <summary>
/// Analysis results for error patterns
/// </summary>
public class ErrorAnalysis
{
    /// <summary>
    /// Time window for the analysis
    /// </summary>
    public TimeSpan TimeWindow { get; set; }

    /// <summary>
    /// Total number of correlations
    /// </summary>
    public int TotalCorrelations { get; set; }

    /// <summary>
    /// Total number of errors
    /// </summary>
    public int TotalErrors { get; set; }

    /// <summary>
    /// Errors grouped by category
    /// </summary>
    public Dictionary<ErrorCategory, int> ErrorsByCategory { get; set; } = new();

    /// <summary>
    /// Errors grouped by severity
    /// </summary>
    public Dictionary<ErrorSeverity, int> ErrorsBySeverity { get; set; } = new();

    /// <summary>
    /// Errors grouped by component
    /// </summary>
    public Dictionary<string, int> ErrorsByComponent { get; set; } = new();

    /// <summary>
    /// Most frequent error types
    /// </summary>
    public Dictionary<string, int> MostFrequentErrors { get; set; } = new();

    /// <summary>
    /// Number of correlations with high severity errors
    /// </summary>
    public int HighSeverityCorrelations { get; set; }

    /// <summary>
    /// Number of critical errors
    /// </summary>
    public int CriticalErrorCount { get; set; }

    /// <summary>
    /// Average number of errors per correlation
    /// </summary>
    public double AverageErrorsPerCorrelation { get; set; }
}

/// <summary>
/// Event arguments for threshold exceeded events
/// </summary>
public class ThresholdExceededEventArgs
{
    /// <summary>
    /// Correlation ID that exceeded the threshold
    /// </summary>
    public string CorrelationId { get; set; } = string.Empty;

    /// <summary>
    /// Type of threshold that was exceeded
    /// </summary>
    public string ThresholdType { get; set; } = string.Empty;

    /// <summary>
    /// Current value that exceeded the threshold
    /// </summary>
    public double CurrentValue { get; set; }

    /// <summary>
    /// The threshold value that was exceeded
    /// </summary>
    public double ThresholdValue { get; set; }

    /// <summary>
    /// The correlation that exceeded the threshold
    /// </summary>
    public ErrorCorrelation? Correlation { get; set; }
}