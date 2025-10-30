using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Conduit.Api;
using Conduit.Common;
using Conduit.Pipeline.Behaviors;
using PipelineMetadata = Conduit.Api.PipelineMetadata;
using PipelineConfiguration = Conduit.Api.PipelineConfiguration;
using RetryPolicy = Conduit.Api.RetryPolicy;
using IPipelineInterceptor = Conduit.Api.IPipelineInterceptor;
using ApiPipelineContext = Conduit.Api.PipelineContext;

namespace Conduit.Pipeline.Composition
{
    /// <summary>
    /// A pipeline that processes collections in parallel.
    /// Implements the Splitter pattern from Enterprise Integration Patterns.
    /// </summary>
    /// <typeparam name="TInput">The input collection element type</typeparam>
    /// <typeparam name="TOutput">The output element type</typeparam>
    public class ParallelPipeline<TInput, TOutput> : IPipeline<IEnumerable<TInput>, IList<TOutput>>
    {
        private readonly IPipeline<TInput, TOutput> _innerPipeline;
        private readonly int _maxConcurrency;
        private readonly bool _preserveOrder;
        private readonly ParallelOptions _parallelOptions;

        /// <summary>
        /// Initializes a new instance of the ParallelPipeline class.
        /// </summary>
        /// <param name="innerPipeline">The pipeline to execute for each element</param>
        /// <param name="maxConcurrency">Maximum degree of parallelism (-1 for unlimited)</param>
        /// <param name="preserveOrder">Whether to preserve the order of results</param>
        public ParallelPipeline(
            IPipeline<TInput, TOutput> innerPipeline,
            int maxConcurrency = -1,
            bool preserveOrder = true)
        {
            Guard.NotNull(innerPipeline, nameof(innerPipeline));

            _innerPipeline = innerPipeline;
            _maxConcurrency = maxConcurrency > 0 ? maxConcurrency : Environment.ProcessorCount;
            _preserveOrder = preserveOrder;
            _parallelOptions = new ParallelOptions
            {
                MaxDegreeOfParallelism = _maxConcurrency
            };
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
                    Name = $"{innerMetadata.Name} -> Parallel",
                    Description = $"Parallel execution of {innerMetadata.Name} with max concurrency {_maxConcurrency}",
                    Type = PipelineType.Parallel,
                    Version = innerMetadata.Version,
                    Stages = new List<string>(innerMetadata.Stages) { "Parallel" }
                };
            }
        }

        /// <inheritdoc />
        public PipelineConfiguration Configuration
        {
            get
            {
                var config = _innerPipeline.Configuration.Clone();
                config.AsyncExecution = true;
                config.MaxConcurrency = _maxConcurrency;
                return config;
            }
        }

        /// <inheritdoc />
        public string Name => $"{_innerPipeline.Name} -> Parallel";

        /// <inheritdoc />
        public string Id => $"{_innerPipeline.Id}_parallel";

        /// <inheritdoc />
        public bool IsEnabled => _innerPipeline.IsEnabled;

        /// <inheritdoc />
        public async Task<IList<TOutput>> ExecuteAsync(IEnumerable<TInput> input, CancellationToken cancellationToken = default)
        {
            var inputList = input.ToList();

            if (inputList.Count == 0)
            {
                return new List<TOutput>();
            }

            if (_preserveOrder)
            {
                return await ExecuteWithOrderPreservationAsync(inputList, cancellationToken);
            }
            else
            {
                return await ExecuteWithoutOrderPreservationAsync(inputList, cancellationToken);
            }
        }

        /// <inheritdoc />
        public async Task<IList<TOutput>> ExecuteAsync(
            IEnumerable<TInput> input,
            ApiPipelineContext context,
            CancellationToken cancellationToken = default)
        {
            var inputList = input.ToList();
            context.SetProperty("ParallelPipeline.InputCount", inputList.Count);
            context.SetProperty("ParallelPipeline.MaxConcurrency", _maxConcurrency);
            context.SetProperty("ParallelPipeline.PreserveOrder", _preserveOrder);
            context.SetProperty("ParallelPipeline.StartTime", DateTimeOffset.UtcNow);

            if (inputList.Count == 0)
            {
                context.SetProperty("ParallelPipeline.OutputCount", 0);
                return new List<TOutput>();
            }

            // Convert ApiPipelineContext to Pipeline.PipelineContext for internal operations
            var pipelineContext = new Conduit.Pipeline.PipelineContext
            {
                CancellationToken = context.CancellationToken,
                Result = context.Result,
                Exception = context.Exception
            };

            IList<TOutput> results;
            if (_preserveOrder)
            {
                results = await ExecuteWithOrderPreservationAsync(inputList, pipelineContext, cancellationToken);
            }
            else
            {
                results = await ExecuteWithoutOrderPreservationAsync(inputList, pipelineContext, cancellationToken);
            }

            context.SetProperty("ParallelPipeline.OutputCount", results.Count);
            context.SetProperty("ParallelPipeline.EndTime", DateTimeOffset.UtcNow);

            var startTime = (DateTimeOffset)context.GetProperty("ParallelPipeline.StartTime")!;
            var endTime = (DateTimeOffset)context.GetProperty("ParallelPipeline.EndTime")!;
            context.SetProperty("ParallelPipeline.Duration", (endTime - startTime).TotalMilliseconds);

            return results;
        }

        private async Task<IList<TOutput>> ExecuteWithOrderPreservationAsync(
            List<TInput> inputList,
            CancellationToken cancellationToken)
        {
            var tasks = new Task<TOutput>[inputList.Count];

            using var semaphore = new SemaphoreSlim(_maxConcurrency, _maxConcurrency);

            for (int i = 0; i < inputList.Count; i++)
            {
                var index = i;
                var item = inputList[index];

                tasks[index] = Task.Run(async () =>
                {
                    await semaphore.WaitAsync(cancellationToken);
                    try
                    {
                        return await _innerPipeline.ExecuteAsync(item, cancellationToken);
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                }, cancellationToken);
            }

            var results = await Task.WhenAll(tasks);
            return results;
        }

        private async Task<IList<TOutput>> ExecuteWithOrderPreservationAsync(
            List<TInput> inputList,
            Conduit.Pipeline.PipelineContext parentContext,
            CancellationToken cancellationToken)
        {
            var tasks = new Task<TOutput>[inputList.Count];

            using var semaphore = new SemaphoreSlim(_maxConcurrency, _maxConcurrency);

            for (int i = 0; i < inputList.Count; i++)
            {
                var index = i;
                var item = inputList[index];

                tasks[index] = Task.Run(async () =>
                {
                    await semaphore.WaitAsync(cancellationToken);
                    try
                    {
                        // Create a child context for each parallel execution
                        var childContext = parentContext.Copy();
                        childContext.SetProperty("ParallelPipeline.Index", index);
                        childContext.SetProperty("ParallelPipeline.ThreadId", Thread.CurrentThread.ManagedThreadId);

                        return await _innerPipeline.ExecuteAsync(item, cancellationToken);
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                }, cancellationToken);
            }

            var results = await Task.WhenAll(tasks);
            return results;
        }

        private async Task<IList<TOutput>> ExecuteWithoutOrderPreservationAsync(
            List<TInput> inputList,
            CancellationToken cancellationToken)
        {
            var results = new List<TOutput>();
            var resultLock = new object();

            await System.Threading.Tasks.Parallel.ForEachAsync(inputList, _parallelOptions, async (item, ct) =>
            {
                var result = await _innerPipeline.ExecuteAsync(item, ct);
                lock (resultLock)
                {
                    results.Add(result);
                }
            });

            return results;
        }

        private async Task<IList<TOutput>> ExecuteWithoutOrderPreservationAsync(
            List<TInput> inputList,
            Conduit.Pipeline.PipelineContext parentContext,
            CancellationToken cancellationToken)
        {
            var results = new List<TOutput>();
            var resultLock = new object();
            var processedCount = 0;

            await System.Threading.Tasks.Parallel.ForEachAsync(inputList, _parallelOptions, async (item, ct) =>
            {
                var childContext = parentContext.Copy();
                childContext.SetProperty("ParallelPipeline.ThreadId", Thread.CurrentThread.ManagedThreadId);

                var result = await _innerPipeline.ExecuteAsync(item, ct);

                lock (resultLock)
                {
                    results.Add(result);
                    processedCount++;
                    childContext.SetProperty("ParallelPipeline.ProcessedCount", processedCount);
                }
            });

            return results;
        }

        // Interface implementation methods
        public IPipeline<IEnumerable<TInput>, IList<TOutput>> AddInterceptor(Conduit.Api.IPipelineInterceptor interceptor)
        {
            _innerPipeline.AddInterceptor(interceptor);
            return this;
        }

        public void AddBehavior(IBehaviorContribution behavior)
        {
            _innerPipeline.AddBehavior(behavior);
        }

        public bool RemoveBehavior(string behaviorId)
        {
            return _innerPipeline.RemoveBehavior(behaviorId);
        }

        public IReadOnlyList<IBehaviorContribution> GetBehaviors()
        {
            return _innerPipeline.GetBehaviors();
        }

        public void ClearBehaviors()
        {
            _innerPipeline.ClearBehaviors();
        }

        /// <inheritdoc />
        public IPipeline<IEnumerable<TInput>, IList<TOutput>> AddStage(object stage)
        {
            _innerPipeline.AddStage(stage);
            return this;
        }

        public void AddStage(IPipelineStage<object, object> stage)
        {
            ((Pipeline<IEnumerable<TInput>, IList<TOutput>>)_innerPipeline)._stages.Add(stage as IPipelineStage<object, object> ?? throw new InvalidCastException("Unable to cast stage to expected type"));
        }

        public void SetErrorHandler(Func<Exception, IList<TOutput>> errorHandler)
        {
            // For ParallelPipeline, we delegate error handling to the inner pipeline
            // Individual element errors will be handled by the inner pipeline
            _innerPipeline.SetErrorHandler(ex =>
            {
                // When an individual element fails, we return a default value
                // The collection-level error handler will be called for pipeline-level errors
                return default(TOutput)!;
            });
        }

        public void SetCompletionHandler(Action<IList<TOutput>> completionHandler)
        {
            // For ParallelPipeline, we can't easily delegate completion to inner pipeline
            // since completion happens at the collection level after all parallel executions
            // This is a limitation - completion handling happens in ExecuteAsync methods
        }

        public void ConfigureCache(Func<IEnumerable<TInput>, string> cacheKeyExtractor, TimeSpan duration)
        {
            // ParallelPipeline caching is complex because we cache entire collections
            // For now, delegate individual element caching to the inner pipeline
            _innerPipeline.ConfigureCache(input => cacheKeyExtractor(new[] { input }), duration);
        }

        // IPipeline interface implementation methods

        /// <inheritdoc />
        public IPipeline<IEnumerable<TInput>, TNewOutput> Map<TNewOutput>(Func<IList<TOutput>, TNewOutput> mapper)
        {
            throw new NotImplementedException("Map operation is not implemented for ParallelPipeline. Apply mapping to individual elements using the inner pipeline.");
        }

        /// <inheritdoc />
        public IPipeline<IEnumerable<TInput>, TNewOutput> MapAsync<TNewOutput>(Func<IList<TOutput>, Task<TNewOutput>> asyncMapper)
        {
            throw new NotImplementedException("MapAsync operation is not implemented for ParallelPipeline. Apply mapping to individual elements using the inner pipeline.");
        }

        /// <inheritdoc />
        public IPipeline<IEnumerable<TInput>, TNext> Map<TNext>(Func<IList<TOutput>, Task<TNext>> transform)
        {
            throw new NotImplementedException("Async Map operation is not implemented for ParallelPipeline. Apply mapping to individual elements using the inner pipeline.");
        }

        /// <inheritdoc />
        public IPipeline<IEnumerable<TInput>, IList<TOutput>> Where(Func<IEnumerable<TInput>, bool> predicate)
        {
            throw new NotImplementedException("Where operation is not implemented for ParallelPipeline. Apply filtering to individual elements using the inner pipeline.");
        }

        /// <inheritdoc />
        public IPipeline<IEnumerable<TInput>, IList<TOutput>> WithErrorHandling(Func<Exception, IEnumerable<TInput>, Task<IList<TOutput>>> errorHandler)
        {
            throw new NotImplementedException("WithErrorHandling operation is not implemented for ParallelPipeline. Apply error handling to individual elements using the inner pipeline.");
        }

        /// <inheritdoc />
        public IPipeline<IEnumerable<TInput>, TNewOutput> Then<TNewOutput>(IPipeline<IList<TOutput>, TNewOutput> nextPipeline)
        {
            throw new NotImplementedException("Then operation is not implemented for ParallelPipeline. Chain pipelines at the element level using the inner pipeline.");
        }

        /// <inheritdoc />
        public IPipeline<IEnumerable<TInput>, TNewOutput> Then<TNewOutput>(Func<IList<TOutput>, TNewOutput> processor)
        {
            throw new NotImplementedException("Then operation is not implemented for ParallelPipeline. Apply processing to individual elements using the inner pipeline.");
        }

        /// <inheritdoc />
        public IPipeline<IEnumerable<TInput>, TNewOutput> ThenAsync<TNewOutput>(Func<IList<TOutput>, Task<TNewOutput>> asyncProcessor)
        {
            throw new NotImplementedException("ThenAsync operation is not implemented for ParallelPipeline. Apply processing to individual elements using the inner pipeline.");
        }

        /// <inheritdoc />
        public IPipeline<IEnumerable<TInput>, IList<TOutput>?> Filter(Predicate<IList<TOutput>> predicate)
        {
            throw new NotImplementedException("Filter operation is not implemented for ParallelPipeline. Apply filtering to individual elements using the inner pipeline.");
        }

        /// <inheritdoc />
        public IPipeline<IEnumerable<TInput>, IList<TOutput>?> FilterAsync(Func<IList<TOutput>, Task<bool>> asyncPredicate)
        {
            throw new NotImplementedException("FilterAsync operation is not implemented for ParallelPipeline. Apply filtering to individual elements using the inner pipeline.");
        }

        /// <inheritdoc />
        public IPipeline<IEnumerable<TInput>, IList<TOutput>> Branch(
            Predicate<IList<TOutput>> condition,
            IPipeline<IList<TOutput>, IList<TOutput>> trueBranch,
            IPipeline<IList<TOutput>, IList<TOutput>> falseBranch)
        {
            throw new NotImplementedException("Branch operation is not implemented for ParallelPipeline. Apply branching to individual elements using the inner pipeline.");
        }

        /// <inheritdoc />
        public IPipeline<IEnumerable<TInput>, IList<TOutput>> HandleError(Func<Exception, IList<TOutput>> errorHandler)
        {
            throw new NotImplementedException("HandleError operation is not implemented for ParallelPipeline. Apply error handling to individual elements using the inner pipeline.");
        }

        /// <inheritdoc />
        public IPipeline<IEnumerable<TInput>, IList<TOutput>> HandleErrorAsync(Func<Exception, Task<IList<TOutput>>> asyncErrorHandler)
        {
            throw new NotImplementedException("HandleErrorAsync operation is not implemented for ParallelPipeline. Apply error handling to individual elements using the inner pipeline.");
        }

        /// <inheritdoc />
        public IPipeline<IEnumerable<TInput>, IList<TOutput>> WithRetry(int maxRetries, TimeSpan retryDelay)
        {
            throw new NotImplementedException("WithRetry operation is not implemented for ParallelPipeline. Apply retry logic to individual elements using the inner pipeline.");
        }

        /// <inheritdoc />
        public IPipeline<IEnumerable<TInput>, IList<TOutput>> WithRetry(RetryPolicy retryPolicy)
        {
            throw new NotImplementedException("WithRetry operation is not implemented for ParallelPipeline. Apply retry logic to individual elements using the inner pipeline.");
        }

        /// <inheritdoc />
        public IPipeline<IEnumerable<TInput>, IList<TOutput>> WithTimeout(TimeSpan timeout)
        {
            throw new NotImplementedException("WithTimeout operation is not implemented for ParallelPipeline. Apply timeout to individual elements using the inner pipeline.");
        }

        /// <inheritdoc />
        public IPipeline<IEnumerable<TInput>, IList<TOutput>> WithCache(TimeSpan cacheDuration)
        {
            throw new NotImplementedException("WithCache operation is not implemented for ParallelPipeline. Caching collections requires special handling.");
        }

        /// <inheritdoc />
        public IPipeline<IEnumerable<TInput>, IList<TOutput>> WithCache(Func<IEnumerable<TInput>, string> cacheKeySelector, TimeSpan cacheDuration)
        {
            throw new NotImplementedException("WithCache operation is not implemented for ParallelPipeline. Caching collections requires special handling.");
        }

        /// <inheritdoc />
        public IPipeline<IEnumerable<TInput>, IEnumerable<IList<TOutput>>> Parallel<TParallelInput>(
            IEnumerable<TParallelInput> items,
            Func<TParallelInput, IEnumerable<TInput>> inputMapper)
        {
            throw new NotImplementedException("Parallel operation is not implemented for ParallelPipeline. ParallelPipeline already implements parallel processing.");
        }

        /// <inheritdoc />
        IPipeline<IEnumerable<TInput>, IList<TOutput>> IPipeline<IEnumerable<TInput>, IList<TOutput>>.AddStage<TStageOutput>(Conduit.Api.IPipelineStage<IList<TOutput>, TStageOutput> stage)
        {
            throw new NotImplementedException("AddStage operation is not implemented for ParallelPipeline. Add stages to individual elements using the inner pipeline.");
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
    /// A pipeline that uses TPL Dataflow for advanced parallel processing.
    /// </summary>
    public class DataflowParallelPipeline<TInput, TOutput> : IPipeline<IEnumerable<TInput>, IList<TOutput>>
    {
        private readonly IPipeline<TInput, TOutput> _innerPipeline;
        private readonly ExecutionDataflowBlockOptions _options;
        private readonly bool _preserveOrder;

        public DataflowParallelPipeline(
            IPipeline<TInput, TOutput> innerPipeline,
            int maxConcurrency = -1,
            bool preserveOrder = true,
            int boundedCapacity = -1)
        {
            Guard.NotNull(innerPipeline, nameof(innerPipeline));

            _innerPipeline = innerPipeline;
            _preserveOrder = preserveOrder;
            _options = new ExecutionDataflowBlockOptions
            {
                MaxDegreeOfParallelism = maxConcurrency > 0 ? maxConcurrency : DataflowBlockOptions.Unbounded,
                BoundedCapacity = boundedCapacity > 0 ? boundedCapacity : DataflowBlockOptions.Unbounded,
                EnsureOrdered = preserveOrder
            };
        }

        public PipelineMetadata Metadata => new PipelineMetadata
        {
            PipelineId = Guid.NewGuid().ToString(),
            Name = $"{_innerPipeline.Metadata.Name} -> Dataflow Parallel",
            Description = "Dataflow-based parallel execution",
            Type = PipelineType.Parallel,
            Version = _innerPipeline.Metadata.Version,
            Stages = new List<string> { "Dataflow" }
        };

        public PipelineConfiguration Configuration
        {
            get
            {
                var config = _innerPipeline.Configuration.Clone();
                config.AsyncExecution = true;
                config.MaxConcurrency = _options.MaxDegreeOfParallelism;
                return config;
            }
        }

        /// <inheritdoc />
        public string Name => $"{_innerPipeline.Name} -> Dataflow Parallel";

        /// <inheritdoc />
        public string Id => $"{_innerPipeline.Id}_dataflow_parallel";

        /// <inheritdoc />
        public bool IsEnabled => _innerPipeline.IsEnabled;

        public async Task<IList<TOutput>> ExecuteAsync(IEnumerable<TInput> input, CancellationToken cancellationToken = default)
        {
            var results = new List<TOutput>();

            var transformBlock = new TransformBlock<TInput, TOutput>(
                async item => await _innerPipeline.ExecuteAsync(item, cancellationToken),
                _options);

            var actionBlock = new ActionBlock<TOutput>(
                result => results.Add(result),
                new ExecutionDataflowBlockOptions { MaxDegreeOfParallelism = 1 });

            transformBlock.LinkTo(actionBlock, new DataflowLinkOptions { PropagateCompletion = true });

            foreach (var item in input)
            {
                await transformBlock.SendAsync(item, cancellationToken);
            }

            transformBlock.Complete();
            await actionBlock.Completion;

            return results;
        }

        public async Task<IList<TOutput>> ExecuteAsync(
            IEnumerable<TInput> input,
            ApiPipelineContext context,
            CancellationToken cancellationToken = default)
        {
            context.SetProperty("DataflowPipeline.MaxConcurrency", _options.MaxDegreeOfParallelism);
            context.SetProperty("DataflowPipeline.BoundedCapacity", _options.BoundedCapacity);
            context.SetProperty("DataflowPipeline.EnsureOrdered", _options.EnsureOrdered);

            var results = new List<TOutput>();
            var processedCount = 0;

            var transformBlock = new TransformBlock<TInput, TOutput>(
                async item =>
                {
                    var childContext = context.Copy();
                    childContext.SetProperty("DataflowPipeline.ThreadId", Thread.CurrentThread.ManagedThreadId);
                    return await _innerPipeline.ExecuteAsync(item, childContext, cancellationToken);
                },
                _options);

            var actionBlock = new ActionBlock<TOutput>(
                result =>
                {
                    results.Add(result);
                    Interlocked.Increment(ref processedCount);
                    context.SetProperty("DataflowPipeline.ProcessedCount", processedCount);
                },
                new ExecutionDataflowBlockOptions { MaxDegreeOfParallelism = 1 });

            transformBlock.LinkTo(actionBlock, new DataflowLinkOptions { PropagateCompletion = true });

            var inputCount = 0;
            foreach (var item in input)
            {
                await transformBlock.SendAsync(item, cancellationToken);
                inputCount++;
            }

            context.SetProperty("DataflowPipeline.InputCount", inputCount);

            transformBlock.Complete();
            await actionBlock.Completion;

            context.SetProperty("DataflowPipeline.OutputCount", results.Count);

            return results;
        }

        // Interface implementation methods
        public IPipeline<IEnumerable<TInput>, IList<TOutput>> AddInterceptor(Conduit.Api.IPipelineInterceptor interceptor)
        {
            _innerPipeline.AddInterceptor(interceptor);
            return this;
        }

        public void AddBehavior(IBehaviorContribution behavior) => _innerPipeline.AddBehavior(behavior);
        public bool RemoveBehavior(string behaviorId) => _innerPipeline.RemoveBehavior(behaviorId);
        public IReadOnlyList<IBehaviorContribution> GetBehaviors() => _innerPipeline.GetBehaviors();
        public void ClearBehaviors() => _innerPipeline.ClearBehaviors();
        public void AddStage(IPipelineStage<object, object> stage) => _innerPipeline.AddStage(stage);
        public void SetErrorHandler(Func<Exception, IList<TOutput>> errorHandler) =>
            _innerPipeline.SetErrorHandler(ex => default(TOutput)!);

        public void SetCompletionHandler(Action<IList<TOutput>> completionHandler)
        {
            // Similar limitation as ParallelPipeline - completion happens at collection level
        }

        public void ConfigureCache(Func<IEnumerable<TInput>, string> cacheKeyExtractor, TimeSpan duration) =>
            _innerPipeline.ConfigureCache(input => cacheKeyExtractor(new[] { input }), duration);

        // IPipeline interface implementation methods

        /// <inheritdoc />
        public IPipeline<IEnumerable<TInput>, TNewOutput> Map<TNewOutput>(Func<IList<TOutput>, TNewOutput> mapper)
        {
            throw new NotImplementedException("Map operation is not implemented for DataflowParallelPipeline. Apply mapping to individual elements using the inner pipeline.");
        }

        /// <inheritdoc />
        public IPipeline<IEnumerable<TInput>, TNewOutput> MapAsync<TNewOutput>(Func<IList<TOutput>, Task<TNewOutput>> asyncMapper)
        {
            throw new NotImplementedException("MapAsync operation is not implemented for DataflowParallelPipeline. Apply mapping to individual elements using the inner pipeline.");
        }

        /// <inheritdoc />
        public IPipeline<IEnumerable<TInput>, TNext> Map<TNext>(Func<IList<TOutput>, Task<TNext>> transform)
        {
            throw new NotImplementedException("Async Map operation is not implemented for DataflowParallelPipeline. Apply mapping to individual elements using the inner pipeline.");
        }

        /// <inheritdoc />
        public IPipeline<IEnumerable<TInput>, IList<TOutput>> Where(Func<IEnumerable<TInput>, bool> predicate)
        {
            throw new NotImplementedException("Where operation is not implemented for DataflowParallelPipeline. Apply filtering to individual elements using the inner pipeline.");
        }

        /// <inheritdoc />
        public IPipeline<IEnumerable<TInput>, IList<TOutput>> WithErrorHandling(Func<Exception, IEnumerable<TInput>, Task<IList<TOutput>>> errorHandler)
        {
            throw new NotImplementedException("WithErrorHandling operation is not implemented for DataflowParallelPipeline. Apply error handling to individual elements using the inner pipeline.");
        }

        /// <inheritdoc />
        public IPipeline<IEnumerable<TInput>, TNewOutput> Then<TNewOutput>(IPipeline<IList<TOutput>, TNewOutput> nextPipeline)
        {
            throw new NotImplementedException("Then operation is not implemented for DataflowParallelPipeline. Chain pipelines at the element level using the inner pipeline.");
        }

        /// <inheritdoc />
        public IPipeline<IEnumerable<TInput>, TNewOutput> Then<TNewOutput>(Func<IList<TOutput>, TNewOutput> processor)
        {
            throw new NotImplementedException("Then operation is not implemented for DataflowParallelPipeline. Apply processing to individual elements using the inner pipeline.");
        }

        /// <inheritdoc />
        public IPipeline<IEnumerable<TInput>, TNewOutput> ThenAsync<TNewOutput>(Func<IList<TOutput>, Task<TNewOutput>> asyncProcessor)
        {
            throw new NotImplementedException("ThenAsync operation is not implemented for DataflowParallelPipeline. Apply processing to individual elements using the inner pipeline.");
        }

        /// <inheritdoc />
        public IPipeline<IEnumerable<TInput>, IList<TOutput>?> Filter(Predicate<IList<TOutput>> predicate)
        {
            throw new NotImplementedException("Filter operation is not implemented for DataflowParallelPipeline. Apply filtering to individual elements using the inner pipeline.");
        }

        /// <inheritdoc />
        public IPipeline<IEnumerable<TInput>, IList<TOutput>?> FilterAsync(Func<IList<TOutput>, Task<bool>> asyncPredicate)
        {
            throw new NotImplementedException("FilterAsync operation is not implemented for DataflowParallelPipeline. Apply filtering to individual elements using the inner pipeline.");
        }

        /// <inheritdoc />
        public IPipeline<IEnumerable<TInput>, IList<TOutput>> Branch(
            Predicate<IList<TOutput>> condition,
            IPipeline<IList<TOutput>, IList<TOutput>> trueBranch,
            IPipeline<IList<TOutput>, IList<TOutput>> falseBranch)
        {
            throw new NotImplementedException("Branch operation is not implemented for DataflowParallelPipeline. Apply branching to individual elements using the inner pipeline.");
        }

        /// <inheritdoc />
        public IPipeline<IEnumerable<TInput>, IList<TOutput>> HandleError(Func<Exception, IList<TOutput>> errorHandler)
        {
            throw new NotImplementedException("HandleError operation is not implemented for DataflowParallelPipeline. Apply error handling to individual elements using the inner pipeline.");
        }

        /// <inheritdoc />
        public IPipeline<IEnumerable<TInput>, IList<TOutput>> HandleErrorAsync(Func<Exception, Task<IList<TOutput>>> asyncErrorHandler)
        {
            throw new NotImplementedException("HandleErrorAsync operation is not implemented for DataflowParallelPipeline. Apply error handling to individual elements using the inner pipeline.");
        }

        /// <inheritdoc />
        public IPipeline<IEnumerable<TInput>, IList<TOutput>> WithRetry(int maxRetries, TimeSpan retryDelay)
        {
            throw new NotImplementedException("WithRetry operation is not implemented for DataflowParallelPipeline. Apply retry logic to individual elements using the inner pipeline.");
        }

        /// <inheritdoc />
        public IPipeline<IEnumerable<TInput>, IList<TOutput>> WithRetry(RetryPolicy retryPolicy)
        {
            throw new NotImplementedException("WithRetry operation is not implemented for DataflowParallelPipeline. Apply retry logic to individual elements using the inner pipeline.");
        }

        /// <inheritdoc />
        public IPipeline<IEnumerable<TInput>, IList<TOutput>> WithTimeout(TimeSpan timeout)
        {
            throw new NotImplementedException("WithTimeout operation is not implemented for DataflowParallelPipeline. Apply timeout to individual elements using the inner pipeline.");
        }

        /// <inheritdoc />
        public IPipeline<IEnumerable<TInput>, IList<TOutput>> WithCache(TimeSpan cacheDuration)
        {
            throw new NotImplementedException("WithCache operation is not implemented for DataflowParallelPipeline. Caching collections requires special handling.");
        }

        /// <inheritdoc />
        public IPipeline<IEnumerable<TInput>, IList<TOutput>> WithCache(Func<IEnumerable<TInput>, string> cacheKeySelector, TimeSpan cacheDuration)
        {
            throw new NotImplementedException("WithCache operation is not implemented for DataflowParallelPipeline. Caching collections requires special handling.");
        }

        /// <inheritdoc />
        public IPipeline<IEnumerable<TInput>, IEnumerable<IList<TOutput>>> Parallel<TParallelInput>(
            IEnumerable<TParallelInput> items,
            Func<TParallelInput, IEnumerable<TInput>> inputMapper)
        {
            throw new NotImplementedException("Parallel operation is not implemented for DataflowParallelPipeline. DataflowParallelPipeline already implements parallel processing.");
        }

        /// <inheritdoc />
        IPipeline<IEnumerable<TInput>, IList<TOutput>> IPipeline<IEnumerable<TInput>, IList<TOutput>>.AddStage<TStageOutput>(Conduit.Api.IPipelineStage<IList<TOutput>, TStageOutput> stage)
        {
            throw new NotImplementedException("AddStage operation is not implemented for DataflowParallelPipeline. Add stages to individual elements using the inner pipeline.");
        }

        /// <inheritdoc />
        public IPipeline<IEnumerable<TInput>, IList<TOutput>> AddStage(object stage)
        {
            throw new NotImplementedException("AddStage operation is not implemented for DataflowParallelPipeline. Add stages to individual elements using the inner pipeline.");
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
    /// Extension methods for creating parallel pipelines.
    /// </summary>
    public static class ParallelPipelineExtensions
    {
        /// <summary>
        /// Creates a pipeline that processes collections in parallel.
        /// </summary>
        public static IPipeline<IEnumerable<TInput>, IList<TOutput>> Parallel<TInput, TOutput>(
            this IPipeline<TInput, TOutput> pipeline,
            int maxConcurrency = -1,
            bool preserveOrder = true)
        {
            return new ParallelPipeline<TInput, TOutput>(pipeline, maxConcurrency, preserveOrder);
        }

        /// <summary>
        /// Creates a dataflow-based parallel pipeline.
        /// </summary>
        public static IPipeline<IEnumerable<TInput>, IList<TOutput>> ParallelDataflow<TInput, TOutput>(
            this IPipeline<TInput, TOutput> pipeline,
            int maxConcurrency = -1,
            bool preserveOrder = true,
            int boundedCapacity = -1)
        {
            return new DataflowParallelPipeline<TInput, TOutput>(pipeline, maxConcurrency, preserveOrder, boundedCapacity);
        }

        /// <summary>
        /// Processes a collection through a pipeline with batching.
        /// </summary>
        public static async Task<IList<TOutput>> ProcessBatchAsync<TInput, TOutput>(
            this IPipeline<TInput, TOutput> pipeline,
            IEnumerable<TInput> items,
            int batchSize,
            int maxConcurrency = -1,
            CancellationToken cancellationToken = default)
        {
            var results = new List<TOutput>();
            var itemsList = items.ToList();
            var parallelPipeline = pipeline.Parallel(maxConcurrency);

            for (int i = 0; i < itemsList.Count; i += batchSize)
            {
                var batch = itemsList.Skip(i).Take(batchSize);
                var batchResults = await parallelPipeline.ExecuteAsync(batch, cancellationToken);
                results.AddRange(batchResults);
            }

            return results;
        }

        /// <summary>
        /// Creates a pipeline that processes items in parallel with a sliding window.
        /// </summary>
        public static async Task<IList<TOutput>> ProcessWithSlidingWindowAsync<TInput, TOutput>(
            this IPipeline<TInput, TOutput> pipeline,
            IEnumerable<TInput> items,
            int windowSize,
            CancellationToken cancellationToken = default)
        {
            var results = new List<TOutput>();
            var semaphore = new SemaphoreSlim(windowSize, windowSize);
            var tasks = new List<Task<TOutput>>();

            foreach (var item in items)
            {
                await semaphore.WaitAsync(cancellationToken);

                var task = Task.Run(async () =>
                {
                    try
                    {
                        return await pipeline.ExecuteAsync(item, cancellationToken);
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                }, cancellationToken);

                tasks.Add(task);
            }

            results.AddRange(await Task.WhenAll(tasks));
            return results;
        }
    }
}