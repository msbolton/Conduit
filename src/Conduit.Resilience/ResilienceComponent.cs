using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Conduit.Api;
using Conduit.Components;
using Microsoft.Extensions.Logging;

namespace Conduit.Resilience
{
    /// <summary>
    /// Resilience component that provides circuit breaker, retry, bulkhead, timeout, and rate limiting capabilities.
    /// </summary>
    [Component(
        "conduit.resilience",
        "Conduit.Resilience",
        "0.1.0",
        Description = "Provides resilience patterns including circuit breaker, retry, bulkhead, timeout, and rate limiting"
    )]
    public class ResilienceComponent : AbstractPluggableComponent, IDisposable
    {
        private readonly ILogger<ResilienceComponent>? _logger;
        private ResiliencePolicyRegistry? _policyRegistry;
        private ComponentContext? _componentContext;
        private bool _disposed;

        /// <summary>
        /// Gets the policy registry for this component.
        /// </summary>
        public ResiliencePolicyRegistry PolicyRegistry => _policyRegistry ?? throw new InvalidOperationException("Component not attached");

        /// <summary>
        /// Initializes a new instance of the ResilienceComponent class.
        /// </summary>
        public ResilienceComponent(ILogger<ResilienceComponent>? logger = null) : base(logger)
        {
            _logger = logger;

            // Override the default manifest
            Manifest = new ComponentManifest
            {
                Id = "conduit.resilience",
                Name = "Conduit.Resilience",
                Version = "0.1.0",
                Description = "Provides resilience patterns including circuit breaker, retry, bulkhead, timeout, and rate limiting",
                Author = "Conduit Contributors",
                MinFrameworkVersion = "0.1.0",
                Dependencies = new List<ComponentDependency>(),
                Tags = new HashSet<string> { "resilience", "circuit-breaker", "retry", "bulkhead", "timeout", "rate-limiting" }
            };
        }

        public override Task OnAttachAsync(ComponentContext context, CancellationToken cancellationToken = default)
        {
            _componentContext = context;
            _policyRegistry = new ResiliencePolicyRegistry(_logger as ILogger<ResiliencePolicyRegistry>);

            // Initialize default policies from configuration if provided
            if (Configuration?.Settings.TryGetValue("ResilienceConfiguration", out var configObj) == true
                && configObj is ResilienceConfiguration resilienceConfig)
            {
                InitializeDefaultPolicies(resilienceConfig);
            }

            Logger.LogInformation("Resilience component '{Name}' v{Version} attached with {PolicyCount} policies",
                Name, Version, _policyRegistry.Count);

            return base.OnAttachAsync(context, cancellationToken);
        }

        public override Task OnDetachAsync(CancellationToken cancellationToken = default)
        {
            // Dispose any rate limiter policies
            DisposePolicies();

            Logger.LogInformation("Resilience component '{Name}' detached", Name);
            return base.OnDetachAsync(cancellationToken);
        }

        public override IEnumerable<ComponentFeature> ExposeFeatures()
        {
            return new[]
            {
                new ComponentFeature
                {
                    Id = "CircuitBreaker",
                    Name = "CircuitBreaker",
                    Description = "Circuit breaker pattern to prevent cascading failures",
                    Version = Version,
                    IsEnabledByDefault = true
                },
                new ComponentFeature
                {
                    Id = "Retry",
                    Name = "Retry",
                    Description = "Retry pattern with exponential, linear, and fixed backoff strategies",
                    Version = Version,
                    IsEnabledByDefault = true
                },
                new ComponentFeature
                {
                    Id = "Bulkhead",
                    Name = "Bulkhead",
                    Description = "Bulkhead isolation pattern to limit concurrent executions",
                    Version = Version,
                    IsEnabledByDefault = true
                },
                new ComponentFeature
                {
                    Id = "Timeout",
                    Name = "Timeout",
                    Description = "Timeout pattern with optimistic and pessimistic strategies",
                    Version = Version,
                    IsEnabledByDefault = true
                },
                new ComponentFeature
                {
                    Id = "RateLimiter",
                    Name = "RateLimiter",
                    Description = "Rate limiting pattern using sliding window algorithm",
                    Version = Version,
                    IsEnabledByDefault = true
                }
            };
        }

        public override IEnumerable<ServiceContract> ProvideServices()
        {
            return new[]
            {
                new ServiceContract
                {
                    ServiceType = typeof(ResiliencePolicyRegistry),
                    ImplementationType = _policyRegistry?.GetType() ?? typeof(ResiliencePolicyRegistry),
                    Lifetime = ServiceLifetime.Singleton,
                    Factory = _ => _policyRegistry ?? new ResiliencePolicyRegistry()
                }
            };
        }

        public override bool IsCompatibleWith(string coreVersion)
        {
            // Simple version compatibility check - in production, use proper semantic versioning
            return Version.CompareTo(coreVersion) >= 0;
        }

        /// <summary>
        /// Creates a circuit breaker policy and adds it to the registry.
        /// </summary>
        /// <param name="name">The policy name</param>
        /// <param name="config">The circuit breaker configuration</param>
        /// <returns>The created policy</returns>
        public CircuitBreakerPolicy CreateCircuitBreakerPolicy(
            string name,
            ResilienceConfiguration.CircuitBreakerConfig config)
        {
            var policy = new CircuitBreakerPolicy(name, config, _logger as ILogger<CircuitBreakerPolicy>);
            _policyRegistry?.AddPolicy(policy);
            return policy;
        }

