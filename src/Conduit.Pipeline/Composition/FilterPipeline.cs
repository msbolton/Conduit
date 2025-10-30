using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Conduit.Api;
using Conduit.Common;
using Conduit.Pipeline.Behaviors;
using PipelineMetadata = Conduit.Api.PipelineMetadata;
using PipelineConfiguration = Conduit.Api.PipelineConfiguration;
using RetryPolicy = Conduit.Api.RetryPolicy;
using IPipelineInterceptor = Conduit.Api.IPipelineInterceptor;

namespace Conduit.Pipeline.Composition
{
    /// <summary>
    /// A pipeline that filters the output of another pipeline based on a predicate.
    /// Implements the Message Filter pattern from Enterprise Integration Patterns.
    /// </summary>
    /// <typeparam name="TInput">The input type</typeparam>
    /// <typeparam name="TOutput">The output type</typeparam>
    public class FilterPipeline<TInput, TOutput> : IPipeline<TInput, TOutput?>
    {
        private readonly IPipeline<TInput, TOutput> _innerPipeline;
        private readonly Predicate<TOutput> _predicate;
        private readonly bool _asyncFilter;
        private readonly Func<TOutput, Task<bool>>? _asyncPredicate;
        private readonly TOutput? _defaultValue;
        private readonly bool _hasDefaultValue;

        /// <summary>
        /// Initializes a new instance of the FilterPipeline class with a synchronous predicate.
        /// </summary>
        /// <param name="innerPipeline">The inner pipeline to wrap</param>
        /// <param name="predicate">The filter predicate</param>
        /// <param name="defaultValue">Default value to return when filter fails</param>
        public FilterPipeline(
            IPipeline<TInput, TOutput> innerPipeline,
            Predicate<TOutput> predicate,
            TOutput? defaultValue = default)
        {
            Guard.NotNull(innerPipeline, nameof(innerPipeline));
            Guard.NotNull(predicate, nameof(predicate));

            _innerPipeline = innerPipeline;
            _predicate = predicate;
            _defaultValue = defaultValue;
            _hasDefaultValue = defaultValue != null;
            _asyncFilter = false;
        }

        /// <summary>
        /// Initializes a new instance of the FilterPipeline class with an asynchronous predicate.
        /// </summary>
        /// <param name="innerPipeline">The inner pipeline to wrap</param>
        /// <param name="asyncPredicate">The async filter predicate</param>
        /// <param name="defaultValue">Default value to return when filter fails</param>
        public FilterPipeline(
            IPipeline<TInput, TOutput> innerPipeline,
            Func<TOutput, Task<bool>> asyncPredicate,
            TOutput? defaultValue = default)
        {
            Guard.NotNull(innerPipeline, nameof(innerPipeline));
            Guard.NotNull(asyncPredicate, nameof(asyncPredicate));

            _innerPipeline = innerPipeline;
            _asyncPredicate = asyncPredicate;
            _predicate = _ => false; // Won't be used
            _defaultValue = defaultValue;
            _hasDefaultValue = defaultValue != null;
            _asyncFilter = true;
        }

        /// <inheritdoc />
        public PipelineMetadata Metadata
        {
            get
            {
                var innerMetadata = _innerPipeline.Metadata;
                return new PipelineMetadata
                {
                    PipelineId = Guid.NewGuid().ToString(),
                    Name = $"{innerMetadata.Name} -> Filter",
                    Description = $"Filters output of {innerMetadata.Name}",
                    Type = innerMetadata.Type,
                    Version = innerMetadata.Version,
                    Stages = new List<string>(innerMetadata.Stages) { "Filter" }
                };
            }
        }

        /// <inheritdoc />
        public PipelineConfiguration Configuration => _innerPipeline.Configuration;

        /// <inheritdoc />
        public string Name => $"{_innerPipeline.Name} -> Filter";

        /// <inheritdoc />
        public string Id => $"{_innerPipeline.Id}_filter";

        /// <inheritdoc />
        public bool IsEnabled => _innerPipeline.IsEnabled;

        /// <inheritdoc />
        public async Task<TOutput?> ExecuteAsync(TInput input, CancellationToken cancellationToken = default)
        {
            // Execute the inner pipeline
            var result = await _innerPipeline.ExecuteAsync(input, cancellationToken);

            // Apply the filter
            bool passed;
            if (_asyncFilter && _asyncPredicate != null)
            {
                passed = await _asyncPredicate(result);
            }
            else
            {
                passed = _predicate(result);
            }

            return passed ? result : _defaultValue;
        }

