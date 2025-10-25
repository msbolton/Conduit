using System;
using System.Threading.Tasks;

namespace Conduit.Pipeline;

/// <summary>
/// Represents an interceptor that can hook into various points of pipeline execution.
/// Interceptors are cross-cutting concerns that apply to the entire pipeline.
/// </summary>
public interface IPipelineInterceptor
{
    /// <summary>
    /// Gets the name of this interceptor.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets the priority of this interceptor. Lower values execute first.
    /// </summary>
    int Priority { get; }

    /// <summary>
    /// Called before the pipeline starts execution.
    /// </summary>
    /// <param name="input">The input to the pipeline</param>
    /// <param name="context">The pipeline context</param>
    Task BeforeExecutionAsync(object input, PipelineContext context);

    /// <summary>
    /// Called after the pipeline completes execution successfully.
    /// </summary>
    /// <param name="input">The input to the pipeline</param>
    /// <param name="output">The output from the pipeline</param>
    /// <param name="context">The pipeline context</param>
    Task AfterExecutionAsync(object input, object output, PipelineContext context);

    /// <summary>
    /// Called when an error occurs during pipeline execution.
    /// </summary>
    /// <param name="input">The input to the pipeline</param>
    /// <param name="error">The error that occurred</param>
    /// <param name="context">The pipeline context</param>
    /// <returns>True to suppress the error and continue, false to propagate the error</returns>
    Task<bool> OnErrorAsync(object input, Exception error, PipelineContext context);

    /// <summary>
    /// Called before a stage starts execution.
    /// </summary>
    /// <param name="stageName">The name of the stage</param>
    /// <param name="stageInput">The input to the stage</param>
    /// <param name="context">The pipeline context</param>
    Task BeforeStageAsync(string stageName, object stageInput, PipelineContext context);

    /// <summary>
    /// Called after a stage completes execution.
    /// </summary>
    /// <param name="stageName">The name of the stage</param>
    /// <param name="stageOutput">The output from the stage</param>
    /// <param name="context">The pipeline context</param>
    Task AfterStageAsync(string stageName, object stageOutput, PipelineContext context);
}

/// <summary>
/// Base implementation of a pipeline interceptor with default no-op implementations.
/// </summary>
public abstract class PipelineInterceptor : IPipelineInterceptor
{
    /// <inheritdoc />
    public virtual string Name => GetType().Name;

    /// <inheritdoc />
    public virtual int Priority => 1000;

