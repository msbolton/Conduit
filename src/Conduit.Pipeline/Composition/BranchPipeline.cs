using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Conduit.Api;
using Conduit.Common;
using Conduit.Pipeline.Behaviors;

namespace Conduit.Pipeline.Composition
{
    /// <summary>
    /// A pipeline that conditionally routes input to different pipelines based on a predicate.
    /// Implements the Content-Based Router pattern from Enterprise Integration Patterns.
    /// </summary>
    /// <typeparam name="TInput">The input type</typeparam>
    /// <typeparam name="TOutput">The output type</typeparam>
    public class BranchPipeline<TInput, TOutput> : IPipeline<TInput, TOutput>
    {
        private readonly Predicate<TInput> _condition;
        private readonly IPipeline<TInput, TOutput> _trueBranch;
        private readonly IPipeline<TInput, TOutput> _falseBranch;
        private readonly bool _asyncCondition;
        private readonly Func<TInput, Task<bool>>? _asyncPredicate;

        /// <summary>
        /// Initializes a new instance of the BranchPipeline class with a synchronous condition.
        /// </summary>
        /// <param name="condition">The branching condition</param>
        /// <param name="trueBranch">Pipeline to execute when condition is true</param>
        /// <param name="falseBranch">Pipeline to execute when condition is false</param>
        public BranchPipeline(
            Predicate<TInput> condition,
            IPipeline<TInput, TOutput> trueBranch,
            IPipeline<TInput, TOutput> falseBranch)
        {
            Guard.AgainstNull(condition, nameof(condition));
            Guard.AgainstNull(trueBranch, nameof(trueBranch));
            Guard.AgainstNull(falseBranch, nameof(falseBranch));

            _condition = condition;
            _trueBranch = trueBranch;
            _falseBranch = falseBranch;
            _asyncCondition = false;
        }

        /// <summary>
        /// Initializes a new instance of the BranchPipeline class with an asynchronous condition.
        /// </summary>
        /// <param name="asyncPredicate">The async branching condition</param>
        /// <param name="trueBranch">Pipeline to execute when condition is true</param>
        /// <param name="falseBranch">Pipeline to execute when condition is false</param>
        public BranchPipeline(
            Func<TInput, Task<bool>> asyncPredicate,
            IPipeline<TInput, TOutput> trueBranch,
            IPipeline<TInput, TOutput> falseBranch)
        {
            Guard.AgainstNull(asyncPredicate, nameof(asyncPredicate));
            Guard.AgainstNull(trueBranch, nameof(trueBranch));
            Guard.AgainstNull(falseBranch, nameof(falseBranch));

            _asyncPredicate = asyncPredicate;
            _condition = _ => false; // Won't be used
            _trueBranch = trueBranch;
            _falseBranch = falseBranch;
            _asyncCondition = true;
        }

        /// <inheritdoc />
        public PipelineMetadata Metadata
        {
            get
            {
                return new PipelineMetadata
                {
                    PipelineId = Guid.NewGuid().ToString(),
                    Name = "Branch Pipeline",
                    Description = $"Branches between '{_trueBranch.Metadata.Name}' and '{_falseBranch.Metadata.Name}'",
                    Type = PipelineType.Conditional,
                    Version = "1.0.0",
                    Stages = new List<string> { "Condition", "True Branch", "False Branch" }
                };
            }
        }

        /// <inheritdoc />
        public PipelineConfiguration Configuration
        {
            get
            {
                // Return the configuration from the true branch as default
                // In practice, you might want to merge or choose based on some logic
                return _trueBranch.Configuration;
            }
        }

        /// <inheritdoc />
        public async Task<TOutput> ExecuteAsync(TInput input, CancellationToken cancellationToken = default)
        {
            // Evaluate the condition
            bool conditionResult;
            if (_asyncCondition && _asyncPredicate != null)
            {
                conditionResult = await _asyncPredicate(input);
            }
            else
            {
                conditionResult = _condition(input);
            }

            // Execute the appropriate branch
            return conditionResult
                ? await _trueBranch.ExecuteAsync(input, cancellationToken)
                : await _falseBranch.ExecuteAsync(input, cancellationToken);
        }