        /// <inheritdoc />
        public async Task<TOutput?> ExecuteAsync(TInput input, Conduit.Api.PipelineContext context, CancellationToken cancellationToken = default)
        {
            context.SetProperty("FilterPipeline.Stage", "InnerExecution");

            // Execute the inner pipeline with context
            var result = await _innerPipeline.ExecuteAsync(input, context, cancellationToken);

            context.SetProperty("FilterPipeline.Stage", "Filtering");

            // Apply the filter
            bool passed;
            if (_asyncFilter && _asyncPredicate != null)
            {
                passed = await _asyncPredicate(result);
            }
            else
            {
                passed = _predicate(result);
            }

            context.SetProperty("FilterPipeline.Passed", passed);

            if (!passed)
            {
                context.SetProperty("FilterPipeline.Filtered", true);
                context.SetProperty("FilterPipeline.FilteredValue", result);
            }

            return passed ? result : _defaultValue;
        }

        /// <summary>
        /// Adds an interceptor to the inner pipeline if supported.
        /// </summary>
        public IPipeline<TInput, TOutput?> AddInterceptor(Conduit.Api.IPipelineInterceptor interceptor)
        {
            _innerPipeline.AddInterceptor(interceptor);
            return this;
        }

        /// <summary>
        /// Adds a behavior to the inner pipeline if supported.
        /// </summary>
        public void AddBehavior(IBehaviorContribution behavior)
        {
            _innerPipeline.AddBehavior(behavior);
        }

        /// <summary>
        /// Removes a behavior from the inner pipeline if supported.
        /// </summary>
        public bool RemoveBehavior(string behaviorId)
        {
            return _innerPipeline.RemoveBehavior(behaviorId);
        }

        /// <summary>
        /// Gets all behaviors from the inner pipeline.
        /// </summary>
        public IReadOnlyList<IBehaviorContribution> GetBehaviors()
        {
            return _innerPipeline.GetBehaviors();
        }

        /// <summary>
        /// Clears all behaviors from the inner pipeline.
        /// </summary>
        public void ClearBehaviors()
        {
            _innerPipeline.ClearBehaviors();
        }

        /// <summary>
        /// Adds a stage to the inner pipeline if supported.
        /// </summary>
        public IPipeline<TInput, TOutput?> AddStage<TStageOutput>(Conduit.Api.IPipelineStage<TOutput?, TStageOutput> stage) where TStageOutput : TOutput?
        {
            ((Pipeline<TInput, TOutput>)_innerPipeline)._stages.Add(stage as IPipelineStage<object, object> ?? throw new InvalidCastException("Unable to cast stage to expected type"));
            return this;
        }

        /// <inheritdoc />
        public IPipeline<TInput, TOutput?> AddStage(object stage)
        {
            _innerPipeline.AddStage(stage);
            return this;
        }

        /// <summary>
        /// Adds a stage to the inner pipeline if supported.
        /// </summary>
        public void AddStage(IPipelineStage<object, object> stage)
        {
            ((Pipeline<TInput, TOutput>)_innerPipeline)._stages.Add(stage as IPipelineStage<object, object> ?? throw new InvalidCastException("Unable to cast stage to expected type"));
        }

        /// <summary>
        /// Configures error handling for the pipeline.
        /// </summary>
        public void SetErrorHandler(Func<Exception, TOutput?> errorHandler)
        {
            // For FilterPipeline, delegate error handling to inner pipeline
            _innerPipeline.SetErrorHandler(ex =>
            {
                var result = errorHandler(ex);
                // Return the non-nullable result to the inner pipeline
                return result!;
            });
        }

        /// <summary>
        /// Configures completion handling for the pipeline.
        /// </summary>
        public void SetCompletionHandler(Action<TOutput?> completionHandler)
        {
            // For FilterPipeline, we handle completion after filtering
            _innerPipeline.SetCompletionHandler(innerResult =>
            {
                try
                {
                    // Apply the filter to determine final result
                    bool passed;
                    if (_asyncFilter && _asyncPredicate != null)
                    {
                        // For async filters, we can't handle completion synchronously
                        // This is a limitation of the current design
                        return;
                    }
                    else
                    {
                        passed = _predicate(innerResult);
                    }

                    var finalResult = passed ? innerResult : _defaultValue;
                    completionHandler(finalResult);
                }
                catch
                {
                    // If filtering or completion handler fails, we can't do much
                    // The exception will be handled by the error handling system
                }
            });
        }

