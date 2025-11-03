using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Conduit.Resilience.ErrorHandling;

/// <summary>
/// Monitors system health based on error patterns and resilience policy metrics
/// </summary>
public class HealthMonitor : IDisposable
{
    private readonly ILogger<HealthMonitor>? _logger;
    private readonly HealthMonitorConfiguration _config;
    private readonly ErrorAggregator _errorAggregator;
    private readonly ResiliencePolicyRegistry _policyRegistry;
    private readonly Timer? _healthCheckTimer;
    private HealthStatus _currentStatus;
    private readonly object _statusLock = new();
    private bool _disposed;

    /// <summary>
    /// Gets the current health status
    /// </summary>
    public HealthStatus CurrentStatus
    {
        get
        {
            lock (_statusLock)
            {
                return _currentStatus;
            }
        }
        private set
        {
            lock (_statusLock)
            {
                if (_currentStatus.Status != value.Status)
                {
                    var previousStatus = _currentStatus.Status;
                    _currentStatus = value;
                    OnHealthStatusChanged?.Invoke(new HealthStatusChangedEventArgs
                    {
                        PreviousStatus = previousStatus,
                        CurrentStatus = value.Status,
                        HealthDetails = value,
                        Timestamp = DateTimeOffset.UtcNow
                    });
                }
                else
                {
                    _currentStatus = value;
                }
            }
        }
    }

    /// <summary>
    /// Initializes a new instance of the HealthMonitor class
    /// </summary>
    public HealthMonitor(
        ErrorAggregator errorAggregator,
        ResiliencePolicyRegistry policyRegistry,
        HealthMonitorConfiguration? config = null,
        ILogger<HealthMonitor>? logger = null)
    {
        _errorAggregator = errorAggregator ?? throw new ArgumentNullException(nameof(errorAggregator));
        _policyRegistry = policyRegistry ?? throw new ArgumentNullException(nameof(policyRegistry));
        _config = config ?? new HealthMonitorConfiguration();
        _logger = logger;
        _currentStatus = new HealthStatus { Status = SystemHealthStatus.Healthy };

        // Start health check timer
        _healthCheckTimer = new Timer(PerformHealthCheck, null,
            _config.HealthCheckInterval, _config.HealthCheckInterval);
    }

    /// <summary>
    /// Gets a detailed health report
    /// </summary>
    public HealthReport GetHealthReport()
    {
        var errorAnalysis = _errorAggregator.AnalyzeErrors(_config.AnalysisTimeWindow);
        var policyMetrics = _policyRegistry.GetAllMetrics().ToList();

        var report = new HealthReport
        {
            Timestamp = DateTimeOffset.UtcNow,
            OverallStatus = CurrentStatus.Status,
            ErrorAnalysis = errorAnalysis,
            PolicyMetrics = policyMetrics,
            SystemMetrics = CalculateSystemMetrics(errorAnalysis, policyMetrics),
            HealthIndicators = CalculateHealthIndicators(errorAnalysis, policyMetrics),
            Recommendations = GenerateRecommendations(errorAnalysis, policyMetrics)
        };

        return report;
    }

    /// <summary>
    /// Forces an immediate health check
    /// </summary>
    public Task<HealthStatus> CheckHealthAsync()
    {
        return Task.FromResult(PerformHealthCheckInternal());
    }

    /// <summary>
    /// Event triggered when health status changes
    /// </summary>
    public event Action<HealthStatusChangedEventArgs>? OnHealthStatusChanged;

    private void PerformHealthCheck(object? state)
    {
        if (_disposed) return;

        try
        {
            CurrentStatus = PerformHealthCheckInternal();
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error during health check");
        }
    }

    private HealthStatus PerformHealthCheckInternal()
    {
        var errorAnalysis = _errorAggregator.AnalyzeErrors(_config.AnalysisTimeWindow);
        var policyMetrics = _policyRegistry.GetAllMetrics().ToList();

        var healthIndicators = CalculateHealthIndicators(errorAnalysis, policyMetrics);

        // Determine overall health status
        var status = DetermineHealthStatus(healthIndicators, errorAnalysis);
        var details = new Dictionary<string, object>
        {
            ["ErrorCount"] = errorAnalysis.TotalErrors,
            ["CriticalErrors"] = errorAnalysis.CriticalErrorCount,
            ["FailureRate"] = CalculateOverallFailureRate(policyMetrics),
            ["HealthScore"] = healthIndicators.OverallHealthScore
        };

        return new HealthStatus
        {
            Status = status,
            Message = GetStatusMessage(status, healthIndicators),
            Details = details,
            Timestamp = DateTimeOffset.UtcNow,
            HealthIndicators = healthIndicators
        };
    }

