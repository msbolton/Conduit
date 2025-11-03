using System;
using System.Threading;
using System.Threading.Tasks;

namespace Conduit.Resilience
{
    /// <summary>
    /// Base interface for all resilience policies.
    /// </summary>
    public interface IResiliencePolicy
    {
        /// <summary>
        /// Gets the name of this policy.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Gets the type of resilience pattern this policy implements.
        /// </summary>
        ResiliencePattern Pattern { get; }

        /// <summary>
        /// Gets whether this policy is enabled.
        /// </summary>
        bool IsEnabled { get; }

        /// <summary>
        /// Executes an action with this resilience policy.
        /// </summary>
        /// <param name="action">The action to execute</param>
        /// <param name="cancellationToken">Cancellation token</param>
        Task ExecuteAsync(Func<CancellationToken, Task> action, CancellationToken cancellationToken = default);

        /// <summary>
        /// Executes a function with this resilience policy.
        /// </summary>
        /// <typeparam name="TResult">The result type</typeparam>
        /// <param name="func">The function to execute</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>The result</returns>
        Task<TResult> ExecuteAsync<TResult>(Func<CancellationToken, Task<TResult>> func, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets metrics for this policy.
        /// </summary>
        PolicyMetrics GetMetrics();

        /// <summary>
        /// Resets the policy state and metrics.
        /// </summary>
        void Reset();
    }

    /// <summary>
    /// Metrics for a resilience policy.
    /// </summary>
    public class PolicyMetrics
    {
        /// <summary>
        /// Gets or sets the policy name.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the resilience pattern type.
        /// </summary>
        public ResiliencePattern Pattern { get; set; }

        /// <summary>
        /// Gets or sets the total number of executions.
        /// </summary>
        public long TotalExecutions { get; set; }

        /// <summary>
        /// Gets or sets the number of successful executions.
        /// </summary>
        public long SuccessfulExecutions { get; set; }

        /// <summary>
        /// Gets or sets the number of failed executions.
        /// </summary>
        public long FailedExecutions { get; set; }

        /// <summary>
        /// Gets or sets the number of rejected executions.
        /// </summary>
        public long RejectedExecutions { get; set; }

        /// <summary>
        /// Gets or sets the number of timeout executions.
        /// </summary>
        public long TimeoutExecutions { get; set; }

        /// <summary>
        /// Gets or sets the number of retried executions.
        /// </summary>
        public long RetriedExecutions { get; set; }

        /// <summary>
        /// Gets or sets the number of fallback executions.
        /// </summary>
        public long FallbackExecutions { get; set; }

        /// <summary>
        /// Gets or sets the number of failed fallback executions.
        /// </summary>
        public long FailedFallbackExecutions { get; set; }

        /// <summary>
        /// Gets or sets the number of compensation executions.
        /// </summary>
        public long CompensationExecutions { get; set; }

        /// <summary>
        /// Gets or sets the number of failed compensation executions.
        /// </summary>
        public long FailedCompensationExecutions { get; set; }

        /// <summary>
        /// Gets or sets the average execution time in milliseconds.
        /// </summary>
        public double AverageExecutionTimeMs { get; set; }

        /// <summary>
        /// Gets or sets additional pattern-specific metrics.
        /// </summary>
        public object? AdditionalMetrics { get; set; }

        /// <summary>
        /// Calculates the failure rate.
        /// </summary>
        public double FailureRate => TotalExecutions > 0
            ? (double)FailedExecutions / TotalExecutions
            : 0.0;

        /// <summary>
        /// Calculates the success rate.
        /// </summary>
        public double SuccessRate => TotalExecutions > 0
            ? (double)SuccessfulExecutions / TotalExecutions
            : 0.0;

        /// <summary>
        /// Calculates the fallback rate.
        /// </summary>
        public double FallbackRate => TotalExecutions > 0
            ? (double)FallbackExecutions / TotalExecutions
            : 0.0;

        /// <summary>
        /// Calculates the compensation rate.
        /// </summary>
        public double CompensationRate => TotalExecutions > 0
            ? (double)CompensationExecutions / TotalExecutions
            : 0.0;
    }
}
