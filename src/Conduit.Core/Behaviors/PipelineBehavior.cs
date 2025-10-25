using System;
using System.Threading.Tasks;
using Conduit.Common;

namespace Conduit.Core.Behaviors
{
    /// <summary>
    /// Represents an individual behavior in the message processing pipeline.
    /// Implements the Chain of Responsibility pattern for behavior chaining.
    /// </summary>
    public interface IPipelineBehavior
    {
        /// <summary>
        /// Executes the behavior logic.
        /// </summary>
        /// <param name="context">The pipeline context</param>
        /// <param name="next">The next behavior in the chain</param>
        /// <returns>The result of the behavior execution</returns>
        Task<object?> ExecuteAsync(PipelineContext context, IBehaviorChain next);
    }

    /// <summary>
    /// Base class for pipeline behaviors providing common functionality.
    /// </summary>
    public abstract class PipelineBehavior : IPipelineBehavior
    {
        /// <summary>
        /// Executes the behavior logic.
        /// </summary>
        public abstract Task<object?> ExecuteAsync(PipelineContext context, IBehaviorChain next);

        /// <summary>
        /// Wraps this behavior with another behavior for cross-cutting concerns.
        /// </summary>
        /// <param name="wrapper">The wrapper behavior</param>
        /// <returns>A wrapped behavior</returns>
        public IPipelineBehavior Wrap(IPipelineBehavior wrapper)
        {
            Guard.AgainstNull(wrapper, nameof(wrapper));
            return new WrappedBehavior(this, wrapper);
        }

        /// <summary>
        /// Chains this behavior with another behavior to execute sequentially.
        /// </summary>
        /// <param name="after">The behavior to execute after this one</param>
        /// <returns>A chained behavior</returns>
        public IPipelineBehavior AndThen(IPipelineBehavior after)
        {
            Guard.AgainstNull(after, nameof(after));
            return new ChainedBehavior(this, after);
        }

        /// <summary>
        /// Creates a conditional behavior that only executes when the predicate is true.
        /// </summary>
        /// <param name="predicate">The condition to check</param>
        /// <param name="behavior">The behavior to conditionally execute</param>
        /// <returns>A conditional behavior</returns>
        public static IPipelineBehavior Conditional(
            Predicate<PipelineContext> predicate,
            IPipelineBehavior behavior)
        {
            Guard.AgainstNull(predicate, nameof(predicate));
            Guard.AgainstNull(behavior, nameof(behavior));

            return new ConditionalBehavior(predicate, behavior);
        }

        /// <summary>
        /// Creates a behavior with built-in error handling.
        /// </summary>
        /// <param name="behavior">The behavior to wrap</param>
        /// <param name="errorHandler">The error handler function</param>
        /// <returns>A behavior with error handling</returns>
        public static IPipelineBehavior WithErrorHandling(
            IPipelineBehavior behavior,
            Func<Exception, object?> errorHandler)
        {
            Guard.AgainstNull(behavior, nameof(behavior));
            Guard.AgainstNull(errorHandler, nameof(errorHandler));

            return new ErrorHandlingBehavior(behavior, errorHandler);
        }

        /// <summary>
        /// Creates a pass-through behavior that does nothing.
        /// </summary>
        /// <returns>A no-op behavior</returns>
        public static IPipelineBehavior PassThrough()
        {
            return new PassThroughBehavior();
        }

        /// <summary>
        /// Creates a behavior from a function.
        /// </summary>
        /// <param name="execute">The function to execute</param>
        /// <returns>A behavior wrapping the function</returns>
        public static IPipelineBehavior FromFunc(
            Func<PipelineContext, IBehaviorChain, Task<object?>> execute)
        {
            Guard.AgainstNull(execute, nameof(execute));
            return new FunctionalBehavior(execute);
        }
    }

    /// <summary>
    /// A behavior that wraps another behavior.
    /// </summary>
    internal class WrappedBehavior : IPipelineBehavior
    {
        private readonly IPipelineBehavior _inner;
        private readonly IPipelineBehavior _wrapper;

        public WrappedBehavior(IPipelineBehavior inner, IPipelineBehavior wrapper)
        {
            _inner = inner;
            _wrapper = wrapper;
        }

        public async Task<object?> ExecuteAsync(PipelineContext context, IBehaviorChain next)
        {
            // Wrapper executes first and decides when to call the inner behavior
            return await _wrapper.ExecuteAsync(context, new BehaviorChain(async ctx =>
            {
                return await _inner.ExecuteAsync(ctx, next);
            }));
        }
    }

    /// <summary>
    /// A behavior that chains two behaviors sequentially.
    /// </summary>
    internal class ChainedBehavior : IPipelineBehavior
    {
        private readonly IPipelineBehavior _first;
        private readonly IPipelineBehavior _second;

        public ChainedBehavior(IPipelineBehavior first, IPipelineBehavior second)
        {
            _first = first;
            _second = second;
        }

        public async Task<object?> ExecuteAsync(PipelineContext context, IBehaviorChain next)
        {
            return await _first.ExecuteAsync(context, new BehaviorChain(async ctx =>
            {
                return await _second.ExecuteAsync(ctx, next);
            }));
        }
    }

    /// <summary>
    /// A behavior that executes conditionally based on a predicate.
    /// </summary>
    internal class ConditionalBehavior : IPipelineBehavior
    {
        private readonly Predicate<PipelineContext> _predicate;
        private readonly IPipelineBehavior _behavior;

        public ConditionalBehavior(Predicate<PipelineContext> predicate, IPipelineBehavior behavior)
        {
            _predicate = predicate;
            _behavior = behavior;
        }

        public async Task<object?> ExecuteAsync(PipelineContext context, IBehaviorChain next)
        {
            if (_predicate(context))
            {
                return await _behavior.ExecuteAsync(context, next);
            }

            return await next.ProceedAsync(context);
        }
    }

    /// <summary>
    /// A behavior that handles errors.
    /// </summary>
    internal class ErrorHandlingBehavior : IPipelineBehavior
    {
        private readonly IPipelineBehavior _behavior;
        private readonly Func<Exception, object?> _errorHandler;

        public ErrorHandlingBehavior(IPipelineBehavior behavior, Func<Exception, object?> errorHandler)
        {
            _behavior = behavior;
            _errorHandler = errorHandler;
        }

        public async Task<object?> ExecuteAsync(PipelineContext context, IBehaviorChain next)
        {
            try
            {
                return await _behavior.ExecuteAsync(context, next);
            }
            catch (Exception ex)
            {
                context.SetProperty("LastError", ex);
                return _errorHandler(ex);
            }
        }
    }

    /// <summary>
    /// A pass-through behavior that does nothing.
    /// </summary>
    internal class PassThroughBehavior : IPipelineBehavior
    {
        public Task<object?> ExecuteAsync(PipelineContext context, IBehaviorChain next)
        {
            return next.ProceedAsync(context);
        }
    }

    /// <summary>
    /// A behavior created from a function.
    /// </summary>
    internal class FunctionalBehavior : IPipelineBehavior
    {
        private readonly Func<PipelineContext, IBehaviorChain, Task<object?>> _execute;

        public FunctionalBehavior(Func<PipelineContext, IBehaviorChain, Task<object?>> execute)
        {
            _execute = execute;
        }

        public Task<object?> ExecuteAsync(PipelineContext context, IBehaviorChain next)
        {
            return _execute(context, next);
        }
    }
}