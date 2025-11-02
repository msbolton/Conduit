namespace Conduit.Resilience;

/// <summary>
/// Interface for managing multiple resilience policies.
/// Provides centralized policy management and composition.
/// </summary>
public interface IResiliencePolicyRegistry
{
    /// <summary>
    /// Gets the number of registered policies.
    /// </summary>
    int Count { get; }

    /// <summary>
    /// Gets all registered policy names.
    /// </summary>
    IEnumerable<string> PolicyNames { get; }

    /// <summary>
    /// Adds a policy to the registry.
    /// </summary>
    /// <param name="policy">The policy to add</param>
    /// <returns>True if added successfully, false if policy with same name already exists</returns>
    bool AddPolicy(IResiliencePolicy policy);

    /// <summary>
    /// Registers a policy by name.
    /// </summary>
    /// <param name="name">The policy name</param>
    /// <param name="policy">The policy to register</param>
    void Register(string name, IResiliencePolicy policy);

    /// <summary>
    /// Gets a policy by name.
    /// </summary>
    /// <param name="name">The policy name</param>
    /// <returns>The policy if found, null otherwise</returns>
    IResiliencePolicy? GetPolicy(string name);

    /// <summary>
    /// Gets a policy by name, throwing if not found.
    /// </summary>
    /// <param name="name">The policy name</param>
    /// <returns>The policy</returns>
    /// <exception cref="InvalidOperationException">Thrown when policy not found</exception>
    IResiliencePolicy GetRequiredPolicy(string name);

    /// <summary>
    /// Removes a policy from the registry.
    /// </summary>
    /// <param name="name">The policy name</param>
    /// <returns>True if removed successfully, false if not found</returns>
    bool RemovePolicy(string name);

    /// <summary>
    /// Clears all policies from the registry.
    /// </summary>
    void Clear();

    /// <summary>
    /// Gets all policies that match a specific pattern.
    /// </summary>
    IEnumerable<IResiliencePolicy> GetPoliciesByPattern(ResiliencePattern pattern);

    /// <summary>
    /// Executes an action with the specified policy.
    /// </summary>
    Task ExecuteAsync(string policyName, Func<CancellationToken, Task> action, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes a function with the specified policy.
    /// </summary>
    Task<TResult> ExecuteAsync<TResult>(string policyName, Func<CancellationToken, Task<TResult>> func, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes an action with multiple composed policies.
    /// </summary>
    Task ExecuteWithComposedPoliciesAsync(
        IEnumerable<string> policyNames,
        Func<CancellationToken, Task> action,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes a function with multiple composed policies.
    /// </summary>
    Task<TResult> ExecuteWithComposedPoliciesAsync<TResult>(
        IEnumerable<string> policyNames,
        Func<CancellationToken, Task<TResult>> func,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets metrics for all policies.
    /// </summary>
    IEnumerable<PolicyMetrics> GetAllMetrics();
}