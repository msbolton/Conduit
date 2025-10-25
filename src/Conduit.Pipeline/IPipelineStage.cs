using System;
using System.Threading.Tasks;

namespace Conduit.Pipeline;

/// <summary>
/// Represents a stage in the pipeline that processes input and produces output.
/// </summary>
/// <typeparam name="TInput">The type of input this stage accepts</typeparam>
/// <typeparam name="TOutput">The type of output this stage produces</typeparam>
public interface IPipelineStage<TInput, TOutput>
{
    /// <summary>
    /// Processes the input and produces output.
    /// </summary>
    /// <param name="input">The input to process</param>
    /// <param name="context">The pipeline context</param>
    /// <returns>The processed output</returns>
    Task<TOutput> ProcessAsync(TInput input, PipelineContext context);

    /// <summary>
    /// Gets the name of this stage.
    /// </summary>
    string Name { get; }
}

/// <summary>
/// Base implementation of a pipeline stage.
/// </summary>
public abstract class PipelineStage<TInput, TOutput> : IPipelineStage<TInput, TOutput>
{
    /// <inheritdoc />
    public virtual string Name => GetType().Name;

    /// <inheritdoc />
    public abstract Task<TOutput> ProcessAsync(TInput input, PipelineContext context);

    /// <summary>
    /// Chains another stage after this one.
    /// </summary>
    public IPipelineStage<TInput, TNextOutput> AndThen<TNextOutput>(IPipelineStage<TOutput, TNextOutput> nextStage)
    {
        return new CompositeStage<TInput, TOutput, TNextOutput>(this, nextStage);
    }

    /// <summary>
    /// Maps the output to a different type.
    /// </summary>
    public IPipelineStage<TInput, TNewOutput> Map<TNewOutput>(Func<TOutput, TNewOutput> mapper)
    {
        return new MappingStage<TInput, TOutput, TNewOutput>(this, mapper);
    }

    /// <summary>
    /// Maps the output asynchronously.
    /// </summary>
    public IPipelineStage<TInput, TNewOutput> MapAsync<TNewOutput>(Func<TOutput, Task<TNewOutput>> asyncMapper)
    {
        return new AsyncMappingStage<TInput, TOutput, TNewOutput>(this, asyncMapper);
    }

    /// <summary>
    /// Filters the output.
    /// </summary>
    public IPipelineStage<TInput, TOutput?> Filter(Predicate<TOutput> predicate)
        where TOutput : class
    {
        return new FilteringStage<TInput, TOutput>(this, predicate);
    }
}

/// <summary>
/// A stage created from a delegate function.
/// </summary>
public class DelegateStage<TInput, TOutput> : PipelineStage<TInput, TOutput>
{
    private readonly Func<TInput, PipelineContext, Task<TOutput>> _processor;
    private readonly string _name;

    /// <summary>
    /// Initializes a new instance of the DelegateStage class.
    /// </summary>
    public DelegateStage(Func<TInput, PipelineContext, Task<TOutput>> processor, string? name = null)
    {
        _processor = processor ?? throw new ArgumentNullException(nameof(processor));
        _name = name ?? "DelegateStage";
    }

    /// <inheritdoc />
    public override string Name => _name;

    /// <inheritdoc />
    public override Task<TOutput> ProcessAsync(TInput input, PipelineContext context)
    {
        return _processor(input, context);
    }

    /// <summary>
    /// Creates a stage from a synchronous function.
    /// </summary>
    public static DelegateStage<TInput, TOutput> Create(
        Func<TInput, TOutput> processor,
        string? name = null)
    {
        return new DelegateStage<TInput, TOutput>(
            (input, _) => Task.FromResult(processor(input)),
            name);
    }

    /// <summary>
    /// Creates a stage from an asynchronous function.
    /// </summary>
    public static DelegateStage<TInput, TOutput> Create(
        Func<TInput, Task<TOutput>> processor,
        string? name = null)
    {
        return new DelegateStage<TInput, TOutput>(
            (input, _) => processor(input),
            name);
    }
}

