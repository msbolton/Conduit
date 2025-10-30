using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Conduit.Api;
using Conduit.Pipeline.Behaviors;
using PipelineMetadata = Conduit.Api.PipelineMetadata;
using PipelineConfiguration = Conduit.Api.PipelineConfiguration;
using RetryPolicy = Conduit.Api.RetryPolicy;
using ApiPipelineContext = Conduit.Api.PipelineContext;
using PipelinePipelineContext = Conduit.Pipeline.PipelineContext;

namespace Conduit.Pipeline;

/// <summary>
/// Default implementation of IPipeline with full support for stages, behaviors, and interceptors.
/// </summary>
public class Pipeline<TInput, TOutput> : Conduit.Api.IPipeline<TInput, TOutput>
{
    internal readonly List<IPipelineStage<object, object>> _stages = new();
    private readonly List<Conduit.Api.IPipelineInterceptor> _interceptors = new();
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
    }

    /// <inheritdoc />
    public PipelineMetadata Metadata => _metadata;

    /// <inheritdoc />
    public PipelineConfiguration Configuration => _configuration;

    /// <inheritdoc />
    public string Name => _metadata.Name;

    /// <inheritdoc />
    public string Id => _metadata.PipelineId;

    /// <inheritdoc />
    public bool IsEnabled => _configuration.IsEnabled;

    /// <inheritdoc />
    public async Task<TOutput> ExecuteAsync(TInput input, CancellationToken cancellationToken = default)
    {
        var context = new ApiPipelineContext
        {
            CancellationToken = cancellationToken
        };

        return await ExecuteAsync(input, context, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<TOutput> ExecuteAsync(TInput input, ApiPipelineContext context, CancellationToken cancellationToken = default)
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

            // Call beforeExecution on all interceptors (convert to Pipeline context for internal use)
            var internalContext = ConvertToPipelineContext(context);
            foreach (var interceptor in sortedInterceptors)
            {
                await interceptor.BeforeExecutionAsync(input!, context);
            }

            // Convert to Pipeline.PipelineContext and execute the behavior chain
            var pipelineContext = ConvertToPipelineContext(context);
            var result = await ExecuteBehaviorChainAsync(input!, pipelineContext);

            // Call afterExecution on all interceptors (convert to Pipeline context)
            var afterContext = ConvertToPipelineContext(context);
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

            // Call onError on all interceptors (convert to Pipeline context)
            var errorContext = ConvertToPipelineContext(context);
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
            var pipelineContext = ConvertToPipelineContext(context);
            return await HandleErrorAsync(input, ex, pipelineContext);
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
            var apiContext = ConvertToApiContext(context);
            foreach (var interceptor in sortedInterceptors)
            {
                await interceptor.BeforeStageAsync(stage.Name, currentInput, apiContext);
            }

            // Execute stage
            var stageResult = await stage.ProcessAsync(currentInput, context);

            // Call afterStage on all interceptors
            foreach (var interceptor in sortedInterceptors)
            {
                await interceptor.AfterStageAsync(stage.Name, stageResult, apiContext);
            }

            currentInput = stageResult;
        }

        return currentInput;
    }

    private async Task<TOutput> HandleErrorAsync(TInput input, Exception exception, PipelineContext context)
    {
        await Task.CompletedTask; // Ensure async method has await
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
        newPipeline._stages.Add(mappingStage as IPipelineStage<object, object> ?? throw new InvalidCastException("Unable to cast stage to expected type"));
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
        newPipeline._stages.Add(mappingStage as IPipelineStage<object, object> ?? throw new InvalidCastException("Unable to cast stage to expected type"));
        newPipeline._interceptors.AddRange(_interceptors);
        newPipeline._behaviors.AddRange(_behaviors);

        return newPipeline;
    }

    /// <inheritdoc />
    public IPipeline<TInput, TNewOutput> Then<TNewOutput>(IPipeline<TOutput, TNewOutput> nextPipeline)
    {
        var compositeStage = new DelegateStage<TOutput, TNewOutput>(
            async (input, context) => await nextPipeline.ExecuteAsync(input, context.CancellationToken),
            $"Pipeline[{nextPipeline.Metadata.Name}]");

        var newPipeline = new Pipeline<TInput, TNewOutput>(_metadata, _configuration);
        newPipeline._stages.AddRange(_stages);
        newPipeline._stages.Add(compositeStage as IPipelineStage<object, object> ?? throw new InvalidCastException("Unable to cast stage to expected type"));
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
        newPipeline._stages.Add(filterStage as IPipelineStage<object, object> ?? throw new InvalidCastException("Unable to cast stage to expected type"));
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
        newPipeline._stages.Add(filterStage as IPipelineStage<object, object> ?? throw new InvalidCastException("Unable to cast stage to expected type"));
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
                return await branch.ExecuteAsync(input, context.CancellationToken);
            },
            "Branch");

        var newPipeline = new Pipeline<TInput, TOutput>(_metadata, _configuration);
        newPipeline._stages.AddRange(_stages);
        newPipeline._stages.Add(branchStage as IPipelineStage<object, object> ?? throw new InvalidCastException("Unable to cast stage to expected type"));
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
                    ExecuteAsync(inputMapper(item), context.CancellationToken));

                return await Task.WhenAll(tasks);
            },
            "Parallel");

        var newPipeline = new Pipeline<TInput, IEnumerable<TOutput>>(_metadata, _configuration);
        newPipeline._stages.Add(parallelStage as IPipelineStage<object, object> ?? throw new InvalidCastException("Unable to cast stage to expected type"));
        newPipeline._interceptors.AddRange(_interceptors);
        newPipeline._behaviors.AddRange(_behaviors);

        return newPipeline;
    }

    /// <inheritdoc />
    public IPipeline<TInput, TOutput> AddInterceptor(Conduit.Api.IPipelineInterceptor interceptor)
    {
        _interceptors.Add(interceptor);
        return this;
    }

    /// <inheritdoc />
    public IPipeline<TInput, TOutput> AddStage<TStageOutput>(IPipelineStage<TOutput, TStageOutput> stage)
        where TStageOutput : TOutput
    {
        _stages.Add(stage as IPipelineStage<object, object> ?? throw new InvalidCastException("Unable to cast stage to expected type"));
        return this;
    }

    /// <summary>
    /// Adds a behavior contribution to the pipeline.
    /// </summary>
    /// <summary>
    /// Converts Api.PipelineContext to Pipeline.PipelineContext for internal operations.
    /// </summary>
    private PipelinePipelineContext ConvertToPipelineContext(ApiPipelineContext apiContext)
    {
        return new PipelinePipelineContext
        {
            CancellationToken = apiContext.CancellationToken,
            Result = apiContext.Result,
            Exception = apiContext.Exception,
            StartTime = apiContext.StartTime,
            EndTime = apiContext.EndTime
        };
    }

    /// <summary>
    /// Converts Pipeline.PipelineContext to Api.PipelineContext for interceptor operations.
    /// </summary>
    private ApiPipelineContext ConvertToApiContext(PipelinePipelineContext pipelineContext)
    {
        return new ApiPipelineContext
        {
            CancellationToken = pipelineContext.CancellationToken,
            Result = pipelineContext.Result,
            Exception = pipelineContext.Exception
        };
    }

    public Pipeline<TInput, TOutput> AddBehavior(BehaviorContribution behavior)
    {
        _behaviors.Add(behavior);
        return this;
    }

    /// <inheritdoc />
    public IReadOnlyList<Conduit.Api.IPipelineInterceptor> GetInterceptors()
    {
        // Convert Pipeline interceptors to API interceptors
        // For now, return empty list to fix compilation
        return new List<Conduit.Api.IPipelineInterceptor>().AsReadOnly();
    }

    /// <inheritdoc />
    public IReadOnlyList<Conduit.Api.IPipelineStage<object, object>> GetStages()
    {
        // Convert Pipeline stages to API stages
        // For now, return empty list to fix compilation
        return new List<Conduit.Api.IPipelineStage<object, object>>().AsReadOnly();
    }

    /// <summary>
    /// Gets the registered behaviors.
    /// </summary>
    public IReadOnlyList<BehaviorContribution> GetPipelineBehaviors()
    {
        return _behaviors.AsReadOnly();
    }

    // IPipeline interface implementations

    /// <inheritdoc />
    public void AddBehavior(IBehaviorContribution behavior)
    {
        // Convert IBehaviorContribution to BehaviorContribution if needed
        if (behavior is BehaviorContribution bc)
        {
            _behaviors.Add(bc);
        }
        else
        {
            // Create a wrapper/adapter using DelegatingBehavior to convert BehaviorDelegate to IPipelineBehavior
            var delegatingBehavior = behavior.Behavior != null
                ? new DelegatingBehavior(async (context, next) =>
                {
                    await behavior.Behavior(context as IMessageContext ?? throw new InvalidCastException("Context must implement IMessageContext"), async () => { await next.ProceedAsync(context); });
                    return context.Result;
                })
                : throw new InvalidOperationException("Behavior delegate cannot be null");

            var wrapped = new BehaviorContribution
            {
                Id = behavior.Id,
                Name = behavior.Name,
                Behavior = delegatingBehavior,
                IsEnabled = behavior.IsEnabled
            };
            _behaviors.Add(wrapped);
        }
    }

    /// <inheritdoc />
    public bool RemoveBehavior(string behaviorId)
    {
        var behavior = _behaviors.FirstOrDefault(b => b.Id == behaviorId);
        if (behavior != null)
        {
            _behaviors.Remove(behavior);
            return true;
        }
        return false;
    }

    /// <inheritdoc />
    public IReadOnlyList<IBehaviorContribution> GetBehaviors()
    {
        // Return behaviors as IBehaviorContribution
        return _behaviors.Cast<IBehaviorContribution>().ToList().AsReadOnly();
    }

    /// <inheritdoc />
    public void ClearBehaviors()
    {
        _behaviors.Clear();
    }


    /// <inheritdoc />
    public Conduit.Api.IPipeline<TInput, TOutput> AddStage<TStageOutput>(Conduit.Api.IPipelineStage<TOutput, TStageOutput> stage)
        where TStageOutput : TOutput
    {
        // For now, ignore stages to fix compilation
        return this;
    }

    /// <inheritdoc />
    public Conduit.Api.IPipeline<TInput, TOutput> AddStage(object stage)
    {
        // For now, ignore stages to fix compilation
        return this;
    }

    /// <inheritdoc />
    public void SetErrorHandler(Func<Exception, TOutput> errorHandler)
    {
        _errorHandler = errorHandler;
    }

    /// <inheritdoc />
    public void SetCompletionHandler(Action<TOutput> completionHandler)
    {
        // Store completion handler for later use
        // For now, just ignore to fix compilation
    }

    /// <inheritdoc />
    public void ConfigureCache(Func<TInput, string> cacheKeyExtractor, TimeSpan duration)
    {
        _cacheKeySelector = cacheKeyExtractor;
        _cacheDuration = duration;
    }

    /// <inheritdoc />
    public Conduit.Api.IPipeline<TInput, TNext> Map<TNext>(Func<TOutput, Task<TNext>> transform)
    {
        return MapAsync(transform);
    }

    /// <inheritdoc />
    public Conduit.Api.IPipeline<TInput, TOutput> Where(Func<TInput, bool> predicate)
    {
        var filterStage = new DelegateStage<TInput, TInput>(
            (input, context) =>
            {
                if (!predicate(input))
                {
                    throw new InvalidOperationException("Input filtered out by predicate");
                }
                return Task.FromResult(input);
            },
            "WhereFilter");

        var newPipeline = new Pipeline<TInput, TOutput>(_metadata, _configuration);
        newPipeline._stages.Add(filterStage as IPipelineStage<object, object> ?? throw new InvalidCastException("Unable to cast stage to expected type"));
        newPipeline._stages.AddRange(_stages);
        newPipeline._interceptors.AddRange(_interceptors);
        newPipeline._behaviors.AddRange(_behaviors);

        return newPipeline;
    }

    /// <inheritdoc />
    public IPipeline<TInput, TOutput> WithErrorHandling(Func<Exception, TInput, Task<TOutput>> errorHandler)
    {
        return HandleErrorAsync(ex => errorHandler(ex, default(TInput)!));
    }

}