        /// <summary>
        /// Configures caching for the pipeline.
        /// </summary>
        public void ConfigureCache(Func<TInput, string> cacheKeyExtractor, TimeSpan duration)
        {
            _innerPipeline.ConfigureCache(cacheKeyExtractor, duration);
        }

        // IPipeline interface implementation methods

        /// <inheritdoc />
        public IPipeline<TInput, TNewOutput> Map<TNewOutput>(Func<TOutput?, TNewOutput> mapper)
        {
            throw new NotImplementedException("Map operation is not implemented for FilterPipeline. Apply mapping to the inner pipeline before filtering.");
        }

        /// <inheritdoc />
        public IPipeline<TInput, TNewOutput> MapAsync<TNewOutput>(Func<TOutput?, Task<TNewOutput>> asyncMapper)
        {
            throw new NotImplementedException("MapAsync operation is not implemented for FilterPipeline. Apply mapping to the inner pipeline before filtering.");
        }

        /// <inheritdoc />
        public IPipeline<TInput, TNext> Map<TNext>(Func<TOutput?, Task<TNext>> transform)
        {
            throw new NotImplementedException("Async Map operation is not implemented for FilterPipeline. Apply mapping to the inner pipeline before filtering.");
        }

        /// <inheritdoc />
        public IPipeline<TInput, TOutput?> Where(Func<TInput, bool> predicate)
        {
            throw new NotImplementedException("Where operation is not implemented for FilterPipeline. FilterPipeline already implements filtering logic.");
        }

        /// <inheritdoc />
        public IPipeline<TInput, TOutput?> WithErrorHandling(Func<Exception, TInput, Task<TOutput?>> errorHandler)
        {
            throw new NotImplementedException("WithErrorHandling operation is not implemented for FilterPipeline. Apply error handling to the inner pipeline before filtering.");
        }

        /// <inheritdoc />
        public IPipeline<TInput, TNewOutput> Then<TNewOutput>(IPipeline<TOutput?, TNewOutput> nextPipeline)
        {
            throw new NotImplementedException("Then operation is not implemented for FilterPipeline. Chain pipelines before applying filtering.");
        }

        /// <inheritdoc />
        public IPipeline<TInput, TNewOutput> Then<TNewOutput>(Func<TOutput?, TNewOutput> processor)
        {
            throw new NotImplementedException("Then operation is not implemented for FilterPipeline. Apply processing to the inner pipeline before filtering.");
        }

        /// <inheritdoc />
        public IPipeline<TInput, TNewOutput> ThenAsync<TNewOutput>(Func<TOutput?, Task<TNewOutput>> asyncProcessor)
        {
            throw new NotImplementedException("ThenAsync operation is not implemented for FilterPipeline. Apply processing to the inner pipeline before filtering.");
        }

        /// <inheritdoc />
        public IPipeline<TInput, TOutput?> Filter(Predicate<TOutput?> predicate)
        {
            throw new NotImplementedException("Filter operation is not implemented for FilterPipeline. FilterPipeline already implements filtering logic.");
        }

        /// <inheritdoc />
        public IPipeline<TInput, TOutput?> FilterAsync(Func<TOutput?, Task<bool>> asyncPredicate)
        {
            throw new NotImplementedException("FilterAsync operation is not implemented for FilterPipeline. FilterPipeline already implements filtering logic.");
        }

        /// <inheritdoc />
        public IPipeline<TInput, TOutput?> Branch(
            Predicate<TOutput?> condition,
            IPipeline<TOutput?, TOutput?> trueBranch,
            IPipeline<TOutput?, TOutput?> falseBranch)
        {
            throw new NotImplementedException("Branch operation is not implemented for FilterPipeline. Apply branching to the inner pipeline before filtering.");
        }

        /// <inheritdoc />
        public IPipeline<TInput, TOutput?> HandleError(Func<Exception, TOutput?> errorHandler)
        {
            throw new NotImplementedException("HandleError operation is not implemented for FilterPipeline. Apply error handling to the inner pipeline before filtering.");
        }

