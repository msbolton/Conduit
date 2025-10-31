using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Conduit.Common;
using Microsoft.Extensions.Logging;

namespace Conduit.Resilience
{
    /// <summary>
    /// Registry for managing multiple resilience policies.
    /// Provides centralized policy management and composition.
    /// </summary>
    public class ResiliencePolicyRegistry : IResiliencePolicyRegistry
    {
        private readonly ConcurrentDictionary<string, IResiliencePolicy> _policies;
        private readonly ILogger? _logger;

        /// <summary>
        /// Gets the number of registered policies.
        /// </summary>
        public int Count => _policies.Count;

        /// <summary>
        /// Gets all registered policy names.
        /// </summary>
        public IEnumerable<string> PolicyNames => _policies.Keys;

        /// <summary>
        /// Initializes a new instance of the ResiliencePolicyRegistry class.
        /// </summary>
        public ResiliencePolicyRegistry(ILogger<ResiliencePolicyRegistry>? logger = null)
        {
            _policies = new ConcurrentDictionary<string, IResiliencePolicy>();
            _logger = logger;
        }

        /// <summary>
        /// Adds a policy to the registry.
        /// </summary>
        /// <param name="policy">The policy to add</param>
        /// <returns>True if added successfully, false if policy with same name already exists</returns>
        public bool AddPolicy(IResiliencePolicy policy)
        {
            Guard.AgainstNull(policy, nameof(policy));

            var added = _policies.TryAdd(policy.Name, policy);
            if (added)
            {
                _logger?.LogInformation("Policy '{Name}' ({Pattern}) added to registry", policy.Name, policy.Pattern);
            }
            else
            {
                _logger?.LogWarning("Policy '{Name}' already exists in registry", policy.Name);
            }

            return added;
        }

        /// <summary>
        /// Gets a policy by name.
        /// </summary>
        /// <param name="name">The policy name</param>
        /// <returns>The policy if found, null otherwise</returns>
        public IResiliencePolicy? GetPolicy(string name)
        {
            Guard.AgainstNullOrEmpty(name, nameof(name));

            _policies.TryGetValue(name, out var policy);
            return policy;
        }

        /// <summary>
        /// Gets a policy by name, throwing if not found.
        /// </summary>
        /// <param name="name">The policy name</param>
        /// <returns>The policy</returns>
        /// <exception cref="InvalidOperationException">Thrown when policy not found</exception>
        public IResiliencePolicy GetRequiredPolicy(string name)
        {
            var policy = GetPolicy(name);
            if (policy == null)
            {
                throw new InvalidOperationException($"Policy '{name}' not found in registry");
            }
            return policy;
        }

        /// <summary>
        /// Registers a policy by name.
        /// </summary>
        /// <param name="name">The policy name</param>
        /// <param name="policy">The policy to register</param>
        public void Register(string name, IResiliencePolicy policy)
        {
            Guard.AgainstNullOrEmpty(name, nameof(name));
            Guard.AgainstNull(policy, nameof(policy));

            _policies[name] = policy;
            _logger?.LogInformation("Policy '{Name}' registered in registry", name);
        }

        /// <summary>
        /// Removes a policy from the registry.
        /// </summary>
        /// <param name="name">The policy name</param>
        /// <returns>True if removed successfully, false if not found</returns>
        public bool RemovePolicy(string name)
        {
            Guard.AgainstNullOrEmpty(name, nameof(name));

            var removed = _policies.TryRemove(name, out var policy);
            if (removed)
            {
                _logger?.LogInformation("Policy '{Name}' removed from registry", name);
            }
            else
            {
                _logger?.LogWarning("Policy '{Name}' not found in registry for removal", name);
            }

            return removed;
        }

        /// <summary>
        /// Removes all policies from the registry.
        /// </summary>
        public void Clear()
        {
            var count = _policies.Count;
            _policies.Clear();
            _logger?.LogInformation("Cleared {Count} policies from registry", count);
        }

        /// <summary>
        /// Gets policies by pattern type.
        /// </summary>
        /// <param name="pattern">The resilience pattern</param>
        /// <returns>All policies matching the pattern</returns>
        public IEnumerable<IResiliencePolicy> GetPoliciesByPattern(ResiliencePattern pattern)
        {
            return _policies.Values.Where(p => p.Pattern == pattern);
        }

        /// <summary>
        /// Executes an action with a specific policy.
        /// </summary>
        /// <param name="policyName">The policy name</param>
        /// <param name="action">The action to execute</param>
        /// <param name="cancellationToken">Cancellation token</param>
        public async Task ExecuteAsync(string policyName, Func<CancellationToken, Task> action, CancellationToken cancellationToken = default)
        {
            var policy = GetRequiredPolicy(policyName);
            await policy.ExecuteAsync(action, cancellationToken);
        }

        /// <summary>
        /// Executes a function with a specific policy.
        /// </summary>
        /// <typeparam name="TResult">The result type</typeparam>
        /// <param name="policyName">The policy name</param>
        /// <param name="func">The function to execute</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>The result</returns>
        public async Task<TResult> ExecuteAsync<TResult>(string policyName, Func<CancellationToken, Task<TResult>> func, CancellationToken cancellationToken = default)
        {
            var policy = GetRequiredPolicy(policyName);
            return await policy.ExecuteAsync(func, cancellationToken);
        }

