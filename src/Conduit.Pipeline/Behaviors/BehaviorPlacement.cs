using System;

namespace Conduit.Pipeline.Behaviors;

/// <summary>
/// Defines placement rules for behaviors in the pipeline.
/// </summary>
public class BehaviorPlacement
{
    /// <summary>
    /// Gets the placement strategy.
    /// </summary>
    public PlacementStrategy Strategy { get; init; }

    /// <summary>
    /// Gets the target behavior ID for relative placement.
    /// </summary>
    public string? TargetBehaviorId { get; init; }

    /// <summary>
    /// Gets the explicit order value for ordered placement.
    /// </summary>
    public int Order { get; init; } = 1000;

    private BehaviorPlacement() { }

    /// <summary>
    /// Places the behavior before the specified behavior.
    /// </summary>
    public static BehaviorPlacement Before(string behaviorId)
    {
        if (string.IsNullOrWhiteSpace(behaviorId))
            throw new ArgumentException("Behavior ID cannot be null or empty", nameof(behaviorId));

        return new BehaviorPlacement
        {
            Strategy = PlacementStrategy.Before,
            TargetBehaviorId = behaviorId
        };
    }

    /// <summary>
    /// Places the behavior after the specified behavior.
    /// </summary>
    public static BehaviorPlacement After(string behaviorId)
    {
        if (string.IsNullOrWhiteSpace(behaviorId))
            throw new ArgumentException("Behavior ID cannot be null or empty", nameof(behaviorId));

        return new BehaviorPlacement
        {
            Strategy = PlacementStrategy.After,
            TargetBehaviorId = behaviorId
        };
    }

    /// <summary>
    /// Replaces the specified behavior.
    /// </summary>
    public static BehaviorPlacement Replace(string behaviorId)
    {
        if (string.IsNullOrWhiteSpace(behaviorId))
            throw new ArgumentException("Behavior ID cannot be null or empty", nameof(behaviorId));

        return new BehaviorPlacement
        {
            Strategy = PlacementStrategy.Replace,
            TargetBehaviorId = behaviorId
        };
    }

    /// <summary>
    /// Places the behavior at the beginning of the pipeline.
    /// </summary>
    public static BehaviorPlacement First()
    {
        return new BehaviorPlacement
        {
            Strategy = PlacementStrategy.First,
            Order = 0
        };
    }

    /// <summary>
    /// Places the behavior at the end of the pipeline.
    /// </summary>
    public static BehaviorPlacement Last()
    {
        return new BehaviorPlacement
        {
            Strategy = PlacementStrategy.Last,
            Order = int.MaxValue
        };
    }

    /// <summary>
    /// Places the behavior at a specific order position.
    /// </summary>
    public static BehaviorPlacement Ordered(int order)
    {
        return new BehaviorPlacement
        {
            Strategy = PlacementStrategy.Ordered,
            Order = order
        };
    }

    /// <summary>
    /// Returns a string representation of the placement.
    /// </summary>
    public override string ToString()
    {
        return Strategy switch
        {
            PlacementStrategy.Before => $"Before({TargetBehaviorId})",
            PlacementStrategy.After => $"After({TargetBehaviorId})",
            PlacementStrategy.Replace => $"Replace({TargetBehaviorId})",
            PlacementStrategy.First => "First",
            PlacementStrategy.Last => "Last",
            PlacementStrategy.Ordered => $"Ordered({Order})",
            _ => "Unknown"
        };
    }
}

/// <summary>
/// Placement strategy for behaviors.
/// </summary>
public enum PlacementStrategy
{
    /// <summary>
    /// Place before a specific behavior.
    /// </summary>
    Before,

    /// <summary>
    /// Place after a specific behavior.
    /// </summary>
    After,

    /// <summary>
    /// Replace an existing behavior.
    /// </summary>
    Replace,

    /// <summary>
    /// Place at the beginning.
    /// </summary>
    First,

    /// <summary>
    /// Place at the end.
    /// </summary>
    Last,

    /// <summary>
    /// Place at a specific order position.
    /// </summary>
    Ordered
}