        /// <inheritdoc />
        public async Task<TOutput> ExecuteAsync(TInput input, PipelineContext context, CancellationToken cancellationToken = default)
        {
            context.SetProperty("BranchPipeline.Stage", "EvaluatingCondition");

            // Evaluate the condition
            bool conditionResult;
            if (_asyncCondition && _asyncPredicate != null)
            {
                conditionResult = await _asyncPredicate(input);
            }
            else
            {
                conditionResult = _condition(input);
            }

            context.SetProperty("BranchPipeline.ConditionResult", conditionResult);
            context.SetProperty("BranchPipeline.SelectedBranch", conditionResult ? "True" : "False");
            context.SetProperty("BranchPipeline.Stage", conditionResult ? "ExecutingTrueBranch" : "ExecutingFalseBranch");

            // Execute the appropriate branch
            return conditionResult
                ? await _trueBranch.ExecuteAsync(input, context, cancellationToken)
                : await _falseBranch.ExecuteAsync(input, context, cancellationToken);
        }

        /// <summary>
        /// Adds an interceptor to both branches.
        /// </summary>
        public IPipeline<TInput, TOutput> AddInterceptor(IPipelineInterceptor interceptor)
        {
            _trueBranch.AddInterceptor(interceptor);
            _falseBranch.AddInterceptor(interceptor);
            return this;
        }

        /// <summary>
        /// Adds a behavior to both branches.
        /// </summary>
        public void AddBehavior(BehaviorContribution behavior)
        {
            _trueBranch.AddBehavior(behavior);
            _falseBranch.AddBehavior(behavior);
        }

        /// <summary>
        /// Adds a stage to both branches.
        /// </summary>
        public void AddStage(IPipelineStage<object, object> stage)
        {
            _trueBranch.AddStage(stage);
            _falseBranch.AddStage(stage);
        }

        /// <summary>
        /// Configures error handling for the pipeline.
        /// </summary>
        public void SetErrorHandler(Func<Exception, TOutput> errorHandler)
        {
            _trueBranch.SetErrorHandler(errorHandler);
            _falseBranch.SetErrorHandler(errorHandler);
        }

        /// <summary>
        /// Configures completion handling for the pipeline.
        /// </summary>
        public void SetCompletionHandler(Action<TOutput> completionHandler)
        {
            _trueBranch.SetCompletionHandler(completionHandler);
            _falseBranch.SetCompletionHandler(completionHandler);
        }

        /// <summary>
        /// Configures caching for both branches.
        /// </summary>
        public void ConfigureCache(Func<TInput, string> cacheKeyExtractor, TimeSpan duration)
        {
            _trueBranch.ConfigureCache(cacheKeyExtractor, duration);
            _falseBranch.ConfigureCache(cacheKeyExtractor, duration);
        }

        // IPipeline interface implementation methods

        /// <inheritdoc />
        public IPipeline<TInput, TNewOutput> Map<TNewOutput>(Func<TOutput, TNewOutput> mapper)
        {
            throw new NotImplementedException("Map operation is not implemented for BranchPipeline. Consider using Then() with a mapped pipeline instead.");
        }

        /// <inheritdoc />
        public IPipeline<TInput, TNewOutput> MapAsync<TNewOutput>(Func<TOutput, Task<TNewOutput>> asyncMapper)
        {
            throw new NotImplementedException("MapAsync operation is not implemented for BranchPipeline. Consider using Then() with a mapped pipeline instead.");
        }

        /// <inheritdoc />
        public IPipeline<TInput, TNewOutput> Then<TNewOutput>(IPipeline<TOutput, TNewOutput> nextPipeline)
        {
            throw new NotImplementedException("Then operation is not implemented for BranchPipeline. Branch pipelines must be composed at the branch level.");
        }

        /// <inheritdoc />
        public IPipeline<TInput, TNewOutput> Then<TNewOutput>(Func<TOutput, TNewOutput> processor)
        {
            throw new NotImplementedException("Then operation is not implemented for BranchPipeline. Branch pipelines must be composed at the branch level.");
        }

        /// <inheritdoc />
        public IPipeline<TInput, TNewOutput> ThenAsync<TNewOutput>(Func<TOutput, Task<TNewOutput>> asyncProcessor)
        {
            throw new NotImplementedException("ThenAsync operation is not implemented for BranchPipeline. Branch pipelines must be composed at the branch level.");
        }

        /// <inheritdoc />
        public IPipeline<TInput, TOutput?> Filter(Predicate<TOutput> predicate)
        {
            throw new NotImplementedException("Filter operation is not implemented for BranchPipeline. Consider applying filters to individual branches.");
        }

