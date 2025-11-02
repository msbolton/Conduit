namespace Conduit.Api;

/// <summary>
/// Represents a behavior contribution from a pluggable component to the message pipeline.
/// </summary>
/// <remarks>
/// This is the base interface for behavior contributions. The actual implementation
/// with full functionality is provided by the pipeline module.
/// BehaviorContribution encapsulates the behavior logic and metadata needed
/// for components to contribute to the message processing pipeline.
/// </remarks>
public interface IBehaviorContribution
{
    /// <summary>
    /// Gets the unique identifier for this behavior.
    /// </summary>
    string Id { get; }

    /// <summary>
    /// Gets the display name for this behavior.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets the description of what this behavior does.
    /// </summary>
    string Description { get; }

    /// <summary>
    /// Gets the priority of this behavior for ordering in the pipeline.
    /// Higher numbers indicate higher priority.
    /// </summary>
    int Priority { get; }

    /// <summary>
    /// Gets a value indicating whether this behavior is enabled.
    /// </summary>
    bool IsEnabled { get; }

    /// <summary>
    /// Gets the behavior placement rules for ordering in the pipeline.
    /// </summary>
    BehaviorPlacement? Placement { get; }

    /// <summary>
    /// Gets the tags associated with this behavior for categorization and filtering.
    /// </summary>
    IReadOnlySet<string> Tags { get; }

    /// <summary>
    /// Gets the execution phase for this behavior.
    /// </summary>
    BehaviorPhase Phase { get; }

    /// <summary>
    /// Gets the behavior execution delegate.
    /// </summary>
    BehaviorDelegate? Behavior { get; }
}

/// <summary>
/// Delegate for behavior execution.
/// </summary>
/// <param name="context">The message context</param>
/// <param name="next">The next behavior in the chain</param>
/// <returns>Task representing the async operation</returns>
public delegate Task BehaviorDelegate(IMessageContext context, Func<Task> next);

/// <summary>
/// Defines the execution phase for a behavior.
/// </summary>
public enum BehaviorPhase
{
    /// <summary>
    /// Executes before the main processing.
    /// </summary>
    PreProcessing,

    /// <summary>
    /// Executes during the main processing.
    /// </summary>
    Processing,

    /// <summary>
    /// Executes after the main processing.
    /// </summary>
    PostProcessing
}