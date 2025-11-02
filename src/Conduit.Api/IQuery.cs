namespace Conduit.Api;

/// <summary>
/// Marker interface for query messages.
/// Queries represent requests to retrieve data without modifying state.
/// They should always return a result and not cause side effects.
/// </summary>
/// <typeparam name="TResult">The type of result this query returns</typeparam>
public interface IQuery<TResult> : IMessage
{
    /// <summary>
    /// Gets the query identifier.
    /// </summary>
    string QueryId => MessageId;

    /// <summary>
    /// Gets the expected result type for this query.
    /// </summary>
    Type ResultType => typeof(TResult);

    /// <summary>
    /// Gets the query name for logging and error reporting.
    /// </summary>
    string QueryName => GetType().Name;

    /// <summary>
    /// Gets a value indicating whether to use cache for this query if available.
    /// </summary>
    bool UseCache => true;

    /// <summary>
    /// Gets the cache key for this query if caching is enabled.
    /// </summary>
    string? CacheKey => null;

    /// <summary>
    /// Gets the cache duration in seconds if caching is enabled.
    /// </summary>
    int? CacheDurationSeconds => null;
}