    private HealthIndicators CalculateHealthIndicators(ErrorAnalysis errorAnalysis, List<PolicyMetrics> policyMetrics)
    {
        var indicators = new HealthIndicators();

        // Error rate indicator
        var totalExecutions = policyMetrics.Sum(m => m.TotalExecutions);
        indicators.ErrorRate = totalExecutions > 0
            ? (double)errorAnalysis.TotalErrors / totalExecutions
            : 0.0;

        // Critical error indicator
        indicators.CriticalErrorRate = totalExecutions > 0
            ? (double)errorAnalysis.CriticalErrorCount / totalExecutions
            : 0.0;

        // Circuit breaker health
        var circuitBreakerMetrics = policyMetrics.Where(m => m.Pattern == ResiliencePattern.CircuitBreaker).ToList();
        if (circuitBreakerMetrics.Any())
        {
            indicators.CircuitBreakerHealth = 1.0 - circuitBreakerMetrics.Average(m => m.FailureRate);
        }

        // Retry effectiveness
        var retryMetrics = policyMetrics.Where(m => m.Pattern == ResiliencePattern.Retry).ToList();
        if (retryMetrics.Any())
        {
            var totalRetries = retryMetrics.Sum(m => m.RetriedExecutions);
            var totalRetryExecutions = retryMetrics.Sum(m => m.TotalExecutions);
            indicators.RetryEffectiveness = totalRetryExecutions > 0
                ? 1.0 - ((double)totalRetries / totalRetryExecutions)
                : 1.0;
        }

        // Fallback effectiveness
        var fallbackMetrics = policyMetrics.Where(m => m.Pattern == ResiliencePattern.Fallback).ToList();
        if (fallbackMetrics.Any())
        {
            var totalFallbacks = fallbackMetrics.Sum(m => m.FallbackExecutions);
            var failedFallbacks = fallbackMetrics.Sum(m => m.FailedFallbackExecutions);
            indicators.FallbackEffectiveness = totalFallbacks > 0
                ? 1.0 - ((double)failedFallbacks / totalFallbacks)
                : 1.0;
        }

        // Performance indicator
        var avgExecutionTime = policyMetrics.Where(m => m.TotalExecutions > 0)
            .Average(m => m.AverageExecutionTimeMs);
        indicators.PerformanceScore = Math.Max(0, 1.0 - (avgExecutionTime / _config.MaxAcceptableResponseTimeMs));

        // Resource utilization (based on bulkhead and rate limiter)
        var bulkheadMetrics = policyMetrics.Where(m => m.Pattern == ResiliencePattern.Bulkhead).ToList();
        var rateLimiterMetrics = policyMetrics.Where(m => m.Pattern == ResiliencePattern.RateLimiter).ToList();
        var rejectedExecutions = bulkheadMetrics.Sum(m => m.RejectedExecutions) + rateLimiterMetrics.Sum(m => m.RejectedExecutions);
        var totalRequestedExecutions = policyMetrics.Sum(m => m.TotalExecutions + m.RejectedExecutions);
        indicators.ResourceUtilization = totalRequestedExecutions > 0
            ? 1.0 - ((double)rejectedExecutions / totalRequestedExecutions)
            : 1.0;

        // Calculate overall health score
        indicators.OverallHealthScore = CalculateOverallHealthScore(indicators);

        return indicators;
    }

    private double CalculateOverallHealthScore(HealthIndicators indicators)
    {
        var weights = new Dictionary<string, double>
        {
            ["ErrorRate"] = 0.3,
            ["CriticalErrorRate"] = 0.25,
            ["CircuitBreakerHealth"] = 0.15,
            ["RetryEffectiveness"] = 0.1,
            ["FallbackEffectiveness"] = 0.1,
            ["PerformanceScore"] = 0.1
        };

        var score = (1.0 - indicators.ErrorRate) * weights["ErrorRate"] +
                   (1.0 - indicators.CriticalErrorRate) * weights["CriticalErrorRate"] +
                   indicators.CircuitBreakerHealth * weights["CircuitBreakerHealth"] +
                   indicators.RetryEffectiveness * weights["RetryEffectiveness"] +
                   indicators.FallbackEffectiveness * weights["FallbackEffectiveness"] +
                   indicators.PerformanceScore * weights["PerformanceScore"];

        return Math.Max(0, Math.Min(1.0, score));
    }

    private SystemHealthStatus DetermineHealthStatus(HealthIndicators indicators, ErrorAnalysis errorAnalysis)
    {
        // Critical conditions
        if (indicators.CriticalErrorRate > _config.CriticalErrorThreshold ||
            errorAnalysis.CriticalErrorCount > _config.MaxCriticalErrors)
        {
            return SystemHealthStatus.Critical;
        }

        // Unhealthy conditions
        if (indicators.ErrorRate > _config.ErrorRateThreshold ||
            indicators.OverallHealthScore < _config.UnhealthyScoreThreshold)
        {
            return SystemHealthStatus.Unhealthy;
        }

        // Degraded conditions
        if (indicators.ErrorRate > _config.ErrorRateThreshold * 0.5 ||
            indicators.OverallHealthScore < _config.DegradedScoreThreshold)
        {
            return SystemHealthStatus.Degraded;
        }

        return SystemHealthStatus.Healthy;
    }

