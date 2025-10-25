using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Conduit.Common;

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
            Guard.AgainstNull(innerPipeline, nameof(innerPipeline));

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
            PipelineContext context,
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

            IList<TOutput> results;
            if (_preserveOrder)
            {
                results = await ExecuteWithOrderPreservationAsync(inputList, context, cancellationToken);
            }
            else
            {
                results = await ExecuteWithoutOrderPreservationAsync(inputList, context, cancellationToken);
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
            PipelineContext parentContext,
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

                        return await _innerPipeline.ExecuteAsync(item, childContext, cancellationToken);
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

            await Parallel.ForEachAsync(inputList, _parallelOptions, async (item, ct) =>
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
            PipelineContext parentContext,
            CancellationToken cancellationToken)
        {
            var results = new List<TOutput>();
            var resultLock = new object();
            var processedCount = 0;

            await Parallel.ForEachAsync(inputList, _parallelOptions, async (item, ct) =>
            {
                var childContext = parentContext.Copy();
                childContext.SetProperty("ParallelPipeline.ThreadId", Thread.CurrentThread.ManagedThreadId);

                var result = await _innerPipeline.ExecuteAsync(item, childContext, ct);

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
        public void AddInterceptor(IPipelineInterceptor interceptor)
        {
            _innerPipeline.AddInterceptor(interceptor);
        }

        public void AddBehavior(BehaviorContribution behavior)
        {
            _innerPipeline.AddBehavior(behavior);
        }

        public void AddStage(IPipelineStage<object, object> stage)
        {
            _innerPipeline.AddStage(stage);
        }

        public void SetErrorHandler(Func<Exception, IList<TOutput>> errorHandler)
        {
            // This would need custom implementation for collection results
            throw new NotImplementedException("Error handler configuration not yet implemented for ParallelPipeline");
        }

        public void SetCompletionHandler(Action<IList<TOutput>> completionHandler)
        {
            // This would need custom implementation for collection results
            throw new NotImplementedException("Completion handler configuration not yet implemented for ParallelPipeline");
        }

        public void ConfigureCache(Func<IEnumerable<TInput>, string> cacheKeyExtractor, TimeSpan duration)
        {
            // Caching would need special handling for collections
            throw new NotImplementedException("Cache configuration not yet implemented for ParallelPipeline");
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
            Guard.AgainstNull(innerPipeline, nameof(innerPipeline));

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
            PipelineContext context,
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
        public void AddInterceptor(IPipelineInterceptor interceptor) => _innerPipeline.AddInterceptor(interceptor);
        public void AddBehavior(BehaviorContribution behavior) => _innerPipeline.AddBehavior(behavior);
        public void AddStage(IPipelineStage<object, object> stage) => _innerPipeline.AddStage(stage);
        public void SetErrorHandler(Func<Exception, IList<TOutput>> errorHandler) => throw new NotImplementedException();
        public void SetCompletionHandler(Action<IList<TOutput>> completionHandler) => throw new NotImplementedException();
        public void ConfigureCache(Func<IEnumerable<TInput>, string> cacheKeyExtractor, TimeSpan duration) => throw new NotImplementedException();
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
            var batches = items.Batch(batchSize);

            var parallelPipeline = pipeline.Parallel(maxConcurrency);

            foreach (var batch in batches)
            {
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