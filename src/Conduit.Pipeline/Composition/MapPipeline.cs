using System;
using System.Threading;
using System.Threading.Tasks;
using Conduit.Common;

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
            Guard.AgainstNull(innerPipeline, nameof(innerPipeline));
            Guard.AgainstNull(transformer, nameof(transformer));

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
            Guard.AgainstNull(innerPipeline, nameof(innerPipeline));
            Guard.AgainstNull(asyncTransformer, nameof(asyncTransformer));

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
        public async Task<TOutput> ExecuteAsync(TInput input, PipelineContext context, CancellationToken cancellationToken = default)
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
        public void SetErrorHandler(Func<Exception, TOutput> errorHandler)
        {
            // This would need to be implemented based on the inner pipeline's capabilities
            throw new NotImplementedException("Error handler configuration not yet implemented for MapPipeline");
        }

        /// <summary>
        /// Configures completion handling for the pipeline.
        /// </summary>
        public void SetCompletionHandler(Action<TOutput> completionHandler)
        {
            // This would need to be implemented based on the inner pipeline's capabilities
            throw new NotImplementedException("Completion handler configuration not yet implemented for MapPipeline");
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
            Guard.AgainstNull(converter, nameof(converter));
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