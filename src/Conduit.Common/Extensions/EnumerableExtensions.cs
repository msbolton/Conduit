namespace Conduit.Common.Extensions;

/// <summary>
/// Extension methods for IEnumerable.
/// </summary>
public static class EnumerableExtensions
{
    /// <summary>
    /// Performs an action on each element.
    /// </summary>
    public static void ForEach<T>(this IEnumerable<T> source, Action<T> action)
    {
        Guard.NotNull(source);
        Guard.NotNull(action);

        foreach (var item in source)
        {
            action(item);
        }
    }

    /// <summary>
    /// Performs an async action on each element.
    /// </summary>
    public static async Task ForEachAsync<T>(
        this IEnumerable<T> source,
        Func<T, Task> action,
        CancellationToken cancellationToken = default)
    {
        Guard.NotNull(source);
        Guard.NotNull(action);

        foreach (var item in source)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await action(item).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Performs an async action on each element in parallel.
    /// </summary>
    public static Task ForEachParallelAsync<T>(
        this IEnumerable<T> source,
        Func<T, Task> action,
        int maxConcurrency = -1,
        CancellationToken cancellationToken = default)
    {
        Guard.NotNull(source);
        Guard.NotNull(action);

        if (maxConcurrency <= 0)
            maxConcurrency = Environment.ProcessorCount;

        var semaphore = new SemaphoreSlim(maxConcurrency, maxConcurrency);
        var tasks = source.Select(async item =>
        {
            await semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                await action(item).ConfigureAwait(false);
            }
            finally
            {
                semaphore.Release();
            }
        });

        return Task.WhenAll(tasks);
    }

    /// <summary>
    /// Safely gets elements or returns empty enumerable.
    /// </summary>
    public static IEnumerable<T> SafeEmpty<T>(this IEnumerable<T>? source)
    {
        return source ?? Enumerable.Empty<T>();
    }

    /// <summary>
    /// Checks if the enumerable is null or empty.
    /// </summary>
    public static bool IsNullOrEmpty<T>(this IEnumerable<T>? source)
    {
        return source?.Any() != true;
    }

    /// <summary>
    /// Partitions elements into batches.
    /// </summary>
    public static IEnumerable<IEnumerable<T>> Batch<T>(
        this IEnumerable<T> source,
        int batchSize)
    {
        Guard.NotNull(source);
        Guard.InRange(batchSize, 1, int.MaxValue);

        using var enumerator = source.GetEnumerator();
        while (enumerator.MoveNext())
        {
            yield return GetBatch(enumerator, batchSize);
        }

        static IEnumerable<T> GetBatch(IEnumerator<T> enumerator, int batchSize)
        {
            yield return enumerator.Current;
            for (int i = 1; i < batchSize && enumerator.MoveNext(); i++)
            {
                yield return enumerator.Current;
            }
        }
    }

    /// <summary>
    /// Returns distinct elements by a key selector.
    /// </summary>
    public static IEnumerable<T> DistinctBy<T, TKey>(
        this IEnumerable<T> source,
        Func<T, TKey> keySelector)
    {
        Guard.NotNull(source);
        Guard.NotNull(keySelector);

        var seen = new HashSet<TKey>();
        foreach (var item in source)
        {
            var key = keySelector(item);
            if (key != null && seen.Add(key))
            {
                yield return item;
            }
        }
    }

    /// <summary>
    /// Converts enumerable to a readonly list.
    /// </summary>
    public static IReadOnlyList<T> ToReadOnlyList<T>(this IEnumerable<T> source)
    {
        Guard.NotNull(source);
        return source as IReadOnlyList<T> ?? source.ToList();
    }

    /// <summary>
    /// Executes actions based on element index.
    /// </summary>
    public static void ForEachIndexed<T>(
        this IEnumerable<T> source,
        Action<T, int> action)
    {
        Guard.NotNull(source);
        Guard.NotNull(action);

        int index = 0;
        foreach (var item in source)
        {
            action(item, index++);
        }
    }

    /// <summary>
    /// Shuffles elements randomly.
    /// </summary>
    public static IEnumerable<T> Shuffle<T>(
        this IEnumerable<T> source,
        Random? random = null)
    {
        Guard.NotNull(source);
        random ??= Random.Shared;

        var list = source.ToList();
        int n = list.Count;
        while (n > 1)
        {
            n--;
            int k = random.Next(n + 1);
            (list[k], list[n]) = (list[n], list[k]);
        }
        return list;
    }

    /// <summary>
    /// Takes elements while a condition is true, including the first false element.
    /// </summary>
    public static IEnumerable<T> TakeWhileInclusive<T>(
        this IEnumerable<T> source,
        Func<T, bool> predicate)
    {
        Guard.NotNull(source);
        Guard.NotNull(predicate);

        foreach (var item in source)
        {
            yield return item;
            if (!predicate(item))
                yield break;
        }
    }
}