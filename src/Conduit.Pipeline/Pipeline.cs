using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Conduit.Pipeline.Behaviors;

namespace Conduit.Pipeline;

/// <summary>
/// Default implementation of IPipeline with full support for stages, behaviors, and interceptors.
/// </summary>
public class Pipeline<TInput, TOutput> : IPipeline<TInput, TOutput>
{
    private readonly List<IPipelineStage<object, object>> _stages = new();
    private readonly List<IPipelineInterceptor> _interceptors = new();
    private readonly List<BehaviorContribution> _behaviors = new();
    private readonly ConcurrentDictionary<string, (object Result, DateTimeOffset Expiry)> _cache = new();
    private readonly PipelineMetadata _metadata;
    private readonly PipelineConfiguration _configuration;
    private Func<Exception, TOutput>? _errorHandler;
    private Func<TInput, string>? _cacheKeySelector;
    private TimeSpan _cacheDuration = TimeSpan.Zero;

    /// <summary>
    /// Initializes a new instance of the Pipeline class.
    /// </summary>
    public Pipeline(PipelineMetadata metadata, PipelineConfiguration configuration)
    {
        _metadata = metadata ?? throw new ArgumentNullException(nameof(metadata));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _configuration.InitializeConcurrencyControl();
    }

    /// <inheritdoc />
    public PipelineMetadata Metadata => _metadata;

    /// <inheritdoc />
    public PipelineConfiguration Configuration => _configuration;