    private string GetStatusMessage(SystemHealthStatus status, HealthIndicators indicators)
    {
        return status switch
        {
            SystemHealthStatus.Healthy => "System is operating normally",
            SystemHealthStatus.Degraded => $"System performance is degraded (Health Score: {indicators.OverallHealthScore:P1})",
            SystemHealthStatus.Unhealthy => $"System is unhealthy (Error Rate: {indicators.ErrorRate:P2})",
            SystemHealthStatus.Critical => $"System is in critical state (Critical Error Rate: {indicators.CriticalErrorRate:P2})",
            _ => "Unknown health status"
        };
    }

    private double CalculateOverallFailureRate(List<PolicyMetrics> policyMetrics)
    {
        var totalExecutions = policyMetrics.Sum(m => m.TotalExecutions);
        var totalFailures = policyMetrics.Sum(m => m.FailedExecutions);
        return totalExecutions > 0 ? (double)totalFailures / totalExecutions : 0.0;
    }

    private SystemMetrics CalculateSystemMetrics(ErrorAnalysis errorAnalysis, List<PolicyMetrics> policyMetrics)
    {
        return new SystemMetrics
        {
            TotalRequests = policyMetrics.Sum(m => m.TotalExecutions),
            TotalErrors = errorAnalysis.TotalErrors,
            AverageResponseTime = policyMetrics.Where(m => m.TotalExecutions > 0)
                .Average(m => m.AverageExecutionTimeMs),
            TotalRetries = policyMetrics.Sum(m => m.RetriedExecutions),
            TotalFallbacks = policyMetrics.Sum(m => m.FallbackExecutions),
            TotalCompensations = policyMetrics.Sum(m => m.CompensationExecutions),
            RejectedRequests = policyMetrics.Sum(m => m.RejectedExecutions)
        };
    }

    private List<string> GenerateRecommendations(ErrorAnalysis errorAnalysis, List<PolicyMetrics> policyMetrics)
    {
        var recommendations = new List<string>();

        // High error rate recommendations
        if (errorAnalysis.TotalErrors > _config.ErrorRateThreshold * 100)
        {
            recommendations.Add("Consider implementing more aggressive retry policies or circuit breakers");
        }

        // High failure rate in specific components
        var highFailureComponents = errorAnalysis.ErrorsByComponent
            .Where(kvp => kvp.Value > 10)
            .OrderByDescending(kvp => kvp.Value)
            .Take(3);

        foreach (var component in highFailureComponents)
        {
            recommendations.Add($"Component '{component.Key}' has high error count ({component.Value}), consider investigation");
        }

        // Performance recommendations
        var slowPolicies = policyMetrics
            .Where(m => m.AverageExecutionTimeMs > _config.MaxAcceptableResponseTimeMs)
            .OrderByDescending(m => m.AverageExecutionTimeMs);

        foreach (var policy in slowPolicies)
        {
            recommendations.Add($"Policy '{policy.Name}' has high response time ({policy.AverageExecutionTimeMs:F0}ms), consider optimization");
        }

        return recommendations;
    }

    /// <summary>
    /// Disposes the health monitor
    /// </summary>
    public void Dispose()
    {
        if (!_disposed)
        {
            _healthCheckTimer?.Dispose();
            _disposed = true;
        }
    }
}

/// <summary>
/// Configuration for the health monitor
/// </summary>
public class HealthMonitorConfiguration
{
    /// <summary>
    /// Interval for performing health checks
    /// </summary>
    public TimeSpan HealthCheckInterval { get; set; } = TimeSpan.FromMinutes(1);

    /// <summary>
    /// Time window for error analysis
    /// </summary>
    public TimeSpan AnalysisTimeWindow { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Error rate threshold for unhealthy status
    /// </summary>
    public double ErrorRateThreshold { get; set; } = 0.05; // 5%

    /// <summary>
    /// Critical error rate threshold
    /// </summary>
    public double CriticalErrorThreshold { get; set; } = 0.01; // 1%

    /// <summary>
    /// Maximum number of critical errors before critical status
    /// </summary>
    public int MaxCriticalErrors { get; set; } = 5;

    /// <summary>
    /// Health score threshold for unhealthy status
    /// </summary>
    public double UnhealthyScoreThreshold { get; set; } = 0.6;

    /// <summary>
    /// Health score threshold for degraded status
    /// </summary>
    public double DegradedScoreThreshold { get; set; } = 0.8;

    /// <summary>
    /// Maximum acceptable response time in milliseconds
    /// </summary>
    public double MaxAcceptableResponseTimeMs { get; set; } = 1000;
}

/// <summary>
/// Health indicators for the system
/// </summary>
public class HealthIndicators
{
    /// <summary>
    /// Overall error rate
    /// </summary>
    public double ErrorRate { get; set; }

