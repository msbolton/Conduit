# Conduit.Resilience

Resilience patterns for the Conduit messaging framework including circuit breaker, retry, bulkhead, timeout, and rate limiting capabilities.

## Features

### Circuit Breaker Pattern
Prevents cascading failures by opening the circuit when failure thresholds are exceeded.

**Features:**
- Advanced circuit breaker with failure rate threshold (not just consecutive failures)
- Three states: Closed, Open, HalfOpen
- Manual isolation capability
- Configurable failure thresholds and wait durations
- Comprehensive metrics tracking

**Configuration:**
```csharp
var config = new ResilienceConfiguration.CircuitBreakerConfig
{
    Enabled = true,
    FailureThreshold = 5,
    SuccessThreshold = 3,
    WaitDurationInOpenState = TimeSpan.FromSeconds(30),
    MinimumThroughput = 10,
    FailureRateThreshold = 0.5 // 50%
};

var policy = new CircuitBreakerPolicy("my-circuit-breaker", config, logger);
```

### Retry Pattern
Automatically retries failed operations with configurable backoff strategies.

**Features:**
- Three backoff strategies: Fixed, Linear, Exponential
- Configurable jitter to prevent thundering herd
- Max retry attempts and wait duration caps
- Tracks retry metrics and success after retry

**Configuration:**
```csharp
var config = new ResilienceConfiguration.RetryConfig
{
    Enabled = true,
    MaxAttempts = 3,
    WaitDuration = TimeSpan.FromMilliseconds(100),
    Strategy = BackoffStrategy.Exponential,
    MaxWaitDuration = TimeSpan.FromSeconds(30),
    BackoffMultiplier = 2.0,
    UseJitter = true
};

var policy = new RetryPolicy("my-retry", config, logger);
```

### Bulkhead Pattern
Limits concurrent executions to prevent resource exhaustion.

**Features:**
- Configurable max concurrent calls
- Request queuing with queue limits
- Tracks concurrent execution metrics
- Queue overflow handling

**Configuration:**
```csharp
var config = new ResilienceConfiguration.BulkheadConfig
{
    Enabled = true,
    MaxConcurrentCalls = 10,
    MaxQueuedCalls = 20,
    MaxWaitDuration = TimeSpan.FromSeconds(30)
};

var policy = new BulkheadPolicy("my-bulkhead", config, logger);
```

### Timeout Pattern
Enforces timeout limits on operations.

