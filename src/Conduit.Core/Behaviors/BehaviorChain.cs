using System;
using System.Threading.Tasks;
using Conduit.Common;

namespace Conduit.Core.Behaviors
{
    /// <summary>
    /// Represents a chain of behaviors in the pipeline processing.
    /// Implements the Chain of Responsibility pattern for message processing.
    /// </summary>
    public interface IBehaviorChain
    {
        /// <summary>
        /// Proceeds to the next behavior in the chain.
        /// </summary>
        /// <param name="context">The pipeline context</param>
        /// <returns>The result of the behavior chain execution</returns>
        Task<object?> ProceedAsync(PipelineContext context);
    }

    /// <summary>
    /// Default implementation of behavior chain.
    /// </summary>
    public class BehaviorChain : IBehaviorChain
    {
        private readonly Func<PipelineContext, Task<object?>> _next;

        /// <summary>
        /// Initializes a new instance of the BehaviorChain class.
        /// </summary>
        /// <param name="next">The next behavior in the chain</param>
        public BehaviorChain(Func<PipelineContext, Task<object?>> next)
        {
            Guard.AgainstNull(next, nameof(next));
            _next = next;
        }

        /// <summary>
        /// Proceeds to the next behavior in the chain.
        /// </summary>
        public Task<object?> ProceedAsync(PipelineContext context)
        {
            Guard.AgainstNull(context, nameof(context));

            if (context.IsCancelled)
            {
                return Task.FromResult<object?>(null);
            }

            return _next(context);
        }

        /// <summary>
        /// Creates a terminal behavior chain that returns a completed task with the specified result.
        /// </summary>
        /// <param name="result">The result to return</param>
        /// <returns>A terminal behavior chain</returns>
        public static IBehaviorChain Terminal(object? result = null)
        {
            return new BehaviorChain(_ => Task.FromResult(result));
        }

        /// <summary>
        /// Creates a terminal behavior chain that returns a failed task with the specified error.
        /// </summary>
        /// <param name="error">The error to return</param>
        /// <returns>A terminal behavior chain that fails</returns>
        public static IBehaviorChain TerminalError(Exception error)
        {
            Guard.AgainstNull(error, nameof(error));
            return new BehaviorChain(_ => Task.FromException<object?>(error));
        }

        /// <summary>
        /// Creates an empty behavior chain that returns the context's current result.
        /// </summary>
        /// <returns>An empty behavior chain</returns>
        public static IBehaviorChain Empty()
        {
            return new BehaviorChain(context => Task.FromResult(context.Result));
        }

        /// <summary>
        /// Creates a behavior chain from a list of behaviors.
        /// </summary>
        /// <param name="behaviors">The behaviors to chain</param>
        /// <returns>A chained behavior</returns>
        public static IBehaviorChain Create(params IPipelineBehavior[] behaviors)
        {
            Guard.AgainstNull(behaviors, nameof(behaviors));

            if (behaviors.Length == 0)
            {
                return Empty();
            }

            // Build the chain in reverse order
            IBehaviorChain chain = Terminal();

            for (int i = behaviors.Length - 1; i >= 0; i--)
            {
                var behavior = behaviors[i];
                var previousChain = chain;
                chain = new BehaviorChain(async context =>
                {
                    return await behavior.ExecuteAsync(context, previousChain);
                });
            }

            return chain;
        }
    }

    /// <summary>
    /// Extension methods for behavior chains.
    /// </summary>
    public static class BehaviorChainExtensions
    {
        /// <summary>
        /// Chains multiple behaviors together.
        /// </summary>
        /// <param name="first">The first behavior</param>
        /// <param name="next">The next behavior</param>
        /// <returns>A combined behavior chain</returns>
        public static IBehaviorChain Then(this IBehaviorChain first, IBehaviorChain next)
        {
            Guard.AgainstNull(first, nameof(first));
            Guard.AgainstNull(next, nameof(next));

            return new BehaviorChain(async context =>
            {
                var result = await first.ProceedAsync(context);
                if (result != null)
                {
                    context.Result = result;
                }
                return await next.ProceedAsync(context);
            });
        }

        /// <summary>
        /// Adds error handling to a behavior chain.
        /// </summary>
        /// <param name="chain">The behavior chain</param>
        /// <param name="errorHandler">The error handler</param>
        /// <returns>A behavior chain with error handling</returns>
        public static IBehaviorChain WithErrorHandling(
            this IBehaviorChain chain,
            Func<Exception, object?> errorHandler)
        {
            Guard.AgainstNull(chain, nameof(chain));
            Guard.AgainstNull(errorHandler, nameof(errorHandler));

            return new BehaviorChain(async context =>
            {
                try
                {
                    return await chain.ProceedAsync(context);
                }
                catch (Exception ex)
                {
                    return errorHandler(ex);
                }
            });
        }

        /// <summary>
        /// Adds a timeout to a behavior chain.
        /// </summary>
        /// <param name="chain">The behavior chain</param>
        /// <param name="timeout">The timeout duration</param>
        /// <returns>A behavior chain with timeout</returns>
        public static IBehaviorChain WithTimeout(this IBehaviorChain chain, TimeSpan timeout)
        {
            Guard.AgainstNull(chain, nameof(chain));

            return new BehaviorChain(async context =>
            {
                using var cts = new System.Threading.CancellationTokenSource(timeout);
                var task = chain.ProceedAsync(context);
                var completedTask = await Task.WhenAny(task, Task.Delay(timeout, cts.Token));

                if (completedTask == task)
                {
                    cts.Cancel();
                    return await task;
                }

                throw new TimeoutException($"Behavior chain execution timed out after {timeout}");
            });
        }
    }
}