        /// <inheritdoc />
        public IPipeline<TInput, TOutput?> FilterAsync(Func<TOutput, Task<bool>> asyncPredicate)
        {
            throw new NotImplementedException("FilterAsync operation is not implemented for BranchPipeline. Consider applying filters to individual branches.");
        }

        /// <inheritdoc />
        public IPipeline<TInput, TOutput> Branch(
            Predicate<TOutput> condition,
            IPipeline<TOutput, TOutput> trueBranch,
            IPipeline<TOutput, TOutput> falseBranch)
        {
            throw new NotImplementedException("Branch operation is not implemented for BranchPipeline. BranchPipeline already implements branching logic.");
        }

        /// <inheritdoc />
        public IPipeline<TInput, TOutput> HandleError(Func<Exception, TOutput> errorHandler)
        {
            throw new NotImplementedException("HandleError operation is not implemented for BranchPipeline. Apply error handling to individual branches.");
        }

        /// <inheritdoc />
        public IPipeline<TInput, TOutput> HandleErrorAsync(Func<Exception, Task<TOutput>> asyncErrorHandler)
        {
            throw new NotImplementedException("HandleErrorAsync operation is not implemented for BranchPipeline. Apply error handling to individual branches.");
        }

        /// <inheritdoc />
        public IPipeline<TInput, TOutput> WithRetry(int maxRetries, TimeSpan retryDelay)
        {
            throw new NotImplementedException("WithRetry operation is not implemented for BranchPipeline. Apply retry logic to individual branches.");
        }

        /// <inheritdoc />
        public IPipeline<TInput, TOutput> WithRetry(RetryPolicy retryPolicy)
        {
            throw new NotImplementedException("WithRetry operation is not implemented for BranchPipeline. Apply retry logic to individual branches.");
        }

        /// <inheritdoc />
        public IPipeline<TInput, TOutput> WithTimeout(TimeSpan timeout)
        {
            throw new NotImplementedException("WithTimeout operation is not implemented for BranchPipeline. Apply timeout to individual branches.");
        }

        /// <inheritdoc />
        public IPipeline<TInput, TOutput> WithCache(TimeSpan cacheDuration)
        {
            throw new NotImplementedException("WithCache operation is not implemented for BranchPipeline. Apply caching to individual branches.");
        }

        /// <inheritdoc />
        public IPipeline<TInput, TOutput> WithCache(Func<TInput, string> cacheKeySelector, TimeSpan cacheDuration)
        {
            throw new NotImplementedException("WithCache operation is not implemented for BranchPipeline. Apply caching to individual branches.");
        }

        /// <inheritdoc />
        public IPipeline<TInput, IEnumerable<TOutput>> Parallel<TParallelInput>(
            IEnumerable<TParallelInput> items,
            Func<TParallelInput, TInput> inputMapper)
        {
            throw new NotImplementedException("Parallel operation is not implemented for BranchPipeline. Consider using ParallelPipeline for parallel processing.");
        }

        /// <inheritdoc />
        public IPipeline<TInput, TOutput> AddStage<TStageOutput>(IPipelineStage<TOutput, TStageOutput> stage)
            where TStageOutput : TOutput
        {
            throw new NotImplementedException("AddStage operation is not implemented for BranchPipeline. Add stages to individual branches.");
        }

        /// <inheritdoc />
        public IReadOnlyList<IPipelineInterceptor> GetInterceptors()
        {
            return new List<IPipelineInterceptor>().AsReadOnly();
        }

        /// <inheritdoc />
        public IReadOnlyList<IPipelineStage<object, object>> GetStages()
        {
            return new List<IPipelineStage<object, object>>().AsReadOnly();
        }
    }

    /// <summary>
    /// A multi-branch pipeline that routes based on multiple conditions.
    /// </summary>
    /// <typeparam name="TInput">The input type</typeparam>
    /// <typeparam name="TOutput">The output type</typeparam>
    public class MultiBranchPipeline<TInput, TOutput> : IPipeline<TInput, TOutput>
    {
        private readonly List<(Predicate<TInput> Condition, IPipeline<TInput, TOutput> Pipeline, string Name)> _branches;
        private readonly IPipeline<TInput, TOutput>? _defaultBranch;