**Features:**
- Optimistic timeout strategy (assumes delegate doesn't support cancellation)
- Pessimistic timeout strategy (actively cancels delegate)
- Configurable timeout duration
- Tracks timeout metrics

**Configuration:**
```csharp
var config = new ResilienceConfiguration.TimeoutConfig
{
    Enabled = true,
    Duration = TimeSpan.FromSeconds(30),
    Strategy = TimeoutStrategy.Pessimistic
};

var policy = new TimeoutPolicy("my-timeout", config, logger);
```

### Rate Limiter Pattern
Controls the rate of executions using a sliding window algorithm.

**Features:**
- Sliding window rate limiting
- Configurable permit limits and time windows
- Request queuing
- Tracks rejection metrics

**Configuration:**
```csharp
var config = new ResilienceConfiguration.RateLimiterConfig
{
    Enabled = true,
    MaxPermits = 100,
    Window = TimeSpan.FromMinutes(1),
    QueueLimit = 10
};

var policy = new RateLimiterPolicy("my-rate-limiter", config, logger);
```

## Usage

### Basic Usage

```csharp
// Create a policy
var retryPolicy = new RetryPolicy("api-retry", new ResilienceConfiguration.RetryConfig
{
    MaxAttempts = 3,
    Strategy = BackoffStrategy.Exponential
}, logger);

// Execute an action
await retryPolicy.ExecuteAsync(async ct =>
{
    await CallExternalApiAsync(ct);
}, cancellationToken);

// Execute a function
var result = await retryPolicy.ExecuteAsync(async ct =>
{
    return await FetchDataAsync(ct);
}, cancellationToken);
```

### Using the Policy Registry

```csharp
// Create registry
var registry = new ResiliencePolicyRegistry(logger);

// Add policies
registry.AddPolicy(new RetryPolicy("retry", retryConfig, logger));
registry.AddPolicy(new CircuitBreakerPolicy("circuit-breaker", circuitConfig, logger));
registry.AddPolicy(new TimeoutPolicy("timeout", timeoutConfig, logger));

// Execute with a single policy
await registry.ExecuteAsync("retry", async ct =>
{
    await DoWorkAsync(ct);
}, cancellationToken);

// Execute with composed policies (chained)
await registry.ExecuteWithComposedPoliciesAsync(
    new[] { "timeout", "retry", "circuit-breaker" },
    async ct => await DoWorkAsync(ct),
    cancellationToken);
```

### Policy Composition

Policies can be composed (chained) to combine multiple resilience patterns:

```csharp
// Apply timeout → retry → circuit breaker → your operation
await registry.ExecuteWithComposedPoliciesAsync(
    new[] { "timeout", "retry", "circuit-breaker" },
    async ct => await CallServiceAsync(ct),
    cancellationToken);
```

The order matters: policies are applied from left to right (outermost to innermost).

### Using the Resilience Component

```csharp
// Create component
var component = new ResilienceComponent(logger);

// Attach to Conduit framework
await component.OnAttachAsync(componentContext);

// Create policies using factory methods
var circuitBreaker = component.CreateCircuitBreakerPolicy("cb", circuitConfig);
var retry = component.CreateRetryPolicy("retry", retryConfig);

// Access registry
var registry = component.PolicyRegistry;
await registry.ExecuteAsync("cb", async ct => await DoWorkAsync(ct));
```

## Metrics

All policies track comprehensive metrics:

```csharp
var metrics = policy.GetMetrics();

Console.WriteLine($"Total Executions: {metrics.TotalExecutions}");
Console.WriteLine($"Successful: {metrics.SuccessfulExecutions}");
Console.WriteLine($"Failed: {metrics.FailedExecutions}");
Console.WriteLine($"Rejected: {metrics.RejectedExecutions}");
Console.WriteLine($"Average Time: {metrics.AverageExecutionTimeMs}ms");
Console.WriteLine($"Failure Rate: {metrics.FailureRate:P2}");
Console.WriteLine($"Success Rate: {metrics.SuccessRate:P2}");

// Pattern-specific metrics
if (metrics.AdditionalMetrics is CircuitBreakerMetrics cbMetrics)
{
    Console.WriteLine($"Circuit State: {cbMetrics.State}");
    Console.WriteLine($"Circuit Opened Count: {cbMetrics.CircuitOpenedCount}");
}
```

### Registry Metrics

```csharp
// Get all metrics
var allMetrics = registry.GetAllMetrics();
foreach (var metric in allMetrics)
{
    Console.WriteLine($"{metric.Name}: {metric.SuccessRate:P2} success rate");
}

// Get summary
var summary = registry.GetSummary();
Console.WriteLine($"Total Policies: {summary.TotalPolicies}");
Console.WriteLine($"Enabled: {summary.EnabledPolicies}");
Console.WriteLine($"Circuit Breakers: {summary.PoliciesByPattern[ResiliencePattern.CircuitBreaker]}");
```

## Integration with Polly

This module uses the [Polly](https://www.pollly.dev/) library (v8.2.0) for resilience policy implementations:

- `CircuitBreakerPolicy` uses `AsyncCircuitBreakerPolicy`
- `RetryPolicy` uses `AsyncRetryPolicy`
- `BulkheadPolicy` uses `AsyncBulkheadPolicy`
- `TimeoutPolicy` uses `AsyncTimeoutPolicy`
- `RateLimiterPolicy` uses `System.Threading.RateLimiting.SlidingWindowRateLimiter`

## Dependencies

- Polly (>= 8.2.0)
- Polly.Extensions (>= 8.2.0)
- Microsoft.Extensions.Logging.Abstractions (>= 8.0.0)
- Conduit.Common
- Conduit.Api

## Best Practices

1. **Always use policy composition in the right order**: Timeout should be outermost, then retry, then circuit breaker
2. **Configure appropriate thresholds**: Don't set failure thresholds too low or retry attempts too high
3. **Monitor metrics**: Regularly check policy metrics to identify issues
4. **Use jitter for retries**: Prevents thundering herd problem
5. **Reset policies when needed**: Use `policy.Reset()` to clear state during testing or after incidents
6. **Dispose rate limiters**: RateLimiterPolicy implements IDisposable - ensure proper cleanup

## Examples

### HTTP Client with Full Resilience

```csharp
var timeout = new TimeoutPolicy("http-timeout", new ResilienceConfiguration.TimeoutConfig
{
    Duration = TimeSpan.FromSeconds(30)
}, logger);

var retry = new RetryPolicy("http-retry", new ResilienceConfiguration.RetryConfig
{
    MaxAttempts = 3,
    Strategy = BackoffStrategy.Exponential,
    UseJitter = true
}, logger);

var circuitBreaker = new CircuitBreakerPolicy("http-cb", new ResilienceConfiguration.CircuitBreakerConfig
{
    FailureRateThreshold = 0.5,
    MinimumThroughput = 10,
    WaitDurationInOpenState = TimeSpan.FromSeconds(60)
}, logger);

var registry = new ResiliencePolicyRegistry(logger);
registry.AddPolicy(timeout);
registry.AddPolicy(retry);
registry.AddPolicy(circuitBreaker);

// Execute with all three policies
var data = await registry.ExecuteWithComposedPoliciesAsync(
    new[] { "http-timeout", "http-retry", "http-cb" },
    async ct => await httpClient.GetStringAsync(url, ct),
    cancellationToken);
```

### Database Operations with Bulkhead

```csharp
var bulkhead = new BulkheadPolicy("db-bulkhead", new ResilienceConfiguration.BulkheadConfig
{
    MaxConcurrentCalls = 20,
    MaxQueuedCalls = 50
}, logger);

await bulkhead.ExecuteAsync(async ct =>
{
    await ExecuteDatabaseQueryAsync(ct);
}, cancellationToken);
```

### API Rate Limiting

```csharp
var rateLimiter = new RateLimiterPolicy("api-rate-limit", new ResilienceConfiguration.RateLimiterConfig
{
    MaxPermits = 100,
    Window = TimeSpan.FromMinutes(1)
}, logger);

await rateLimiter.ExecuteAsync(async ct =>
{
    await ProcessApiRequestAsync(ct);
}, cancellationToken);
```

## Version

0.1.0

## License

See LICENSE file in the repository root.
