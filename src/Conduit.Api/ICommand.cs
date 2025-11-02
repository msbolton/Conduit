namespace Conduit.Api;

/// <summary>
/// Marker interface for command messages.
/// Commands represent requests to perform an action or operation.
/// They are typically processed by a single handler and may or may not return a result.
/// </summary>
/// <typeparam name="TResponse">The type of response this command returns, or Unit if no response</typeparam>
public interface ICommand<TResponse> : IMessage
{
    /// <summary>
    /// Gets the command identifier.
    /// </summary>
    string CommandId => MessageId;

    /// <summary>
    /// Gets the expected response type for this command.
    /// </summary>
    Type ResponseType => typeof(TResponse);

    /// <summary>
    /// Gets the command name for logging and error reporting.
    /// </summary>
    string CommandName => GetType().Name;
}

/// <summary>
/// Marker interface for commands that don't return a response.
/// </summary>
public interface ICommand : ICommand<Unit>
{
}