        /// <inheritdoc />
        public IPipeline<TInput, TOutput?> HandleErrorAsync(Func<Exception, Task<TOutput?>> asyncErrorHandler)
        {
            throw new NotImplementedException("HandleErrorAsync operation is not implemented for FilterPipeline. Apply error handling to the inner pipeline before filtering.");
        }

        /// <inheritdoc />
        public IPipeline<TInput, TOutput?> WithRetry(int maxRetries, TimeSpan retryDelay)
        {
            throw new NotImplementedException("WithRetry operation is not implemented for FilterPipeline. Apply retry logic to the inner pipeline before filtering.");
        }

        /// <inheritdoc />
        public IPipeline<TInput, TOutput?> WithRetry(RetryPolicy retryPolicy)
        {
            throw new NotImplementedException("WithRetry operation is not implemented for FilterPipeline. Apply retry logic to the inner pipeline before filtering.");
        }

        /// <inheritdoc />
        public IPipeline<TInput, TOutput?> WithTimeout(TimeSpan timeout)
        {
            throw new NotImplementedException("WithTimeout operation is not implemented for FilterPipeline. Apply timeout to the inner pipeline before filtering.");
        }

        /// <inheritdoc />
        public IPipeline<TInput, TOutput?> WithCache(TimeSpan cacheDuration)
        {
            throw new NotImplementedException("WithCache operation is not implemented for FilterPipeline. Apply caching to the inner pipeline before filtering.");
        }

        /// <inheritdoc />
        public IPipeline<TInput, TOutput?> WithCache(Func<TInput, string> cacheKeySelector, TimeSpan cacheDuration)
        {
            throw new NotImplementedException("WithCache operation is not implemented for FilterPipeline. Apply caching to the inner pipeline before filtering.");
        }

        /// <inheritdoc />
        public IPipeline<TInput, IEnumerable<TOutput?>> Parallel<TParallelInput>(
            IEnumerable<TParallelInput> items,
            Func<TParallelInput, TInput> inputMapper)
        {
            throw new NotImplementedException("Parallel operation is not implemented for FilterPipeline. Consider using ParallelPipeline for parallel processing.");
        }

        /// <inheritdoc />
        public IPipeline<TInput, TOutput?> AddStage<TStageOutput>(IPipelineStage<TOutput?, TStageOutput> stage)
            where TStageOutput : TOutput?
        {
            throw new NotImplementedException("AddStage operation is not implemented for FilterPipeline. Add stages to the inner pipeline before filtering.");
        }

        /// <inheritdoc />
        public IReadOnlyList<Conduit.Api.IPipelineInterceptor> GetInterceptors()
        {
            return _innerPipeline.GetInterceptors();
        }

        /// <inheritdoc />
        public IReadOnlyList<Conduit.Api.IPipelineStage<object, object>> GetStages()
        {
            return _innerPipeline.GetStages();
        }
    }

    /// <summary>
    /// Extension methods for creating filter pipelines.
    /// </summary>
    public static class FilterPipelineExtensions
    {
        /// <summary>
        /// Creates a pipeline that filters output based on a predicate.
        /// </summary>
        /// <typeparam name="TInput">Input type</typeparam>
        /// <typeparam name="TOutput">Output type</typeparam>
        /// <param name="pipeline">The pipeline to filter</param>
        /// <param name="predicate">The filter predicate</param>
        /// <returns>A new pipeline with filtered output</returns>
        public static IPipeline<TInput, TOutput?> Filter<TInput, TOutput>(
            this IPipeline<TInput, TOutput> pipeline,
            Predicate<TOutput> predicate)
        {
            return new FilterPipeline<TInput, TOutput>(pipeline, predicate);
        }

        /// <summary>
        /// Creates a pipeline that filters output based on an async predicate.
        /// </summary>
        /// <typeparam name="TInput">Input type</typeparam>
        /// <typeparam name="TOutput">Output type</typeparam>
        /// <param name="pipeline">The pipeline to filter</param>
        /// <param name="asyncPredicate">The async filter predicate</param>
        /// <returns>A new pipeline with filtered output</returns>
        public static IPipeline<TInput, TOutput?> FilterAsync<TInput, TOutput>(
            this IPipeline<TInput, TOutput> pipeline,
            Func<TOutput, Task<bool>> asyncPredicate)
        {
            return new FilterPipeline<TInput, TOutput>(pipeline, asyncPredicate);
        }

