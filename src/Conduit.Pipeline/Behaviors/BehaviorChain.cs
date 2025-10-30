using System;
using System.Threading.Tasks;
using RetryPolicy = Conduit.Api.RetryPolicy;

namespace Conduit.Pipeline.Behaviors;

/// <summary>
/// Represents the chain of behaviors in a pipeline.
/// This is the continuation that each behavior calls to proceed to the next behavior.
/// </summary>
public class BehaviorChain
{
    private readonly Func<PipelineContext, Task<object?>> _proceedFunc;

    /// <summary>
    /// Initializes a new instance of the BehaviorChain class.
    /// </summary>
    public BehaviorChain(Func<PipelineContext, Task<object?>> proceedFunc)
    {
        _proceedFunc = proceedFunc ?? throw new ArgumentNullException(nameof(proceedFunc));
    }

    /// <summary>
    /// Proceeds to the next behavior in the chain.
    /// </summary>
    /// <param name="context">The pipeline context</param>
    /// <returns>The result of the next behavior</returns>
    public Task<object?> ProceedAsync(PipelineContext context)
    {
        if (context == null)
            throw new ArgumentNullException(nameof(context));

        if (context.IsCancelled)
        {
            throw new OperationCanceledException("Pipeline execution was cancelled");
        }

        return _proceedFunc(context);
    }

    /// <summary>
    /// Creates a terminal chain that returns a specific result.
    /// </summary>
    public static BehaviorChain Terminal(object? result)
    {
        return new BehaviorChain(_ => Task.FromResult(result));
    }

    /// <summary>
    /// Creates a terminal chain that returns the context's result.
    /// </summary>
    public static BehaviorChain Terminal()
    {
        return new BehaviorChain(context => Task.FromResult(context.Result));
    }

    /// <summary>
    /// Creates a terminal chain that throws an error.
    /// </summary>
    public static BehaviorChain TerminalError(Exception error)
    {
        return new BehaviorChain(_ => Task.FromException<object?>(error));
    }

    /// <summary>
    /// Creates an empty chain that returns the context's result.
    /// </summary>
    public static BehaviorChain Empty()
    {
        return Terminal();
    }

    /// <summary>
    /// Creates a chain from a function.
    /// </summary>
    public static BehaviorChain Create(Func<PipelineContext, Task<object?>> proceedFunc)
    {
        return new BehaviorChain(proceedFunc);
    }

    /// <summary>
    /// Creates a chain from a synchronous function.
    /// </summary>
    public static BehaviorChain Create(Func<PipelineContext, object?> proceedFunc)
    {
        return new BehaviorChain(context => Task.FromResult(proceedFunc(context)));
    }

    /// <summary>
    /// Combines two chains sequentially.
    /// </summary>
    public BehaviorChain Then(BehaviorChain next)
    {
        return new BehaviorChain(async context =>
        {
            var result = await ProceedAsync(context);
            context.Result = result;
            return await next.ProceedAsync(context);
        });
    }

    /// <summary>
    /// Adds error handling to the chain.
    /// </summary>
    public BehaviorChain WithErrorHandling(Func<Exception, PipelineContext, Task<object?>> errorHandler)
    {
        return new BehaviorChain(async context =>
        {
            try
            {
                return await ProceedAsync(context);
            }
            catch (Exception ex)
            {
                context.Exception = ex;
                return await errorHandler(ex, context);
            }
        });
    }

    /// <summary>
    /// Adds a timeout to the chain.
    /// </summary>
    public BehaviorChain WithTimeout(TimeSpan timeout)
    {
        return new BehaviorChain(async context =>
        {
            using var cts = new System.Threading.CancellationTokenSource(timeout);
            using var combined = System.Threading.CancellationTokenSource.CreateLinkedTokenSource(
                context.CancellationToken, cts.Token);

            try
            {
                var task = ProceedAsync(context);
                var completedTask = await Task.WhenAny(task, Task.Delay(timeout, combined.Token));

                if (completedTask == task)
                {
                    return await task;
                }

                throw new TimeoutException($"Pipeline execution timed out after {timeout.TotalSeconds} seconds");
            }
            catch (OperationCanceledException) when (cts.Token.IsCancellationRequested)
            {
                throw new TimeoutException($"Pipeline execution timed out after {timeout.TotalSeconds} seconds");
            }
        });
    }

    /// <summary>
    /// Adds retry logic to the chain.
    /// </summary>
    public BehaviorChain WithRetry(int maxRetries, TimeSpan retryDelay)
    {
        return WithRetry(new RetryPolicy
        {
            MaxRetries = maxRetries,
            BaseDelay = retryDelay,
            UseExponentialBackoff = false
        });
    }

    /// <summary>
    /// Adds retry logic with a retry policy.
    /// </summary>
    public BehaviorChain WithRetry(RetryPolicy policy)
    {
        return new BehaviorChain(async context =>
        {
            Exception? lastException = null;

            for (int attempt = 0; attempt <= policy.MaxRetries; attempt++)
            {
                try
                {
                    return await ProceedAsync(context);
                }
                catch (Exception ex) when (attempt < policy.MaxRetries && policy.IsRetryable && (policy.ShouldRetry?.Invoke(ex) ?? true))
                {
                    lastException = ex;
                    var delay = policy.CalculateDelay(attempt + 1);

                    context.SetProperty($"RetryAttempt.{attempt + 1}", DateTimeOffset.UtcNow);
                    context.SetProperty($"RetryDelay.{attempt + 1}", delay.TotalMilliseconds);

                    await Task.Delay(delay, context.CancellationToken);
                }
            }

            throw lastException ?? new InvalidOperationException("Retry logic failed unexpectedly");
        });
    }

    /// <summary>
    /// Creates a conditional chain.
    /// </summary>
    public BehaviorChain When(Predicate<PipelineContext> condition, BehaviorChain? elseChain = null)
    {
        return new BehaviorChain(async context =>
        {
            if (condition(context))
            {
                return await ProceedAsync(context);
            }

            return elseChain != null
                ? await elseChain.ProceedAsync(context)
                : context.Result;
        });
    }
}