/// <summary>
/// A composite stage that chains two stages together.
/// </summary>
internal class CompositeStage<TInput, TMiddle, TOutput> : PipelineStage<TInput, TOutput>
{
    private readonly IPipelineStage<TInput, TMiddle> _first;
    private readonly IPipelineStage<TMiddle, TOutput> _second;

    public CompositeStage(IPipelineStage<TInput, TMiddle> first, IPipelineStage<TMiddle, TOutput> second)
    {
        _first = first ?? throw new ArgumentNullException(nameof(first));
        _second = second ?? throw new ArgumentNullException(nameof(second));
    }

    public override string Name => $"{_first.Name} -> {_second.Name}";

    public override async Task<TOutput> ProcessAsync(TInput input, PipelineContext context)
    {
        var middle = await _first.ProcessAsync(input, context);
        return await _second.ProcessAsync(middle, context);
    }
}

/// <summary>
/// A stage that maps output to a different type.
/// </summary>
internal class MappingStage<TInput, TOriginalOutput, TNewOutput> : PipelineStage<TInput, TNewOutput>
{
    private readonly IPipelineStage<TInput, TOriginalOutput> _innerStage;
    private readonly Func<TOriginalOutput, TNewOutput> _mapper;

    public MappingStage(IPipelineStage<TInput, TOriginalOutput> innerStage, Func<TOriginalOutput, TNewOutput> mapper)
    {
        _innerStage = innerStage ?? throw new ArgumentNullException(nameof(innerStage));
        _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
    }

    public override string Name => $"{_innerStage.Name} -> Map";

    public override async Task<TNewOutput> ProcessAsync(TInput input, PipelineContext context)
    {
        var result = await _innerStage.ProcessAsync(input, context);
        return _mapper(result);
    }
}

/// <summary>
/// A stage that maps output asynchronously.
/// </summary>
internal class AsyncMappingStage<TInput, TOriginalOutput, TNewOutput> : PipelineStage<TInput, TNewOutput>
{
    private readonly IPipelineStage<TInput, TOriginalOutput> _innerStage;
    private readonly Func<TOriginalOutput, Task<TNewOutput>> _asyncMapper;

    public AsyncMappingStage(IPipelineStage<TInput, TOriginalOutput> innerStage, Func<TOriginalOutput, Task<TNewOutput>> asyncMapper)
    {
        _innerStage = innerStage ?? throw new ArgumentNullException(nameof(innerStage));
        _asyncMapper = asyncMapper ?? throw new ArgumentNullException(nameof(asyncMapper));
    }

    public override string Name => $"{_innerStage.Name} -> MapAsync";

    public override async Task<TNewOutput> ProcessAsync(TInput input, PipelineContext context)
    {
        var result = await _innerStage.ProcessAsync(input, context);
        return await _asyncMapper(result);
    }
}

/// <summary>
/// A stage that filters output.
/// </summary>
internal class FilteringStage<TInput, TOutput> : PipelineStage<TInput, TOutput?>
    where TOutput : class
{
    private readonly IPipelineStage<TInput, TOutput> _innerStage;
    private readonly Predicate<TOutput> _predicate;

    public FilteringStage(IPipelineStage<TInput, TOutput> innerStage, Predicate<TOutput> predicate)
    {
        _innerStage = innerStage ?? throw new ArgumentNullException(nameof(innerStage));
        _predicate = predicate ?? throw new ArgumentNullException(nameof(predicate));
    }

    public override string Name => $"{_innerStage.Name} -> Filter";

    public override async Task<TOutput?> ProcessAsync(TInput input, PipelineContext context)
    {
        var result = await _innerStage.ProcessAsync(input, context);
        return _predicate(result) ? result : null;
    }
}