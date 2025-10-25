using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Conduit.Pipeline;

/// <summary>
/// Defines a pipeline for processing messages through a series of stages.
/// Implements Enterprise Integration Patterns like Message Translator, Pipes and Filters,
/// Content-Based Router, and Dead Letter Channel.
/// </summary>
/// <typeparam name="TInput">The type of input this pipeline accepts</typeparam>
/// <typeparam name="TOutput">The type of output this pipeline produces</typeparam>
public interface IPipeline<TInput, TOutput>
{
    /// <summary>
    /// Executes the pipeline with the given input.
    /// </summary>
    /// <param name="input">The input to process</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The processed output</returns>
    Task<TOutput> ExecuteAsync(TInput input, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes the pipeline with the given input and context.
    /// </summary>
    /// <param name="input">The input to process</param>
    /// <param name="context">The pipeline context</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The processed output</returns>
    Task<TOutput> ExecuteAsync(TInput input, PipelineContext context, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the pipeline metadata.
    /// </summary>
    PipelineMetadata Metadata { get; }

    /// <summary>
    /// Gets the pipeline configuration.
    /// </summary>
    PipelineConfiguration Configuration { get; }

    // Fluent API Methods

    /// <summary>
    /// Maps the output of this pipeline to a new type (Message Translator pattern).
    /// </summary>
    IPipeline<TInput, TNewOutput> Map<TNewOutput>(Func<TOutput, TNewOutput> mapper);

    /// <summary>
    /// Maps the output asynchronously to a new type.
    /// </summary>
    IPipeline<TInput, TNewOutput> MapAsync<TNewOutput>(Func<TOutput, Task<TNewOutput>> asyncMapper);

    /// <summary>
    /// Chains another pipeline after this one (Pipes and Filters pattern).
    /// </summary>
    IPipeline<TInput, TNewOutput> Then<TNewOutput>(IPipeline<TOutput, TNewOutput> nextPipeline);

    /// <summary>
    /// Chains a processing function after this pipeline.
    /// </summary>
    IPipeline<TInput, TNewOutput> Then<TNewOutput>(Func<TOutput, TNewOutput> processor);

    /// <summary>
    /// Chains an async processing function after this pipeline.
    /// </summary>
    IPipeline<TInput, TNewOutput> ThenAsync<TNewOutput>(Func<TOutput, Task<TNewOutput>> asyncProcessor);

    /// <summary>
    /// Filters the output (Message Filter pattern).
    /// Returns default(TOutput) if predicate returns false.
    /// </summary>
    IPipeline<TInput, TOutput?> Filter(Predicate<TOutput> predicate);

    /// <summary>
    /// Filters the output asynchronously.
    /// </summary>
    IPipeline<TInput, TOutput?> FilterAsync(Func<TOutput, Task<bool>> asyncPredicate);

    /// <summary>
    /// Branches the pipeline based on a condition (Content-Based Router pattern).
    /// </summary>
    IPipeline<TInput, TOutput> Branch(
        Predicate<TOutput> condition,
        IPipeline<TOutput, TOutput> trueBranch,
        IPipeline<TOutput, TOutput> falseBranch);

    /// <summary>
    /// Handles errors in the pipeline (Dead Letter Channel pattern).
    /// </summary>
    IPipeline<TInput, TOutput> HandleError(Func<Exception, TOutput> errorHandler);

    /// <summary>
    /// Handles errors asynchronously.
    /// </summary>
    IPipeline<TInput, TOutput> HandleErrorAsync(Func<Exception, Task<TOutput>> asyncErrorHandler);

    /// <summary>
    /// Adds retry logic to the pipeline.
    /// </summary>
    IPipeline<TInput, TOutput> WithRetry(int maxRetries, TimeSpan retryDelay);

    /// <summary>
    /// Adds retry logic with exponential backoff.
    /// </summary>
    IPipeline<TInput, TOutput> WithRetry(RetryPolicy retryPolicy);

    /// <summary>
    /// Adds a timeout to the pipeline execution.
    /// </summary>
    IPipeline<TInput, TOutput> WithTimeout(TimeSpan timeout);

    /// <summary>
    /// Adds caching to the pipeline.
    /// </summary>
    IPipeline<TInput, TOutput> WithCache(TimeSpan cacheDuration);

    /// <summary>
    /// Adds caching with a custom cache key selector.
    /// </summary>
    IPipeline<TInput, TOutput> WithCache(Func<TInput, string> cacheKeySelector, TimeSpan cacheDuration);

    /// <summary>
    /// Executes multiple pipelines in parallel (Splitter pattern).
    /// </summary>
    IPipeline<TInput, IEnumerable<TOutput>> Parallel<TParallelInput>(
        IEnumerable<TParallelInput> items,
        Func<TParallelInput, TInput> inputMapper);

    /// <summary>
    /// Adds an interceptor to the pipeline.
    /// </summary>
    IPipeline<TInput, TOutput> AddInterceptor(IPipelineInterceptor interceptor);

    /// <summary>
    /// Adds a stage to the pipeline.
    /// </summary>
    IPipeline<TInput, TOutput> AddStage<TStageOutput>(IPipelineStage<TOutput, TStageOutput> stage)
        where TStageOutput : TOutput;

    /// <summary>
    /// Gets all registered interceptors.
    /// </summary>
    IReadOnlyList<IPipelineInterceptor> GetInterceptors();

    /// <summary>
    /// Gets all pipeline stages.
    /// </summary>
    IReadOnlyList<IPipelineStage<object, object>> GetStages();
}

/// <summary>
/// Retry policy for pipeline execution.
/// </summary>
public class RetryPolicy
{
    /// <summary>
    /// Gets or sets the maximum number of retry attempts.
    /// </summary>
    public int MaxRetries { get; set; } = 3;

    /// <summary>
    /// Gets or sets the base delay between retries.
    /// </summary>
    public TimeSpan BaseDelay { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Gets or sets whether to use exponential backoff.
    /// </summary>
    public bool UseExponentialBackoff { get; set; } = true;

    /// <summary>
    /// Gets or sets the maximum delay between retries.
    /// </summary>
    public TimeSpan MaxDelay { get; set; } = TimeSpan.FromMinutes(1);

    /// <summary>
    /// Gets or sets the jitter factor (0.0 to 1.0) to add randomness to delays.
    /// </summary>
    public double JitterFactor { get; set; } = 0.2;

    /// <summary>
    /// Gets or sets the types of exceptions to retry on.
    /// If empty, retries on all exceptions.
    /// </summary>
    public HashSet<Type> RetryableExceptions { get; set; } = new();

    /// <summary>
    /// Calculates the delay for a given retry attempt.
    /// </summary>
    public TimeSpan CalculateDelay(int attemptNumber)
    {
        if (attemptNumber <= 0) return TimeSpan.Zero;

        var delay = UseExponentialBackoff
            ? TimeSpan.FromMilliseconds(BaseDelay.TotalMilliseconds * Math.Pow(2, attemptNumber - 1))
            : BaseDelay;

        // Apply max delay cap
        if (delay > MaxDelay)
            delay = MaxDelay;

        // Apply jitter
        if (JitterFactor > 0)
        {
            var random = new Random();
            var jitter = delay.TotalMilliseconds * JitterFactor * (random.NextDouble() - 0.5);
            delay = TimeSpan.FromMilliseconds(delay.TotalMilliseconds + jitter);
        }

        return delay;
    }

    /// <summary>
    /// Determines if an exception is retryable.
    /// </summary>
    public bool IsRetryable(Exception exception)
    {
        return RetryableExceptions.Count == 0 ||
               RetryableExceptions.Contains(exception.GetType());
    }

    /// <summary>
    /// Creates a default retry policy.
    /// </summary>
    public static RetryPolicy Default() => new();

    /// <summary>
    /// Creates an aggressive retry policy with short delays.
    /// </summary>
    public static RetryPolicy Aggressive() => new()
    {
        MaxRetries = 5,
        BaseDelay = TimeSpan.FromMilliseconds(100),
        UseExponentialBackoff = false
    };

    /// <summary>
    /// Creates a conservative retry policy with longer delays.
    /// </summary>
    public static RetryPolicy Conservative() => new()
    {
        MaxRetries = 3,
        BaseDelay = TimeSpan.FromSeconds(5),
        UseExponentialBackoff = true,
        MaxDelay = TimeSpan.FromMinutes(5)
    };
}