        /// <summary>
        /// Initializes a new instance of the MultiBranchPipeline class.
        /// </summary>
        /// <param name="defaultBranch">Optional default branch if no conditions match</param>
        public MultiBranchPipeline(IPipeline<TInput, TOutput>? defaultBranch = null)
        {
            _branches = new List<(Predicate<TInput>, IPipeline<TInput, TOutput>, string)>();
            _defaultBranch = defaultBranch;
        }

        /// <summary>
        /// Adds a branch to the pipeline.
        /// </summary>
        /// <param name="condition">The condition for this branch</param>
        /// <param name="pipeline">The pipeline to execute</param>
        /// <param name="name">Optional name for the branch</param>
        /// <returns>This instance for fluent configuration</returns>
        public MultiBranchPipeline<TInput, TOutput> AddBranch(
            Predicate<TInput> condition,
            IPipeline<TInput, TOutput> pipeline,
            string? name = null)
        {
            Guard.AgainstNull(condition, nameof(condition));
            Guard.AgainstNull(pipeline, nameof(pipeline));

            _branches.Add((condition, pipeline, name ?? $"Branch {_branches.Count + 1}"));
            return this;
        }

        /// <inheritdoc />
        public PipelineMetadata Metadata
        {
            get
            {
                var branchNames = _branches.Select(b => b.Name).ToList();
                if (_defaultBranch != null)
                    branchNames.Add("Default");

                return new PipelineMetadata
                {
                    PipelineId = Guid.NewGuid().ToString(),
                    Name = "Multi-Branch Pipeline",
                    Description = $"Routes to one of {_branches.Count} branches",
                    Type = PipelineType.Conditional,
                    Version = "1.0.0",
                    Stages = branchNames
                };
            }
        }

        /// <inheritdoc />
        public PipelineConfiguration Configuration =>
            _defaultBranch?.Configuration ??
            _branches.FirstOrDefault().Pipeline?.Configuration ??
            PipelineConfiguration.Default();

        /// <inheritdoc />
        public async Task<TOutput> ExecuteAsync(TInput input, CancellationToken cancellationToken = default)
        {
            foreach (var (condition, pipeline, _) in _branches)
            {
                if (condition(input))
                {
                    return await pipeline.ExecuteAsync(input, cancellationToken);
                }
            }

            if (_defaultBranch != null)
            {
                return await _defaultBranch.ExecuteAsync(input, cancellationToken);
            }

            throw new InvalidOperationException("No matching branch found and no default branch configured");
        }

        /// <inheritdoc />
        public async Task<TOutput> ExecuteAsync(TInput input, PipelineContext context, CancellationToken cancellationToken = default)
        {
            context.SetProperty("MultiBranchPipeline.Stage", "EvaluatingConditions");

            for (int i = 0; i < _branches.Count; i++)
            {
                var (condition, pipeline, name) = _branches[i];
                if (condition(input))
                {
                    context.SetProperty("MultiBranchPipeline.SelectedBranch", name);
                    context.SetProperty("MultiBranchPipeline.SelectedBranchIndex", i);
                    context.SetProperty("MultiBranchPipeline.Stage", $"Executing{name}");
                    return await pipeline.ExecuteAsync(input, context, cancellationToken);
                }
            }

            if (_defaultBranch != null)
            {
                context.SetProperty("MultiBranchPipeline.SelectedBranch", "Default");
                context.SetProperty("MultiBranchPipeline.Stage", "ExecutingDefaultBranch");
                return await _defaultBranch.ExecuteAsync(input, context, cancellationToken);
            }

            throw new InvalidOperationException("No matching branch found and no default branch configured");
        }

        // Other interface methods implementation...
        public IPipeline<TInput, TOutput> AddInterceptor(IPipelineInterceptor interceptor)
        {
            foreach (var (_, pipeline, _) in _branches)
            {
                pipeline.AddInterceptor(interceptor);
            }
            _defaultBranch?.AddInterceptor(interceptor);
            return this;
        }

        public void AddBehavior(BehaviorContribution behavior)
        {
            foreach (var (_, pipeline, _) in _branches)
            {
                pipeline.AddBehavior(behavior);
            }
            _defaultBranch?.AddBehavior(behavior);
        }

        public void AddStage(IPipelineStage<object, object> stage)
        {
            foreach (var (_, pipeline, _) in _branches)
            {
                pipeline.AddStage(stage);
            }
            _defaultBranch?.AddStage(stage);
        }

