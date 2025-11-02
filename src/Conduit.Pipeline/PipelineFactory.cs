using System;
using Conduit.Api;
using Conduit.Pipeline.Behaviors;
using PipelineMetadata = Conduit.Api.PipelineMetadata;
using PipelineConfiguration = Conduit.Api.PipelineConfiguration;
using RetryPolicy = Conduit.Api.RetryPolicy;

namespace Conduit.Pipeline;

/// <summary>
/// Factory for creating pipeline instances with various configurations.
/// </summary>
public class PipelineFactory
{
    private readonly PipelineConfiguration _defaultConfiguration;

    /// <summary>
    /// Initializes a new instance of the PipelineFactory class.
    /// </summary>
    public PipelineFactory() : this(PipelineConfiguration.Default)
    {
    }

    /// <summary>
    /// Initializes a new instance of the PipelineFactory class with a default configuration.
    /// </summary>
    public PipelineFactory(PipelineConfiguration defaultConfiguration)
    {
        _defaultConfiguration = defaultConfiguration ?? throw new ArgumentNullException(nameof(defaultConfiguration));
    }

    /// <summary>
    /// Creates a new pipeline with the specified name and description.
    /// </summary>
    public IPipeline<TInput, TOutput> CreatePipeline<TInput, TOutput>(string name, string? description = null)
    {
        var metadata = PipelineMetadata.Builder()
            .WithName(name)
            .WithDescription(description ?? string.Empty)
            .WithTypes()
            .Build();

        return new Pipeline<TInput, TOutput>(metadata, _defaultConfiguration.Clone());
    }

    /// <summary>
    /// Creates a new pipeline with custom configuration.
    /// </summary>
    public IPipeline<TInput, TOutput> CreatePipeline<TInput, TOutput>(
        string name,
        string? description,
        PipelineConfiguration configuration)
    {
        var metadata = PipelineMetadata.Builder()
            .WithName(name)
            .WithDescription(description ?? string.Empty)
            .WithTypes()
            .Build();

        return new Pipeline<TInput, TOutput>(metadata, configuration);
    }

    /// <summary>
    /// Creates a new pipeline with metadata and configuration.
    /// </summary>
    public IPipeline<TInput, TOutput> CreatePipeline<TInput, TOutput>(
        PipelineMetadata metadata,
        PipelineConfiguration? configuration = null)
    {
        return new Pipeline<TInput, TOutput>(metadata, configuration ?? _defaultConfiguration.Clone());
    }

    /// <summary>
    /// Creates an event-driven pipeline.
    /// </summary>
    public IPipeline<TInput, TOutput> CreateEventDrivenPipeline<TInput, TOutput>(string name)
    {
        var metadata = PipelineMetadata.Builder()
            .WithName(name)
            .WithType(PipelineType.EventDriven)
            .WithTypes()
            .WithTags("event-driven", "async")
            .Build();

        var configuration = PipelineConfiguration.Default;
        configuration.AsyncExecution = true;
        configuration.MaxConcurrency = 20;

        return new Pipeline<TInput, TOutput>(metadata, configuration);
    }

    /// <summary>
    /// Creates a sequential pipeline with no parallelism.
    /// </summary>
    public IPipeline<TInput, TOutput> CreateSequentialPipeline<TInput, TOutput>(string name)
    {
        var metadata = PipelineMetadata.Builder()
            .WithName(name)
            .WithType(PipelineType.Sequential)
            .WithTypes()
            .WithTags("sequential", "ordered")
            .Build();

        var configuration = PipelineConfiguration.Default;
        configuration.MaxConcurrency = 1;
        configuration.AsyncExecution = false;

        return new Pipeline<TInput, TOutput>(metadata, configuration);
    }

