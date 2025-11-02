using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Conduit.Api;
using Conduit.Common;
using Conduit.Pipeline.Behaviors;
using PipelineMetadata = Conduit.Api.PipelineMetadata;
using PipelineConfiguration = Conduit.Api.PipelineConfiguration;
using RetryPolicy = Conduit.Api.RetryPolicy;

namespace Conduit.Pipeline
{
    /// <summary>
    /// Fluent builder for constructing pipelines with stages, behaviors, and interceptors.
    /// </summary>
    /// <typeparam name="TInput">The input type for the pipeline</typeparam>
    /// <typeparam name="TOutput">The output type for the pipeline</typeparam>
    public class PipelineBuilder<TInput, TOutput>
    {
        private readonly List<IPipelineStage<object, object>> _stages;
        private readonly List<IPipelineInterceptor> _interceptors;
        private readonly List<Behaviors.BehaviorContribution> _behaviors;
        private PipelineConfiguration _configuration;
        private PipelineMetadataBuilder _metadataBuilder;
        private Func<Exception, TOutput>? _errorHandler;
        private Action<TOutput>? _completeHandler;
        private Func<TInput, string>? _cacheKeyExtractor;
        private TimeSpan _cacheDuration = TimeSpan.Zero;

        /// <summary>
        /// Initializes a new instance of the PipelineBuilder class.
        /// </summary>
        public PipelineBuilder()
        {
            _stages = new List<IPipelineStage<object, object>>();
            _interceptors = new List<IPipelineInterceptor>();
            _behaviors = new List<Behaviors.BehaviorContribution>();
            _configuration = PipelineConfiguration.Default;
            _metadataBuilder = PipelineMetadata.Builder()
                .WithName("Pipeline")
                .WithType(PipelineType.Sequential);
        }

        /// <summary>
        /// Sets the pipeline name.
        /// </summary>
        /// <param name="name">The pipeline name</param>
        /// <returns>The builder for method chaining</returns>
        public PipelineBuilder<TInput, TOutput> WithName(string name)
        {
            Guard.NotNullOrEmpty(name, nameof(name));
            _metadataBuilder.WithName(name);
            return this;
        }

        /// <summary>
        /// Sets the pipeline description.
        /// </summary>
        /// <param name="description">The pipeline description</param>
        /// <returns>The builder for method chaining</returns>
        public PipelineBuilder<TInput, TOutput> WithDescription(string description)
        {
            Guard.NotNullOrEmpty(description, nameof(description));
            _metadataBuilder.WithDescription(description);
            return this;
        }

        /// <summary>
        /// Sets the pipeline type.
        /// </summary>
        /// <param name="type">The pipeline type</param>
        /// <returns>The builder for method chaining</returns>
        public PipelineBuilder<TInput, TOutput> WithType(PipelineType type)
        {
            _metadataBuilder.WithType(type);
            return this;
        }

        /// <summary>
        /// Adds a stage to the pipeline.
        /// </summary>
        /// <typeparam name="TStageIn">The stage input type</typeparam>
        /// <typeparam name="TStageOut">The stage output type</typeparam>
        /// <param name="stage">The stage to add</param>
        /// <returns>The builder for method chaining</returns>
        public PipelineBuilder<TInput, TOutput> AddStage<TStageIn, TStageOut>(IPipelineStage<TStageIn, TStageOut> stage)
        {
            Guard.NotNull(stage, nameof(stage));

            // Cast to object types for internal storage - type safety is ensured at runtime
            var adaptedStage = new StageAdapter<TStageIn, TStageOut>(stage);
            _stages.Add(adaptedStage);
            _metadataBuilder.AddStage(stage.Name);

            return this;
        }

        /// <summary>
        /// Adds a simple processing function as a stage.
        /// </summary>
        /// <typeparam name="TStageIn">The stage input type</typeparam>
        /// <typeparam name="TStageOut">The stage output type</typeparam>
        /// <param name="name">The stage name</param>
        /// <param name="processor">The processing function</param>
        /// <returns>The builder for method chaining</returns>
        public PipelineBuilder<TInput, TOutput> AddStage<TStageIn, TStageOut>(
            string name,
            Func<TStageIn, Task<TStageOut>> processor)
        {
            Guard.NotNullOrEmpty(name, nameof(name));
            Guard.NotNull(processor, nameof(processor));

            var stage = new FunctionalStage<TStageIn, TStageOut>(name, processor);
            return AddStage(stage);
        }

        /// <summary>
        /// Adds a synchronous processing function as a stage.
        /// </summary>
        /// <typeparam name="TStageIn">The stage input type</typeparam>
        /// <typeparam name="TStageOut">The stage output type</typeparam>
        /// <param name="name">The stage name</param>
        /// <param name="processor">The processing function</param>
        /// <returns>The builder for method chaining</returns>
        public PipelineBuilder<TInput, TOutput> AddStage<TStageIn, TStageOut>(
            string name,
            Func<TStageIn, TStageOut> processor)
        {
            Guard.NotNullOrEmpty(name, nameof(name));
            Guard.NotNull(processor, nameof(processor));

            return AddStage<TStageIn, TStageOut>(name, input => Task.FromResult(processor(input)));
        }

