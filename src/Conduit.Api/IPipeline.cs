namespace Conduit.Api;

/// <summary>
/// Represents a pipeline for processing input and producing output.
/// </summary>
/// <typeparam name="TIn">The input type</typeparam>
/// <typeparam name="TOut">The output type</typeparam>
public interface IPipeline<in TIn, TOut>
{
    /// <summary>
    /// Executes the pipeline with the given input.
    /// </summary>
    /// <param name="input">The input to process</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The pipeline output</returns>
    Task<TOut> ExecuteAsync(TIn input, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the pipeline name.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets the pipeline ID.
    /// </summary>
    string Id { get; }

    /// <summary>
    /// Gets a value indicating whether the pipeline is enabled.
    /// </summary>
    bool IsEnabled { get; }

    /// <summary>
    /// Adds a behavior to the pipeline.
    /// </summary>
    /// <param name="behavior">The behavior to add</param>
    void AddBehavior(IBehaviorContribution behavior);

    /// <summary>
    /// Removes a behavior from the pipeline.
    /// </summary>
    /// <param name="behaviorId">The ID of the behavior to remove</param>
    bool RemoveBehavior(string behaviorId);

    /// <summary>
    /// Gets all behaviors in the pipeline.
    /// </summary>
    IReadOnlyList<IBehaviorContribution> GetBehaviors();

    /// <summary>
    /// Clears all behaviors from the pipeline.
    /// </summary>
    void ClearBehaviors();

    /// <summary>
    /// Creates a new pipeline by composing this pipeline with another.
    /// </summary>
    /// <typeparam name="TNext">The output type of the next pipeline</typeparam>
    /// <param name="next">The next pipeline to compose with</param>
    /// <returns>A new composed pipeline</returns>
    IPipeline<TIn, TNext> Then<TNext>(IPipeline<TOut, TNext> next);

    /// <summary>
    /// Creates a new pipeline that applies a transformation to the output.
    /// </summary>
    /// <typeparam name="TNext">The transformed output type</typeparam>
    /// <param name="transform">The transformation function</param>
    /// <returns>A new pipeline with transformed output</returns>
    IPipeline<TIn, TNext> Map<TNext>(Func<TOut, Task<TNext>> transform);

    /// <summary>
    /// Creates a new pipeline that filters the input.
    /// </summary>
    /// <param name="predicate">The filter predicate</param>
    /// <returns>A new pipeline with filtering</returns>
    IPipeline<TIn, TOut> Where(Func<TIn, bool> predicate);

    /// <summary>
    /// Creates a pipeline with error handling.
    /// </summary>
    /// <param name="errorHandler">The error handler</param>
    /// <returns>A new pipeline with error handling</returns>
    IPipeline<TIn, TOut> WithErrorHandling(Func<Exception, TIn, Task<TOut>> errorHandler);

    /// <summary>
    /// Creates a pipeline with retry logic.
    /// </summary>
    /// <param name="maxRetries">Maximum number of retries</param>
    /// <param name="retryDelay">Delay between retries</param>
    /// <returns>A new pipeline with retry logic</returns>
    IPipeline<TIn, TOut> WithRetry(int maxRetries, TimeSpan retryDelay);

    /// <summary>
    /// Creates a pipeline with timeout.
    /// </summary>
    /// <param name="timeout">The timeout duration</param>
    /// <returns>A new pipeline with timeout</returns>
    IPipeline<TIn, TOut> WithTimeout(TimeSpan timeout);

    /// <summary>
    /// Creates a pipeline with caching.
    /// </summary>
    /// <param name="cacheKeySelector">Function to generate cache key from input</param>
    /// <param name="cacheDuration">Cache duration</param>
    /// <returns>A new pipeline with caching</returns>
    IPipeline<TIn, TOut> WithCache(Func<TIn, string> cacheKeySelector, TimeSpan cacheDuration);
}