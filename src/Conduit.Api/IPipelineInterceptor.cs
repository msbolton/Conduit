namespace Conduit.Api;

/// <summary>
/// Represents an interceptor that can modify pipeline behavior.
/// </summary>
public interface IPipelineInterceptor
{
    /// <summary>
    /// Gets the priority of this interceptor (lower values execute first).
    /// </summary>
    int Priority { get; }

    /// <summary>
    /// Called before the pipeline execution.
    /// </summary>
    Task BeforeExecutionAsync(object input, PipelineContext context);

    /// <summary>
    /// Called after the pipeline execution.
    /// </summary>
    Task AfterExecutionAsync(object input, object result, PipelineContext context);

    /// <summary>
    /// Called before a stage execution.
    /// </summary>
    Task BeforeStageAsync(string stageName, object input, PipelineContext context);

    /// <summary>
    /// Called after a stage execution.
    /// </summary>
    Task AfterStageAsync(string stageName, object result, PipelineContext context);

    /// <summary>
    /// Called when an error occurs.
    /// </summary>
    /// <returns>True if the error was handled, false otherwise</returns>
    Task<bool> OnErrorAsync(object input, Exception exception, PipelineContext context);
}