using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Conduit.Api;
using Microsoft.Extensions.Logging;

namespace Conduit.Resilience
{
    /// <summary>
    /// Resilience component that provides circuit breaker, retry, bulkhead, timeout, and rate limiting capabilities.
    /// </summary>
    [Component(
        Name = "Conduit.Resilience",
        Version = "0.1.0",
        Description = "Provides resilience patterns including circuit breaker, retry, bulkhead, timeout, and rate limiting"
    )]
    public class ResilienceComponent : IPluggableComponent
    {
        private readonly ILogger<ResilienceComponent>? _logger;
        private ResiliencePolicyRegistry? _policyRegistry;
        private ComponentContext? _componentContext;

        /// <inheritdoc/>
        public string Id { get; } = "conduit.resilience";

        /// <inheritdoc/>
        public string Name { get; } = "Conduit.Resilience";

        /// <inheritdoc/>
        public string Version { get; } = "0.1.0";

        /// <inheritdoc/>
        public string Description { get; } = "Provides resilience patterns including circuit breaker, retry, bulkhead, timeout, and rate limiting";

        /// <inheritdoc/>
        public ComponentConfiguration? Configuration { get; set; }

        /// <inheritdoc/>
        public ISecurityContext? SecurityContext { get; set; }

        /// <inheritdoc/>
        public ComponentManifest Manifest { get; }

        /// <inheritdoc/>
        public IsolationRequirements IsolationRequirements { get; }

        /// <summary>
        /// Gets the policy registry for this component.
        /// </summary>
        public ResiliencePolicyRegistry PolicyRegistry => _policyRegistry ?? throw new InvalidOperationException("Component not attached");

        /// <summary>
        /// Initializes a new instance of the ResilienceComponent class.
        /// </summary>
        public ResilienceComponent(ILogger<ResilienceComponent>? logger = null)
        {
            _logger = logger;

            Manifest = new ComponentManifest
            {
                ComponentId = Id,
                Name = Name,
                Version = Version,
                Description = Description,
                Author = "Conduit Contributors",
                MinimumCoreVersion = "0.1.0",
                Dependencies = Array.Empty<string>(),
                Tags = new[] { "resilience", "circuit-breaker", "retry", "bulkhead", "timeout", "rate-limiting" }
            };

            IsolationRequirements = new IsolationRequirements
            {
                RequiresSeparateAppDomain = false,
                RequiresNetworkAccess = false,
                RequiresFileSystemAccess = false,
                TrustLevel = TrustLevel.Full
            };
        }

        /// <inheritdoc/>
        public Task OnAttachAsync(ComponentContext context, CancellationToken cancellationToken = default)
        {
            _componentContext = context;
            _policyRegistry = new ResiliencePolicyRegistry(_logger as ILogger<ResiliencePolicyRegistry>);

            // Initialize default policies from configuration if provided
            if (Configuration?.Settings.TryGetValue("ResilienceConfiguration", out var configObj) == true
                && configObj is ResilienceConfiguration resilienceConfig)
            {
                InitializeDefaultPolicies(resilienceConfig);
            }

            _logger?.LogInformation("Resilience component '{Name}' v{Version} attached with {PolicyCount} policies",
                Name, Version, _policyRegistry.Count);

            return Task.CompletedTask;
        }

        /// <inheritdoc/>
        public Task OnDetachAsync(CancellationToken cancellationToken = default)
        {
            // Dispose any rate limiter policies
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

            _logger?.LogInformation("Resilience component '{Name}' detached", Name);
            return Task.CompletedTask;
        }

        /// <inheritdoc/>
        public IEnumerable<IBehaviorContribution> ContributeBehaviors()
        {
            // Resilience policies are typically applied explicitly via the registry
            // rather than as automatic pipeline behaviors
            return Array.Empty<IBehaviorContribution>();
        }

        /// <inheritdoc/>
        public IEnumerable<ComponentFeature> ExposeFeatures()
        {
            return new[]
            {
                new ComponentFeature
                {
                    Name = "CircuitBreaker",
                    Description = "Circuit breaker pattern to prevent cascading failures",
                    Version = Version,
                    IsEnabled = true
                },
                new ComponentFeature
                {
                    Name = "Retry",
                    Description = "Retry pattern with exponential, linear, and fixed backoff strategies",
                    Version = Version,
                    IsEnabled = true
                },
                new ComponentFeature
                {
                    Name = "Bulkhead",
                    Description = "Bulkhead isolation pattern to limit concurrent executions",
                    Version = Version,
                    IsEnabled = true
                },
                new ComponentFeature
                {
                    Name = "Timeout",
                    Description = "Timeout pattern with optimistic and pessimistic strategies",
                    Version = Version,
                    IsEnabled = true
                },
                new ComponentFeature
                {
                    Name = "RateLimiter",
                    Description = "Rate limiting pattern using sliding window algorithm",
                    Version = Version,
                    IsEnabled = true
                }
            };
        }

        /// <inheritdoc/>
        public IEnumerable<ServiceContract> ProvideServices()
        {
            return new[]
            {
                new ServiceContract
                {
                    ServiceType = typeof(ResiliencePolicyRegistry),
                    ImplementationType = _policyRegistry?.GetType() ?? typeof(ResiliencePolicyRegistry),
                    ServiceInstance = _policyRegistry,
                    Lifetime = ServiceLifetime.Singleton
                }
            };
        }

        /// <inheritdoc/>
        public IEnumerable<MessageHandlerRegistration> RegisterHandlers()
        {
            // Resilience component doesn't register message handlers
            return Array.Empty<MessageHandlerRegistration>();
        }

        /// <inheritdoc/>
        public bool IsCompatibleWith(string coreVersion)
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
    }
}
