using System.Collections.Generic;

namespace Conduit.Api;

/// <summary>
/// Represents a pipeline for processing input and producing output.
/// </summary>
/// <typeparam name="TIn">The input type</typeparam>
/// <typeparam name="TOut">The output type</typeparam>
public interface IPipeline<TIn, TOut>
{
    /// <summary>
    /// Executes the pipeline with the given input.
    /// </summary>
    /// <param name="input">The input to process</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The pipeline output</returns>
    Task<TOut> ExecuteAsync(TIn input, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes the pipeline with the given input and context.
    /// </summary>
    /// <param name="input">The input to process</param>
    /// <param name="context">The pipeline execution context</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The pipeline output</returns>
    Task<TOut> ExecuteAsync(TIn input, PipelineContext context, CancellationToken cancellationToken = default);

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
    /// Gets the pipeline metadata.
    /// </summary>
    PipelineMetadata Metadata { get; }

    /// <summary>
    /// Gets the pipeline configuration.
    /// </summary>
    PipelineConfiguration Configuration { get; }

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

    #region Additional Pipeline Operations

    /// <summary>
    /// Maps the output synchronously.
    /// </summary>
    /// <typeparam name="TNewOutput">The new output type</typeparam>
    /// <param name="mapper">The mapping function</param>
    /// <returns>A new pipeline with mapped output</returns>
    IPipeline<TIn, TNewOutput> Map<TNewOutput>(Func<TOut, TNewOutput> mapper);

    /// <summary>
    /// Maps the output asynchronously.
    /// </summary>
    /// <typeparam name="TNewOutput">The new output type</typeparam>
    /// <param name="asyncMapper">The async mapping function</param>
    /// <returns>A new pipeline with mapped output</returns>
    IPipeline<TIn, TNewOutput> MapAsync<TNewOutput>(Func<TOut, Task<TNewOutput>> asyncMapper);

    /// <summary>
    /// Chains a synchronous processor after this pipeline.
    /// </summary>
    /// <typeparam name="TNewOutput">The output type after processing</typeparam>
    /// <param name="processor">The processor function</param>
    /// <returns>A new pipeline with the processor chained</returns>
    IPipeline<TIn, TNewOutput> Then<TNewOutput>(Func<TOut, TNewOutput> processor);

    /// <summary>
    /// Chains an asynchronous processor after this pipeline.
    /// </summary>
    /// <typeparam name="TNewOutput">The output type after processing</typeparam>
    /// <param name="asyncProcessor">The async processor function</param>
    /// <returns>A new pipeline with the processor chained</returns>
    IPipeline<TIn, TNewOutput> ThenAsync<TNewOutput>(Func<TOut, Task<TNewOutput>> asyncProcessor);

    /// <summary>
    /// Filters the output based on a predicate.
    /// </summary>
    /// <param name="predicate">The filter predicate</param>
    /// <returns>A new pipeline with filtering</returns>
    IPipeline<TIn, TOut?> Filter(Predicate<TOut> predicate);

    /// <summary>
    /// Filters the output asynchronously based on a predicate.
    /// </summary>
    /// <param name="asyncPredicate">The async filter predicate</param>
    /// <returns>A new pipeline with filtering</returns>
    IPipeline<TIn, TOut?> FilterAsync(Func<TOut, Task<bool>> asyncPredicate);

    /// <summary>
    /// Creates a branching pipeline based on a condition.
    /// </summary>
    /// <param name="condition">The branching condition</param>
    /// <param name="trueBranch">Pipeline for when condition is true</param>
    /// <param name="falseBranch">Pipeline for when condition is false</param>
    /// <returns>A new branching pipeline</returns>
    IPipeline<TIn, TOut> Branch(
        Predicate<TOut> condition,
        IPipeline<TOut, TOut> trueBranch,
        IPipeline<TOut, TOut> falseBranch);

    /// <summary>
    /// Handles errors with a synchronous handler.
    /// </summary>
    /// <param name="errorHandler">The error handler</param>
    /// <returns>A new pipeline with error handling</returns>
    IPipeline<TIn, TOut> HandleError(Func<Exception, TOut> errorHandler);

    /// <summary>
    /// Handles errors with an asynchronous handler.
    /// </summary>
    /// <param name="asyncErrorHandler">The async error handler</param>
    /// <returns>A new pipeline with error handling</returns>
    IPipeline<TIn, TOut> HandleErrorAsync(Func<Exception, Task<TOut>> asyncErrorHandler);

    /// <summary>
    /// Creates a pipeline with retry logic using a retry policy.
    /// </summary>
    /// <param name="retryPolicy">The retry policy</param>
    /// <returns>A new pipeline with retry logic</returns>
    IPipeline<TIn, TOut> WithRetry(RetryPolicy retryPolicy);

    /// <summary>
    /// Creates a pipeline with caching using default key generation.
    /// </summary>
    /// <param name="cacheDuration">Cache duration</param>
    /// <returns>A new pipeline with caching</returns>
    IPipeline<TIn, TOut> WithCache(TimeSpan cacheDuration);

    /// <summary>
    /// Creates a parallel execution pipeline.
    /// </summary>
    /// <typeparam name="TParallelInput">The type of parallel input items</typeparam>
    /// <param name="items">The items to process in parallel</param>
    /// <param name="inputMapper">Function to map parallel items to pipeline input</param>
    /// <returns>A new pipeline that processes items in parallel</returns>
    IPipeline<TIn, IEnumerable<TOut>> Parallel<TParallelInput>(
        IEnumerable<TParallelInput> items,
        Func<TParallelInput, TIn> inputMapper);

    /// <summary>
    /// Adds an interceptor to the pipeline.
    /// </summary>
    /// <param name="interceptor">The interceptor to add</param>
    /// <returns>The pipeline for fluent chaining</returns>
    IPipeline<TIn, TOut> AddInterceptor(IPipelineInterceptor interceptor);

    /// <summary>
    /// Adds a stage to the pipeline.
    /// </summary>
    /// <typeparam name="TStageOutput">The output type of the stage</typeparam>
    /// <param name="stage">The stage to add</param>
    /// <returns>The pipeline for fluent chaining</returns>
    IPipeline<TIn, TOut> AddStage<TStageOutput>(IPipelineStage<TOut, TStageOutput> stage)
        where TStageOutput : TOut;

    /// <summary>
    /// Adds a stage to the pipeline (dynamic version for internal use).
    /// </summary>
    /// <param name="stage">The stage to add</param>
    /// <returns>The pipeline for fluent chaining</returns>
    IPipeline<TIn, TOut> AddStage(object stage);

    /// <summary>
    /// Gets all interceptors in the pipeline.
    /// </summary>
    /// <returns>The list of interceptors</returns>
    IReadOnlyList<IPipelineInterceptor> GetInterceptors();

    /// <summary>
    /// Gets all stages in the pipeline.
    /// </summary>
    /// <returns>The list of stages</returns>
    IReadOnlyList<IPipelineStage<object, object>> GetStages();

    /// <summary>
    /// Sets an error handler for the pipeline.
    /// </summary>
    /// <param name="errorHandler">The error handler function</param>
    void SetErrorHandler(Func<Exception, TOut> errorHandler);

    /// <summary>
    /// Sets a completion handler for the pipeline.
    /// </summary>
    /// <param name="completionHandler">The completion handler action</param>
    void SetCompletionHandler(Action<TOut> completionHandler);

    /// <summary>
    /// Configures caching for the pipeline.
    /// </summary>
    /// <param name="cacheKeyExtractor">Function to extract cache key from input</param>
    /// <param name="duration">Cache duration</param>
    void ConfigureCache(Func<TIn, string> cacheKeyExtractor, TimeSpan duration);

    #endregion
}