    /// <inheritdoc />
    public async Task<TOutput> ExecuteAsync(TInput input, CancellationToken cancellationToken = default)
    {
        var context = new PipelineContext
        {
            PipelineId = _metadata.PipelineId,
            PipelineName = _metadata.Name,
            Input = input,
            CancellationToken = cancellationToken,
            StartTime = DateTimeOffset.UtcNow
        };

        return await ExecuteAsync(input, context, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<TOutput> ExecuteAsync(TInput input, PipelineContext context, CancellationToken cancellationToken = default)
    {
        context.CancellationToken = CancellationTokenSource.CreateLinkedTokenSource(
            context.CancellationToken, cancellationToken).Token;

        // Check cache if enabled
        if (_configuration.CacheEnabled && _cacheKeySelector != null)
        {
            var cacheKey = _cacheKeySelector(input);
            if (TryGetFromCache(cacheKey, out var cachedResult))
            {
                return cachedResult;
            }
        }

        // Apply concurrency control
        if (_configuration.ConcurrencySemaphore != null)
        {
            await _configuration.ConcurrencySemaphore.WaitAsync(cancellationToken);
        }

        try
        {
            // Sort interceptors by priority
            var sortedInterceptors = _interceptors.OrderBy(i => i.Priority).ToList();

            // Call beforeExecution on all interceptors
            foreach (var interceptor in sortedInterceptors)
            {
                await interceptor.BeforeExecutionAsync(input!, context);
            }

            // Build and execute the behavior chain
            var result = await ExecuteBehaviorChainAsync(input!, context);

            // Call afterExecution on all interceptors
            foreach (var interceptor in sortedInterceptors)
            {
                await interceptor.AfterExecutionAsync(input!, result!, context);
            }

            context.EndTime = DateTimeOffset.UtcNow;
            context.Result = result;

            // Cache result if configured
            if (_configuration.CacheEnabled && _cacheKeySelector != null)
            {
                var cacheKey = _cacheKeySelector(input);
                AddToCache(cacheKey, (TOutput)result!);
            }

            return (TOutput)result!;
        }
        catch (Exception ex)
        {
            context.Exception = ex;

            // Call onError on all interceptors
            var sortedInterceptors = _interceptors.OrderBy(i => i.Priority).ToList();
            foreach (var interceptor in sortedInterceptors)
            {
                if (await interceptor.OnErrorAsync(input!, ex, context))
                {
                    // If interceptor handled the error, return the context result
                    return (TOutput)context.Result!;
                }
            }

            // Apply error handler if configured
            if (_errorHandler != null)
            {
                return _errorHandler(ex);
            }

            // Apply error strategy
            return await HandleErrorAsync(input, ex, context);
        }
        finally
        {
            _configuration.ConcurrencySemaphore?.Release();
        }
    }

    private async Task<object?> ExecuteBehaviorChainAsync(object input, PipelineContext context)
    {
        // Get enabled behaviors sorted by placement
        var enabledBehaviors = _behaviors
            .WhereEnabled()
            .WhereConstraintsSatisfied(context)
            .SortByPlacement()
            .ToList();

        // If no behaviors, execute stages directly
        if (!enabledBehaviors.Any())
        {
            return await ExecuteStagesAsync(input, context);
        }

        // Build behavior chain
        var chain = BuildBehaviorChain(enabledBehaviors, async ctx =>
        {
            // This is the terminal behavior that executes the actual stages
            return await ExecuteStagesAsync(ctx.Input!, ctx);
        });

        return await chain.ProceedAsync(context);
    }

    private BehaviorChain BuildBehaviorChain(
        List<BehaviorContribution> behaviors,
        Func<PipelineContext, Task<object?>> terminal)
    {
        // Start with the terminal chain
        var chain = new BehaviorChain(terminal);

        // Build chain in reverse order so first behavior executes first
        for (int i = behaviors.Count - 1; i >= 0; i--)
        {
            var behavior = behaviors[i];
            var previousChain = chain;
            chain = new BehaviorChain(async ctx =>
                await behavior.Behavior.ExecuteAsync(ctx, previousChain));
        }

        // Apply configuration-based decorators
        if (_configuration.DefaultTimeout > TimeSpan.Zero)
        {
            chain = chain.WithTimeout(_configuration.DefaultTimeout);
        }

        if (_configuration.MaxRetries > 0)
        {
            chain = chain.WithRetry(_configuration.MaxRetries, _configuration.RetryDelay);
        }

        return chain;
    }

    private async Task<object?> ExecuteStagesAsync(object input, PipelineContext context)
    {
        if (!_stages.Any())
        {
            return input;
        }

        var currentInput = input;
        var sortedInterceptors = _interceptors.OrderBy(i => i.Priority).ToList();

        for (int i = 0; i < _stages.Count; i++)
        {
            var stage = _stages[i];
            context.CurrentStage = stage.Name;
            context.MarkStageCompleted(i);

            // Call beforeStage on all interceptors
            foreach (var interceptor in sortedInterceptors)
            {
                await interceptor.BeforeStageAsync(stage.Name, currentInput, context);
            }

            // Execute stage
            var stageResult = await stage.ProcessAsync(currentInput, context);

            // Call afterStage on all interceptors
            foreach (var interceptor in sortedInterceptors)
            {
                await interceptor.AfterStageAsync(stage.Name, stageResult, context);
            }

            currentInput = stageResult;
        }

        return currentInput;
    }

    private async Task<TOutput> HandleErrorAsync(TInput input, Exception exception, PipelineContext context)
    {
        switch (_configuration.ErrorStrategy)
        {
            case ErrorHandlingStrategy.Retry:
                // Retry logic is handled by behavior chain
                throw exception;

            case ErrorHandlingStrategy.Continue:
                // Return default value and log error
                return default!;

            case ErrorHandlingStrategy.DeadLetter:
                // Would send to dead letter queue
                context.SetProperty("DeadLetter", true);
                context.SetProperty("DeadLetterReason", exception.Message);
                throw new InvalidOperationException($"Message sent to dead letter: {exception.Message}", exception);

            case ErrorHandlingStrategy.FailFast:
            case ErrorHandlingStrategy.Custom:
            default:
                throw exception;
        }
    }

    private bool TryGetFromCache(string key, out TOutput result)
    {
        if (_cache.TryGetValue(key, out var cached))
        {
            if (cached.Expiry > DateTimeOffset.UtcNow)
            {
                result = (TOutput)cached.Result;
                return true;
            }
            else
            {
                _cache.TryRemove(key, out _);
            }
        }

        result = default!;
        return false;
    }

    private void AddToCache(string key, TOutput result)
    {
        var expiry = DateTimeOffset.UtcNow.Add(_cacheDuration > TimeSpan.Zero
            ? _cacheDuration
            : _configuration.DefaultCacheDuration);

        _cache[key] = (result!, expiry);

        // Simple cache size management
        if (_cache.Count > _configuration.MaxCacheSize)
        {
            // Remove expired entries
            var expired = _cache.Where(kvp => kvp.Value.Expiry <= DateTimeOffset.UtcNow)
                                .Select(kvp => kvp.Key)
                                .ToList();

            foreach (var expiredKey in expired)
            {
                _cache.TryRemove(expiredKey, out _);
            }
        }
    }

    // Fluent API implementations

    /// <inheritdoc />
    public IPipeline<TInput, TNewOutput> Map<TNewOutput>(Func<TOutput, TNewOutput> mapper)
    {
        var mappingStage = DelegateStage<TOutput, TNewOutput>.Create(mapper, "Map");
        var newPipeline = new Pipeline<TInput, TNewOutput>(_metadata, _configuration);

        // Copy existing stages and add mapping
        newPipeline._stages.AddRange(_stages);
        newPipeline._stages.Add(mappingStage as IPipelineStage<object, object>);
        newPipeline._interceptors.AddRange(_interceptors);
        newPipeline._behaviors.AddRange(_behaviors);

        return newPipeline;
    }

    /// <inheritdoc />
    public IPipeline<TInput, TNewOutput> MapAsync<TNewOutput>(Func<TOutput, Task<TNewOutput>> asyncMapper)
    {
        var mappingStage = DelegateStage<TOutput, TNewOutput>.Create(asyncMapper, "MapAsync");
        var newPipeline = new Pipeline<TInput, TNewOutput>(_metadata, _configuration);

        // Copy existing stages and add mapping
        newPipeline._stages.AddRange(_stages);
        newPipeline._stages.Add(mappingStage as IPipelineStage<object, object>);
        newPipeline._interceptors.AddRange(_interceptors);
        newPipeline._behaviors.AddRange(_behaviors);

        return newPipeline;
    }

    /// <inheritdoc />
    public IPipeline<TInput, TNewOutput> Then<TNewOutput>(IPipeline<TOutput, TNewOutput> nextPipeline)
    {
        var compositeStage = new DelegateStage<TOutput, TNewOutput>(
            async (input, context) => await nextPipeline.ExecuteAsync(input, context),
            $"Pipeline[{nextPipeline.Metadata.Name}]");

        var newPipeline = new Pipeline<TInput, TNewOutput>(_metadata, _configuration);
        newPipeline._stages.AddRange(_stages);
        newPipeline._stages.Add(compositeStage as IPipelineStage<object, object>);
        newPipeline._interceptors.AddRange(_interceptors);
        newPipeline._behaviors.AddRange(_behaviors);

        return newPipeline;
    }

    /// <inheritdoc />
    public IPipeline<TInput, TNewOutput> Then<TNewOutput>(Func<TOutput, TNewOutput> processor)
    {
        return Map(processor);
    }

    /// <inheritdoc />
    public IPipeline<TInput, TNewOutput> ThenAsync<TNewOutput>(Func<TOutput, Task<TNewOutput>> asyncProcessor)
    {
        return MapAsync(asyncProcessor);
    }

    /// <inheritdoc />
    public IPipeline<TInput, TOutput?> Filter(Predicate<TOutput> predicate)
    {
        var filterStage = new DelegateStage<TOutput, TOutput?>(
            (input, context) =>
            {
                var result = predicate(input) ? input : default(TOutput);
                return Task.FromResult(result);
            },
            "Filter");

        var newPipeline = new Pipeline<TInput, TOutput?>(_metadata, _configuration);
        newPipeline._stages.AddRange(_stages);
        newPipeline._stages.Add(filterStage as IPipelineStage<object, object>);
        newPipeline._interceptors.AddRange(_interceptors);
        newPipeline._behaviors.AddRange(_behaviors);

        return newPipeline;
    }

    /// <inheritdoc />
    public IPipeline<TInput, TOutput?> FilterAsync(Func<TOutput, Task<bool>> asyncPredicate)
    {
        var filterStage = new DelegateStage<TOutput, TOutput?>(
            async (input, context) =>
            {
                var passes = await asyncPredicate(input);
                return passes ? input : default(TOutput);
            },
            "FilterAsync");

        var newPipeline = new Pipeline<TInput, TOutput?>(_metadata, _configuration);
        newPipeline._stages.AddRange(_stages);
        newPipeline._stages.Add(filterStage as IPipelineStage<object, object>);
        newPipeline._interceptors.AddRange(_interceptors);
        newPipeline._behaviors.AddRange(_behaviors);

        return newPipeline;
    }

    /// <inheritdoc />
    public IPipeline<TInput, TOutput> Branch(
        Predicate<TOutput> condition,
        IPipeline<TOutput, TOutput> trueBranch,
        IPipeline<TOutput, TOutput> falseBranch)
    {
        var branchStage = new DelegateStage<TOutput, TOutput>(
            async (input, context) =>
            {
                var branch = condition(input) ? trueBranch : falseBranch;
                return await branch.ExecuteAsync(input, context);
            },
            "Branch");

        var newPipeline = new Pipeline<TInput, TOutput>(_metadata, _configuration);
        newPipeline._stages.AddRange(_stages);
        newPipeline._stages.Add(branchStage as IPipelineStage<object, object>);
        newPipeline._interceptors.AddRange(_interceptors);
        newPipeline._behaviors.AddRange(_behaviors);

        return newPipeline;
    }

    /// <inheritdoc />
    public IPipeline<TInput, TOutput> HandleError(Func<Exception, TOutput> errorHandler)
    {
        var newPipeline = new Pipeline<TInput, TOutput>(_metadata, _configuration);
        newPipeline._stages.AddRange(_stages);
        newPipeline._interceptors.AddRange(_interceptors);
        newPipeline._behaviors.AddRange(_behaviors);
        newPipeline._errorHandler = errorHandler;

        return newPipeline;
    }

    /// <inheritdoc />
    public IPipeline<TInput, TOutput> HandleErrorAsync(Func<Exception, Task<TOutput>> asyncErrorHandler)
    {
        var newPipeline = new Pipeline<TInput, TOutput>(_metadata, _configuration);
        newPipeline._stages.AddRange(_stages);
        newPipeline._interceptors.AddRange(_interceptors);
        newPipeline._behaviors.AddRange(_behaviors);
        newPipeline._errorHandler = ex => asyncErrorHandler(ex).GetAwaiter().GetResult();

        return newPipeline;
    }

    /// <inheritdoc />
    public IPipeline<TInput, TOutput> WithRetry(int maxRetries, TimeSpan retryDelay)
    {
        var newConfig = _configuration.Clone();
        newConfig.MaxRetries = maxRetries;
        newConfig.RetryDelay = retryDelay;
        newConfig.ErrorStrategy = ErrorHandlingStrategy.Retry;

        var newPipeline = new Pipeline<TInput, TOutput>(_metadata, newConfig);
        newPipeline._stages.AddRange(_stages);
        newPipeline._interceptors.AddRange(_interceptors);
        newPipeline._behaviors.AddRange(_behaviors);

        return newPipeline;
    }

    /// <inheritdoc />
    public IPipeline<TInput, TOutput> WithRetry(RetryPolicy retryPolicy)
    {
        var retryBehavior = BehaviorContribution.Create(
            $"retry-{Guid.NewGuid()}",
            new DelegatingBehavior(async (context, next) =>
            {
                return await next.WithRetry(retryPolicy).ProceedAsync(context);
            }));

        var newPipeline = new Pipeline<TInput, TOutput>(_metadata, _configuration);
        newPipeline._stages.AddRange(_stages);
        newPipeline._interceptors.AddRange(_interceptors);
        newPipeline._behaviors.AddRange(_behaviors);
        newPipeline._behaviors.Add(retryBehavior);

        return newPipeline;
    }

    /// <inheritdoc />
    public IPipeline<TInput, TOutput> WithTimeout(TimeSpan timeout)
    {
        var newConfig = _configuration.Clone();
        newConfig.DefaultTimeout = timeout;

        var newPipeline = new Pipeline<TInput, TOutput>(_metadata, newConfig);
        newPipeline._stages.AddRange(_stages);
        newPipeline._interceptors.AddRange(_interceptors);
        newPipeline._behaviors.AddRange(_behaviors);

        return newPipeline;
    }

    /// <inheritdoc />
    public IPipeline<TInput, TOutput> WithCache(TimeSpan cacheDuration)
    {
        return WithCache(input => input?.GetHashCode().ToString() ?? "null", cacheDuration);
    }

    /// <inheritdoc />
    public IPipeline<TInput, TOutput> WithCache(Func<TInput, string> cacheKeySelector, TimeSpan cacheDuration)
    {
        var newConfig = _configuration.Clone();
        newConfig.CacheEnabled = true;
        newConfig.DefaultCacheDuration = cacheDuration;

        var newPipeline = new Pipeline<TInput, TOutput>(_metadata, newConfig);
        newPipeline._stages.AddRange(_stages);
        newPipeline._interceptors.AddRange(_interceptors);
        newPipeline._behaviors.AddRange(_behaviors);
        newPipeline._cacheKeySelector = cacheKeySelector;
        newPipeline._cacheDuration = cacheDuration;

        return newPipeline;
    }

    /// <inheritdoc />
    public IPipeline<TInput, IEnumerable<TOutput>> Parallel<TParallelInput>(
        IEnumerable<TParallelInput> items,
        Func<TParallelInput, TInput> inputMapper)
    {
        var parallelStage = new DelegateStage<TInput, IEnumerable<TOutput>>(
            async (_, context) =>
            {
                var tasks = items.Select(item =>
                    ExecuteAsync(inputMapper(item), context.CreateChildContext()));

                return await Task.WhenAll(tasks);
            },
            "Parallel");

        var newPipeline = new Pipeline<TInput, IEnumerable<TOutput>>(_metadata, _configuration);
        newPipeline._stages.Add(parallelStage as IPipelineStage<object, object>);
        newPipeline._interceptors.AddRange(_interceptors);
        newPipeline._behaviors.AddRange(_behaviors);

        return newPipeline;
    }

    /// <inheritdoc />
    public IPipeline<TInput, TOutput> AddInterceptor(IPipelineInterceptor interceptor)
    {
        _interceptors.Add(interceptor);
        return this;
    }

    /// <inheritdoc />
    public IPipeline<TInput, TOutput> AddStage<TStageOutput>(IPipelineStage<TOutput, TStageOutput> stage)
        where TStageOutput : TOutput
    {
        _stages.Add(stage as IPipelineStage<object, object>);
        return this;
    }

    /// <summary>
    /// Adds a behavior contribution to the pipeline.
    /// </summary>
    public Pipeline<TInput, TOutput> AddBehavior(BehaviorContribution behavior)
    {
        _behaviors.Add(behavior);
        return this;
    }

    /// <inheritdoc />
    public IReadOnlyList<IPipelineInterceptor> GetInterceptors()
    {
        return _interceptors.AsReadOnly();
    }

    /// <inheritdoc />
    public IReadOnlyList<IPipelineStage<object, object>> GetStages()
    {
        return _stages.AsReadOnly();
    }

    /// <summary>
    /// Gets the registered behaviors.
    /// </summary>
    public IReadOnlyList<BehaviorContribution> GetBehaviors()
    {
        return _behaviors.AsReadOnly();
    }
}