        /// <summary>
        /// Creates a pipeline that filters output with a default value for filtered items.
        /// </summary>
        /// <typeparam name="TInput">Input type</typeparam>
        /// <typeparam name="TOutput">Output type</typeparam>
        /// <param name="pipeline">The pipeline to filter</param>
        /// <param name="predicate">The filter predicate</param>
        /// <param name="defaultValue">Default value when filter fails</param>
        /// <returns>A new pipeline with filtered output</returns>
        public static IPipeline<TInput, TOutput?> FilterWithDefault<TInput, TOutput>(
            this IPipeline<TInput, TOutput> pipeline,
            Predicate<TOutput> predicate,
            TOutput defaultValue)
        {
            return new FilterPipeline<TInput, TOutput>(pipeline, predicate, defaultValue);
        }

        /// <summary>
        /// Creates a pipeline that only passes non-null values.
        /// </summary>
        /// <typeparam name="TInput">Input type</typeparam>
        /// <typeparam name="TOutput">Output type</typeparam>
        /// <param name="pipeline">The pipeline to filter</param>
        /// <returns>A new pipeline that filters null values</returns>
        public static IPipeline<TInput, TOutput?> WhereNotNull<TInput, TOutput>(
            this IPipeline<TInput, TOutput?> pipeline)
            where TOutput : class
        {
            return pipeline.Filter(output => output != null);
        }

        /// <summary>
        /// Creates a pipeline that filters based on a condition.
        /// </summary>
        /// <typeparam name="TInput">Input type</typeparam>
        /// <typeparam name="TOutput">Output type</typeparam>
        /// <param name="pipeline">The pipeline to filter</param>
        /// <param name="condition">The condition to check</param>
        /// <returns>A new pipeline with conditional filtering</returns>
        public static IPipeline<TInput, TOutput?> Where<TInput, TOutput>(
            this IPipeline<TInput, TOutput> pipeline,
            Func<TOutput, bool> condition)
        {
            return pipeline.Filter(new Predicate<TOutput>(condition));
        }

        /// <summary>
        /// Creates a pipeline that filters based on type.
        /// </summary>
        /// <typeparam name="TInput">Input type</typeparam>
        /// <typeparam name="TOutput">Output type</typeparam>
        /// <typeparam name="TFiltered">The type to filter for</typeparam>
        /// <param name="pipeline">The pipeline to filter</param>
        /// <returns>A new pipeline that only passes values of the specified type</returns>
        public static IPipeline<TInput, TFiltered?> OfType<TInput, TOutput, TFiltered>(
            this IPipeline<TInput, TOutput> pipeline)
            where TFiltered : class, TOutput
        {
            return pipeline
                .Filter(output => output is TFiltered)
                .Map(output => output as TFiltered);
        }

        /// <summary>
        /// Creates a pipeline that filters based on a value range.
        /// </summary>
        /// <typeparam name="TInput">Input type</typeparam>
        /// <typeparam name="TOutput">Output type that is comparable</typeparam>
        /// <param name="pipeline">The pipeline to filter</param>
        /// <param name="min">Minimum value (inclusive)</param>
        /// <param name="max">Maximum value (inclusive)</param>
        /// <returns>A new pipeline with range filtering</returns>
        public static IPipeline<TInput, TOutput?> InRange<TInput, TOutput>(
            this IPipeline<TInput, TOutput> pipeline,
            TOutput min,
            TOutput max)
            where TOutput : IComparable<TOutput>
        {
            return pipeline.Filter(output =>
                output.CompareTo(min) >= 0 && output.CompareTo(max) <= 0);
        }

        /// <summary>
        /// Creates a pipeline that takes only the first result that passes a condition.
        /// </summary>
        /// <typeparam name="TInput">Input type</typeparam>
        /// <typeparam name="TOutput">Output type</typeparam>
        /// <param name="pipeline">The pipeline to filter</param>
        /// <param name="condition">The condition to check</param>
        /// <returns>A new pipeline that returns the first matching result</returns>
        public static IPipeline<TInput, TOutput?> FirstOrDefault<TInput, TOutput>(
            this IPipeline<TInput, TOutput> pipeline,
            Func<TOutput, bool> condition)
        {
            var hasMatched = false;
            return pipeline.Filter(output =>
            {
                if (hasMatched) return false;
                if (condition(output))
                {
                    hasMatched = true;
                    return true;
                }
                return false;
            });
        }
    }
}