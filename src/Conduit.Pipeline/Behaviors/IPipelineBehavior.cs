using System;
using System.Threading.Tasks;

namespace Conduit.Pipeline.Behaviors;

/// <summary>
/// Represents a behavior in the pipeline that can intercept and modify the execution flow.
/// Implements the Chain of Responsibility pattern.
/// </summary>
public interface IPipelineBehavior
{
    /// <summary>
    /// Executes the behavior logic.
    /// </summary>
    /// <param name="context">The pipeline context</param>
    /// <param name="next">The next behavior in the chain</param>
    /// <returns>The result of the behavior execution</returns>
    Task<object?> ExecuteAsync(PipelineContext context, BehaviorChain next);
}

/// <summary>
/// Functional delegate for pipeline behaviors.
/// </summary>
public delegate Task<object?> PipelineBehaviorDelegate(PipelineContext context, BehaviorChain next);

/// <summary>
/// Extension methods for pipeline behaviors.
/// </summary>
public static class PipelineBehaviorExtensions
{
    /// <summary>
    /// Wraps a behavior with additional logic.
    /// </summary>
    public static IPipelineBehavior Wrap(this IPipelineBehavior behavior, IPipelineBehavior wrapper)
    {
        return new DelegatingBehavior(async (context, next) =>
        {
            return await wrapper.ExecuteAsync(context, new BehaviorChain(async ctx =>
                await behavior.ExecuteAsync(ctx, next)));
        });
    }

    /// <summary>
    /// Chains behaviors in sequence.
    /// </summary>
    public static IPipelineBehavior AndThen(this IPipelineBehavior first, IPipelineBehavior second)
    {
        return new DelegatingBehavior(async (context, next) =>
        {
            return await first.ExecuteAsync(context, new BehaviorChain(async ctx =>
                await second.ExecuteAsync(ctx, next)));
        });
    }

    /// <summary>
    /// Creates a conditional behavior.
    /// </summary>
    public static IPipelineBehavior Conditional(
        this IPipelineBehavior behavior,
        Predicate<PipelineContext> condition)
    {
        return new DelegatingBehavior(async (context, next) =>
        {
            if (condition(context))
            {
                return await behavior.ExecuteAsync(context, next);
            }
            return await next.ProceedAsync(context);
        });
    }

    /// <summary>
    /// Creates a behavior with error handling.
    /// </summary>
    public static IPipelineBehavior WithErrorHandling(
        this IPipelineBehavior behavior,
        Func<Exception, PipelineContext, Task<object?>> errorHandler)
    {
        return new DelegatingBehavior(async (context, next) =>
        {
            try
            {
                return await behavior.ExecuteAsync(context, next);
            }
            catch (Exception ex)
            {
                context.Exception = ex;
                return await errorHandler(ex, context);
            }
        });
    }

    /// <summary>
    /// Creates a behavior that measures execution time.
    /// </summary>
    public static IPipelineBehavior WithTiming(
        this IPipelineBehavior behavior,
        Action<TimeSpan, PipelineContext>? onComplete = null)
    {
        return new DelegatingBehavior(async (context, next) =>
        {
            var startTime = DateTimeOffset.UtcNow;
            try
            {
                return await behavior.ExecuteAsync(context, next);
            }
            finally
            {
                var duration = DateTimeOffset.UtcNow - startTime;
                context.SetProperty($"Timing.{behavior.GetType().Name}", duration.TotalMilliseconds);
                onComplete?.Invoke(duration, context);
            }
        });
    }

    /// <summary>
    /// Creates a pass-through behavior that doesn't modify the execution.
    /// </summary>
    public static IPipelineBehavior PassThrough()
    {
        return new DelegatingBehavior(async (context, next) => await next.ProceedAsync(context));
    }
}

/// <summary>
/// Implementation of IPipelineBehavior using a delegate.
/// </summary>
public class DelegatingBehavior : IPipelineBehavior
{
    private readonly PipelineBehaviorDelegate _delegate;

    /// <summary>
    /// Initializes a new instance of the DelegatingBehavior class.
    /// </summary>
    public DelegatingBehavior(PipelineBehaviorDelegate behaviorDelegate)
    {
        _delegate = behaviorDelegate ?? throw new ArgumentNullException(nameof(behaviorDelegate));
    }

    /// <inheritdoc />
    public Task<object?> ExecuteAsync(PipelineContext context, BehaviorChain next)
    {
        return _delegate(context, next);
    }

    /// <summary>
    /// Creates a behavior from a delegate.
    /// </summary>
    public static IPipelineBehavior Create(PipelineBehaviorDelegate behaviorDelegate)
    {
        return new DelegatingBehavior(behaviorDelegate);
    }

    /// <summary>
    /// Creates a behavior from an action.
    /// </summary>
    public static IPipelineBehavior Create(Action<PipelineContext> action)
    {
        return new DelegatingBehavior(async (context, next) =>
        {
            action(context);
            return await next.ProceedAsync(context);
        });
    }

    /// <summary>
    /// Creates a behavior from an async action.
    /// </summary>
    public static IPipelineBehavior Create(Func<PipelineContext, Task> asyncAction)
    {
        return new DelegatingBehavior(async (context, next) =>
        {
            await asyncAction(context);
            return await next.ProceedAsync(context);
        });
    }
}