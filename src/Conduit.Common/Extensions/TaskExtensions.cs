using System.Runtime.CompilerServices;

namespace Conduit.Common.Extensions;

/// <summary>
/// Extension methods for Task and Task&lt;T&gt;.
/// </summary>
public static class TaskExtensions
{
    /// <summary>
    /// Safely executes a task without throwing exceptions.
    /// </summary>
    public static async Task<Result<T>> SafeExecuteAsync<T>(
        this Task<T> task,
        Action<Exception>? onError = null)
    {
        try
        {
            var result = await task.ConfigureAwait(false);
            return Result<T>.Success(result);
        }
        catch (Exception ex)
        {
            onError?.Invoke(ex);
            return Result<T>.Failure(ex.Message);
        }
    }

    /// <summary>
    /// Safely executes a task without throwing exceptions.
    /// </summary>
    public static async Task<Result> SafeExecuteAsync(
        this Task task,
        Action<Exception>? onError = null)
    {
        try
        {
            await task.ConfigureAwait(false);
            return Result.Success();
        }
        catch (Exception ex)
        {
            onError?.Invoke(ex);
            return Result.Failure(ex.Message);
        }
    }

    /// <summary>
    /// Adds a timeout to the task.
    /// </summary>
    public static async Task<T> WithTimeout<T>(
        this Task<T> task,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(timeout);

        var completedTask = await Task.WhenAny(
            task,
            Task.Delay(Timeout.Infinite, cts.Token)
        ).ConfigureAwait(false);

        if (completedTask == task)
        {
            cts.Cancel();
            return await task.ConfigureAwait(false);
        }

        throw new TimeoutException($"Task timed out after {timeout}");
    }

    /// <summary>
    /// Adds a timeout to the task.
    /// </summary>
    public static async Task WithTimeout(
        this Task task,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(timeout);

        var completedTask = await Task.WhenAny(
            task,
            Task.Delay(Timeout.Infinite, cts.Token)
        ).ConfigureAwait(false);

        if (completedTask == task)
        {
            cts.Cancel();
            await task.ConfigureAwait(false);
            return;
        }

        throw new TimeoutException($"Task timed out after {timeout}");
    }

