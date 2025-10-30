namespace Conduit.Api;

/// <summary>
/// Represents a stage in the pipeline that processes input and produces output.
/// </summary>
/// <typeparam name="TInput">The type of input this stage accepts</typeparam>
/// <typeparam name="TOutput">The type of output this stage produces</typeparam>
public interface IPipelineStage<in TInput, TOutput>
{
    /// <summary>
    /// Gets the name of this stage.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Processes the input and produces output.
    /// </summary>
    /// <param name="input">The input to process</param>
    /// <param name="context">The pipeline context</param>
    /// <returns>The processed output</returns>
    Task<TOutput> ProcessAsync(TInput input, PipelineContext context);
}