    /// <summary>
    /// Critical error rate
    /// </summary>
    public double CriticalErrorRate { get; set; }

    /// <summary>
    /// Circuit breaker health score
    /// </summary>
    public double CircuitBreakerHealth { get; set; } = 1.0;

    /// <summary>
    /// Retry effectiveness score
    /// </summary>
    public double RetryEffectiveness { get; set; } = 1.0;

    /// <summary>
    /// Fallback effectiveness score
    /// </summary>
    public double FallbackEffectiveness { get; set; } = 1.0;

    /// <summary>
    /// Performance score
    /// </summary>
    public double PerformanceScore { get; set; } = 1.0;

    /// <summary>
    /// Resource utilization score
    /// </summary>
    public double ResourceUtilization { get; set; } = 1.0;

    /// <summary>
    /// Overall health score
    /// </summary>
    public double OverallHealthScore { get; set; }
}

/// <summary>
/// System health status
/// </summary>
public enum SystemHealthStatus
{
    /// <summary>
    /// System is healthy
    /// </summary>
    Healthy,

    /// <summary>
    /// System performance is degraded but functional
    /// </summary>
    Degraded,

    /// <summary>
    /// System is unhealthy
    /// </summary>
    Unhealthy,

    /// <summary>
    /// System is in critical state
    /// </summary>
    Critical
}

/// <summary>
/// Current health status
/// </summary>
public class HealthStatus
{
    /// <summary>
    /// Current system health status
    /// </summary>
    public SystemHealthStatus Status { get; set; }

    /// <summary>
    /// Status message
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Additional status details
    /// </summary>
    public Dictionary<string, object> Details { get; set; } = new();

    /// <summary>
    /// Timestamp of this status
    /// </summary>
    public DateTimeOffset Timestamp { get; set; }

    /// <summary>
    /// Health indicators
    /// </summary>
    public HealthIndicators? HealthIndicators { get; set; }
}

/// <summary>
/// Comprehensive health report
/// </summary>
public class HealthReport
{
    /// <summary>
    /// Report timestamp
    /// </summary>
    public DateTimeOffset Timestamp { get; set; }

    /// <summary>
    /// Overall system status
    /// </summary>
    public SystemHealthStatus OverallStatus { get; set; }

    /// <summary>
    /// Error analysis
    /// </summary>
    public ErrorAnalysis? ErrorAnalysis { get; set; }

    /// <summary>
    /// Policy metrics
    /// </summary>
    public List<PolicyMetrics> PolicyMetrics { get; set; } = new();

    /// <summary>
    /// System metrics
    /// </summary>
    public SystemMetrics? SystemMetrics { get; set; }

    /// <summary>
    /// Health indicators
    /// </summary>
    public HealthIndicators? HealthIndicators { get; set; }

    /// <summary>
    /// Recommendations for improvement
    /// </summary>
    public List<string> Recommendations { get; set; } = new();
}

/// <summary>
/// System-wide metrics
/// </summary>
public class SystemMetrics
{
    /// <summary>
    /// Total number of requests
    /// </summary>
    public long TotalRequests { get; set; }

    /// <summary>
    /// Total number of errors
    /// </summary>
    public long TotalErrors { get; set; }

    /// <summary>
    /// Average response time in milliseconds
    /// </summary>
    public double AverageResponseTime { get; set; }

    /// <summary>
    /// Total number of retries
    /// </summary>
    public long TotalRetries { get; set; }

    /// <summary>
    /// Total number of fallbacks
    /// </summary>
    public long TotalFallbacks { get; set; }

    /// <summary>
    /// Total number of compensations
    /// </summary>
    public long TotalCompensations { get; set; }

    /// <summary>
    /// Total number of rejected requests
    /// </summary>
    public long RejectedRequests { get; set; }
}

/// <summary>
/// Event arguments for health status changes
/// </summary>
public class HealthStatusChangedEventArgs
{
    /// <summary>
    /// Previous health status
    /// </summary>
    public SystemHealthStatus PreviousStatus { get; set; }

    /// <summary>
    /// Current health status
    /// </summary>
    public SystemHealthStatus CurrentStatus { get; set; }

    /// <summary>
    /// Detailed health information
    /// </summary>
    public HealthStatus? HealthDetails { get; set; }

    /// <summary>
    /// Timestamp of the change
    /// </summary>
    public DateTimeOffset Timestamp { get; set; }
}