    /// <summary>
    /// Adds retry logic to the task.
    /// </summary>
    public static async Task<T> WithRetry<T>(
        this Func<Task<T>> taskFactory,
        int maxAttempts = 3,
        TimeSpan? delay = null,
        Func<Exception, int, bool>? shouldRetry = null,
        CancellationToken cancellationToken = default)
    {
        Guard.NotNull(taskFactory);
        Guard.InRange(maxAttempts, 1, 100);

        delay ??= TimeSpan.FromSeconds(1);
        shouldRetry ??= (_, _) => true;

        Exception? lastException = null;
        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                return await taskFactory().ConfigureAwait(false);
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
    /// Continues the task on a captured context.
    /// </summary>
    public static ConfiguredTaskAwaitable<T> OnContext<T>(
        this Task<T> task,
        bool continueOnCapturedContext = true)
    {
        return task.ConfigureAwait(continueOnCapturedContext);
    }

    /// <summary>
    /// Continues the task on a captured context.
    /// </summary>
    public static ConfiguredTaskAwaitable OnContext(
        this Task task,
        bool continueOnCapturedContext = true)
    {
        return task.ConfigureAwait(continueOnCapturedContext);
    }

    /// <summary>
    /// Fires and forgets the task, with optional error handling.
    /// </summary>
    public static void FireAndForget(
        this Task task,
        Action<Exception>? errorHandler = null)
    {
        task.ContinueWith(t =>
        {
            if (t.IsFaulted && t.Exception != null)
            {
                if (errorHandler != null)
                {
                    errorHandler(t.Exception.GetBaseException());
                }
                else
                {
                    // Log or handle unobserved exceptions
                    _ = t.Exception;
                }
            }
        }, TaskContinuationOptions.OnlyOnFaulted);
    }

    /// <summary>
    /// Converts a task to a ValueTask.
    /// </summary>
    public static ValueTask<T> ToValueTask<T>(this Task<T> task)
    {
        return new ValueTask<T>(task);
    }

    /// <summary>
    /// Converts a task to a ValueTask.
    /// </summary>
    public static ValueTask ToValueTask(this Task task)
    {
        return new ValueTask(task);
    }

    /// <summary>
    /// Executes tasks in sequence.
    /// </summary>
    public static async Task<IEnumerable<T>> Sequence<T>(
        this IEnumerable<Task<T>> tasks)
    {
        Guard.NotNull(tasks);

        var results = new List<T>();
        foreach (var task in tasks)
        {
            results.Add(await task.ConfigureAwait(false));
        }
        return results;
    }

    /// <summary>
    /// Executes tasks in parallel with a maximum degree of parallelism.
    /// </summary>
    public static async Task<T[]> WhenAll<T>(
        this IEnumerable<Task<T>> tasks,
        int maxConcurrency = -1)
    {
        Guard.NotNull(tasks);

        var taskList = tasks.ToList();
        if (!taskList.Any())
            return Array.Empty<T>();

        if (maxConcurrency <= 0 || maxConcurrency >= taskList.Count)
            return await Task.WhenAll(taskList).ConfigureAwait(false);

        var results = new T[taskList.Count];
        var semaphore = new SemaphoreSlim(maxConcurrency, maxConcurrency);

        var taskWrappers = taskList.Select(async (task, index) =>
        {
            await semaphore.WaitAsync().ConfigureAwait(false);
            try
            {
                results[index] = await task.ConfigureAwait(false);
            }
            finally
            {
                semaphore.Release();
            }
        });

        await Task.WhenAll(taskWrappers).ConfigureAwait(false);
        return results;
    }

    /// <summary>
    /// Waits for the first task to complete successfully.
    /// </summary>
    public static async Task<T> WhenAnySuccessful<T>(
        this IEnumerable<Task<T>> tasks,
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

        throw new AggregateException("All tasks failed", exceptions);
    }

    /// <summary>
    /// Adds a cancellation token to a task.
    /// </summary>
    public static async Task<T> WithCancellation<T>(
        this Task<T> task,
        CancellationToken cancellationToken)
    {
        var tcs = new TaskCompletionSource<object?>();
        using (cancellationToken.Register(() => tcs.TrySetCanceled()))
        {
            var completedTask = await Task.WhenAny(task, tcs.Task).ConfigureAwait(false);
            if (completedTask == tcs.Task)
            {
                throw new OperationCanceledException(cancellationToken);
            }
            return await task.ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Transforms the result of a task.
    /// </summary>
    public static async Task<TResult> Then<T, TResult>(
        this Task<T> task,
        Func<T, TResult> transform)
    {
        Guard.NotNull(task);
        Guard.NotNull(transform);

        var result = await task.ConfigureAwait(false);
        return transform(result);
    }

    /// <summary>
    /// Transforms the result of a task asynchronously.
    /// </summary>
    public static async Task<TResult> Then<T, TResult>(
        this Task<T> task,
        Func<T, Task<TResult>> transform)
    {
        Guard.NotNull(task);
        Guard.NotNull(transform);

        var result = await task.ConfigureAwait(false);
        return await transform(result).ConfigureAwait(false);
    }

    /// <summary>
    /// Adds a finally block to a task.
    /// </summary>
    public static async Task<T> Finally<T>(
        this Task<T> task,
        Action finallyAction)
    {
        Guard.NotNull(task);
        Guard.NotNull(finallyAction);

        try
        {
            return await task.ConfigureAwait(false);
        }
        finally
        {
            finallyAction();
        }
    }

    /// <summary>
    /// Adds a finally block to a task.
    /// </summary>
    public static async Task<T> Finally<T>(
        this Task<T> task,
        Func<Task> finallyAction)
    {
        Guard.NotNull(task);
        Guard.NotNull(finallyAction);

        try
        {
            return await task.ConfigureAwait(false);
        }
        finally
        {
            await finallyAction().ConfigureAwait(false);
        }
    }
}