        /// <summary>
        /// Adds an interceptor to the pipeline.
        /// </summary>
        /// <param name="interceptor">The interceptor to add</param>
        /// <returns>The builder for method chaining</returns>
        public PipelineBuilder<TInput, TOutput> AddInterceptor(IPipelineInterceptor interceptor)
        {
            Guard.NotNull(interceptor, nameof(interceptor));
            _interceptors.Add(interceptor);
            return this;
        }

        /// <summary>
        /// Adds a behavior to the pipeline.
        /// </summary>
        /// <param name="behavior">The behavior contribution to add</param>
        /// <returns>The builder for method chaining</returns>
        public PipelineBuilder<TInput, TOutput> AddBehavior(Behaviors.BehaviorContribution behavior)
        {
            Guard.NotNull(behavior, nameof(behavior));
            _behaviors.Add(behavior);
            return this;
        }

        /// <summary>
        /// Adds a behavior with the specified phase and priority.
        /// </summary>
        /// <param name="name">The behavior name</param>
        /// <param name="behavior">The behavior implementation</param>
        /// <param name="phase">The execution phase</param>
        /// <param name="priority">The priority within the phase</param>
        /// <returns>The builder for method chaining</returns>
        public PipelineBuilder<TInput, TOutput> AddBehavior(
            string name,
            IPipelineBehavior behavior,
            Conduit.Pipeline.Behaviors.BehaviorPhase phase = Conduit.Pipeline.Behaviors.BehaviorPhase.Processing,
            int priority = 100)
        {
            Guard.NotNullOrEmpty(name, nameof(name));
            Guard.NotNull(behavior, nameof(behavior));

            var contribution = new Behaviors.BehaviorContribution
            {
                Id = Guid.NewGuid().ToString(),
                Name = name,
                Behavior = behavior,
                Phase = phase,
                Priority = priority,
                IsEnabled = true
            };

            return AddBehavior(contribution);
        }

        /// <summary>
        /// Sets the error handler for the pipeline.
        /// </summary>
        /// <param name="errorHandler">The error handler function</param>
        /// <returns>The builder for method chaining</returns>
        public PipelineBuilder<TInput, TOutput> OnError(Func<Exception, TOutput> errorHandler)
        {
            Guard.NotNull(errorHandler, nameof(errorHandler));
            _errorHandler = errorHandler;
            return this;
        }

        /// <summary>
        /// Sets the completion handler for the pipeline.
        /// </summary>
        /// <param name="completeHandler">The completion handler action</param>
        /// <returns>The builder for method chaining</returns>
        public PipelineBuilder<TInput, TOutput> OnComplete(Action<TOutput> completeHandler)
        {
            Guard.NotNull(completeHandler, nameof(completeHandler));
            _completeHandler = completeHandler;
            return this;
        }

        /// <summary>
        /// Configures the pipeline with custom configuration.
        /// </summary>
        /// <param name="configuration">The pipeline configuration</param>
        /// <returns>The builder for method chaining</returns>
        public PipelineBuilder<TInput, TOutput> Configure(PipelineConfiguration configuration)
        {
            Guard.NotNull(configuration, nameof(configuration));
            _configuration = configuration;
            return this;
        }

        /// <summary>
        /// Configures the pipeline using a configuration action.
        /// </summary>
        /// <param name="configureAction">The configuration action</param>
        /// <returns>The builder for method chaining</returns>
        public PipelineBuilder<TInput, TOutput> Configure(Action<PipelineConfiguration> configureAction)
        {
            Guard.NotNull(configureAction, nameof(configureAction));
            configureAction(_configuration);
            return this;
        }

        /// <summary>
        /// Enables caching for the pipeline.
        /// </summary>
        /// <param name="cacheKeyExtractor">Function to extract cache key from input</param>
        /// <param name="duration">Cache duration</param>
        /// <returns>The builder for method chaining</returns>
        public PipelineBuilder<TInput, TOutput> WithCache(Func<TInput, string> cacheKeyExtractor, TimeSpan duration)
        {
            Guard.NotNull(cacheKeyExtractor, nameof(cacheKeyExtractor));
            if (duration.TotalSeconds < 0)
                throw new ArgumentException("Duration cannot be negative", nameof(duration));

            _cacheKeyExtractor = cacheKeyExtractor;
            _cacheDuration = duration;
            _configuration.CacheEnabled = true;

            return this;
        }

        /// <summary>
        /// Enables parallel processing for the pipeline.
        /// </summary>
        /// <param name="maxConcurrency">Maximum degree of parallelism</param>
        /// <returns>The builder for method chaining</returns>
        public PipelineBuilder<TInput, TOutput> AsParallel(int maxConcurrency = -1)
        {
            _configuration.AsyncExecution = true;
            _configuration.MaxConcurrency = maxConcurrency > 0 ? maxConcurrency : Environment.ProcessorCount;
            _metadataBuilder.WithType(PipelineType.Parallel);

            return this;
        }