        /// <summary>
        /// Creates a retry policy and adds it to the registry.
        /// </summary>
        /// <param name="name">The policy name</param>
        /// <param name="config">The retry configuration</param>
        /// <returns>The created policy</returns>
        public RetryPolicy CreateRetryPolicy(
            string name,
            ResilienceConfiguration.RetryConfig config)
        {
            var policy = new RetryPolicy(name, config, _logger as ILogger<RetryPolicy>);
            _policyRegistry?.AddPolicy(policy);
            return policy;
        }

        /// <summary>
        /// Creates a bulkhead policy and adds it to the registry.
        /// </summary>
        /// <param name="name">The policy name</param>
        /// <param name="config">The bulkhead configuration</param>
        /// <returns>The created policy</returns>
        public BulkheadPolicy CreateBulkheadPolicy(
            string name,
            ResilienceConfiguration.BulkheadConfig config)
        {
            var policy = new BulkheadPolicy(name, config, _logger as ILogger<BulkheadPolicy>);
            _policyRegistry?.AddPolicy(policy);
            return policy;
        }

        /// <summary>
        /// Creates a timeout policy and adds it to the registry.
        /// </summary>
        /// <param name="name">The policy name</param>
        /// <param name="config">The timeout configuration</param>
        /// <returns>The created policy</returns>
        public TimeoutPolicy CreateTimeoutPolicy(
            string name,
            ResilienceConfiguration.TimeoutConfig config)
        {
            var policy = new TimeoutPolicy(name, config, _logger as ILogger<TimeoutPolicy>);
            _policyRegistry?.AddPolicy(policy);
            return policy;
        }

        /// <summary>
        /// Creates a rate limiter policy and adds it to the registry.
        /// </summary>
        /// <param name="name">The policy name</param>
        /// <param name="config">The rate limiter configuration</param>
        /// <returns>The created policy</returns>
        public RateLimiterPolicy CreateRateLimiterPolicy(
            string name,
            ResilienceConfiguration.RateLimiterConfig config)
        {
            var policy = new RateLimiterPolicy(name, config, _logger as ILogger<RateLimiterPolicy>);
            _policyRegistry?.AddPolicy(policy);
            return policy;
        }

        private void InitializeDefaultPolicies(ResilienceConfiguration config)
        {
            if (config.CircuitBreaker.Enabled)
            {
                CreateCircuitBreakerPolicy("default-circuit-breaker", config.CircuitBreaker);
            }

            if (config.Retry.Enabled)
            {
                CreateRetryPolicy("default-retry", config.Retry);
            }

            if (config.Bulkhead.Enabled)
            {
                CreateBulkheadPolicy("default-bulkhead", config.Bulkhead);
            }

            if (config.Timeout.Enabled)
            {
                CreateTimeoutPolicy("default-timeout", config.Timeout);
            }

            if (config.RateLimiter.Enabled)
            {
                CreateRateLimiterPolicy("default-rate-limiter", config.RateLimiter);
            }

            _logger?.LogInformation("Initialized {Count} default resilience policies", _policyRegistry?.Count ?? 0);
        }

        protected override Task OnStartAsync(CancellationToken cancellationToken = default)
        {
            Logger.LogInformation("Resilience component '{Name}' started", Name);
            return Task.CompletedTask;
        }

        protected override Task OnStopAsync(CancellationToken cancellationToken = default)
        {
            Logger.LogInformation("Resilience component '{Name}' stopped", Name);
            return Task.CompletedTask;
        }

        protected override Task OnInitializeAsync(CancellationToken cancellationToken = default)
        {
            Logger.LogInformation("Resilience component '{Name}' initialized", Name);
            return Task.CompletedTask;
        }

        protected override Task OnDisposeAsync()
        {
            if (!_disposed)
            {
                DisposePolicies();
                _disposed = true;
                Logger.LogInformation("Resilience component '{Name}' disposed", Name);
            }
            return Task.CompletedTask;
        }

        public override Task<ComponentHealth> CheckHealth(CancellationToken cancellationToken = default)
        {
            var currentState = GetState();
            var isHealthy = currentState == ComponentState.Running;
            var healthData = new Dictionary<string, object>
            {
                ["State"] = currentState.ToString(),
                ["PolicyCount"] = _policyRegistry?.Count ?? 0
            };

            var health = isHealthy
                ? ComponentHealth.Healthy(Id, healthData)
                : ComponentHealth.Degraded(Id, $"Component state: {currentState}", healthData);

            return Task.FromResult(health);
        }

        protected override void CollectMetrics(ComponentMetrics metrics)
        {
            metrics.SetCounter("policies_count", _policyRegistry?.Count ?? 0);
            metrics.SetGauge("component_state", (int)GetState());
        }

        private void DisposePolicies()
        {
            if (_policyRegistry != null)
            {
                foreach (var policyName in _policyRegistry.PolicyNames)
                {
                    var policy = _policyRegistry.GetPolicy(policyName);
                    if (policy is IDisposable disposable)
                    {
                        disposable.Dispose();
                    }
                }
            }
        }

        protected override ComponentHealth? PerformHealthCheck()
        {
            var currentState = GetState();
            var isHealthy = currentState == ComponentState.Running;
            var healthData = new Dictionary<string, object>
            {
                ["State"] = currentState.ToString(),
                ["PolicyCount"] = _policyRegistry?.Count ?? 0
            };

            return isHealthy
                ? ComponentHealth.Healthy(Id, healthData)
                : ComponentHealth.Degraded(Id, $"Component state: {currentState}", healthData);
        }
    }
}
