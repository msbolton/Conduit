using System;
using System.Threading;
using System.Threading.Tasks;
using Conduit.Common;

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
            Guard.AgainstNull(innerPipeline, nameof(innerPipeline));
            Guard.AgainstNull(predicate, nameof(predicate));

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
            Guard.AgainstNull(innerPipeline, nameof(innerPipeline));
            Guard.AgainstNull(asyncPredicate, nameof(asyncPredicate));

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
        public async Task<TOutput?> ExecuteAsync(TInput input, PipelineContext context, CancellationToken cancellationToken = default)
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
        public void AddInterceptor(IPipelineInterceptor interceptor)
        {
            _innerPipeline.AddInterceptor(interceptor);
        }

        /// <summary>
        /// Adds a behavior to the inner pipeline if supported.
        /// </summary>
        public void AddBehavior(BehaviorContribution behavior)
        {
            _innerPipeline.AddBehavior(behavior);
        }

        /// <summary>
        /// Adds a stage to the inner pipeline if supported.
        /// </summary>
        public void AddStage(IPipelineStage<object, object> stage)
        {
            _innerPipeline.AddStage(stage);
        }

        /// <summary>
        /// Configures error handling for the pipeline.
        /// </summary>
        public void SetErrorHandler(Func<Exception, TOutput?> errorHandler)
        {
            // This would need to be implemented based on the inner pipeline's capabilities
            throw new NotImplementedException("Error handler configuration not yet implemented for FilterPipeline");
        }

        /// <summary>
        /// Configures completion handling for the pipeline.
        /// </summary>
        public void SetCompletionHandler(Action<TOutput?> completionHandler)
        {
            // This would need to be implemented based on the inner pipeline's capabilities
            throw new NotImplementedException("Completion handler configuration not yet implemented for FilterPipeline");
        }

        /// <summary>
        /// Configures caching for the pipeline.
        /// </summary>
        public void ConfigureCache(Func<TInput, string> cacheKeyExtractor, TimeSpan duration)
        {
            _innerPipeline.ConfigureCache(cacheKeyExtractor, duration);
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