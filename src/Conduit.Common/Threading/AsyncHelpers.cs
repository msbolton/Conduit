using System.Runtime.CompilerServices;

namespace Conduit.Common.Threading;

/// <summary>
/// Provides helpers for async operations.
/// </summary>
public static class AsyncHelpers
{
    /// <summary>
    /// Runs an async method synchronously.
    /// </summary>
    /// <remarks>
    /// This should be used sparingly as it can cause deadlocks.
    /// </remarks>
    public static void RunSync(Func<Task> task)
    {
        var oldContext = SynchronizationContext.Current;
        var synch = new ExclusiveSynchronizationContext();
        SynchronizationContext.SetSynchronizationContext(synch);
        synch.Post(async _ =>
        {
            try
            {
                await task();
            }
            catch (Exception ex)
            {
                synch.InnerException = ex;
                throw;
            }
            finally
            {
                synch.EndMessageLoop();
            }
        }, null);
        synch.BeginMessageLoop();
        SynchronizationContext.SetSynchronizationContext(oldContext);
        if (synch.InnerException != null)
            throw synch.InnerException;
    }

    /// <summary>
    /// Runs an async method synchronously and returns the result.
    /// </summary>
    public static T RunSync<T>(Func<Task<T>> task)
    {
        var oldContext = SynchronizationContext.Current;
        var synch = new ExclusiveSynchronizationContext();
        SynchronizationContext.SetSynchronizationContext(synch);
        T? result = default;
        synch.Post(async _ =>
        {
            try
            {
                result = await task();
            }
            catch (Exception ex)
            {
                synch.InnerException = ex;
                throw;
            }
            finally
            {
                synch.EndMessageLoop();
            }
        }, null);
        synch.BeginMessageLoop();
        SynchronizationContext.SetSynchronizationContext(oldContext);
        if (synch.InnerException != null)
            throw synch.InnerException;
        return result!;
    }

    /// <summary>
    /// Executes tasks with retry logic.
    /// </summary>
    public static async Task<T> RetryAsync<T>(
        Func<Task<T>> operation,
        int maxAttempts = 3,
        TimeSpan? delay = null,
        Func<Exception, int, bool>? shouldRetry = null,
        CancellationToken cancellationToken = default)
    {
        Guard.NotNull(operation);
        Guard.InRange(maxAttempts, 1, 100);

        delay ??= TimeSpan.FromSeconds(1);
        shouldRetry ??= (_, _) => true;

        Exception? lastException = null;
        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                return await operation().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                lastException = ex;
                if (attempt == maxAttempts || !shouldRetry(ex, attempt))
                    throw;

                if (delay.Value > TimeSpan.Zero)
                {
                    var actualDelay = TimeSpan.FromMilliseconds(
                        delay.Value.TotalMilliseconds * Math.Pow(2, attempt - 1));
                    await Task.Delay(actualDelay, cancellationToken).ConfigureAwait(false);
                }
            }
        }

        throw lastException ?? new InvalidOperationException("Retry failed");
    }

    /// <summary>
    /// Executes a task with timeout.
    /// </summary>
    public static async Task<T> TimeoutAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        Guard.NotNull(operation);

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(timeout);

        try
        {
            return await operation(cts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException($"Operation timed out after {timeout}");
        }
    }

    /// <summary>
    /// Creates a task that completes when any of the tasks complete successfully.
    /// </summary>
    public static async Task<T> WhenAnySuccessful<T>(
        IEnumerable<Task<T>> tasks,
        CancellationToken cancellationToken = default)
    {
        Guard.NotNull(tasks);

        var taskList = tasks.ToList();
        if (!taskList.Any())
            throw new ArgumentException("No tasks provided", nameof(tasks));

        var exceptions = new List<Exception>();
        var remaining = new List<Task<T>>(taskList);

        while (remaining.Any())
        {
            var completed = await Task.WhenAny(remaining).ConfigureAwait(false);
            remaining.Remove(completed);

            try
            {
                return await completed.ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                exceptions.Add(ex);
            }

            cancellationToken.ThrowIfCancellationRequested();
        }

        throw new AggregateException(
            "All tasks failed",
            exceptions);
    }

    /// <summary>
    /// Debounces an async operation.
    /// </summary>
    public static Func<Task> Debounce(
        Func<Task> operation,
        TimeSpan delay)
    {
        Guard.NotNull(operation);

        CancellationTokenSource? cts = null;

        return async () =>
        {
            cts?.Cancel();
            cts = new CancellationTokenSource();
            var token = cts.Token;

            try
            {
                await Task.Delay(delay, token).ConfigureAwait(false);
                if (!token.IsCancellationRequested)
                {
                    await operation().ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException)
            {
                // Ignored - operation was debounced
            }
        };
    }

    /// <summary>
    /// Throttles an async operation.
    /// </summary>
    public static Func<Task<T>> Throttle<T>(
        Func<Task<T>> operation,
        TimeSpan interval)
    {
        Guard.NotNull(operation);

        DateTime lastRun = DateTime.MinValue;
        Task<T>? lastTask = null;
        var syncLock = new object();

        return () =>
        {
            lock (syncLock)
            {
                var now = DateTime.UtcNow;
                if (now - lastRun >= interval)
                {
                    lastRun = now;
                    lastTask = operation();
                }
                return lastTask ?? Task.FromResult(default(T)!);
            }
        };
    }

    private sealed class ExclusiveSynchronizationContext : SynchronizationContext
    {
        private readonly Queue<(SendOrPostCallback, object?)> _items = new();
        private readonly AutoResetEvent _workItemsWaiting = new(false);
        private bool _done;

        public Exception? InnerException { get; set; }

        public override void Send(SendOrPostCallback d, object? state)
        {
            throw new NotSupportedException("Synchronously sending is not supported.");
        }

        public override void Post(SendOrPostCallback d, object? state)
        {
            lock (_items)
            {
                _items.Enqueue((d, state));
            }
            _workItemsWaiting.Set();
        }

        public void EndMessageLoop()
        {
            Post(_ => _done = true, null);
        }

        public void BeginMessageLoop()
        {
            while (!_done)
            {
                (SendOrPostCallback, object?)? task = null;
                lock (_items)
                {
                    if (_items.Count > 0)
                    {
                        task = _items.Dequeue();
                    }
                }
                if (task != null)
                {
                    task.Value.Item1(task.Value.Item2);
                }
                else
                {
                    _workItemsWaiting.WaitOne();
                }
            }
        }

        public override SynchronizationContext CreateCopy()
        {
            return this;
        }
    }
}