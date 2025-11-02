namespace Conduit.Api;

/// <summary>
/// Interface for handling queries.
/// Query handlers should not modify state and should be idempotent.
/// </summary>
/// <typeparam name="TQuery">The type of query to handle</typeparam>
/// <typeparam name="TResult">The type of result the query returns</typeparam>
public interface IQueryHandler<in TQuery, TResult>
    where TQuery : IQuery<TResult>
{
    /// <summary>
    /// Handles the query and returns a result.
    /// </summary>
    /// <param name="query">The query to handle</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The query result</returns>
    Task<TResult> HandleAsync(TQuery query, CancellationToken cancellationToken = default);
}