        /// <summary>
        /// Builds the configured pipeline.
        /// </summary>
        /// <returns>The constructed pipeline</returns>
        public IPipeline<TInput, TOutput> Build()
        {
            // Validate configuration
            if (_stages.Count == 0 && _behaviors.Count == 0)
            {
                throw new InvalidOperationException("Pipeline must have at least one stage or behavior");
            }

            // Create the pipeline instance
            var pipeline = new Pipeline<TInput, TOutput>(_metadataBuilder.Build(), _configuration);

            // Add stages
            foreach (var stage in _stages)
            {
                pipeline.AddStage(stage);
            }

            // Add interceptors
            foreach (var interceptor in _interceptors)
            {
                pipeline.AddInterceptor(interceptor as Conduit.Api.IPipelineInterceptor ?? throw new InvalidCastException("Unable to cast interceptor to Api type"));
            }

            // Add behaviors
            foreach (var behavior in _behaviors)
            {
                pipeline.AddBehavior(behavior);
            }

            // Configure error handler
            if (_errorHandler != null)
            {
                pipeline.SetErrorHandler(_errorHandler);
            }

            // Configure completion handler
            if (_completeHandler != null)
            {
                pipeline.SetCompletionHandler(_completeHandler);
            }

            // Configure caching
            if (_cacheKeyExtractor != null)
            {
                pipeline.ConfigureCache(_cacheKeyExtractor, _cacheDuration);
            }

            return pipeline;
        }

        /// <summary>
        /// Creates a new pipeline builder.
        /// </summary>
        /// <typeparam name="TIn">Input type</typeparam>
        /// <typeparam name="TOut">Output type</typeparam>
        /// <returns>A new pipeline builder</returns>
        public static PipelineBuilder<TIn, TOut> Create<TIn, TOut>()
        {
            return new PipelineBuilder<TIn, TOut>();
        }

        /// <summary>
        /// Adapter to convert stages to object types for internal storage.
        /// </summary>
        private class StageAdapter<TStageIn, TStageOut> : IPipelineStage<object, object>
        {
            private readonly IPipelineStage<TStageIn, TStageOut> _innerStage;

            public StageAdapter(IPipelineStage<TStageIn, TStageOut> innerStage)
            {
                _innerStage = innerStage;
            }

            public string Name => _innerStage.Name;

            public async Task<object> ProcessAsync(object input, PipelineContext context)
            {
                if (input is TStageIn typedInput)
                {
                    var result = await _innerStage.ProcessAsync(typedInput, context);
                    return result!;
                }

                throw new InvalidOperationException($"Stage {Name} expected input of type {typeof(TStageIn)} but received {input?.GetType()}");
            }
        }

        /// <summary>
        /// Functional stage implementation.
        /// </summary>
        private class FunctionalStage<TStageIn, TStageOut> : IPipelineStage<TStageIn, TStageOut>
        {
            private readonly Func<TStageIn, Task<TStageOut>> _processor;

            public string Name { get; }

            public FunctionalStage(string name, Func<TStageIn, Task<TStageOut>> processor)
            {
                Name = name;
                _processor = processor;
            }

            public async Task<TStageOut> ProcessAsync(TStageIn input, PipelineContext context)
            {
                var result = await _processor(input);
                return result;
            }
        }
    }

    /// <summary>
    /// Extension methods for pipeline builder.
    /// </summary>
    public static class PipelineBuilderExtensions
    {
        /// <summary>
        /// Creates a sequential pipeline builder.
        /// </summary>
        public static PipelineBuilder<TInput, TOutput> Sequential<TInput, TOutput>(this PipelineBuilder<TInput, TOutput> builder)
        {
            return builder.WithType(PipelineType.Sequential)
                          .Configure(c => c.AsyncExecution = false);
        }

        /// <summary>
        /// Creates an event-driven pipeline builder.
        /// </summary>
        public static PipelineBuilder<TInput, TOutput> EventDriven<TInput, TOutput>(this PipelineBuilder<TInput, TOutput> builder)
        {
            return builder.WithType(PipelineType.EventDriven)
                          .Configure(c => c.AsyncExecution = true);
        }

        /// <summary>
        /// Creates a streaming pipeline builder.
        /// </summary>
        public static PipelineBuilder<TInput, TOutput> Streaming<TInput, TOutput>(this PipelineBuilder<TInput, TOutput> builder)
        {
            return builder.WithType(PipelineType.Stream)
                          .Configure(c =>
                          {
                              c.AsyncExecution = true;
                              c.MaxConcurrency = 1;
                          });
        }

        /// <summary>
        /// Creates a batch processing pipeline builder.
        /// </summary>
        public static PipelineBuilder<TInput, TOutput> Batch<TInput, TOutput>(this PipelineBuilder<TInput, TOutput> builder, int batchSize)
        {
            return builder.WithType(PipelineType.Batch)
                          .Configure(c =>
                          {
                              c.AsyncExecution = true;
                              c.MaxConcurrency = batchSize;
                          });
        }
    }
}