        public void SetErrorHandler(Func<Exception, TOutput> errorHandler)
        {
            foreach (var (_, pipeline, _) in _branches)
            {
                pipeline.SetErrorHandler(errorHandler);
            }
            _defaultBranch?.SetErrorHandler(errorHandler);
        }

        public void SetCompletionHandler(Action<TOutput> completionHandler)
        {
            foreach (var (_, pipeline, _) in _branches)
            {
                pipeline.SetCompletionHandler(completionHandler);
            }
            _defaultBranch?.SetCompletionHandler(completionHandler);
        }

        public void ConfigureCache(Func<TInput, string> cacheKeyExtractor, TimeSpan duration)
        {
            foreach (var (_, pipeline, _) in _branches)
            {
                pipeline.ConfigureCache(cacheKeyExtractor, duration);
            }
            _defaultBranch?.ConfigureCache(cacheKeyExtractor, duration);
        }

        // IPipeline interface implementation methods

        /// <inheritdoc />
        public IPipeline<TInput, TNewOutput> Map<TNewOutput>(Func<TOutput, TNewOutput> mapper)
        {
            throw new NotImplementedException("Map operation is not implemented for MultiBranchPipeline. Consider using Then() with a mapped pipeline instead.");
        }

        /// <inheritdoc />
        public IPipeline<TInput, TNewOutput> MapAsync<TNewOutput>(Func<TOutput, Task<TNewOutput>> asyncMapper)
        {
            throw new NotImplementedException("MapAsync operation is not implemented for MultiBranchPipeline. Consider using Then() with a mapped pipeline instead.");
        }

        /// <inheritdoc />
        public IPipeline<TInput, TNewOutput> Then<TNewOutput>(IPipeline<TOutput, TNewOutput> nextPipeline)
        {
            throw new NotImplementedException("Then operation is not implemented for MultiBranchPipeline. Branch pipelines must be composed at the branch level.");
        }

        /// <inheritdoc />
        public IPipeline<TInput, TNewOutput> Then<TNewOutput>(Func<TOutput, TNewOutput> processor)
        {
            throw new NotImplementedException("Then operation is not implemented for MultiBranchPipeline. Branch pipelines must be composed at the branch level.");
        }

        /// <inheritdoc />
        public IPipeline<TInput, TNewOutput> ThenAsync<TNewOutput>(Func<TOutput, Task<TNewOutput>> asyncProcessor)
        {
            throw new NotImplementedException("ThenAsync operation is not implemented for MultiBranchPipeline. Branch pipelines must be composed at the branch level.");
        }

        /// <inheritdoc />
        public IPipeline<TInput, TOutput?> Filter(Predicate<TOutput> predicate)
        {
            throw new NotImplementedException("Filter operation is not implemented for MultiBranchPipeline. Consider applying filters to individual branches.");
        }

        /// <inheritdoc />
        public IPipeline<TInput, TOutput?> FilterAsync(Func<TOutput, Task<bool>> asyncPredicate)
        {
            throw new NotImplementedException("FilterAsync operation is not implemented for MultiBranchPipeline. Consider applying filters to individual branches.");
        }

        /// <inheritdoc />
        public IPipeline<TInput, TOutput> Branch(
            Predicate<TOutput> condition,
            IPipeline<TOutput, TOutput> trueBranch,
            IPipeline<TOutput, TOutput> falseBranch)
        {
            throw new NotImplementedException("Branch operation is not implemented for MultiBranchPipeline. MultiBranchPipeline already implements branching logic.");
        }

        /// <inheritdoc />
        public IPipeline<TInput, TOutput> HandleError(Func<Exception, TOutput> errorHandler)
        {
            throw new NotImplementedException("HandleError operation is not implemented for MultiBranchPipeline. Apply error handling to individual branches.");
        }

        /// <inheritdoc />
        public IPipeline<TInput, TOutput> HandleErrorAsync(Func<Exception, Task<TOutput>> asyncErrorHandler)
        {
            throw new NotImplementedException("HandleErrorAsync operation is not implemented for MultiBranchPipeline. Apply error handling to individual branches.");
        }

        /// <inheritdoc />
        public IPipeline<TInput, TOutput> WithRetry(int maxRetries, TimeSpan retryDelay)
        {
            throw new NotImplementedException("WithRetry operation is not implemented for MultiBranchPipeline. Apply retry logic to individual branches.");
        }