    /// <inheritdoc />
    public virtual Task BeforeExecutionAsync(object input, PipelineContext context)
    {
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public virtual Task AfterExecutionAsync(object input, object output, PipelineContext context)
    {
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public virtual Task<bool> OnErrorAsync(object input, Exception error, PipelineContext context)
    {
        return Task.FromResult(false); // Don't suppress errors by default
    }

    /// <inheritdoc />
    public virtual Task BeforeStageAsync(string stageName, object stageInput, PipelineContext context)
    {
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public virtual Task AfterStageAsync(string stageName, object stageOutput, PipelineContext context)
    {
        return Task.CompletedTask;
    }
}

/// <summary>
/// A logging interceptor that logs pipeline execution.
/// </summary>
public class LoggingInterceptor : PipelineInterceptor
{
    private readonly Action<string> _logAction;

    /// <summary>
    /// Initializes a new instance of the LoggingInterceptor class.
    /// </summary>
    public LoggingInterceptor(Action<string> logAction)
    {
        _logAction = logAction ?? throw new ArgumentNullException(nameof(logAction));
    }

    /// <inheritdoc />
    public override string Name => "Logging";

    /// <inheritdoc />
    public override int Priority => 100; // Run early

    /// <inheritdoc />
    public override Task BeforeExecutionAsync(object input, PipelineContext context)
    {
        _logAction($"[{context.ContextId}] Pipeline execution started - Input type: {input?.GetType().Name ?? "null"}");
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public override Task AfterExecutionAsync(object input, object output, PipelineContext context)
    {
        var duration = context.GetExecutionDuration();
        _logAction($"[{context.ContextId}] Pipeline execution completed - Output type: {output?.GetType().Name ?? "null"}, Duration: {duration?.TotalMilliseconds ?? 0}ms");
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public override Task<bool> OnErrorAsync(object input, Exception error, PipelineContext context)
    {
        _logAction($"[{context.ContextId}] Pipeline error: {error.GetType().Name} - {error.Message}");
        return Task.FromResult(false);
    }

    /// <inheritdoc />
    public override Task BeforeStageAsync(string stageName, object stageInput, PipelineContext context)
    {
        _logAction($"[{context.ContextId}] Stage '{stageName}' starting");
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public override Task AfterStageAsync(string stageName, object stageOutput, PipelineContext context)
    {
        _logAction($"[{context.ContextId}] Stage '{stageName}' completed");
        return Task.CompletedTask;
    }
}

/// <summary>
/// A metrics interceptor that collects pipeline metrics.
/// </summary>
public class MetricsInterceptor : PipelineInterceptor
{
    private readonly Action<string, double>? _recordMetric;
    private readonly Action<string, long>? _incrementCounter;

    /// <summary>
    /// Initializes a new instance of the MetricsInterceptor class.
    /// </summary>
    public MetricsInterceptor(
        Action<string, double>? recordMetric = null,
        Action<string, long>? incrementCounter = null)
    {
        _recordMetric = recordMetric;
        _incrementCounter = incrementCounter;
    }

    /// <inheritdoc />
    public override string Name => "Metrics";

    /// <inheritdoc />
    public override int Priority => 200;

    /// <inheritdoc />
    public override Task BeforeExecutionAsync(object input, PipelineContext context)
    {
        context.StartTime = DateTimeOffset.UtcNow;
        _incrementCounter?.Invoke($"pipeline.{context.PipelineName ?? "unknown"}.started", 1);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public override Task AfterExecutionAsync(object input, object output, PipelineContext context)
    {
        context.EndTime = DateTimeOffset.UtcNow;
        var duration = context.GetExecutionDuration();

        if (duration.HasValue)
        {
            _recordMetric?.Invoke($"pipeline.{context.PipelineName ?? "unknown"}.duration_ms", duration.Value.TotalMilliseconds);
        }

        _incrementCounter?.Invoke($"pipeline.{context.PipelineName ?? "unknown"}.completed", 1);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public override Task<bool> OnErrorAsync(object input, Exception error, PipelineContext context)
    {
        _incrementCounter?.Invoke($"pipeline.{context.PipelineName ?? "unknown"}.errors", 1);
        _incrementCounter?.Invoke($"pipeline.{context.PipelineName ?? "unknown"}.errors.{error.GetType().Name}", 1);
        return Task.FromResult(false);
    }

    /// <inheritdoc />
    public override Task BeforeStageAsync(string stageName, object stageInput, PipelineContext context)
    {
        context.SetProperty($"Stage.{stageName}.StartTime", DateTimeOffset.UtcNow);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public override Task AfterStageAsync(string stageName, object stageOutput, PipelineContext context)
    {
        if (context.HasProperty($"Stage.{stageName}.StartTime"))
        {
            var startTime = context.GetValueProperty<DateTimeOffset>($"Stage.{stageName}.StartTime");
            var duration = DateTimeOffset.UtcNow - startTime;

            _recordMetric?.Invoke($"pipeline.{context.PipelineName ?? "unknown"}.stage.{stageName}.duration_ms", duration.TotalMilliseconds);
        }

        return Task.CompletedTask;
    }
}

/// <summary>
/// A validation interceptor that validates inputs and outputs.
/// </summary>
public class ValidationInterceptor : PipelineInterceptor
{
    private readonly Func<object, bool>? _inputValidator;
    private readonly Func<object, bool>? _outputValidator;

    /// <summary>
    /// Initializes a new instance of the ValidationInterceptor class.
    /// </summary>
    public ValidationInterceptor(
        Func<object, bool>? inputValidator = null,
        Func<object, bool>? outputValidator = null)
    {
        _inputValidator = inputValidator;
        _outputValidator = outputValidator;
    }

    /// <inheritdoc />
    public override string Name => "Validation";

    /// <inheritdoc />
    public override int Priority => 300;

    /// <inheritdoc />
    public override Task BeforeExecutionAsync(object input, PipelineContext context)
    {
        if (_inputValidator != null && !_inputValidator(input))
        {
            throw new ArgumentException("Pipeline input validation failed", nameof(input));
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public override Task AfterExecutionAsync(object input, object output, PipelineContext context)
    {
        if (_outputValidator != null && !_outputValidator(output))
        {
            throw new InvalidOperationException("Pipeline output validation failed");
        }

        return Task.CompletedTask;
    }
}