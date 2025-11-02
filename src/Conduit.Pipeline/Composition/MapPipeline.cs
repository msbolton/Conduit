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
    /// A pipeline that transforms the output of another pipeline using a mapping function.
    /// Implements the Message Translator pattern from Enterprise Integration Patterns.
    /// </summary>
    /// <typeparam name="TInput">The input type</typeparam>
    /// <typeparam name="TIntermediate">The intermediate output type from the inner pipeline</typeparam>
    /// <typeparam name="TOutput">The final output type after transformation</typeparam>
    public class MapPipeline<TInput, TIntermediate, TOutput> : IPipeline<TInput, TOutput>
    {
        private readonly IPipeline<TInput, TIntermediate> _innerPipeline;
        private readonly Func<TIntermediate, TOutput> _transformer;
        private readonly bool _asyncTransform;
        private readonly Func<TIntermediate, Task<TOutput>>? _asyncTransformer;

        /// <summary>
        /// Initializes a new instance of the MapPipeline class with a synchronous transformer.
        /// </summary>
        /// <param name="innerPipeline">The inner pipeline to wrap</param>
        /// <param name="transformer">The transformation function</param>
        public MapPipeline(IPipeline<TInput, TIntermediate> innerPipeline, Func<TIntermediate, TOutput> transformer)
        {
            Guard.NotNull(innerPipeline, nameof(innerPipeline));
            Guard.NotNull(transformer, nameof(transformer));

            _innerPipeline = innerPipeline;
            _transformer = transformer;
            _asyncTransform = false;
        }

        /// <summary>
        /// Initializes a new instance of the MapPipeline class with an asynchronous transformer.
        /// </summary>
        /// <param name="innerPipeline">The inner pipeline to wrap</param>
        /// <param name="asyncTransformer">The async transformation function</param>
        public MapPipeline(IPipeline<TInput, TIntermediate> innerPipeline, Func<TIntermediate, Task<TOutput>> asyncTransformer)
        {
            Guard.NotNull(innerPipeline, nameof(innerPipeline));
            Guard.NotNull(asyncTransformer, nameof(asyncTransformer));

            _innerPipeline = innerPipeline;
            _asyncTransformer = asyncTransformer;
            _transformer = _ => default!; // Won't be used
            _asyncTransform = true;
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
                    Name = $"{innerMetadata.Name} -> Map",
                    Description = $"Maps output of {innerMetadata.Name} to {typeof(TOutput).Name}",
                    Type = innerMetadata.Type,
                    Version = innerMetadata.Version,
                    Stages = new List<string>(innerMetadata.Stages) { "Map" }
                };
            }
        }

        /// <inheritdoc />
        public PipelineConfiguration Configuration => _innerPipeline.Configuration;

        /// <inheritdoc />
        public string Name => $"{_innerPipeline.Name} -> Map";

        /// <inheritdoc />
        public string Id => $"{_innerPipeline.Id}_map";

        /// <inheritdoc />
        public bool IsEnabled => _innerPipeline.IsEnabled;

        /// <inheritdoc />
        public async Task<TOutput> ExecuteAsync(TInput input, CancellationToken cancellationToken = default)
        {
            // Execute the inner pipeline
            var intermediateResult = await _innerPipeline.ExecuteAsync(input, cancellationToken);

            // Transform the result
            if (_asyncTransform && _asyncTransformer != null)
            {
                return await _asyncTransformer(intermediateResult);
            }

            return _transformer(intermediateResult);
        }

        /// <inheritdoc />
        public async Task<TOutput> ExecuteAsync(TInput input, Conduit.Api.PipelineContext context, CancellationToken cancellationToken = default)
        {
            context.SetProperty("MapPipeline.Stage", "InnerExecution");

            // Execute the inner pipeline with context
            var intermediateResult = await _innerPipeline.ExecuteAsync(input, context, cancellationToken);

            context.SetProperty("MapPipeline.Stage", "Transformation");
            context.SetProperty("MapPipeline.IntermediateType", typeof(TIntermediate).Name);
            context.SetProperty("MapPipeline.OutputType", typeof(TOutput).Name);

            // Transform the result
            if (_asyncTransform && _asyncTransformer != null)
            {
                return await _asyncTransformer(intermediateResult);
            }

            return _transformer(intermediateResult);
        }

        /// <summary>
        /// Adds an interceptor to the inner pipeline if supported.
        /// </summary>
        public IPipeline<TInput, TOutput> AddInterceptor(Conduit.Api.IPipelineInterceptor interceptor)
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
        public IPipeline<TInput, TOutput> AddStage<TStageOutput>(Conduit.Api.IPipelineStage<TOutput, TStageOutput> stage) where TStageOutput : TOutput
        {
            ((Pipeline<TInput, TIntermediate>)_innerPipeline)._stages.Add(stage as IPipelineStage<object, object> ?? throw new InvalidCastException("Unable to cast stage to expected type"));
            return this;
        }

        /// <inheritdoc />
        public IPipeline<TInput, TOutput> AddStage(object stage)
        {
            _innerPipeline.AddStage(stage);
            return this;
        }

        /// <summary>
        /// Adds a stage to the inner pipeline if supported.
        /// </summary>
        public void AddStage(IPipelineStage<object, object> stage)
        {
            ((Pipeline<TInput, TIntermediate>)_innerPipeline)._stages.Add(stage as IPipelineStage<object, object> ?? throw new InvalidCastException("Unable to cast stage to expected type"));
        }

        /// <summary>
        /// Configures error handling for the pipeline.
        /// </summary>
        public void SetErrorHandler(Func<Exception, TOutput> errorHandler)
        {
            // For MapPipeline, we need to delegate error handling to the inner pipeline
            // but map any exceptions that occur during transformation
            _innerPipeline.SetErrorHandler(ex =>
            {
                try
                {
                    var result = errorHandler(ex);
                    return (TIntermediate)(object)result!;
                }
                catch
                {
                    // If the error handler itself throws, we can't do much more
                    throw;
                }
            });
        }

        /// <summary>
        /// Configures completion handling for the pipeline.
        /// </summary>
        public void SetCompletionHandler(Action<TOutput> completionHandler)
        {
            // For MapPipeline, we handle completion after the mapping transformation
            _innerPipeline.SetCompletionHandler(intermediateResult =>
            {
                try
                {
                    TOutput finalResult;
                    if (_asyncTransform && _asyncTransformer != null)
                    {
                        // For async transformers, we can't handle completion synchronously
                        // This is a limitation of the current design
                        return;
                    }
                    else
                    {
                        finalResult = _transformer(intermediateResult);
                    }
                    completionHandler(finalResult);
                }
                catch
                {
                    // If transformation or completion handler fails, we can't do much
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
        public IPipeline<TInput, TNewOutput> Map<TNewOutput>(Func<TOutput, TNewOutput> mapper)
        {
            throw new NotImplementedException("Map operation is not implemented for MapPipeline. MapPipeline already implements mapping logic.");
        }

        /// <inheritdoc />
        public IPipeline<TInput, TNewOutput> MapAsync<TNewOutput>(Func<TOutput, Task<TNewOutput>> asyncMapper)
        {
            throw new NotImplementedException("MapAsync operation is not implemented for MapPipeline. MapPipeline already implements mapping logic.");
        }

        /// <inheritdoc />
        public IPipeline<TInput, TNext> Map<TNext>(Func<TOutput, Task<TNext>> transform)
        {
            throw new NotImplementedException("Async Map operation is not implemented for MapPipeline. MapPipeline already implements mapping logic.");
        }

        /// <inheritdoc />
        public IPipeline<TInput, TOutput> Where(Func<TInput, bool> predicate)
        {
            throw new NotImplementedException("Where operation is not implemented for MapPipeline. Apply filtering to the inner pipeline before mapping.");
        }

        /// <inheritdoc />
        public IPipeline<TInput, TOutput> WithErrorHandling(Func<Exception, TInput, Task<TOutput>> errorHandler)
        {
            throw new NotImplementedException("WithErrorHandling operation is not implemented for MapPipeline. Apply error handling to the inner pipeline before mapping.");
        }

        /// <inheritdoc />
        public IPipeline<TInput, TNewOutput> Then<TNewOutput>(IPipeline<TOutput, TNewOutput> nextPipeline)
        {
            throw new NotImplementedException("Then operation is not implemented for MapPipeline. Chain pipelines before applying mapping.");
        }

        /// <inheritdoc />
        public IPipeline<TInput, TNewOutput> Then<TNewOutput>(Func<TOutput, TNewOutput> processor)
        {
            throw new NotImplementedException("Then operation is not implemented for MapPipeline. Apply additional processing to the mapped result is not supported.");
        }

        /// <inheritdoc />
        public IPipeline<TInput, TNewOutput> ThenAsync<TNewOutput>(Func<TOutput, Task<TNewOutput>> asyncProcessor)
        {
            throw new NotImplementedException("ThenAsync operation is not implemented for MapPipeline. Apply additional processing to the mapped result is not supported.");
        }

        /// <inheritdoc />
        public IPipeline<TInput, TOutput?> Filter(Predicate<TOutput> predicate)
        {
            throw new NotImplementedException("Filter operation is not implemented for MapPipeline. Apply filtering after mapping using a FilterPipeline.");
        }

        /// <inheritdoc />
        public IPipeline<TInput, TOutput?> FilterAsync(Func<TOutput, Task<bool>> asyncPredicate)
        {
            throw new NotImplementedException("FilterAsync operation is not implemented for MapPipeline. Apply filtering after mapping using a FilterPipeline.");
        }

        /// <inheritdoc />
        public IPipeline<TInput, TOutput> Branch(
            Predicate<TOutput> condition,
            IPipeline<TOutput, TOutput> trueBranch,
            IPipeline<TOutput, TOutput> falseBranch)
        {
            throw new NotImplementedException("Branch operation is not implemented for MapPipeline. Apply branching after mapping using a BranchPipeline.");
        }

        /// <inheritdoc />
        public IPipeline<TInput, TOutput> HandleError(Func<Exception, TOutput> errorHandler)
        {
            throw new NotImplementedException("HandleError operation is not implemented for MapPipeline. Apply error handling to the inner pipeline before mapping.");
        }

        /// <inheritdoc />
        public IPipeline<TInput, TOutput> HandleErrorAsync(Func<Exception, Task<TOutput>> asyncErrorHandler)
        {
            throw new NotImplementedException("HandleErrorAsync operation is not implemented for MapPipeline. Apply error handling to the inner pipeline before mapping.");
        }

        /// <inheritdoc />
        public IPipeline<TInput, TOutput> WithRetry(int maxRetries, TimeSpan retryDelay)
        {
            throw new NotImplementedException("WithRetry operation is not implemented for MapPipeline. Apply retry logic to the inner pipeline before mapping.");
        }

        /// <inheritdoc />
        public IPipeline<TInput, TOutput> WithRetry(RetryPolicy retryPolicy)
        {
            throw new NotImplementedException("WithRetry operation is not implemented for MapPipeline. Apply retry logic to the inner pipeline before mapping.");
        }

        /// <inheritdoc />
        public IPipeline<TInput, TOutput> WithTimeout(TimeSpan timeout)
        {
            throw new NotImplementedException("WithTimeout operation is not implemented for MapPipeline. Apply timeout to the inner pipeline before mapping.");
        }

        /// <inheritdoc />
        public IPipeline<TInput, TOutput> WithCache(TimeSpan cacheDuration)
        {
            throw new NotImplementedException("WithCache operation is not implemented for MapPipeline. Apply caching to the inner pipeline before mapping.");
        }

        /// <inheritdoc />
        public IPipeline<TInput, TOutput> WithCache(Func<TInput, string> cacheKeySelector, TimeSpan cacheDuration)
        {
            throw new NotImplementedException("WithCache operation is not implemented for MapPipeline. Apply caching to the inner pipeline before mapping.");
        }

        /// <inheritdoc />
        public IPipeline<TInput, IEnumerable<TOutput>> Parallel<TParallelInput>(
            IEnumerable<TParallelInput> items,
            Func<TParallelInput, TInput> inputMapper)
        {
            throw new NotImplementedException("Parallel operation is not implemented for MapPipeline. Consider using ParallelPipeline for parallel processing.");
        }

        /// <inheritdoc />
        public IPipeline<TInput, TOutput> AddStage<TStageOutput>(IPipelineStage<TOutput, TStageOutput> stage)
            where TStageOutput : TOutput
        {
            throw new NotImplementedException("AddStage operation is not implemented for MapPipeline. Add stages to the inner pipeline before mapping.");
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
    /// Extension methods for creating map pipelines.
    /// </summary>
    public static class MapPipelineExtensions
    {
        /// <summary>
        /// Creates a pipeline that transforms the output using a mapping function.
        /// </summary>
        /// <typeparam name="TInput">Input type</typeparam>
        /// <typeparam name="TIntermediate">Intermediate output type</typeparam>
        /// <typeparam name="TOutput">Final output type</typeparam>
        /// <param name="pipeline">The pipeline to transform</param>
        /// <param name="transformer">The transformation function</param>
        /// <returns>A new pipeline with transformed output</returns>
        public static IPipeline<TInput, TOutput> Map<TInput, TIntermediate, TOutput>(
            this IPipeline<TInput, TIntermediate> pipeline,
            Func<TIntermediate, TOutput> transformer)
        {
            return new MapPipeline<TInput, TIntermediate, TOutput>(pipeline, transformer);
        }

        /// <summary>
        /// Creates a pipeline that transforms the output using an async mapping function.
        /// </summary>
        /// <typeparam name="TInput">Input type</typeparam>
        /// <typeparam name="TIntermediate">Intermediate output type</typeparam>
        /// <typeparam name="TOutput">Final output type</typeparam>
        /// <param name="pipeline">The pipeline to transform</param>
        /// <param name="asyncTransformer">The async transformation function</param>
        /// <returns>A new pipeline with transformed output</returns>
        public static IPipeline<TInput, TOutput> MapAsync<TInput, TIntermediate, TOutput>(
            this IPipeline<TInput, TIntermediate> pipeline,
            Func<TIntermediate, Task<TOutput>> asyncTransformer)
        {
            return new MapPipeline<TInput, TIntermediate, TOutput>(pipeline, asyncTransformer);
        }

        /// <summary>
        /// Creates a pipeline that selects a property from the output.
        /// </summary>
        /// <typeparam name="TInput">Input type</typeparam>
        /// <typeparam name="TIntermediate">Intermediate output type</typeparam>
        /// <typeparam name="TOutput">Selected property type</typeparam>
        /// <param name="pipeline">The pipeline to transform</param>
        /// <param name="selector">The property selector</param>
        /// <returns>A new pipeline with selected output</returns>
        public static IPipeline<TInput, TOutput> Select<TInput, TIntermediate, TOutput>(
            this IPipeline<TInput, TIntermediate> pipeline,
            Func<TIntermediate, TOutput> selector)
        {
            return pipeline.Map(selector);
        }

        /// <summary>
        /// Creates a pipeline that casts the output to a different type.
        /// </summary>
        /// <typeparam name="TInput">Input type</typeparam>
        /// <typeparam name="TIntermediate">Intermediate output type</typeparam>
        /// <typeparam name="TOutput">Target type to cast to</typeparam>
        /// <param name="pipeline">The pipeline to transform</param>
        /// <returns>A new pipeline with casted output</returns>
        public static IPipeline<TInput, TOutput> Cast<TInput, TIntermediate, TOutput>(
            this IPipeline<TInput, TIntermediate> pipeline)
            where TIntermediate : TOutput
        {
            return pipeline.Map(intermediate => (TOutput)intermediate);
        }

        /// <summary>
        /// Creates a pipeline that converts the output using a converter function.
        /// </summary>
        /// <typeparam name="TInput">Input type</typeparam>
        /// <typeparam name="TIntermediate">Intermediate output type</typeparam>
        /// <typeparam name="TOutput">Converted output type</typeparam>
        /// <param name="pipeline">The pipeline to transform</param>
        /// <param name="converter">The converter instance</param>
        /// <returns>A new pipeline with converted output</returns>
        public static IPipeline<TInput, TOutput> Convert<TInput, TIntermediate, TOutput>(
            this IPipeline<TInput, TIntermediate> pipeline,
            IConverter<TIntermediate, TOutput> converter)
        {
            Guard.NotNull(converter, nameof(converter));
            return pipeline.Map(intermediate => converter.Convert(intermediate));
        }
    }

    /// <summary>
    /// Interface for type converters.
    /// </summary>
    /// <typeparam name="TSource">Source type</typeparam>
    /// <typeparam name="TDestination">Destination type</typeparam>
    public interface IConverter<TSource, TDestination>
    {
        /// <summary>
        /// Converts a value from source type to destination type.
        /// </summary>
        /// <param name="source">The source value</param>
        /// <returns>The converted value</returns>
        TDestination Convert(TSource source);
    }
}