        /// <inheritdoc />
        public IPipeline<TInput, TOutput> WithRetry(RetryPolicy retryPolicy)
        {
            throw new NotImplementedException("WithRetry operation is not implemented for MultiBranchPipeline. Apply retry logic to individual branches.");
        }

        /// <inheritdoc />
        public IPipeline<TInput, TOutput> WithTimeout(TimeSpan timeout)
        {
            throw new NotImplementedException("WithTimeout operation is not implemented for MultiBranchPipeline. Apply timeout to individual branches.");
        }

        /// <inheritdoc />
        public IPipeline<TInput, TOutput> WithCache(TimeSpan cacheDuration)
        {
            throw new NotImplementedException("WithCache operation is not implemented for MultiBranchPipeline. Apply caching to individual branches.");
        }

        /// <inheritdoc />
        public IPipeline<TInput, TOutput> WithCache(Func<TInput, string> cacheKeySelector, TimeSpan cacheDuration)
        {
            throw new NotImplementedException("WithCache operation is not implemented for MultiBranchPipeline. Apply caching to individual branches.");
        }

        /// <inheritdoc />
        public IPipeline<TInput, IEnumerable<TOutput>> Parallel<TParallelInput>(
            IEnumerable<TParallelInput> items,
            Func<TParallelInput, TInput> inputMapper)
        {
            throw new NotImplementedException("Parallel operation is not implemented for MultiBranchPipeline. Consider using ParallelPipeline for parallel processing.");
        }

        /// <inheritdoc />
        public IPipeline<TInput, TOutput> AddStage<TStageOutput>(IPipelineStage<TOutput, TStageOutput> stage)
            where TStageOutput : TOutput
        {
            throw new NotImplementedException("AddStage operation is not implemented for MultiBranchPipeline. Add stages to individual branches.");
        }

        /// <inheritdoc />
        public IReadOnlyList<IPipelineInterceptor> GetInterceptors()
        {
            return new List<IPipelineInterceptor>().AsReadOnly();
        }

        /// <inheritdoc />
        public IReadOnlyList<IPipelineStage<object, object>> GetStages()
        {
            return new List<IPipelineStage<object, object>>().AsReadOnly();
        }
    }

    /// <summary>
    /// Extension methods for creating branch pipelines.
    /// </summary>
    public static class BranchPipelineExtensions
    {
        /// <summary>
        /// Creates a pipeline that branches based on a condition.
        /// </summary>
        public static IPipeline<TInput, TOutput> Branch<TInput, TOutput>(
            this IPipeline<TInput, TOutput> pipeline,
            Predicate<TInput> condition,
            IPipeline<TInput, TOutput> alternativePipeline)
        {
            return new BranchPipeline<TInput, TOutput>(condition, pipeline, alternativePipeline);
        }

        /// <summary>
        /// Creates a pipeline with if-then-else semantics.
        /// </summary>
        public static IPipeline<TInput, TOutput> If<TInput, TOutput>(
            this Predicate<TInput> condition,
            IPipeline<TInput, TOutput> thenPipeline,
            IPipeline<TInput, TOutput> elsePipeline)
        {
            return new BranchPipeline<TInput, TOutput>(condition, thenPipeline, elsePipeline);
        }

        /// <summary>
        /// Creates a switch-like multi-branch pipeline.
        /// </summary>
        public static MultiBranchPipeline<TInput, TOutput> Switch<TInput, TOutput>(
            this IPipeline<TInput, TOutput> defaultBranch)
        {
            return new MultiBranchPipeline<TInput, TOutput>(defaultBranch);
        }

        /// <summary>
        /// Creates a pipeline that routes based on input type.
        /// </summary>
        public static IPipeline<object, TOutput> TypeSwitch<TOutput>(
            this Dictionary<Type, IPipeline<object, TOutput>> typePipelines,
            IPipeline<object, TOutput>? defaultPipeline = null)
        {
            var multiBranch = new MultiBranchPipeline<object, TOutput>(defaultPipeline);

            foreach (var kvp in typePipelines)
            {
                var type = kvp.Key;
                multiBranch.AddBranch(
                    input => input.GetType() == type || type.IsAssignableFrom(input.GetType()),
                    kvp.Value,
                    $"{type.Name} Handler");
            }

            return multiBranch;
        }
    }
}