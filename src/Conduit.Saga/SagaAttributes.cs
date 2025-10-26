namespace Conduit.Saga;

/// <summary>
/// Attribute to mark which message types can start a saga.
/// This replaces the IAmStartedByMessages interface to avoid generic limitations.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
public class SagaStartedByAttribute : Attribute
{
    /// <summary>
    /// The message types that can start this saga.
    /// </summary>
    public Type[] MessageTypes { get; }

    public SagaStartedByAttribute(params Type[] messageTypes)
    {
        MessageTypes = messageTypes;
    }
}

/// <summary>
/// Attribute to mark which message types a saga can handle.
/// This provides metadata for the saga orchestrator to route messages appropriately.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
public class SagaHandlesAttribute : Attribute
{
    /// <summary>
    /// The message types that this saga can handle.
    /// </summary>
    public Type[] MessageTypes { get; }

    public SagaHandlesAttribute(params Type[] messageTypes)
    {
        MessageTypes = messageTypes;
    }
}
