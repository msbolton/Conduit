namespace Conduit.Api;

/// <summary>
/// Interface for handling commands.
/// </summary>
/// <typeparam name="TCommand">The type of command to handle</typeparam>
/// <typeparam name="TResponse">The type of response the command returns</typeparam>
public interface ICommandHandler<in TCommand, TResponse>
    where TCommand : ICommand<TResponse>
{
    /// <summary>
    /// Handles the command and returns a response.
    /// </summary>
    /// <param name="command">The command to handle</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The command response</returns>
    Task<TResponse> HandleAsync(TCommand command, CancellationToken cancellationToken = default);
}

/// <summary>
/// Interface for handling commands that don't return a response.
/// </summary>
/// <typeparam name="TCommand">The type of command to handle</typeparam>
public interface ICommandHandler<in TCommand> : ICommandHandler<TCommand, Unit>
    where TCommand : ICommand
{
}