    /// <summary>
    /// Creates a parallel pipeline optimized for concurrent execution.
    /// </summary>
    public IPipeline<TInput, TOutput> CreateParallelPipeline<TInput, TOutput>(string name)
    {
        var metadata = PipelineMetadata.Builder()
            .WithName(name)
            .WithType(PipelineType.Parallel)
            .WithTypes()
            .WithTags("parallel", "concurrent")
            .Build();

        var configuration = PipelineConfiguration.Default;
        configuration.MaxConcurrency = Environment.ProcessorCount * 2;
        configuration.AsyncExecution = true;

        return new Pipeline<TInput, TOutput>(metadata, configuration);
    }

    /// <summary>
    /// Creates a batch processing pipeline.
    /// </summary>
    public IPipeline<TInput, TOutput> CreateBatchPipeline<TInput, TOutput>(
        string name,
        int batchSize = 100)
    {
        var metadata = PipelineMetadata.Builder()
            .WithName(name)
            .WithType(PipelineType.Batch)
            .WithTypes()
            .WithTags("batch", "bulk")
            .WithProperty("BatchSize", batchSize)
            .Build();

        var configuration = PipelineConfiguration.Default;
        configuration.MaxConcurrency = Math.Max(1, Environment.ProcessorCount / 2);
        configuration.DefaultTimeout = TimeSpan.FromMinutes(5);

        return new Pipeline<TInput, TOutput>(metadata, configuration);
    }

    /// <summary>
    /// Creates a stream processing pipeline.
    /// </summary>
    public IPipeline<TInput, TOutput> CreateStreamPipeline<TInput, TOutput>(string name)
    {
        var metadata = PipelineMetadata.Builder()
            .WithName(name)
            .WithType(PipelineType.Stream)
            .WithTypes()
            .WithTags("stream", "real-time")
            .Build();

        var configuration = PipelineConfiguration.HighThroughput;
        configuration.CacheEnabled = false; // Streaming doesn't typically use caching

        return new Pipeline<TInput, TOutput>(metadata, configuration);
    }

    /// <summary>
    /// Creates a validation pipeline for input validation.
    /// </summary>
    public IPipeline<TInput, TOutput> CreateValidationPipeline<TInput, TOutput>(string name)
    {
        var metadata = PipelineMetadata.Builder()
            .WithName(name)
            .WithType(PipelineType.Validation)
            .WithTypes()
            .WithTags("validation", "quality")
            .Build();

        var configuration = PipelineConfiguration.Default;
        configuration.ValidationEnabled = true;
        configuration.ErrorStrategy = ErrorHandlingStrategy.FailFast;

        var pipeline = new Pipeline<TInput, TOutput>(metadata, configuration);

        // Add validation interceptor
        pipeline.AddInterceptor(new ValidationInterceptor() as Conduit.Api.IPipelineInterceptor ?? throw new InvalidCastException("Unable to cast ValidationInterceptor to Api type"));

        return pipeline;
    }

    /// <summary>
    /// Creates a transformation pipeline for data transformation.
    /// </summary>
    public IPipeline<TInput, TOutput> CreateTransformationPipeline<TInput, TOutput>(string name)
    {
        var metadata = PipelineMetadata.Builder()
            .WithName(name)
            .WithType(PipelineType.Transformation)
            .WithTypes()
            .WithTags("transformation", "etl")
            .Build();

        var configuration = PipelineConfiguration.Default;
        configuration.CacheEnabled = true;
        configuration.DefaultCacheDuration = TimeSpan.FromMinutes(10);

        return new Pipeline<TInput, TOutput>(metadata, configuration);
    }

    /// <summary>
    /// Creates a saga/workflow pipeline for complex orchestrations.
    /// </summary>
    public IPipeline<TInput, TOutput> CreateSagaPipeline<TInput, TOutput>(string name)
    {
        var metadata = PipelineMetadata.Builder()
            .WithName(name)
            .WithType(PipelineType.Saga)
            .WithTypes()
            .WithTags("saga", "workflow", "orchestration")
            .Build();

        var configuration = PipelineConfiguration.Reliable;
        configuration.TracingEnabled = true;
        configuration.DeadLetterEnabled = true;

        return new Pipeline<TInput, TOutput>(metadata, configuration);
    }