        /// <summary>
        /// Executes an action with composed policies (multiple policies chained together).
        /// Policies are applied in the order specified.
        /// </summary>
        /// <param name="policyNames">The policy names to compose</param>
        /// <param name="action">The action to execute</param>
        /// <param name="cancellationToken">Cancellation token</param>
        public async Task ExecuteWithComposedPoliciesAsync(
            IEnumerable<string> policyNames,
            Func<CancellationToken, Task> action,
            CancellationToken cancellationToken = default)
        {
            Guard.AgainstNull(policyNames, nameof(policyNames));
            Guard.AgainstNull(action, nameof(action));

            var policies = policyNames.Select(GetRequiredPolicy).ToList();

            if (policies.Count == 0)
            {
                await action(cancellationToken);
                return;
            }

            // Build nested policy execution (outermost policy first)
            Func<CancellationToken, Task> execution = action;
            for (int i = policies.Count - 1; i >= 0; i--)
            {
                var policy = policies[i];
                var currentExecution = execution;
                execution = ct => policy.ExecuteAsync(currentExecution, ct);
            }

            await execution(cancellationToken);
        }

        /// <summary>
        /// Executes a function with composed policies (multiple policies chained together).
        /// Policies are applied in the order specified.
        /// </summary>
        /// <typeparam name="TResult">The result type</typeparam>
        /// <param name="policyNames">The policy names to compose</param>
        /// <param name="func">The function to execute</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>The result</returns>
        public async Task<TResult> ExecuteWithComposedPoliciesAsync<TResult>(
            IEnumerable<string> policyNames,
            Func<CancellationToken, Task<TResult>> func,
            CancellationToken cancellationToken = default)
        {
            Guard.AgainstNull(policyNames, nameof(policyNames));
            Guard.AgainstNull(func, nameof(func));

            var policies = policyNames.Select(GetRequiredPolicy).ToList();

            if (policies.Count == 0)
            {
                return await func(cancellationToken);
            }

            // Build nested policy execution (outermost policy first)
            Func<CancellationToken, Task<TResult>> execution = func;
            for (int i = policies.Count - 1; i >= 0; i--)
            {
                var policy = policies[i];
                var currentExecution = execution;
                execution = ct => policy.ExecuteAsync(currentExecution, ct);
            }

            return await execution(cancellationToken);
        }

        /// <summary>
        /// Gets aggregated metrics from all policies.
        /// </summary>
        /// <returns>Metrics for all policies</returns>
        public IEnumerable<PolicyMetrics> GetAllMetrics()
        {
            return _policies.Values.Select(p => p.GetMetrics());
        }

        /// <summary>
        /// Gets metrics for a specific policy.
        /// </summary>
        /// <param name="policyName">The policy name</param>
        /// <returns>The policy metrics</returns>
        public PolicyMetrics GetMetrics(string policyName)
        {
            var policy = GetRequiredPolicy(policyName);
            return policy.GetMetrics();
        }

        /// <summary>
        /// Resets all policies.
        /// </summary>
        public void ResetAll()
        {
            foreach (var policy in _policies.Values)
            {
                policy.Reset();
            }
            _logger?.LogInformation("Reset all {Count} policies in registry", _policies.Count);
        }

        /// <summary>
        /// Resets a specific policy.
        /// </summary>
        /// <param name="policyName">The policy name</param>
        public void Reset(string policyName)
        {
            var policy = GetRequiredPolicy(policyName);
            policy.Reset();
        }

        /// <summary>
        /// Gets a summary of all policies in the registry.
        /// </summary>
        /// <returns>Summary information</returns>
        public RegistrySummary GetSummary()
        {
            var policies = _policies.Values.ToList();
            return new RegistrySummary
            {
                TotalPolicies = policies.Count,
                EnabledPolicies = policies.Count(p => p.IsEnabled),
                DisabledPolicies = policies.Count(p => !p.IsEnabled),
                PoliciesByPattern = policies
                    .GroupBy(p => p.Pattern)
                    .ToDictionary(g => g.Key, g => g.Count())
            };
        }

        /// <summary>
        /// Creates a circuit breaker policy.
        /// </summary>
        public static IResiliencePolicy CreateCircuitBreaker(string name, int failureThreshold, TimeSpan timeout)
        {
            return new CircuitBreakerPolicy(name, new ResilienceConfiguration.CircuitBreakerConfig
            {
                Enabled = true,
                FailureThreshold = failureThreshold,
                WaitDurationInOpenState = timeout
            });
        }

        /// <summary>
        /// Creates a retry policy.
        /// </summary>
        public static IResiliencePolicy CreateRetry(string name, int retryCount, RetryStrategy strategy)
        {
            return new RetryPolicy(name, retryCount, strategy);
        }

        /// <summary>
        /// Creates a timeout policy.
        /// </summary>
        public static IResiliencePolicy CreateTimeout(string name, TimeSpan timeout)
        {
            return new TimeoutPolicy(name, new ResilienceConfiguration.TimeoutConfig
            {
                Enabled = true,
                Duration = timeout
            });
        }
    }

    /// <summary>
    /// Summary information for the policy registry.
    /// </summary>
    public class RegistrySummary
    {
        /// <summary>
        /// Gets or sets the total number of policies.
        /// </summary>
        public int TotalPolicies { get; set; }

        /// <summary>
        /// Gets or sets the number of enabled policies.
        /// </summary>
        public int EnabledPolicies { get; set; }

        /// <summary>
        /// Gets or sets the number of disabled policies.
        /// </summary>
        public int DisabledPolicies { get; set; }

        /// <summary>
        /// Gets or sets the count of policies grouped by pattern.
        /// </summary>
        public Dictionary<ResiliencePattern, int> PoliciesByPattern { get; set; } = new();
    }
}