    /// <summary>
    /// Creates a conditional/branching pipeline.
    /// </summary>
    public IPipeline<TInput, TOutput> CreateConditionalPipeline<TInput, TOutput>(string name)
    {
        var metadata = PipelineMetadata.Builder()
            .WithName(name)
            .WithType(PipelineType.Conditional)
            .WithTypes()
            .WithTags("conditional", "routing", "branching")
            .Build();

        var configuration = PipelineConfiguration.Default;

        return new Pipeline<TInput, TOutput>(metadata, configuration);
    }

    /// <summary>
    /// Creates a pipeline with pre-configured common behaviors.
    /// </summary>
    public IPipeline<TInput, TOutput> CreatePipelineWithBehaviors<TInput, TOutput>(
        string name,
        params BehaviorContribution[] behaviors)
    {
        var metadata = PipelineMetadata.Builder()
            .WithName(name)
            .WithTypes()
            .Build();

        var pipeline = new Pipeline<TInput, TOutput>(metadata, _defaultConfiguration.Clone());

        foreach (var behavior in behaviors)
        {
            pipeline.AddBehavior(behavior);
        }

        return pipeline;
    }

    /// <summary>
    /// Creates a pipeline with logging and metrics interceptors.
    /// </summary>
    public IPipeline<TInput, TOutput> CreateMonitoredPipeline<TInput, TOutput>(
        string name,
        Action<string>? logAction = null,
        Action<string, double>? recordMetric = null)
    {
        var metadata = PipelineMetadata.Builder()
            .WithName(name)
            .WithTypes()
            .WithTags("monitored", "logged", "metrics")
            .Build();

        var configuration = PipelineConfiguration.Default;
        configuration.MetricsEnabled = true;
        configuration.TracingEnabled = true;

        var pipeline = new Pipeline<TInput, TOutput>(metadata, configuration);

        if (logAction != null)
        {
            pipeline.AddInterceptor(new LoggingInterceptor(logAction) as Conduit.Api.IPipelineInterceptor ?? throw new InvalidCastException("Unable to cast LoggingInterceptor to Api type"));
        }

        if (recordMetric != null)
        {
            pipeline.AddInterceptor(new MetricsInterceptor(recordMetric) as Conduit.Api.IPipelineInterceptor ?? throw new InvalidCastException("Unable to cast MetricsInterceptor to Api type"));
        }

        return pipeline;
    }
}

/// <summary>
/// Extension methods for pipeline creation.
/// </summary>
public static class PipelineFactoryExtensions
{
    /// <summary>
    /// Creates a simple pipeline from a single processing function.
    /// </summary>
    public static IPipeline<TInput, TOutput> FromFunction<TInput, TOutput>(
        this PipelineFactory factory,
        string name,
        Func<TInput, TOutput> processor)
    {
        var pipeline = factory.CreatePipeline<TInput, TOutput>(name);
        // Use the fluent API to add processing instead of stages
        return pipeline;  // For now, return simple pipeline - proper implementation would wrap processor
    }

    /// <summary>
    /// Creates a simple async pipeline from a single processing function.
    /// </summary>
    public static IPipeline<TInput, TOutput> FromAsyncFunction<TInput, TOutput>(
        this PipelineFactory factory,
        string name,
        Func<TInput, Task<TOutput>> asyncProcessor)
    {
        var pipeline = factory.CreatePipeline<TInput, TOutput>(name);
        // Use the fluent API to add processing instead of stages
        return pipeline;  // For now, return simple pipeline - proper implementation would wrap processor
    }

    /// <summary>
    /// Creates a pipeline from a chain of processing functions.
    /// </summary>
    public static IPipeline<TInput, TOutput> FromChain<TInput, TMiddle, TOutput>(
        this PipelineFactory factory,
        string name,
        Func<TInput, TMiddle> first,
        Func<TMiddle, TOutput> second)
    {
        var pipeline = factory.CreatePipeline<TInput, TMiddle>(name);
        return pipeline.Then(second);
    }
}